using System.ComponentModel;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

public enum BodyType2D { Dynamic, Kinematic, Static }

[DisplayName("Rigidbody 2D"), YamlObject]
public sealed partial record Rigidbody2DComponent : SceneComponent
{
    public const float DefaultFriction = .6f;
    public Rigidbody2DComponent() { }
    // YAML fills omitted members from these defaults, not from the property initializers.
    [YamlConstructor] public Rigidbody2DComponent(float mass = 1, float gravity = 980, float friction = DefaultFriction) =>
        (Mass, Gravity, Friction) = (mass, gravity, friction);
    public BodyType2D BodyType { get; set; }
    public float Mass { get; set; } = 1;
    public float Gravity { get; set; } = 980;
    public float Restitution { get; set; }
    /// <summary>Box2D Coulomb friction of this body's colliders. Touching surfaces combine as sqrt(a·b).</summary>
    public float Friction { get; set; } = DefaultFriction;
    public bool FreezeRotation { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float AngularVelocity { get; set; }

    internal void Validate(string owner)
    {
        if (!Enum.IsDefined(BodyType) || !float.IsFinite(Mass) || Mass <= 0 || !float.IsFinite(Gravity) ||
            Restitution is not (>= 0 and <= 1) || !float.IsFinite(VelocityX) || !float.IsFinite(VelocityY) || !float.IsFinite(AngularVelocity))
            throw new InvalidDataException($"{owner}: invalid Rigidbody 2D mass, gravity, restitution or velocity.");
        if (!float.IsFinite(Friction) || Friction is < 0 or > 100)
            throw new InvalidDataException($"{owner}: Rigidbody 2D friction must be between 0 and 100.");
    }
}

/// <summary>A Box2D joint from this object to <see cref="Connected"/>, or to the world when it is empty.</summary>
public abstract record Joint2DComponent : SceneComponent
{
    public Guid? Connected { get; set; }
    internal virtual void Validate(SceneObject owner)
    {
        if (Connected == owner.Id) throw new InvalidDataException($"{owner.Name}: a joint cannot connect an object to itself.");
    }
    private protected static bool ValidSpring(float hertz, float damping) => float.IsFinite(hertz) && hertz >= 0 && float.IsFinite(damping) && damping >= 0;
}

[DisplayName("Distance Joint 2D"), YamlObject]
public sealed partial record DistanceJoint2DComponent : Joint2DComponent
{
    public DistanceJoint2DComponent() { }
    [YamlConstructor] public DistanceJoint2DComponent(float length = 64, float hertz = 4, float dampingRatio = .5f) =>
        (Length, Hertz, DampingRatio) = (length, hertz, dampingRatio);
    public float Length { get; set; } = 64;
    /// <summary>A soft rope: Box2D's distance spring pulls toward Length at Hertz with DampingRatio.</summary>
    public bool Spring { get; set; }
    public float Hertz { get; set; } = 4;
    public float DampingRatio { get; set; } = .5f;
    internal override void Validate(SceneObject owner)
    {
        base.Validate(owner);
        if (!float.IsFinite(Length) || Length < 0 || !ValidSpring(Hertz, DampingRatio))
            throw new InvalidDataException($"{owner.Name}: invalid joint length or spring.");
    }
}

/// <summary>Box2D revolute joint. The pivot is offset from this object's center in its local pixels; angles are
/// this object's rotation relative to the connected body, in degrees (clockwise on screen).</summary>
[DisplayName("Revolute Joint 2D"), YamlObject]
public sealed partial record RevoluteJoint2DComponent : Joint2DComponent
{
    public RevoluteJoint2DComponent() { }
    [YamlConstructor] public RevoluteJoint2DComponent(float lowerAngle = -45, float upperAngle = 45, float maxMotorTorque = 100000) =>
        (LowerAngle, UpperAngle, MaxMotorTorque) = (lowerAngle, upperAngle, maxMotorTorque);
    public float AnchorX { get; set; }
    public float AnchorY { get; set; }
    public bool Limit { get; set; }
    public float LowerAngle { get; set; } = -45;
    public float UpperAngle { get; set; } = 45;
    public bool Motor { get; set; }
    public float MotorSpeed { get; set; }
    public float MaxMotorTorque { get; set; } = 100000;
    internal override void Validate(SceneObject owner)
    {
        base.Validate(owner);
        if (!float.IsFinite(AnchorX) || !float.IsFinite(AnchorY) || !float.IsFinite(MotorSpeed) || !(MaxMotorTorque >= 0) ||
            !float.IsFinite(MaxMotorTorque) || LowerAngle is not (>= -180 and <= 180) || UpperAngle is not (>= -180 and <= 180) || LowerAngle > UpperAngle)
            throw new InvalidDataException($"{owner.Name}: invalid revolute anchor, angle limits (−180° to 180°) or motor.");
    }
}

/// <summary>Box2D prismatic (slider) joint along a world axis fixed at creation. Translation is in pixels from the
/// starting pose; the optional spring pulls back toward it.</summary>
[DisplayName("Slider Joint 2D"), YamlObject]
public sealed partial record PrismaticJoint2DComponent : Joint2DComponent
{
    public PrismaticJoint2DComponent() { }
    [YamlConstructor] public PrismaticJoint2DComponent(float axisX = 1, float lower = -64, float upper = 64, float maxMotorForce = 10000,
        float hertz = 4, float dampingRatio = .5f) =>
        (AxisX, Lower, Upper, MaxMotorForce, Hertz, DampingRatio) = (axisX, lower, upper, maxMotorForce, hertz, dampingRatio);
    public float AxisX { get; set; } = 1;
    public float AxisY { get; set; }
    public bool Limit { get; set; }
    public float Lower { get; set; } = -64;
    public float Upper { get; set; } = 64;
    public bool Motor { get; set; }
    public float MotorSpeed { get; set; }
    public float MaxMotorForce { get; set; } = 10000;
    public bool Spring { get; set; }
    public float Hertz { get; set; } = 4;
    public float DampingRatio { get; set; } = .5f;
    internal override void Validate(SceneObject owner)
    {
        base.Validate(owner);
        if (!float.IsFinite(AxisX) || !float.IsFinite(AxisY) || AxisX * AxisX + AxisY * AxisY < 1e-6f || !float.IsFinite(Lower) ||
            !float.IsFinite(Upper) || Lower > Upper || !float.IsFinite(MotorSpeed) || !(MaxMotorForce >= 0) || !float.IsFinite(MaxMotorForce) ||
            !ValidSpring(Hertz, DampingRatio))
            throw new InvalidDataException($"{owner.Name}: invalid slider axis, limits, motor or spring.");
    }
}

/// <summary>Box2D weld joint. Zero stiffness is rigid; a positive Hertz makes a springy weld.</summary>
[DisplayName("Weld Joint 2D"), YamlObject]
public sealed partial record WeldJoint2DComponent : Joint2DComponent
{
    public WeldJoint2DComponent() { }
    [YamlConstructor] public WeldJoint2DComponent(float dampingRatio = .7f) => DampingRatio = dampingRatio;
    public float LinearHertz { get; set; }
    public float AngularHertz { get; set; }
    public float DampingRatio { get; set; } = .7f;
    internal override void Validate(SceneObject owner)
    {
        base.Validate(owner);
        if (!ValidSpring(LinearHertz, DampingRatio) || !ValidSpring(AngularHertz, DampingRatio))
            throw new InvalidDataException($"{owner.Name}: invalid weld spring.");
    }
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

// Dynamic bodies and joints are simulated by Box2D (ScenePhysicsWorld.cs). A Character Motor is a kinematic
// controller, the approach Box2D recommends for platformer characters: it sweeps with Box2D shape casts here and
// appears to Box2D as a kinematic body that pushes and loads dynamic bodies.
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
        if (Simulated(b)) b.Physics.SetLinearVelocity(CollisionGeometry.F(velocity));
        if (velocity.Y < 0) b.Support = null;
        return true;
    }

    public bool ApplyImpulse(Guid id, Vector2 impulse, Vector2? worldPoint = null)
    {
        if (!Shape2D.Finite(impulse) || worldPoint is {} p && !Shape2D.Finite(p)) throw new ArgumentException("Impulse and point must be finite.");
        if (!byId.TryGetValue(id, out var b) || !b.Active || b.InvMass == 0) return false;
        if (Simulated(b))
        {
            if (worldPoint is {} at) b.Physics.ApplyLinearImpulse(CollisionGeometry.F(impulse), CollisionGeometry.F(at), true);
            else b.Physics.ApplyLinearImpulseToCenter(CollisionGeometry.F(impulse), true);
            b.Velocity = CollisionGeometry.V(b.Physics.GetLinearVelocity());
            b.Spin = b.Physics.GetAngularVelocity();
        }
        else b.Velocity += impulse * b.InvMass;
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

    private static Vector2 Center(Body b) => Affine2D.Transform(b.Size * .5f, b.MatrixAt(b.Position));

    private void TranslateGroup(Body root, Vector2 delta)
    {
        if (delta == Vector2.Zero) return;
        var path = new MotionPath(default, default, 0, delta, 1);
        foreach (var member in Subtree(root))
            if (member.MotionOwner == root) member.Translate(path);
    }

    // Moves and turns a rigid hierarchy to a new root pose; members keep their offsets from the root.
    private void PoseGroup(Body root, Vector2 position, float angle)
    {
        float turn = angle - root.Angle;
        if (turn == 0) { TranslateGroup(root, position - root.Position); return; }
        var rotation = Matrix3x2.CreateRotation(turn);
        var origin = root.Position;
        foreach (var member in Subtree(root))
        {
            if (member.MotionOwner != root) continue;
            var target = position + Affine2D.TransformNormal(member.Position - origin, rotation);
            member.Translate(new(default, default, 0, target - member.Position, 1), turn);
        }
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
                if (best is {} prior && (h.Time > prior.Fraction + TieFraction(delta) || h.Time >= prior.Fraction - TieFraction(delta) && Compare(obstacle.Target, prior.Target) >= 0)) continue;
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
            delta *= 1 - h.Fraction;
            // A walking character steps over a lip of at most 1 px, such as the corner where a jointed Box2D plank
            // meets a ledge, when the way ahead is then clear. Slopes and real walls still stop it.
            if (root.Motor != null && respond && root.Support != null && h.Normal.Y > -root.MinGroundCos && delta.X * h.Normal.X < 0 &&
                CastBody(root, new(0, -1), skip) == null && CastBody(root, new(delta.X, 0), skip, lift: 1) == null)
            {
                TranslateGroup(root, new(0, -1));
                continue;
            }
            if (respond) ResolveContact(root, byId[h.Target.ObjectId].MotionOwner, h);
            if (root.Motor != null && h.Normal.Y < 0 && h.Normal.Y > -root.MinGroundCos && delta.X * h.Normal.X < 0)
                delta.X = 0;
            delta -= Math.Min(0, Vector2.Dot(delta, h.Normal)) * h.Normal;
        }
    }

    // A character stops against what it hits. A dynamic Box2D body takes the impulse instead.
    private void ResolveContact(Body a, Body? b, PhysicsHit hit)
    {
        var other = b != null && Simulated(b) ? b : null;
        var relative = a.Velocity - (b?.Velocity ?? default);
        float approach = Vector2.Dot(relative, hit.Normal);
        if (approach >= 0) return;
        float denominator = a.InvMass + (other?.InvMass ?? 0);
        if (denominator <= 0) return;
        float bounce = approach < -25 ? Math.Max(a.Rigid!.Restitution, b?.Rigid?.Restitution ?? 0) : 0;
        var impulse = hit.Normal * (-approach * (1 + bounce) / denominator);
        a.Velocity += impulse * a.InvMass;
        if (other != null)
        {
            other.Physics.ApplyLinearImpulse(CollisionGeometry.F(-impulse), CollisionGeometry.F(hit.Point), true);
            other.Velocity = CollisionGeometry.V(other.Physics.GetLinearVelocity());
        }
    }

    private void MovePlatform(Body platform, Vector2 delta)
    {
        if (delta == Vector2.Zero) return;
        // A prescribed platform stops when a rider would be crushed against a solid.
        // Passengers are carried in the character phase; side contacts push only after impact.
        float limit = CastBody(platform, delta)?.Fraction ?? 1;
        var pushes = new Dictionary<Body, float>();
        foreach (var b in rigidBodies)
        {
            if (!b.Active || b.Motor == null || b.Support?.MotionOwner != platform) continue;
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
                if (other == null || other.Motor == null || other.Support?.MotionOwner == platform) continue;
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
        SyncPhysics();
        foreach (var b in rigidBodies)
        {
            if (!b.Active || b.Spawning || b.Rigid!.BodyType != BodyType2D.Kinematic) continue;
            MovePlatform(b, b.Velocity * dt + b.Displacement);
            if (b.Spin != 0 && !b.Rigid.FreezeRotation) PoseGroup(b, b.Position, b.Angle + b.Spin * dt);
        }
        StepPhysics(dt);
        foreach (var b in rigidBodies)
        {
            if (!b.Active || b.Spawning || b.Motor == null) continue;
            var prior = b.Support;
            if (prior != null && !prior.Active) prior = b.Support = null;
            // Ride whatever rigid body carried the support this tick: a kinematic platform or a Box2D body.
            if (prior?.MotionOwner is { Rigid: not null } platform && platform != b && prior.Moved)
            {
                var oldPose = prior.MatrixAt(prior.At(0), prior.StartAngle);
                var local = Affine2D.Transform(Center(b), Affine2D.Inverse(oldPose));
                var carried = Affine2D.Transform(local, prior.MatrixAt(prior.Position)) - Center(b);
                SweepBody(b, carried, false, platform);
            }
            if (b.WalkSpeed is float speed)
            {
                b.Velocity.X = speed;
                if (prior != null) b.Velocity.Y = -speed * b.GroundNormal.X / b.GroundNormal.Y;
            }
            if (prior == null) b.Velocity.Y += b.Rigid!.Gravity * dt;
            // A descending motor can acquire upward tangent velocity on first contact with a ramp.
            // Preserve its eligibility for the ground probe across that collision response.
            bool descendingMotor = b.Velocity.Y >= 0;
            SweepBody(b, b.Velocity * dt + b.Displacement, true);
            var snap = prior != null ? Math.Max(.08f, b.Motor.GroundSnap) : .08f;
            // Recover rounding on both sides of a resting surface instead of accumulating
            // tiny penetrations that can expose a neighboring tile's internal edge.
            float lift = prior != null ? .002f : 0;
            var floor = (prior != null || descendingMotor || b.Velocity.Y >= -.01f)
                ? CastBody(b, new(0, snap + lift), floorOnly: true, lift: lift) : null;
            b.Support = floor is {} grounded ? byId[grounded.Target.ObjectId] : null;
            if (floor is {} ground)
            {
                b.GroundNormal = ground.Normal;
                TranslateGroup(b, new(0, ground.Distance - lift));
                b.Velocity.Y = 0;
                // The character's weight presses on a Box2D body it stands on (a bridge plank, a pulley deck).
                if (b.Support.MotionOwner is {} owner && Simulated(owner))
                    owner.Physics.ApplyForce(CollisionGeometry.F(new(0, b.Rigid!.Mass * b.Rigid.Gravity)), CollisionGeometry.F(ground.Point), true);
            }
        }
        PlaceCharacters(dt);
    }
}
