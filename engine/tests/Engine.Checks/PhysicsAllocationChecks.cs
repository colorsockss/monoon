using System.Numerics;
using Engine.Core;

// Steady-state physics allocates nothing per tick: PlatformerLab Slopes (character motor capsule over tile seams and an edge
// slope, a Box2D capsule boulder, a trigger marker) driven from here instead of its script (whose readout formats strings),
// plus one raycast and one sweep per tick. Warm-up laps fill the tile shape cache and let the boulder settle; a turning
// non-circle body rebuilds its shape once per new angle, which is the one allocation left on purpose.
internal static class PhysicsAllocationChecks
{
    [Check(4000, "physics-alloc", "physics", StopAfter = "platformer")] static void Registered(CheckRun c) => Run(c.Repository);
    internal static void Run(string repository)
    {
        var scene = SceneDocument.Load(Path.Combine(repository, "samples", "PlatformerLab", "Scenes", "Slopes.scene"));
        var runner = scene.Objects.Single(o => o.Name == "Runner");
        runner.RemoveComponent<ScriptComponent>();
        var boulder = scene.Objects.Single(o => o.Name == "Boulder").Id;
        using var game = new SceneGame(scene);
        var probe = Shape2D.Circle(new(300, 150), 6);
        void Lap()
        {
            for (int i = 0; i < 400; i++)
            {
                game.Motion.DriveCharacter(runner.Id, i < 200 ? 150 : -150, 0);
                game.FixedUpdate(Vector2.Zero, 1 / 60f);
                game.Physics.Raycast(new(100, 100), Vector2.UnitY, 300);
                game.Physics.Sweep(probe, new(0, 200));
            }
        }
        Matrix3x2 rest = default;
        for (int lap = 0; lap < 8; lap++)
        {
            Lap();
            game.TryGetWorldMatrix(boulder, out var pose);
            if (pose == rest) break;
            rest = pose;
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        Lap();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        if (bytes != 0) throw new Exception($"Physics allocation: Slopes steady state allocated {bytes} B in 400 ticks (expected 0)");
        Console.WriteLine("PASS physics steady state: Slopes motor, tiles, edge chain, Box2D boulder, trigger, raycast and sweep allocate 0 B per tick");
    }
}
