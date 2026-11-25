using System.Numerics;
using Engine.Core;

// Steady-state physics allocates nothing per tick: PlatformerLab ShowcaseCourse with the character motor capsule driven from
// here instead of CourseRunner (whose coin counter formats strings), laps over the Hill edge chain between two box ledges, while
// the dynamic pulley decks on their Box2D sliders (tied by PulleyPair) stay in the world, plus one raycast and one sweep per tick.
// Warm-up laps let the decks settle; a turning non-circle body rebuilds its shape once per new angle, which is the one
// allocation left on purpose.
internal static class PhysicsAllocationChecks
{
    [Check(4000, "physics-alloc", "physics", StopAfter = "platformer")] static void Registered(CheckRun c) => Run(c.Repository);
    internal static void Run(string repository)
    {
        var scene = SceneDocument.Load(Path.Combine(repository, "samples", "PlatformerLab", "Scenes", "ShowcaseCourse.scene"));
        var runner = scene.Objects.Single(o => o.Name == "Runner");
        runner.RemoveComponent<ScriptComponent>();
        (runner.X, runner.Y) = (1580, 204); // On the lift goal ledge, just west of the Hill.
        // The rope bridge never comes to rest (its planks keep turning by tiny angles), so each plank would rebuild its shape every
        // tick, the one allocation this check leaves on purpose; it is taken out so the steady state can be measured.
        scene.Objects.RemoveAll(o => o.Name.StartsWith("Plank "));
        var decks = scene.Objects.Where(o => o.Name.StartsWith("Pulley deck ")).Select(o => o.Id).ToArray();
        using var game = new SceneGame(scene);
        var probe = Shape2D.Circle(new(1700, 150), 6);
        void Lap()
        {
            for (int i = 0; i < 400; i++)
            {
                game.Motion.DriveCharacter(runner.Id, i < 200 ? 150 : -150, 0);
                game.FixedUpdate(Vector2.Zero, 1 / 60f);
                game.Physics.Raycast(new(1840, 100), Vector2.UnitY, 300);
                game.Physics.Sweep(probe, new(0, 200));
            }
        }
        Matrix3x2[] rest = [];
        for (int lap = 0; lap < 8; lap++)
        {
            Lap();
            var pose = decks.Select(id => { game.TryGetWorldMatrix(id, out var m); return m; }).ToArray();
            if (pose.SequenceEqual(rest)) break;
            rest = pose;
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        Lap();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        if (bytes != 0) throw new Exception($"Physics allocation: Course steady state allocated {bytes} B in 400 ticks (expected 0)");
        game.TryGetPosition(runner.Id, out var back);
        if (Math.Abs(back.X - 1580) > 2) throw new Exception($"Physics allocation: the lap did not return over the Hill ({back.X:F1})");
        Console.WriteLine("PASS physics steady state: Course motor capsule, edge chain, box ledges, pulley sliders, raycast and sweep allocate 0 B per tick");
    }
}
