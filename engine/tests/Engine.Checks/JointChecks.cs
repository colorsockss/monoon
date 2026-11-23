using System.Numerics;
using Engine.Core;

// PlatformerLab Joints scene: a revolute rope bridge and Box2D slider decks tied by the PulleyPair script.
internal static class JointChecks
{
    private const float Step = 1f / 60;
    // Same timeline as engine/tests/PlatformerLab.Joints/Cross.inputreplay (frame: key down/up).
    internal static readonly (int Frame, string Key, bool Down)[] Route =
        [(10, "Right", true), (JumpA, "Space", true), (JumpA + 1, "Space", false), (JumpB, "Space", true), (JumpB + 1, "Space", false),
         (TraceEnd - 20, "Right", false)];
    private const int JumpA = 190, JumpB = 244, TraceEnd = 300;

    public static void Run(string repository, string scratch)
    {
        var path = Path.Combine(repository, "samples", "PlatformerLab", "Scenes", "Joints.scene");
        var scene = SceneDocument.Load(path);
        Guid Id(string name) => scene.Objects.Single(o => o.Name == name).Id;
        var runner = Id("Runner");
        var storage = new PlayerStorage(Path.Combine(scratch, "joints-profile"));
        var trace = Environment.GetEnvironmentVariable("MONOON_JOINT_DEBUG") != null;
        float lowestPlank = 0, deckA = 0, deckB = 0, maxPivotError = 0;
        using (var session = new GameSession(scene, null, null, storage))
        {
            var world = session.Current;
            var planks = Enumerable.Range(1, 8).Select(i => Id($"Plank {i}")).ToArray();
            world.TryGetPosition(Id("Pulley deck A"), out var a0);
            world.TryGetPosition(Id("Pulley deck B"), out var b0);
            for (int frame = 0, next = 0; frame < TraceEnd; frame++)
            {
                for (; next < Route.Length && Route[next].Frame == frame; next++)
                    if (Route[next].Down) session.Actions.Press(Route[next].Key); else session.Actions.Release(Route[next].Key);
                session.FixedUpdate(session.Actions.Take(), Step);
                world.TryGetPosition(runner, out var p);
                world.TryGetMotion(runner, out var m);
                foreach (var plank in planks) { world.TryGetPosition(plank, out var q); lowestPlank = Math.Max(lowestPlank, q.Y); }
                // Neighbouring planks meet at the revolute pivot: plank i's top right corner is plank i+1's top left corner.
                for (int i = 0; i + 1 < planks.Length; i++)
                {
                    world.TryGetWorldMatrix(planks[i], out var left); world.TryGetWorldMatrix(planks[i + 1], out var right);
                    maxPivotError = Math.Max(maxPivotError, Vector2.Distance(Vector2.Transform(new(24, 0), left), Vector2.Transform(new(0, 0), right)));
                }
                world.TryGetPosition(Id("Pulley deck A"), out var a); world.TryGetPosition(Id("Pulley deck B"), out var b);
                deckA = Math.Max(deckA, a.Y - a0.Y); deckB = Math.Min(deckB, b.Y - b0.Y);
                if (trace && frame % 30 == 0) Console.WriteLine("planks " + string.Join(" ", planks.Select(id => { world.TryGetWorldMatrix(id, out var w); return $"{w.Translation.X:F1},{w.Translation.Y:F1}@{float.RadiansToDegrees(MathF.Atan2(w.M12, w.M11)):F0}"; })));
                if (trace) Console.WriteLine($"{frame} runner {p.X:F2},{p.Y:F2} vy {m.Velocity.Y:F1} support {scene.Objects.FirstOrDefault(o => o.Id == m.Support)?.Name} decks {a.Y - a0.Y:F2} {b.Y - b0.Y:F2}");
            }
            var data = storage.LoadSlot(1, 1);
            Check(data?.GetBool("crossed") == true, $"runner crosses the rope bridge and both pulley decks ({data?.ToYaml()})");
        }
        Check(lowestPlank > 300 && maxPivotError < .5f, $"revolute bridge sags under the runner and keeps its pivots together (lowest plank {lowestPlank:F1}, pivot gap {maxPivotError:F3} px)");
        Check(deckA > 10 && deckB < -10 && deckA <= 16.5f && deckB >= -16.5f, $"pulley decks move opposite within the ±16 px slider limits ({deckA:F2}, {deckB:F2})");
        Sandbox();
    }

    // One small world per joint feature; each body is 16×16 px, mass 1.
    private static void Sandbox()
    {
        var scene = SceneDocument.Empty(new());
        SceneObject Body(string name, float x, float y, BodyType2D type = BodyType2D.Dynamic, float gravity = 980, params SceneComponent[] extra)
        {
            var obj = new SceneObject { Name = name, X = x, Y = y, Width = 16, Height = 16 };
            obj.AddComponent(new BoxCollider2DComponent());
            obj.AddComponent(new Rigidbody2DComponent { BodyType = type, Gravity = gravity });
            foreach (var c in extra) obj.AddComponent(c);
            scene.Objects.Add(obj);
            return obj;
        }
        var wheel = Body("Wheel", 0, 0, gravity: 0, extra: new RevoluteJoint2DComponent { Motor = true, MotorSpeed = 180, MaxMotorTorque = 1e6f });
        var flap = Body("Flap", 100, 0, extra: new RevoluteJoint2DComponent { AnchorX = -8, Limit = true, LowerAngle = -10, UpperAngle = 30 });
        var spring = Body("Spring", 200, 0, extra: new DistanceJoint2DComponent { Length = 40, Spring = true, Hertz = 2, DampingRatio = .2f });
        var hinge = Body("Hinge", 300, 0, BodyType2D.Static);
        var rod = Body("Rod", 300, 40, extra: new DistanceJoint2DComponent { Connected = hinge.Id, Length = 40 });
        var slider = Body("Slider", 400, 0, gravity: 0, extra: new PrismaticJoint2DComponent { AxisX = 1, Limit = true, Lower = 0, Upper = 50, Motor = true, MotorSpeed = 100, MaxMotorForce = 2000 });
        var weld = Body("Weld", 500, 0, extra: new WeldJoint2DComponent { LinearHertz = 3, AngularHertz = 3, DampingRatio = .1f });
        var slope = new SceneObject { Name = "Slope", X = 600, Y = 100, Width = 200, Height = 10, Rotation = 20 };
        slope.AddComponent(new BoxCollider2DComponent());
        scene.Objects.Add(slope);
        var Rotated = (float degrees, float x) => Vector2.Transform(new(x, -16), Matrix3x2.CreateRotation(float.DegreesToRadians(degrees)));
        var slick = new SceneObject { Name = "Slick", X = 600 + Rotated(20, 140).X, Y = 100 + Rotated(20, 140).Y, Width = 16, Height = 16, Rotation = 20 };
        var grippy = new SceneObject { Name = "Grippy", X = 600 + Rotated(20, 30).X, Y = 100 + Rotated(20, 30).Y, Width = 16, Height = 16, Rotation = 20 };
        foreach (var (box, friction) in new[] { (slick, 0f), (grippy, 1f) })
        {
            box.AddComponent(new BoxCollider2DComponent());
            box.AddComponent(new Rigidbody2DComponent { Friction = friction, FreezeRotation = true });
            scene.Objects.Add(box);
        }
        var floor = new SceneObject { Name = "Floor", X = 900, Y = 200, Width = 100, Height = 10 };
        floor.AddComponent(new BoxCollider2DComponent());
        scene.Objects.Add(floor);
        var ball = Body("Ball", 940, 100);
        ball.GetComponent<Rigidbody2DComponent>()!.Restitution = .8f;
        float springMin = float.MaxValue, springMax = float.MinValue, flapMin = 0, flapMax = 0, rodError = 0, weldMax = 0, ballApex = float.MaxValue;
        bool bounced = false;
        using var game = new SceneGame(scene);
        float Angle(Guid id) { game.TryGetWorldMatrix(id, out var m); return float.RadiansToDegrees(MathF.Atan2(m.M12, m.M11)); }
        Vector2 At(Guid id) { game.TryGetPosition(id, out var p); return p; }
        game.ApplyImpulse(flap.Id, new(0, 400), new(116, 8));
        for (int i = 0; i < 180; i++)
        {
            game.FixedUpdate(Vector2.Zero, Step);
            flapMin = Math.Min(flapMin, Angle(flap.Id)); flapMax = Math.Max(flapMax, Angle(flap.Id));
            springMin = Math.Min(springMin, At(spring.Id).Y); springMax = Math.Max(springMax, At(spring.Id).Y);
            rodError = Math.Max(rodError, Math.Abs(Vector2.Distance(At(rod.Id), At(hinge.Id)) - 40));
            weldMax = Math.Max(weldMax, At(weld.Id).Y);
            game.TryGetMotion(ball.Id, out var motion);
            if (motion.Velocity.Y < 0) bounced = true;
            if (bounced) ballApex = Math.Min(ballApex, At(ball.Id).Y);
        }
        game.TryGetMotion(wheel.Id, out var spin);
        game.TryGetWorldMatrix(wheel.Id, out var turned);
        var hub = Vector2.Transform(new(8, 8), turned);
        Check(Math.Abs(spin.AngularVelocity - 180) < 1 && Vector2.Distance(hub, new(8, 8)) < .1f,
            $"revolute motor turns at its speed about its pivot ({spin.AngularVelocity:F1} °/s, hub {hub.X:F2},{hub.Y:F2})");
        Check(flapMin >= -10.5f && flapMax <= 30.5f && flapMax > 25, $"revolute limit holds −10°…30° ({flapMin:F1}°, {flapMax:F1}°)");
        Check(springMax - springMin > 10 && springMax > 40, $"distance spring stretches and oscillates around its length ({springMin:F1}…{springMax:F1} px)");
        Check(rodError < .5f, $"rigid distance joint keeps its length ({rodError:F3} px)");
        Check(Math.Abs(At(slider.Id).X - 450) < .5f && Math.Abs(At(slider.Id).Y) < .01f, $"slider motor runs to its upper limit along the axis ({At(slider.Id).X:F2}, {At(slider.Id).Y:F2})");
        // A 3 Hz weld under 980 px/s² deflects g/ω² ≈ 2.8 px at rest and up to twice that while it swings.
        Check(weldMax > 2 && weldMax < 8, $"soft weld sags on its spring ({weldMax:F1} px)");
        float Along(SceneObject box) => Vector2.Dot(At(box.Id) - new Vector2(box.X, box.Y), Vector2.Normalize(Rotated(20, 1) - Rotated(20, 0)));
        Check(Along(slick) > 50 && Math.Abs(Along(grippy)) < 1, $"friction 0 slides down a 20° slope, friction 1 holds ({Along(slick):F1}, {Along(grippy):F2} px)");
        Check(bounced && ballApex < 160, $"restitution 0.8 bounces back up ({ballApex:F1})");
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new Exception("Joints: " + message);
        Console.WriteLine("PASS joints: " + message);
    }
}
