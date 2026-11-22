using System.ComponentModel;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

public enum BodyType2D { Dynamic, Kinematic, Static }

[DisplayName("Rigidbody 2D"), YamlObject]
public sealed partial record Rigidbody2DComponent : SceneComponent
{
    public Rigidbody2DComponent() { }
    [YamlConstructor] public Rigidbody2DComponent(float mass = 1, float gravity = 980) => (Mass, Gravity) = (mass, gravity);
    public BodyType2D BodyType { get; set; }
    public float Mass { get; set; } = 1;
    public float Gravity { get; set; } = 980;
    public float Restitution { get; set; }
    public bool FreezeRotation { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float AngularVelocity { get; set; }

    internal void Validate(string owner)
    {
        if (!Enum.IsDefined(BodyType) || !float.IsFinite(Mass) || Mass <= 0 || !float.IsFinite(Gravity) ||
            Restitution is not (>= 0 and <= 1) || !float.IsFinite(VelocityX) || !float.IsFinite(VelocityY) || !float.IsFinite(AngularVelocity))
            throw new InvalidDataException($"{owner}: invalid Rigidbody 2D mass, gravity, restitution or velocity.");
    }
}

[DisplayName("Distance Joint 2D"), YamlObject]
public sealed partial record DistanceJoint2DComponent : SceneComponent
{
    public DistanceJoint2DComponent() { }
    [YamlConstructor] public DistanceJoint2DComponent(float length = 64) => Length = length;
    public Guid? Connected { get; set; }
    public float Length { get; set; } = 64;
}

[DisplayName("Character Motor 2D"), YamlObject]
public sealed partial record CharacterMotor2DComponent : SceneComponent
{
    public CharacterMotor2DComponent() { }
    [YamlConstructor] public CharacterMotor2DComponent(float maxSlope = 48, float groundSnap = 4) => (MaxSlope, GroundSnap) = (maxSlope, groundSnap);
    public float MaxSlope { get; set; } = 48;
    public float GroundSnap { get; set; } = 4;
}

public readonly record struct MotionState2D(Vector2 Velocity, float AngularVelocity, Guid? Support, Vector2 GroundNormal)
{
    public bool Grounded => Support.HasValue;
}

public interface IBodyMotion2D
{
    bool TryGetMotion(Guid id, out MotionState2D state);
    bool SetVelocity(Guid id, Vector2 velocity);
    bool ApplyImpulse(Guid id, Vector2 impulse, Vector2? worldPoint = null);
    bool DriveCharacter(Guid id, float horizontalSpeed, float jumpSpeed = 0);
}

public sealed partial class SceneGame : IBodyMotion2D
{
    public IBodyMotion2D Motion => this;
    private Body[] rigidBodies = [], joints = [];

    public bool TryGetMotion(Guid id, out MotionState2D state)
    {
        if (byId.TryGetValue(id, out var b) && b.Rigid != null && !removed.Contains(id))
        {
            state = new(b.Velocity, float.RadiansToDegrees(b.Spin), b.Support?.Id, b.GroundNormal);
            return true;
        }
        state = default;
        return false;
    }

    public bool SetVelocity(Guid id, Vector2 velocity)
    {
        if (!Shape2D.Finite(velocity)) throw new ArgumentException("Velocity must be finite.");
        if (!byId.TryGetValue(id, out var b) || !b.Active || b.Rigid == null || b.Rigid.BodyType == BodyType2D.Static) return false;
        b.Velocity = velocity;
        if (velocity.Y < 0) b.Support = null;
        return true;
    }

    public bool ApplyImpulse(Guid id, Vector2 impulse, Vector2? worldPoint = null)
    {
        if (!Shape2D.Finite(impulse) || worldPoint is {} p && !Shape2D.Finite(p)) throw new ArgumentException("Impulse and point must be finite.");
        if (!byId.TryGetValue(id, out var b) || !b.Active || b.InvMass == 0) return false;
        Impulse(b, impulse, worldPoint ?? Center(b));
        if (impulse.Y < 0) b.Support = null;
        return true;
    }

    public bool DriveCharacter(Guid id, float horizontalSpeed, float jumpSpeed = 0)
    {
        if (!float.IsFinite(horizontalSpeed) || !float.IsFinite(jumpSpeed) || jumpSpeed < 0) throw new ArgumentException("Invalid character speed.");
        if (!byId.TryGetValue(id, out var b) || !b.Active || b.Motor == null || b.InvMass == 0) return false;
        b.WalkSpeed = horizontalSpeed;
        if (jumpSpeed > 0 && b.Support?.Active == true)
        {
            b.Support = null;
            b.Velocity.Y = -jumpSpeed;
        }
        return true;
    }

    private static Vector2 Center(Body b) => Vector2.Transform(b.Size * .5f, b.MatrixAt(b.Position));
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static void Impulse(Body b, Vector2 impulse, Vector2 at)
    {
        b.Velocity += impulse * b.InvMass;
        b.Spin += Cross(at - Center(b), impulse) * b.InvInertia;
    }

    private static Vector2 PointVelocity(Body b, Vector2 arm) => b.Velocity + new Vector2(-arm.Y, arm.X) * b.Spin;

    private void TranslateGroup(Body root, Vector2 delta)
    {
        if (delta == Vector2.Zero) return;
        var path = new MotionPath(default, default, 0, delta, 1);
        foreach (var member in Subtree(root))
            if (member.MotionOwner == root) member.Translate(path);
    }

    private PhysicsHit? CastBody(Body root, Vector2 delta, Body? skip = null, bool floorOnly = false, float lift = 0)
    {
        PhysicsHit? best = null;
        foreach (var member in Subtree(root))
        {
            if (member.MotionOwner != root || !member.Active || member.Collider is not { IsTrigger: false } collider) continue;
            var shape = member.ShapeAt(member.Position - new Vector2(0, lift));
            foreach (var obstacle in Candidates(SweptBounds(shape, delta), collider.Mask, false, null, movingLayer: collider.Layer, ignoreOwner: root))
            {
                var targetBody = byId[obstacle.Target.ObjectId];
                if (skip != null && targetBody.MotionOwner == skip) continue;
                if (root.Rigid!.BodyType == BodyType2D.Kinematic && targetBody.MotionOwner?.InvMass > 0) continue;
                var hit = CollisionGeometry.Cast(shape, delta, obstacle.Shape, false, true);
                if (hit == null || floorOnly && hit.Value.Normal.Y > -root.MinGroundCos) continue;
                var h = hit.Value;
                if (best is {} prior && (h.Time > prior.Fraction || h.Time == prior.Fraction && Compare(obstacle.Target, prior.Target) >= 0)) continue;
                var point = CollisionGeometry.ContactPoint(shape.Translated(delta * h.Time), obstacle.Shape, h.Normal);
                best = new(obstacle.Target, h.Time, h.Time * delta.Length(), point, h.Normal);
            }
        }
        if (Diagnostics is { Queries: true } debug && root.Collider != null)
            debug.Record(new(Time.TickCount, QueryKind.Movement, root.ShapeAt(root.Position), delta, best.HasValue ? 1 : 0, best?.Target, best));
        return best;
    }

    private void SweepBody(Body root, Vector2 delta, bool respond, Body? skip = null)
    {
        for (int pass = 0; pass < 4 && delta.LengthSquared() > 1e-12f; pass++)
        {
            var hit = CastBody(root, delta, skip);
            if (hit == null) { TranslateGroup(root, delta); return; }
            var h = hit.Value;
            TranslateGroup(root, delta * h.Fraction);
            if (respond) ResolveContact(root, byId[h.Target.ObjectId].MotionOwner, h);
            delta *= 1 - h.Fraction;
            if (root.Motor != null && h.Normal.Y < 0 && h.Normal.Y > -root.MinGroundCos && delta.X * h.Normal.X < 0)
                delta.X = 0;
            delta -= Math.Min(0, Vector2.Dot(delta, h.Normal)) * h.Normal;
        }
    }

    private static void ResolveContact(Body a, Body? b, PhysicsHit hit)
    {
        var ra = hit.Point - Center(a);
        var rb = b == null ? default : hit.Point - Center(b);
        var relative = PointVelocity(a, ra) - (b == null ? default : PointVelocity(b, rb));
        float approach = Vector2.Dot(relative, hit.Normal);
        if (approach >= 0) return;
        float armA = Cross(ra, hit.Normal), armB = Cross(rb, hit.Normal);
        float denominator = a.InvMass + (b?.InvMass ?? 0) + armA * armA * a.InvInertia + armB * armB * (b?.InvInertia ?? 0);
        if (denominator <= 0) return;
        float bounce = approach < -25 ? Math.Max(a.Rigid!.Restitution, b?.Rigid?.Restitution ?? 0) : 0;
        var impulse = hit.Normal * (-approach * (1 + bounce) / denominator);
        Impulse(a, impulse, hit.Point);
        if (b?.InvMass > 0) Impulse(b, -impulse, hit.Point);
    }

    private void RotateBody(Body b, float radians)
    {
        if (radians == 0 || b.Rigid!.FreezeRotation || b.Motor != null) return;
        // Subdivide angular travel and reject a pose that enters solid geometry.
        var group = Subtree(b);
        var center = Center(b);
        int steps = Math.Clamp((int)MathF.Ceiling(Math.Abs(radians) / .06f), 1, 64);
        float step = radians / steps;
        var turn = Matrix3x2.CreateRotation(step, center);
        for (int i = 0; i < steps; i++)
        {
            foreach (var member in group)
            {
                if (member.MotionOwner != b || !member.Active || member.Collider is not { IsTrigger: false } c) continue;
                var point = Vector2.Transform(member.Position, turn);
                var next = member.ShapeAt(point, member.Angle + step);
                foreach (var obstacle in Candidates(next.Bounds, c.Mask, false, null, movingLayer: c.Layer, ignoreOwner: b))
                    if (CollisionGeometry.Overlap(next, obstacle.Shape) && (obstacle.Shape.FaceNormal == default ||
                        CollisionGeometry.FaceGap(member.ShapeAt(member.Position), obstacle.Shape) >= -.0001f))
                    { b.Spin = 0; return; }
            }
            foreach (var member in group)
            {
                if (member.MotionOwner != b) continue;
                var delta = Vector2.Transform(member.Position, turn) - member.Position;
                member.Translate(new(default, default, 0, delta, 1), step);
            }
        }
    }

    private void MovePlatform(Body platform, Vector2 delta)
    {
        if (delta == Vector2.Zero) return;
        // A prescribed platform stops when a rider would be crushed against a solid.
        // Passengers are carried in the dynamic phase; side contacts push only after impact.
        float limit = CastBody(platform, delta)?.Fraction ?? 1;
        var pushes = new Dictionary<Body, float>();
        foreach (var b in rigidBodies)
        {
            if (!b.Active || b.InvMass == 0 || b.Support?.MotionOwner != platform) continue;
            var blocked = CastBody(b, delta, platform);
            if (blocked is {} h) limit = Math.Min(limit, h.Fraction);
        }
        foreach (var part in Subtree(platform))
        {
            if (part.MotionOwner != platform || !part.Active || part.Collider is not { IsTrigger: false } c) continue;
            var shape = part.ShapeAt(part.Position);
            foreach (var obstacle in Candidates(SweptBounds(shape, delta), c.Mask, false, null, movingLayer: c.Layer, ignoreOwner: platform))
            {
                var other = byId[obstacle.Target.ObjectId].MotionOwner;
                if (other == null || other.InvMass == 0 || other.Support?.MotionOwner == platform) continue;
                var hit = CollisionGeometry.Cast(shape, delta, obstacle.Shape, false, true);
                if (hit is {} h && h.Time < limit)
                    pushes[other] = Math.Min(pushes.GetValueOrDefault(other, 1), h.Time);
            }
        }
        foreach (var (other, time) in pushes)
        {
            if (time >= limit) continue;
            if (CastBody(other, delta * (limit - time), platform) is {} stop)
                limit = time + (limit - time) * stop.Fraction;
        }
        foreach (var (other, time) in pushes)
            if (time < limit) TranslateGroup(other, delta * (limit - time));
        TranslateGroup(platform, delta * limit);
    }

    private void StepRigidBodies(float dt)
    {
        foreach (var b in rigidBodies)
        {
            if (!b.Active || b.Spawning || b.Rigid!.BodyType != BodyType2D.Kinematic) continue;
            MovePlatform(b, b.Velocity * dt + b.Displacement);
            RotateBody(b, b.Spin * dt);
        }
        foreach (var b in rigidBodies)
        {
            if (!b.Active || b.Spawning || b.InvMass == 0) continue;
            var prior = b.Support;
            if (prior != null && !prior.Active) prior = b.Support = null;
            if (prior?.MotionOwner is { Rigid.BodyType: BodyType2D.Kinematic } platform)
            {
                var oldPose = prior.MatrixAt(prior.At(0), prior.StartAngle);
                var local = Vector2.Transform(Center(b), Affine2D.Inverse(oldPose));
                var carried = Vector2.Transform(local, prior.MatrixAt(prior.Position)) - Center(b);
                SweepBody(b, carried, false, platform);
            }
            if (b.Motor != null && b.WalkSpeed is float speed)
            {
                b.Velocity.X = speed;
                if (prior != null) b.Velocity.Y = -speed * b.GroundNormal.X / b.GroundNormal.Y;
            }
            if (b.Motor == null || prior == null) b.Velocity.Y += b.Rigid!.Gravity * dt;
            // A descending motor can acquire upward tangent velocity on first contact with a ramp.
            // Preserve its eligibility for the ground probe across that collision response.
            bool descendingMotor = b.Motor != null && b.Velocity.Y >= 0;
            SweepBody(b, b.Velocity * dt + b.Displacement, true);
            RotateBody(b, b.Spin * dt);
            var snap = b.Motor != null && prior != null ? Math.Max(.08f, b.Motor.GroundSnap) : .08f;
            // Recover rounding on both sides of a resting surface instead of accumulating
            // tiny penetrations that can expose a neighboring tile's internal edge.
            float lift = b.Motor != null && prior != null ? .002f : 0;
            var floor = (prior != null || descendingMotor || b.Velocity.Y >= -.01f)
                ? CastBody(b, new(0, snap + lift), floorOnly: true, lift: lift) : null;
            b.Support = floor is {} grounded ? byId[grounded.Target.ObjectId] : null;
            if (floor is {} ground)
            {
                b.GroundNormal = ground.Normal;
                if (b.Motor != null)
                {
                    TranslateGroup(b, new(0, ground.Distance - lift));
                    b.Velocity.Y = 0;
                }
            }
        }
        for (int iteration = 0; iteration < 6; iteration++)
            foreach (var b in joints) SolveDistance(b);
    }

    private void SolveDistance(Body b)
    {
        if (!b.Active || b.Joint?.Connected is not Guid id || !byId.TryGetValue(id, out var connected) || !connected.Active) return;
        var owner = b.MotionOwner;
        var target = connected.MotionOwner;
        if (owner != null && owner == target) return;
        float a = owner?.InvMass ?? 0, c = target?.InvMass ?? 0;
        if (a + c == 0) return;
        var axis = Center(b) - Center(connected);
        float distance = axis.Length();
        axis = distance > 1e-6f ? axis / distance : Vector2.UnitX;
        var correction = axis * ((b.Joint.Length - distance) / (a + c));
        if (a > 0) SweepBody(owner!, correction * a, false);
        if (c > 0) SweepBody(target!, -correction * c, false);
        var speed = Vector2.Dot((owner?.Velocity ?? default) - (target?.Velocity ?? default), axis) / (a + c);
        if (a > 0) owner!.Velocity -= axis * (speed * a);
        if (c > 0) target!.Velocity += axis * (speed * c);
    }
}
