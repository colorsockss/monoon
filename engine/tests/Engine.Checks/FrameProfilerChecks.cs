using System.Diagnostics;
using System.Drawing;
using Engine.Core;

internal static class FrameProfilerChecks
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("Frame profiler: " + message);
        Console.WriteLine("PASS frame profiler: " + message);
    }

    private sealed class Idle : GameBehaviour { }

    private static void Spin(double ms)
    {
        long until = Stopwatch.GetTimestamp() + (long)(ms * Stopwatch.Frequency / 1000);
        while (Stopwatch.GetTimestamp() < until) { }
    }

    internal static void Run(string scratch)
    {
        Nesting();
        RingBuffer();
        Allocation();
        Workload(scratch);
        Profiles(scratch);
    }

    private static void Nesting()
    {
        int outer = FrameProfiler.Marker("Check.Outer"), inner = FrameProfiler.Marker("Check.Inner");
        using (FrameProfiler.Sample(outer)) Check(FrameProfiler.Current == null, "samples are inert without an installed profiler");
        var profiler = new FrameProfiler(8);
        profiler.Install();
        try
        {
            using (FrameProfiler.Sample(outer))
            {
                Spin(2);
                for (int i = 0; i < 3; i++)
                    using (FrameProfiler.Sample(inner)) Spin(1);
            }
            using (FrameProfiler.Sample(outer)) { }
            profiler.EndFrame();
            var f = profiler.At(0);
            int o = f.FirstChildOf(0), n = f.FirstChildOf(o);
            Check(f.NodeCount == 3 && f.Name(o) == "Check.Outer" && f.Calls(o) == 2 && f.Name(n) == "Check.Inner" && f.Calls(n) == 3 &&
                  f.Parent(n) == o && f.Depth(n) == 2 && f.NextSibling(o) < 0, "repeated calls merge into one node per caller with their call count");
            Check(f.InclusiveMilliseconds(n) >= 3 && f.SelfMilliseconds(o) >= 2 &&
                  Math.Abs(f.SelfMilliseconds(o) - (f.InclusiveMilliseconds(o) - f.InclusiveMilliseconds(n))) < 1e-9 &&
                  f.Milliseconds >= f.InclusiveMilliseconds(o) && f.InclusiveMilliseconds(o) >= f.InclusiveMilliseconds(n),
                "self time excludes nested samples and inclusive time contains them");
            Check(f.Total(inner) == (3, f.InclusiveMilliseconds(n)), "Total sums a marker across the tree");

            using (FrameProfiler.Sample(outer)) using (FrameProfiler.Sample(outer)) using (FrameProfiler.Sample(inner)) { }
            profiler.EndFrame();
            var nested = profiler.At(1);
            Check(nested.Total(outer).Calls == 2 && nested.Total(outer).Milliseconds == nested.InclusiveMilliseconds(nested.FirstChildOf(0)),
                "recursive samples count every call but time only the outermost");

            void Deep(int level) { using var s = FrameProfiler.Sample(inner); if (level > 0) Deep(level - 1); }
            Deep(FrameProfiler.MaxDepth + 5);
            profiler.EndFrame();
            Check(profiler.At(2).Dropped == 6 && profiler.At(2).NodeCount == FrameProfiler.MaxDepth + 1, "samples deeper than the limit are counted as dropped and stay balanced");

            bool threw = false;
            using (FrameProfiler.Sample(outer)) { try { profiler.EndFrame(); } catch (InvalidOperationException) { threw = true; } }
            profiler.EndFrame();
            var other = Task.Run(() => { using (FrameProfiler.Sample(inner)) { } }).Wait(5000);
            profiler.EndFrame();
            Check(threw && other && profiler.At(4).NodeCount == 1, "a frame cannot end inside a sample and other threads are ignored");
        }
        finally { profiler.Uninstall(); }
        Check(FrameProfiler.Current == null, "Uninstall stops sampling");
    }

    private static void RingBuffer()
    {
        int heavy = FrameProfiler.Marker("Check.Heavy");
        var profiler = new FrameProfiler(4);
        profiler.Install();
        try
        {
            for (int i = 1; i <= 6; i++)
            {
                if (i == 5) using (FrameProfiler.Sample(heavy)) Spin(3);
                profiler.EndFrame();
            }
            Check(profiler.Count == 4 && profiler.Sequence == 6 && profiler.At(0).Sequence == 3 && profiler.At(3).Sequence == 6,
                "the ring keeps the newest frames in order");
            var slowest = Enumerable.Range(0, profiler.Count).Select(profiler.At).MaxBy(f => f.Milliseconds)!;
            Check(profiler.Find(2) == null && profiler.Find(5) == slowest && slowest.Total(heavy).Calls == 1 && slowest.Total(heavy).Milliseconds >= 3,
                "a frame is selected by number and the slowest frame holds the heavy sample");
            profiler.Restart();
            using (FrameProfiler.Sample(heavy)) { }
            profiler.Restart();
            profiler.EndFrame();
            Check(profiler.At(3).NodeCount == 1 && profiler.Sequence == 7, "Restart discards the frame in progress");
        }
        finally { profiler.Uninstall(); }
    }

    private static void Allocation()
    {
        int marker = FrameProfiler.Marker("Check.Hot");
        var behaviour = new Idle();
        long Measure()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 20; frame++)
            {
                for (int i = 0; i < 500; i++)
                {
                    using (FrameProfiler.Sample(marker)) using (FrameProfiler.Script(behaviour, nameof(GameBehaviour.FixedUpdate))) { }
                }
                FrameProfiler.Current?.EndFrame();
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Measure();
        long off = Measure();
        var profiler = new FrameProfiler(16);
        profiler.Install();
        try
        {
            Measure();
            long on = Measure();
            Check(off == 0 && on == 0, $"20,000 samples and 20 frames allocate {on} bytes when installed and {off} when not");
        }
        finally { profiler.Uninstall(); }
    }

    private static void Workload(string scratch)
    {
        const int Rays = 37, Overlaps = 11;
        var root = Path.Combine(scratch, ".frame-profiler");
        Directory.CreateDirectory(Path.Combine(root, "Scripts"));
        File.WriteAllText(Path.Combine(root, "Scripts", "Prober.cs"), $$"""
            using System.Numerics;
            using Engine.Core;
            public sealed class Prober : GameBehaviour
            {
                public override void FixedUpdate(ScriptTick tick)
                {
                    var physics = tick.Objects.Physics;
                    for (int i = 0; i < {{Rays}}; i++) physics.Raycast(new(0, 5 + i % 10), Vector2.UnitX, 400);
                    for (int i = 0; i < {{Overlaps}}; i++) physics.Overlap(Shape2D.Circle(new(200, 10), 30));
                }
            }
            """);
        var scene = SceneDocument.Empty(new(400, 200));
        scene.ProjectDirectory = root;
        var wall = new SceneObject { Name = "Wall", X = 200, Y = 0, Width = 20, Height = 20 };
        wall.AddComponent(new BoxCollider2DComponent());
        var prober = new SceneObject { Name = "Prober", Width = 1, Height = 1 };
        prober.AddComponent(new ScriptComponent { Script = "Prober" });
        scene.Objects.AddRange([wall, prober]);
        using var game = new SceneGame(scene);
        var loop = new GameLoop(game, game.Actions);
        var profiler = new FrameProfiler(32);
        profiler.Install();
        try
        {
            for (int i = 0; i < 10; i++) { loop.Advance(1 / 60d); profiler.EndFrame(); }
            loop.Advance(3 / 60d);
            profiler.EndFrame();
        }
        finally { profiler.Uninstall(); }
        var steady = profiler.At(9);
        int simulate = steady.FirstChildOf(0);
        while (simulate >= 0 && steady.Name(simulate) != "Simulate") simulate = steady.NextSibling(simulate);
        int script = steady.FirstChildOf(simulate);
        while (script >= 0 && steady.Name(script) != "Prober.FixedUpdate") script = steady.NextSibling(script);
        var children = new Dictionary<string, int>();
        for (int c = steady.FirstChildOf(script); c >= 0; c = steady.NextSibling(c)) children[steady.Name(c)] = steady.Calls(c);
        Check(steady.Calls(simulate) == 1 && steady.Calls(script) == 1 && children.Count == 2 &&
              children["Physics.Raycast"] == Rays && children["Physics.Overlap"] == Overlaps,
            $"{Rays} raycasts and {Overlaps} overlaps in one script tick appear under that script's FixedUpdate");
        var catchUp = profiler.At(10);
        int ticks = catchUp.Total(FrameProfiler.Simulate).Calls;
        Check(ticks == 3 && catchUp.Total(FrameProfiler.Raycast).Calls == 3 * Rays && catchUp.Total("Prober.FixedUpdate").Calls == 3 &&
              catchUp.Total(FrameProfiler.UpdateUi).Calls == 1, "a frame with three fixed ticks counts three times the queries");
    }

    private static void Profiles(string scratch)
    {
        var player = Path.Combine(scratch, ".frame-profile-player");
        Directory.CreateDirectory(player);
        var capture = Path.Combine(player, "frames.csv");
        var previous = Environment.GetEnvironmentVariable(FrameProfileWriter.Variable);
        Environment.SetEnvironmentVariable(FrameProfileWriter.Variable, capture);
        try
        {
            Check(FrameProfileWriter.Open(player) == null && !File.Exists(capture), "a player without a build receipt ignores the capture request");
            void Receipt(string profile)
            {
                var data = new GameData();
                data.Set("profile", profile);
                File.WriteAllText(Path.Combine(player, "MoNoonBuild"), data.ToYaml());
            }
            Receipt("distribution");
            Check(FrameProfileWriter.Open(player) == null && !File.Exists(capture) && FrameProfiler.Current == null,
                "a distribution build installs no profiler and writes no capture");
            Receipt("development");
            int marker = FrameProfiler.Marker("Check.Written");
            using (var writer = FrameProfileWriter.Open(player))
            {
                Check(writer != null && FrameProfiler.Current == writer.Profiler, "a development build installs the capture profiler");
                writer!.Profiler.EndFrame();
                using (FrameProfiler.Sample(marker)) using (FrameProfiler.Sample(marker)) { }
                writer.Profiler.EndFrame();
                long Stream()
                {
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 50; i++)
                    {
                        using (FrameProfiler.Sample(marker)) using (FrameProfiler.Sample(FrameProfiler.Raycast)) { }
                        writer.Profiler.EndFrame();
                    }
                    return GC.GetAllocatedBytesForCurrentThread() - before;
                }
                Stream();
                long written = Stream();
                Check(written == 0, $"streaming 50 more frames to CSV allocates {written} bytes");
            }
            var rows = File.ReadAllLines(capture);
            var second = rows.Where(r => r.StartsWith("2,")).Select(r => r.Split(',')).ToArray();
            Check(FrameProfiler.Current == null && rows[0] == FrameProfileWriter.Header && rows.Count(r => r.Split(',')[1] == "0") == 102 &&
                  second.Select(r => r[4]).SequenceEqual(["Frame", "Profiler.Write", "Check.Written", "Check.Written"]) &&
                  second[2][2] == "0" && second[3][2] == second[2][1] && second[3][3] == "2" && second[0][8].Length > 0 && second[2][8].Length == 0,
                "CSV rows list each frame's tree depth-first with parents; writing shows up in the next frame");
        }
        finally { Environment.SetEnvironmentVariable(FrameProfileWriter.Variable, previous); }
    }
}
