using Engine.Core;
using System.Numerics;

internal static class RigidBodyChecks
{
    [Check(3700, "physics")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private const float Step = 1f / 60;

    public static void Run(string repository, string scratch)
    {
        var path = Path.Combine(repository, "samples", "PlatformerLab", "Scenes", "Platform.scene");
        var scene = SceneDocument.Load(path);
        var runner = scene.Objects.Single(o => o.Name == "Runner");
        var storage = new PlayerStorage(Path.Combine(scratch, "platform-profile"));
        using (var session = new GameSession(scene, null, null, storage))
        {
            session.FixedUpdate(Vector2.Zero, Step);
            session.Actions.Press("R");
            session.FixedUpdate(session.Actions.Take(), Step);
            session.Actions.Release("R");
            for (int i = 0; i < 400; i++) session.FixedUpdate(session.Actions.Take(), Step);
            session.Current.TryGetPosition(runner.Id, out var end);
            session.Current.TryGetMotion(runner.Id, out var motion);
            var data = storage.LoadSlot(1, 1);
            if (data?.GetBool("passed") != true)
                throw new Exception($"Platform route failed at {end}, {motion} {data?.ToYaml()}");
            Console.WriteLine($"PASS platformer: ramp, moving platform, joint swing and relative slip (climb {data.GetNumber("climb"):F2}, ride {data.GetNumber("ride"):F4}, slip {data.GetNumber("slip"):F4}, swing {data.GetNumber("swing"):F4}, joint error {data.GetNumber("jointError"):F6} px)");
            session.Actions.Press("X"); session.FixedUpdate(session.Actions.Take(), Step); session.Actions.Release("X");
            session.Actions.Press("Space"); session.FixedUpdate(session.Actions.Take(), Step);
            session.Current.TryGetMotion(runner.Id, out motion);
            Check(!motion.Grounded && motion.Velocity.Y < -250, "jump releases the moving support");
        }

        var sandbox = SceneDocument.Empty(new());
        SceneObject Box(string name, float x, float y, float mass)
        {
            var obj = new SceneObject { Name = name, X = x, Y = y, Width = 16, Height = 16 };
            obj.AddComponent(new BoxCollider2DComponent());
            obj.AddComponent(new Rigidbody2DComponent { Gravity = 0, Mass = mass });
            sandbox.Objects.Add(obj);
            return obj;
        }
        var a = Box("A", 10, 10, 2);
        var b = Box("B", 30, 10, 1);
        using (var game = new SceneGame(sandbox))
        {
            game.ApplyImpulse(a.Id, new(120,0));
            for (int i=0;i<30;i++) game.FixedUpdate(default(Vector2),Step);
            game.TryGetMotion(a.Id, out var ma); game.TryGetMotion(b.Id, out var mb);
            // Box2D soft contact leaves the pair separating at ~0.5 px/s (39.83 and 40.33); momentum is exact.
            Check(Math.Abs(2*ma.Velocity.X+mb.Velocity.X-120)<.01f && Math.Abs(ma.Velocity.X-40)<.5f && Math.Abs(mb.Velocity.X-40)<.5f, $"dynamic contacts share impulse by inverse mass ({ma.Velocity.X:F3}, {mb.Velocity.X:F3})");
            game.ApplyImpulse(b.Id, new(0,20),new(150,20));
            game.TryGetMotion(b.Id, out var angular);
            Check(angular.AngularVelocity > 1, "off-center impulse produces angular velocity");
        }
        var wall = Box("Wall", 50, 90, 10);
        wall.GetComponent<Rigidbody2DComponent>()!.BodyType = BodyType2D.Static;
        var fast = Box("Fast", 0, 90, 1);
        using (var game = new SceneGame(sandbox))
        {
            game.SetVelocity(fast.Id, new(8000,0));
            game.FixedUpdate(Vector2.Zero, Step);
            game.TryGetPosition(fast.Id, out var pos);
            Check(Math.Abs(pos.X-34)<.01, "fast body stops at a static wall without tunnelling");
        }
        var lift = Box("Lift", 0, 180, 1);
        lift.GetComponent<Rigidbody2DComponent>()!.BodyType = BodyType2D.Kinematic;
        var rider = Box("Rider", 0, 164, 1);
        rider.AddComponent(new CharacterMotor2DComponent());
        var ceiling = Box("Ceiling", 0, 120, 1);
        ceiling.GetComponent<Rigidbody2DComponent>()!.BodyType = BodyType2D.Static;
        using (var game = new SceneGame(sandbox))
        {
            game.FixedUpdate(Vector2.Zero, Step);
            game.SetVelocity(lift.Id, new(0, -120));
            for (int i = 0; i < 60; i++) game.FixedUpdate(Vector2.Zero, Step);
            game.TryGetPosition(rider.Id, out var head);
            game.TryGetPosition(lift.Id, out var deck);
            Check(Math.Abs(head.Y - 136) < .01 && Math.Abs(deck.Y - head.Y - 16) < .01,
                "vertical platform carries its rider and stops under a ceiling");
        }
        var nested = Box("Independent", 80, 0, 1);
        nested.ParentId = a.Id;
        using (var game = new SceneGame(sandbox))
        {
            game.TryGetPosition(nested.Id, out var before);
            game.SetVelocity(a.Id, new(-100,0));
            for (int i=0; i<30; i++) game.FixedUpdate(Vector2.Zero, Step);
            game.TryGetPosition(nested.Id, out var after);
            Check(Vector2.Distance(before, after) < .001f, "nested rigid body simulates independently of its parent");
        }
        var steep = SceneDocument.Load(path);
        var climber = steep.Objects.Single(o => o.Id == runner.Id);
        climber.GetComponent<CharacterMotor2DComponent>()!.MaxSlope = 12;
        using (var session = new GameSession(steep))
        {
            session.Actions.Press("Right");
            for (int i=0; i<240; i++) session.FixedUpdate(session.Actions.Take(),Step);
            session.Current.TryGetPosition(runner.Id, out var blocked);
            Check(blocked.X < 165 && blocked.Y >= 299, $"slope above motor limit blocks ascent ({blocked.X:F1}, {blocked.Y:F1})");
        }
        var original = scene.CaptureObjects();
        var pendulum = scene.Objects.Single(o=>o.Name=="Pendulum");
        Check(pendulum.GetComponent<DistanceJoint2DComponent>()?.Connected == scene.Objects.Single(o=>o.Name=="Anchor").Id &&
            original.Contains("rigidbody2D") && runner.X == 78 && runner.Y == 300, "YAML joint target and authored pose survive Play");
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new Exception("Platformer: " + message);
        Console.WriteLine("PASS platformer: " + message);
    }
}
