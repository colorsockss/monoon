using System.Numerics;
using Brutal.Box2DApi;
using Brutal.Numerics;
using static Engine.Core.CollisionGeometry;

namespace Engine.Core;

// One Box2D world per running scene that has a Rigidbody 2D. Box2D steps dynamic bodies, joints, friction and
// restitution. MoNoon keeps the authored hierarchy: each rigid root is one Box2D body whose shapes are the
// colliders it moves, other solid colliders and tile layers are static bodies, and triggers stay out of the
// world (contacts and trigger events use the same Box2D narrow phase in SceneContacts.cs).
public sealed partial class SceneGame
{
    // Box2D's speculative margin and slop scale with this; one unit is one MoNoon pixel.
    private const int SubSteps = 4;
    // A character's kinematic Box2D shape stops this far above its feet: the controller already carries its weight to
    // the body it stands on, and an infinitely heavy kinematic overlap would stretch a jointed bridge.
    private const float Feet = 2;
    private B2World physicsWorld;
    private B2Body physicsGround;
    private readonly List<Body> physicsOwners = [];
    private readonly HashSet<Body> physicsOwnerSet = [];

    // Box2D inline math (b2Rot_GetAngle, b2RelativeAngle) is not exported by the native library.
    private static float Angle(B2Rot q) => MathF.Atan2(q.S, q.C);

    private bool Simulated(Body b) => !b.Physics.IsNull() && b.Rigid is { BodyType: BodyType2D.Dynamic } && b.Motor == null;

    private void EnsurePhysicsWorld()
    {
        if (!physicsWorld.IsNull()) return;
        B2WorldDef def;
        try { def = B2WorldDef.Default; }
        catch (Exception missing) when (EngineFeatures.Missing(missing)) { throw EngineFeatures.Excluded(EngineFeature.Physics, missing); }
        def.Gravity = new(0, 1); // Each body scales it by its own Rigidbody 2D gravity (px/s²).
        def.RestitutionThreshold = 25;
        def.MaxContactPushSpeed = 300;
        def.MaximumLinearSpeed = 100000;
        def.EnableSleep = false;
        def.WorkerCount = 0; // Single-threaded: the same input gives the same positions.
        physicsWorld = Box2D.CreateWorld(in def);
        var ground = B2BodyDef.Default;
        physicsGround = physicsWorld.CreateBody(in ground);
    }

    private void DisposePhysics()
    {
        if (!EngineFeatures.Physics) return;
        if (physicsWorld.IsNull()) return;
        physicsWorld.Dispose();
        physicsWorld = default;
        physicsGround = default;
        physicsOwners.Clear();
        physicsOwnerSet.Clear();
        foreach (var b in byId.Values) { b.Physics = default; b.PhysicsJoints = null; }
    }

    private bool PhysicsOwner(Body b) => b.Rigid != null ||
        b.MotionOwner == null && (b.ColliderConfig is { IsTrigger: false } || b.Map != null && b.Palette != null);

    private void SyncPhysics()
    {
        if (rigidBodies.Length == 0) return;
        EnsurePhysicsWorld();
        // Bodies destroyed since the last tick leave the world with their joints.
        for (int i = physicsOwners.Count - 1; i >= 0; i--)
        {
            var b = physicsOwners[i];
            if (byId.TryGetValue(b.Id, out var live) && live == b && !removed.Contains(b.Id) && PhysicsOwner(b)) continue;
            if (!b.Physics.IsNull()) b.Physics.Dispose();
            b.Physics = default;
            b.PhysicsJoints = null;
            physicsOwners.RemoveAt(i);
            physicsOwnerSet.Remove(b);
        }
        foreach (var b in parentFirst)
        {
            if (!PhysicsOwner(b)) continue;
            bool enabled = b.Active && !b.Spawning;
            if (b.Physics.IsNull() || !ShapesCurrent(b))
            {
                if (!enabled && b.Physics.IsNull()) continue;
                CreatePhysicsBody(b);
            }
            // Only SyncPhysics enables or disables Box2D bodies, so the managed flag saves a native call per body.
            if (enabled != b.PhysicsEnabled)
            {
                if (enabled) b.Physics.Enable(); else b.Physics.Disable();
                b.PhysicsEnabled = enabled;
            }
            if (enabled && (b.Position != b.PhysicsPosition || b.Angle != b.PhysicsAngle))
            {
                b.Physics.SetTransform(F(b.Position), new B2Rot(b.Angle));
                (b.PhysicsPosition, b.PhysicsAngle) = (b.Position, b.Angle);
            }
        }
        foreach (var b in joints)
        {
            if (!b.Active) continue;
            b.PhysicsJoints ??= new B2Joint?[b.Joints.Length];
            for (int i = 0; i < b.Joints.Length; i++)
                if (b.PhysicsJoints[i] is not {} joint || !joint.IsValid()) b.PhysicsJoints[i] = CreateJoint(b, b.Joints[i]);
        }
    }

    // The exact inputs of a Box2D body's shapes. A HashCode signature could collide and is seeded per process, so two
    // runs of the same game could rebuild bodies on different ticks.
    internal readonly record struct ShapeKey(Tilemap2DComponent? Map, long Loads, long Unloads, int Edits, int Pose,
        Vector2 Position, float Angle, bool Solid);

    // Changes when a collider of this Box2D body is enabled, disabled, resized or reparented, or a tile layer edits.
    private static ShapeKey KeyOf(Body b) => b.Map != null && b.Rigid == null
        ? new(b.Map, b.Stream?.State.Loads ?? -1, b.Stream?.State.Unloads ?? -1, b.Stream?.Edits ?? -1, b.PoseRevision, b.Position, b.Angle, false)
        : new(null, 0, 0, 0, b.PoseRevision, default, 0, b.Collider is { IsTrigger: false });

    private bool ShapesCurrent(Body b)
    {
        if (b.Rigid == null) return b.PhysicsKey == KeyOf(b);
        var members = b.PhysicsMembers!;
        int n = 0;
        foreach (var member in Subtree(b))
        {
            if (member.MotionOwner != b && member != b) continue;
            if (n == members.Count || members[n] != (member, member.Collider is { IsTrigger: false }, member.PoseRevision)) return false;
            n++;
        }
        return n == members.Count;
    }

    private void RecordShapes(Body b)
    {
        if (b.Rigid == null) { b.PhysicsKey = KeyOf(b); return; }
        var members = b.PhysicsMembers ??= [];
        members.Clear();
        foreach (var member in Subtree(b))
            if (member.MotionOwner == b || member == b) members.Add((member, member.Collider is { IsTrigger: false }, member.PoseRevision));
    }

    private void CreatePhysicsBody(Body b)
    {
        // Its joints go too; SyncPhysics recreates every joint that is no longer valid.
        if (!b.Physics.IsNull()) b.Physics.Dispose();
        var rigid = b.Rigid;
        var def = B2BodyDef.Default;
        bool tiles = b.Map != null && rigid == null;
        def.Type = rigid == null || rigid.BodyType == BodyType2D.Static ? B2BodyType.Static :
            rigid.BodyType == BodyType2D.Kinematic || b.Motor != null ? B2BodyType.Kinematic : B2BodyType.Dynamic;
        // Tile shapes are built in world coordinates.
        var origin = tiles ? Vector2.Zero : b.Position;
        float angle = tiles ? 0 : b.Angle;
        def.Position = F(origin);
        def.Rotation = new B2Rot(angle);
        if (def.Type != B2BodyType.Static)
        {
            def.LinearVelocity = F(b.Velocity);
            def.AngularVelocity = b.Spin;
        }
        def.GravityScale = def.Type == B2BodyType.Dynamic ? rigid!.Gravity : 0;
        def.FixedRotation = rigid != null && (rigid.FreezeRotation || b.Motor != null);
        def.EnableSleep = false;
        b.Physics = physicsWorld.CreateBody(in def);
        b.PhysicsEnabled = true; // B2BodyDef.Default.IsEnabled
        RecordShapes(b);
        (b.PhysicsPosition, b.PhysicsAngle) = (b.Position, b.Angle);
        if (physicsOwnerSet.Add(b)) physicsOwners.Add(b);

        var shape = B2ShapeDef.Default;
        shape.UpdateBodyMass = false;
        shape.Material.Friction = rigid?.Friction ?? Rigidbody2DComponent.DefaultFriction;
        shape.Material.Restitution = rigid?.Restitution ?? 0;
        if (tiles) AddTileShapes(b, shape);
        else
            foreach (var member in rigid != null ? Subtree(b) : [b])
            {
                if (member != b && member.MotionOwner != b || member.Collider is not { IsTrigger: false } collider) continue;
                shape.Filter = new B2Filter { CategoryBits = 1ul << collider.Layer, MaskBits = collider.Mask, GroupIndex = 0 };
                AddShape(b.Physics, member.ShapeAt(member.Position), origin, angle, shape, b.Motor != null ? Feet : 0);
            }
        if (def.Type != B2BodyType.Dynamic) return;
        b.Physics.ApplyMassFromShapes();
        var mass = b.Physics.GetMassData();
        if (mass.Mass > 0)
        {
            float scale = rigid!.Mass / mass.Mass;
            mass.Mass = rigid.Mass;
            mass.RotationalInertia *= scale;
        }
        else
        {
            var size = b.Size;
            mass = new B2MassData { Mass = rigid!.Mass, Center = F(size * .5f), RotationalInertia = rigid.Mass * (size.X * size.X + size.Y * size.Y) / 12 };
        }
        b.Physics.SetMassData(mass);
    }

    private static Vector2 Local(Vector2 world, Vector2 origin, float angle) =>
        Affine2D.TransformNormal(world - origin, Matrix3x2.CreateRotation(-angle));

    private static unsafe void AddShape(B2Body body, Shape2D s, Vector2 origin, float angle, B2ShapeDef def, float feet = 0)
    {
        if (s.Parts != null)
        {
            // An edge collider is a Box2D chain: smooth along its vertices and one-sided. An open chain's first and last
            // points are ghosts, extended along its end segments.
            int n = s.Vertices!.Length;
            if (s.Parts.Length == 0) return;
            bool loop = s.Parts.Length == n;
            var points = new float2[loop ? n : n + 2];
            for (int i = 0; i < n; i++) points[loop ? i : i + 1] = F(Local(s.Vertices[i] + s.Offset, origin, angle));
            if (!loop)
            {
                points[0] = points[1] + (points[1] - points[2]);
                points[n + 1] = points[n] + (points[n] - points[n - 1]);
            }
            fixed (float2* p = points)
            {
                var chainDef = B2ChainDef.Default;
                chainDef.Points = p;
                chainDef.Count = points.Length;
                chainDef.IsLoop = loop;
                chainDef.Filter = def.Filter;
                chainDef.Materials = &def.Material;
                chainDef.MaterialCount = 1;
                body.CreateChain(in chainDef);
            }
        }
        else if (s.Poly is { Polygon.Count: 2 } round)
        {
            // A capsule. A character's capsule stops Feet above its lowest point, like its polygon below.
            var a = Local(s.Vertices![0] + s.Offset, origin, angle);
            var b = Local(s.Vertices[1] + s.Offset, origin, angle);
            float r = round.Polygon.Radius, top = Math.Max(a.Y, b.Y) - feet;
            var capsule = new B2Capsule { Center1 = F(a with { Y = Math.Min(a.Y, top) }), Center2 = F(b with { Y = Math.Min(b.Y, top) }), Radius = r };
            if (r > 0) body.CreateCapsuleShape(in def, in capsule);
        }
        else if (s.Poly != null)
        {
            Span<float2> points = stackalloc float2[s.Vertices!.Length];
            float bottom = float.MinValue;
            foreach (var v in s.Vertices) bottom = Math.Max(bottom, Local(v + s.Offset, origin, angle).Y);
            for (int i = 0; i < points.Length; i++)
            {
                var p = Local(s.Vertices[i] + s.Offset, origin, angle);
                points[i] = F(p with { Y = Math.Min(p.Y, bottom - feet) });
            }
            var hull = Box2D.ComputeHull(points, points.Length);
            if (hull.Count < 3) return; // A degenerate sliver cannot touch anything.
            var polygon = Box2D.MakePolygon(in hull, 0);
            body.CreatePolygonShape(in def, in polygon);
        }
        else if (s.FaceNormal != default)
        {
            // A one-way face is a Box2D chain segment, which collides only on its right side; ghost points keep it straight.
            var a = Local(s.Vertices![0] + s.Offset, origin, angle);
            var c = Local(s.Vertices[1] + s.Offset, origin, angle);
            var normal = Affine2D.TransformNormal(s.FaceNormal, Matrix3x2.CreateRotation(-angle));
            var edge = c - a;
            if (Vector2.Dot(new(edge.Y, -edge.X), normal) < 0) { (a, c) = (c, a); edge = -edge; }
            var chain = stackalloc float2[] { F(a - edge), F(a), F(c), F(c + edge) };
            var chainDef = B2ChainDef.Default;
            chainDef.Points = chain;
            chainDef.Count = 4;
            chainDef.Filter = def.Filter;
            chainDef.Materials = &def.Material;
            chainDef.MaterialCount = 1;
            body.CreateChain(in chainDef);
        }
        else if (s.Radius > 0)
        {
            var circle = new B2Circle { Center = F(Local(s.Offset, origin, angle) - new Vector2(0, feet * .5f)), Radius = Math.Max(.5f, s.Radius - feet * .5f) };
            body.CreateCircleShape(in def, in circle);
        }
    }

    private static void AddTileShapes(Body layer, B2ShapeDef def)
    {
        var map = layer.Map!;
        def.Filter = new B2Filter { CategoryBits = 1ul << map.CollisionLayer, MaskBits = map.CollisionMask, GroupIndex = 0 };
        var matrix = layer.MatrixAt(layer.Position);
        foreach (var chunk in map.Chunks)
            foreach (var cell in chunk.Cells)
            {
                var point = new System.Drawing.Point(chunk.X * 32 + (cell.Index & 31), chunk.Y * 32 + (cell.Index >> 5));
                if (layer.Palette!.Tiles.GetValueOrDefault(cell.Tile) is not {} tile ||
                    TileCollision.WorldShape(tile, map.Bounds(Vector2.Zero, point), map.Layout, matrix) is not {} shape) continue;
                AddShape(layer.Physics, shape, Vector2.Zero, 0, def);
            }
    }

    private B2Body PhysicsBodyOf(Body b) =>
        b.MotionOwner is {} owner && !owner.Physics.IsNull() ? owner.Physics : !b.Physics.IsNull() ? b.Physics : physicsGround;

    private B2Joint? CreateJoint(Body b, Joint2DComponent component)
    {
        Body? other = null;
        if (component.Connected is Guid id && (!byId.TryGetValue(id, out other) || !other.Active || removed.Contains(id))) return null;
        var self = PhysicsBodyOf(b);
        var target = other == null ? physicsGround : PhysicsBodyOf(other);
        if (self.Equals(target)) return null;
        var center = Center(b);
        float reference = (float)Math.IEEERemainder(Angle(self.GetRotation()) - Angle(target.GetRotation()), Math.Tau);
        switch (component)
        {
            case DistanceJoint2DComponent distance:
            {
                var def = B2DistanceJointDef.Default;
                (def.BodyIdA, def.BodyIdB) = (self, target);
                def.LocalAnchorA = self.GetLocalPoint(F(center));
                def.LocalAnchorB = target.GetLocalPoint(F(other == null ? center : Center(other)));
                def.Length = Math.Max(distance.Length, .01f);
                def.EnableSpring = distance.Spring;
                def.Hertz = distance.Hertz;
                def.DampingRatio = distance.DampingRatio;
                return physicsWorld.CreateDistanceJoint(in def);
            }
            case RevoluteJoint2DComponent revolute:
            {
                var pivot = F(Affine2D.Transform(b.Size * .5f + new Vector2(revolute.AnchorX, revolute.AnchorY), b.MatrixAt(b.Position)));
                var def = B2RevoluteJointDef.Default;
                (def.BodyIdA, def.BodyIdB) = (target, self);
                def.LocalAnchorA = target.GetLocalPoint(pivot);
                def.LocalAnchorB = self.GetLocalPoint(pivot);
                def.ReferenceAngle = reference;
                def.EnableLimit = revolute.Limit;
                def.LowerAngle = float.DegreesToRadians(revolute.LowerAngle);
                def.UpperAngle = float.DegreesToRadians(revolute.UpperAngle);
                def.EnableMotor = revolute.Motor;
                def.MotorSpeed = float.DegreesToRadians(revolute.MotorSpeed);
                def.MaxMotorTorque = revolute.MaxMotorTorque;
                return physicsWorld.CreateRevoluteJoint(in def);
            }
            case PrismaticJoint2DComponent slider:
            {
                var def = B2PrismaticJointDef.Default;
                (def.BodyIdA, def.BodyIdB) = (target, self);
                def.LocalAnchorA = target.GetLocalPoint(F(center));
                def.LocalAnchorB = self.GetLocalPoint(F(center));
                def.LocalAxisA = target.GetLocalVector(F(Vector2.Normalize(new(slider.AxisX, slider.AxisY))));
                def.ReferenceAngle = reference;
                def.EnableLimit = slider.Limit;
                def.LowerTranslation = slider.Lower;
                def.UpperTranslation = slider.Upper;
                def.EnableMotor = slider.Motor;
                def.MotorSpeed = slider.MotorSpeed;
                def.MaxMotorForce = slider.MaxMotorForce;
                def.EnableSpring = slider.Spring;
                def.Hertz = slider.Hertz;
                def.DampingRatio = slider.DampingRatio;
                return physicsWorld.CreatePrismaticJoint(in def);
            }
            case WeldJoint2DComponent weld:
            {
                var def = B2WeldJointDef.Default;
                (def.BodyIdA, def.BodyIdB) = (target, self);
                def.LocalAnchorA = target.GetLocalPoint(F(center));
                def.LocalAnchorB = self.GetLocalPoint(F(center));
                def.ReferenceAngle = reference;
                def.LinearHertz = weld.LinearHertz;
                def.AngularHertz = weld.AngularHertz;
                def.LinearDampingRatio = def.AngularDampingRatio = weld.DampingRatio;
                return physicsWorld.CreateWeldJoint(in def);
            }
        }
        return null;
    }

    private void StepPhysics(float dt)
    {
        if (physicsWorld.IsNull()) return;
        // Prescribed platforms have already moved in MoNoon; Box2D moves its kinematic body to the same pose.
        foreach (var b in rigidBodies)
            if (b.Active && !b.Spawning && b.Rigid!.BodyType == BodyType2D.Kinematic && b.Motor == null && !b.Physics.IsNull())
            {
                b.Physics.SetTargetTransform(new B2Transform(F(b.Position), new B2Rot(b.Angle)), dt);
                (b.PhysicsPosition, b.PhysicsAngle) = (b.Position, b.Angle);
            }
        physicsWorld.Step(dt, SubSteps);
        foreach (var b in rigidBodies)
        {
            if (!b.Active || b.Spawning || !Simulated(b)) continue;
            var transform = b.Physics.GetTransform();
            float angle = b.Angle + (float)Math.IEEERemainder(Angle(transform.Q) - b.Angle, Math.Tau);
            PoseGroup(b, V(transform.P), angle);
            (b.PhysicsPosition, b.PhysicsAngle) = (b.Position, b.Angle);
            b.Velocity = V(b.Physics.GetLinearVelocity());
            b.Spin = b.Physics.GetAngularVelocity();
            var floor = CastBody(b, new(0, .08f), floorOnly: true);
            b.Support = floor is {} grounded ? byId[grounded.Target.ObjectId] : null;
            if (floor is {} ground) b.GroundNormal = ground.Normal;
        }
    }

    // A character's kinematic Box2D body follows the controller, carrying its velocity into the next step so
    // that dynamic bodies it walks into are pushed by a moving body.
    private void PlaceCharacters(float dt)
    {
        if (physicsWorld.IsNull()) return;
        foreach (var b in rigidBodies)
        {
            if (!b.Active || b.Spawning || b.Motor == null || b.Physics.IsNull()) continue;
            b.Physics.SetTransform(F(b.Position), new B2Rot(b.Angle));
            b.Physics.SetLinearVelocity(F(b.Velocity));
            (b.PhysicsPosition, b.PhysicsAngle) = (b.Position, b.Angle);
        }
    }
}
