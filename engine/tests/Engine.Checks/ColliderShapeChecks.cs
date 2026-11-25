using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Engine.Core;

// 16.4: Capsule Collider 2D and Edge Collider 2D (Box2D capsule and chain) in queries, contacts, the Box2D world, YAML and
// the PlatformerLab ShowcaseCourse scene (scripted capsule runner over the Hill edge chain), plus the 8.8-style spatial grid comparison at 1,000 and 10,000 colliders.
internal static class ColliderShapeChecks
{
    [Check(3900, "colliders", "physics")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private const float Step = 1f / 60;
    internal const int Frames = 1500;
    private static void Check(bool ok, string what) { if (!ok) throw new Exception("Colliders: " + what); }
    private static void Near(float actual, float expected, string what, float tolerance = .001f) =>
        Check(Math.Abs(actual - expected) < tolerance, $"{what}: {actual} vs {expected}");

    public static void Run(string repository, string scratch)
    {
        Queries();
        Documents();
        Course(repository);
        Allocation();
        foreach (int count in new[] { 1000, 10000 }) Spatial(count);
    }

    private static SceneObject Add(SceneDocument scene, string name, float x, float y, float w, float h, params SceneComponent[] components)
    {
        var obj = new SceneObject { Name = name, X = x, Y = y, Width = w, Height = h };
        foreach (var c in components) obj.AddComponent(c);
        scene.Objects.Add(obj);
        return obj;
    }

    private static void Queries()
    {
        var scene = SceneDocument.Empty(new());
        // A vertical capsule 20×60 at (100,0): centers (110,10) and (110,50), radius 10.
        var pill = Add(scene, "Pill", 100, 0, 20, 60, new CapsuleCollider2DComponent());
        // A horizontal capsule with an explicit radius 4 centred on y=125.
        var bar = Add(scene, "Bar", 100, 120, 60, 10, new CapsuleCollider2DComponent { Radius = 4 });
        // An open edge (0,200)→(100,200)→(200,150), two-sided for queries.
        var edge = Add(scene, "Edge", 0, 150, 200, 50, new EdgeCollider2DComponent { Points = [new(0, 1), new(.5f, 1), new(1, 0)] });
        var loop = Add(scene, "Loop", 300, 0, 40, 40, new EdgeCollider2DComponent { Points = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)], Loop = true });
        var sensor = Add(scene, "Sensor", 400, 0, 20, 40, new CapsuleCollider2DComponent { IsTrigger = true });
        using var game = new SceneGame(scene);
        PhysicsHit Hit(PhysicsHit? h, string what) => h ?? throw new Exception("Colliders: missing hit " + what);
        var h = Hit(game.Raycast(new(0, 30), Vector2.UnitX, 500), "capsule side");
        Check(h.Target.ObjectId == pill.Id, "capsule side owner"); Near(h.Distance, 100, "capsule side"); Near(h.Normal.X, -1, "capsule side normal");
        Near(Hit(game.Raycast(new(110, -40), Vector2.UnitY, 500), "capsule cap").Distance, 40, "capsule cap");
        // Off-axis ray meets the rounded cap: x=105 → y = 10 - sqrt(100-25).
        Near(Hit(game.Raycast(new(105, -40), Vector2.UnitY, 500), "rounded cap").Distance, 50 - MathF.Sqrt(75), "rounded cap");
        Check(game.Raycast(new(0, 125), Vector2.UnitX, 99.99f) == null, "explicit radius range");
        Near(Hit(game.Raycast(new(0, 125), Vector2.UnitX, 500), "horizontal capsule").Distance, 100, "horizontal capsule (radius 4 clamped ≤ 5)");
        Near(Hit(game.Raycast(new(50, 100), Vector2.UnitY, 500), "edge from above").Distance, 100, "edge from above");
        var below = Hit(game.Raycast(new(50, 260), -Vector2.UnitY, 500), "edge from below");
        Check(below.Target.ObjectId == edge.Id && Math.Abs(below.Distance - 60) < .001f && below.Normal.Y > .99f, "edge is two-sided for queries");
        var slope = Hit(game.Raycast(new(170, 100), Vector2.UnitY, 500), "edge slope");
        Near(slope.Distance, 65, "edge slope"); Near(slope.Normal.Y, -2 / MathF.Sqrt(5), "slope normal");
        Check(game.Raycast(new(250, 100), Vector2.UnitY, 500) == null, "edge has no area past its end");
        Check(game.Overlap(Shape2D.Circle(new(320, 20), 5)).Count == 0, "a loop is only its edges");
        Check(game.Overlap(Shape2D.Circle(new(320, 2), 5)).Single().ObjectId == loop.Id, "loop edge overlap");
        // Public capsule query shape: a capsule swept down onto the edge lands with its lower cap.
        var sweep = Hit(game.Sweep(Shape2D.Capsule(new(50, 100), new(50, 160), 6), new(0, 100)), "capsule sweep");
        Check(sweep.Target.ObjectId == edge.Id, "capsule sweep owner"); Near(sweep.Distance, 34, "capsule sweep", .01f);
        Near(Hit(game.Sweep(Shape2D.Box(new(0, 20, 10, 10)), new(200, 0)), "box onto capsule").Distance, 90, "box onto capsule", .01f);
        Check(game.Overlap(Shape2D.Capsule(new(105, 30), new(95, 30), 2)).Single().ObjectId == pill.Id, "capsule vs capsule overlap");
        Check(game.Overlap(Shape2D.Circle(new(410, 20), 2)).Count == 0 && game.Overlap(Shape2D.Circle(new(410, 20), 2), new(IncludeTriggers: true)).Single().ObjectId == sensor.Id, "trigger capsule filter");
        // Rotated and mirrored: a capsule turned 90° lies along x.
        var turned = SceneDocument.Empty(new());
        var lying = Add(turned, "Lying", 0, 0, 10, 40, new CapsuleCollider2DComponent());
        lying.Rotation = 90; lying.ScaleX = -1;
        using var turnedGame = new SceneGame(turned);
        var center = lying.LocalToWorld(new(5, 20));
        Near(Hit(turnedGame.Raycast(center - new Vector2(40, 0), Vector2.UnitX, 100), "rotated capsule").Distance, 20, "rotated capsule");
        Near(Hit(turnedGame.Raycast(center - new Vector2(0, 40), Vector2.UnitY, 100), "rotated capsule thickness").Distance, 35, "rotated capsule thickness");

        // Contacts: a box moved through a capsule trigger and onto an edge gets enter/exit events.
        var contacts = SceneDocument.Empty(new());
        var actor = Add(contacts, "Actor", 0, 0, 10, 10, new BoxCollider2DComponent(), new ScriptComponent { Script = "Probe" });
        Add(contacts, "Gate", 20, -10, 10, 30, new CapsuleCollider2DComponent { IsTrigger = true });
        Add(contacts, "Rail", 0, 10, 200, 1, new EdgeCollider2DComponent { Points = [new(0, 0), new(1, 0)] });
        using var events = new SceneGame(contacts, null, null, _ => new Probe());
        var probe = events.GetComponent<Probe>(actor.Id)!;
        for (int i = 0; i < 12; i++) { probe.Move = new(5, 0); events.FixedUpdate(Vector2.Zero, Step); }
        probe.Move = new(0, -20); events.FixedUpdate(Vector2.Zero, Step); events.FixedUpdate(Vector2.Zero, Step);
        string seen = string.Join(",", probe.Events.Distinct());
        Check(seen == "CE,CS,TE,TS,TX,CX", "contact phases with capsule trigger and edge: " + seen);
        Console.WriteLine("PASS capsule and edge queries: rays (side, cap, rounded cap, two-sided edge, slope), sweeps, overlaps, loop, filters, rotation, contacts");
    }

    private sealed class Probe : GameBehaviour
    {
        internal readonly List<string> Events = [];
        internal Vector2 Move;
        public override void FixedUpdate(ScriptTick tick) { tick.Move(Move); Move = default; }
        public override void OnTriggerEnter(ScriptContact c) => Events.Add("TE");
        public override void OnTriggerStay(ScriptContact c) => Events.Add("TS");
        public override void OnTriggerExit(ScriptContact c) => Events.Add("TX");
        public override void OnCollisionEnter(ScriptContact c) => Events.Add("CE");
        public override void OnCollisionStay(ScriptContact c) => Events.Add("CS");
        public override void OnCollisionExit(ScriptContact c) => Events.Add("CX");
    }

    private static void Documents()
    {
        var scene = SceneDocument.Empty(new());
        var pill = Add(scene, "Pill", 0, 0, 10, 30, new CapsuleCollider2DComponent { Radius = 3, Layer = 4, Mask = 9 });
        var edge = Add(scene, "Edge", 0, 0, 50, 50, new EdgeCollider2DComponent { Points = [new(0, 1), new(.5f, .25f), new(1, 1)], Loop = true });
        var copy = SceneDocument.Empty(new());
        copy.RestoreObjects(scene.CaptureObjects());
        var k = copy.Objects.Single(o => o.Id == pill.Id).GetComponent<CapsuleCollider2DComponent>()!;
        var e = copy.Objects.Single(o => o.Id == edge.Id).GetComponent<EdgeCollider2DComponent>()!;
        Check(k.Radius == 3 && k.Layer == 4 && k.Mask == 9 && e.Loop && e.Points.SequenceEqual(edge.GetComponent<EdgeCollider2DComponent>()!.Points), "capsule and edge round-trip");
        Check(new EdgeCollider2DComponent().Points.Length == 2 && !new EdgeCollider2DComponent().Loop, "edge defaults");
        var invalid = SceneDocument.Empty(new());
        var bad = new SceneObject { Name = "Malformed" }; invalid.Objects.Add(bad);
        foreach (var c in new Collider2DComponent[] { new EdgeCollider2DComponent { Points = [new(0, 0)] },
                     new EdgeCollider2DComponent { Points = [new(0, 0), new(1, 0)], Loop = true },
                     new EdgeCollider2DComponent { Points = [new(0, 0), new(0, 0), new(1, 0)] },
                     new EdgeCollider2DComponent { Points = [new(0, 0), new(float.NaN, 0)] },
                     new CapsuleCollider2DComponent { Radius = -1 } })
        {
            bad.Components.Clear(); bad.AddComponent(c);
            try { copy.RestoreObjects(invalid.CaptureObjects()); throw new Exception("Colliders: accepted " + c.GetType().Name); }
            catch (InvalidDataException) { }
        }
        Console.WriteLine("PASS capsule and edge documents: round-trip, defaults, validation");
    }

    internal static string Trace(SceneGame world, SceneDocument scene, int frame)
    {
        world.TryGetPosition(scene.Objects.Single(o => o.Name == "Runner").Id, out var p);
        world.TryGetWorldMatrix(scene.Objects.Single(o => o.Name == "Plank 4").Id, out var b);
        return string.Create(CultureInfo.InvariantCulture,
            $"{frame} {p.X:F4} {p.Y:F4} {b.Translation.X:F4} {b.Translation.Y:F4} {MathF.Atan2(b.M12, b.M11) * 180 / MathF.PI:F3}\n");
    }

    // The CourseRunner script drives the 24×56 capsule (radius 12) without input; the check watches it ride the Hill edge chain.
    private static void Course(string repository)
    {
        var path = Path.Combine(repository, "samples", "PlatformerLab", "Scenes", "ShowcaseCourse.scene");
        string Play(out int hillTicks, out float sink, out float hover, out float slowest, out Vector2 end)
        {
            var scene = SceneDocument.Load(path);
            var hill = scene.Objects.Single(o => o.Name == "Hill");
            var points = hill.GetComponent<EdgeCollider2DComponent>()!.Points.Select(q => new Vector2(hill.X + q.X * hill.Width, hill.Y + q.Y * hill.Height)).ToArray();
            float Surface(float x)
            {
                for (int i = 0; i + 1 < points.Length; i++)
                    if (x <= points[i + 1].X) return float.Lerp(points[i].Y, points[i + 1].Y, (x - points[i].X) / (points[i + 1].X - points[i].X));
                return points[^1].Y;
            }
            var trace = new StringBuilder();
            (hillTicks, sink, hover, slowest) = (0, 0, 0, float.MaxValue);
            using (var session = new GameSession(scene))
            {
                var world = session.Current;
                var runner = scene.Objects.Single(o => o.Name == "Runner").Id;
                for (int frame = 0; frame < Frames; frame++)
                {
                    session.FixedUpdate(session.Actions.Take(), Step);
                    trace.Append(Trace(world, scene, frame));
                    world.TryGetPosition(runner, out var p);
                    world.TryGetMotion(runner, out var m);
                    if (m.Support != hill.Id || p.X + 12 < points[0].X + 12 || p.X + 12 > points[^1].X - 12) continue;
                    hillTicks++;
                    // A capsule on a slope touches it off-centre, so its bottom-centre may sit up to r(1/cos θ − 1) above the line.
                    float gap = Surface(p.X + 12) - (p.Y + 56);
                    sink = Math.Max(sink, -gap); hover = Math.Max(hover, gap);
                    slowest = Math.Min(slowest, MathF.Abs(m.Velocity.X));
                }
                world.TryGetPosition(runner, out end);
            }
            return trace.ToString();
        }
        var first = Play(out int ticks, out float sink, out float hover, out float slowest, out var end);
        var second = Play(out _, out _, out _, out _, out _);
        Check(first == second, "Course trace repeats exactly");
        Check(ticks > 60 && sink < .05f && hover < 1.5f, $"capsule runner rides the edge chain without sinking ({ticks} ticks, sink {sink:F3}, hover {hover:F3} px)");
        Check(slowest > 100 && end.X >= 2240, $"no snag on the chain's 16 segments: speed stays above 100 px/s ({slowest:F1}) and the runner reaches the pennant ({end.X:F1})");
        Console.WriteLine($"PASS Course: capsule over the Hill edge chain for {ticks} ticks, sink {sink:F3} px, slowest {slowest:F1} px/s, repeatable trace {SHA256.HashData(Encoding.UTF8.GetBytes(first)).AsSpan(0, 4).ToArray().Aggregate("", (s, b) => s + b.ToString("x2"))}");
    }

    // Box2D step and contact collection allocate no more with capsules and edges than with boxes (rotation frozen: a turned
    // shape is rebuilt per query for every collider type).
    private static void Allocation()
    {
        long Measure(bool round)
        {
            var scene = SceneDocument.Empty(new());
            Add(scene, "Ground", 0, 300, 400, 20, round ? new EdgeCollider2DComponent { Points = [new(0, 0), new(1, 0)] } : new BoxCollider2DComponent());
            for (int i = 0; i < 6; i++)
                Add(scene, $"Body {i}", 20 + i * 50, 240, 20, 40, round ? new CapsuleCollider2DComponent() : new BoxCollider2DComponent(), new Rigidbody2DComponent { FreezeRotation = true });
            using var game = new SceneGame(scene);
            for (int i = 0; i < 120; i++) game.FixedUpdate(Vector2.Zero, Step);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 120; i++) game.FixedUpdate(Vector2.Zero, Step);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Measure(true); Measure(false);
        long boxes = Measure(false), rounded = Measure(true);
        Check(rounded <= boxes, $"capsules on an edge allocate {rounded} B per 120 ticks, boxes on a box {boxes} B");
        Console.WriteLine($"PASS capsule/edge step allocation: {rounded} B per 120 ticks (boxes {boxes} B)");
    }

    // 8.8 method: fixed seeded paths, full scan vs grid, identical ordered answers, candidates and p95 per query kind.
    private static void Spatial(int count)
    {
        var scene = SceneDocument.Empty(new());
        var random = new Random(16_4 + count);
        int side = (int)Math.Ceiling(Math.Sqrt(count));
        for (int i = 0; i < count; i++)
        {
            float x = i % side * 40 + random.Next(0, 16), y = i / side * 40 + random.Next(0, 16);
            Collider2DComponent c = (i % 3) switch
            {
                0 => new CapsuleCollider2DComponent { Layer = random.Next(0, 3), IsTrigger = i % 17 == 0 },
                1 => new EdgeCollider2DComponent { Layer = random.Next(0, 3), Points = [new(0, 1), new(.5f, (float)random.NextDouble()), new(1, 0)] },
                _ => new BoxCollider2DComponent { Layer = random.Next(0, 3) }
            };
            var o = new SceneObject { Id = new Guid(i + 1, 0, 0, new byte[8]), Name = $"C{i}", X = x, Y = y, Width = 12 + random.Next(0, 12), Height = 12 + random.Next(0, 20) };
            o.AddComponent(c);
            scene.Objects.Add(o);
        }
        using var game = new SceneGame(scene);
        var diagnostics = game.QueryDiagnostics;
        float extent = side * 40;
        var paths = Enumerable.Range(0, 96).Select(_ => (From: new Vector2((float)random.NextDouble() * extent, (float)random.NextDouble() * extent),
            Travel: new Vector2((float)random.NextDouble() * 160 - 80, (float)random.NextDouble() * 160 - 80))).ToArray();
        var filter = new PhysicsFilter(0b11, IncludeTriggers: true);
        string Answer(int kind, int i)
        {
            var (from, travel) = paths[i];
            return kind switch
            {
                0 => string.Join(",", game.Overlap(Shape2D.Capsule(from, from + new Vector2(0, 30), 8), filter).Select(t => t.ObjectId)),
                1 => game.Raycast(from, travel, travel.Length(), filter) is {} r ? FormattableString.Invariant($"{r.Target.ObjectId} {r.Distance:R} {r.Point} {r.Normal}") : "-",
                _ => game.Sweep(Shape2D.Capsule(from, from + new Vector2(0, 20), 5), travel, filter) is {} s ? FormattableString.Invariant($"{s.Target.ObjectId} {s.Distance:R} {s.Point} {s.Normal}") : "-"
            };
        }
        var rows = new List<string>();
        var all = new StringBuilder();
        string[] names = ["Overlap", "Raycast", "Sweep"];
        for (int kind = 0; kind < 3; kind++)
        {
            var answers = new string[2][];
            var candidates = new long[2];
            var samples = new List<double>[] { [], [] };
            for (int round = 0; round < 7; round++)
                foreach (bool indexed in round % 2 == 0 ? new[] { false, true } : [true, false])
                {
                    diagnostics.UseSpatialIndex = indexed;
                    var got = new string[paths.Length];
                    for (int i = 0; i < paths.Length; i++)
                    {
                        long t0 = Stopwatch.GetTimestamp();
                        got[i] = Answer(kind, i);
                        double us = Stopwatch.GetElapsedTime(t0).TotalMicroseconds;
                        if (round == 0) { candidates[indexed ? 1 : 0] += diagnostics.LastColliderCandidates; continue; } // warm-up
                        samples[indexed ? 1 : 0].Add(us);
                    }
                    var slot = indexed ? 1 : 0;
                    if (answers[slot] == null) answers[slot] = got;
                    else Check(answers[slot].SequenceEqual(got), $"{count} {names[kind]} repeats");
                }
            Check(answers[0].SequenceEqual(answers[1]), $"{count} {names[kind]}: grid and full scan agree");
            foreach (var a in answers[0]) all.Append(a).Append('\n');
            double P95(List<double> s) { s.Sort(); return s[(int)Math.Ceiling(.95 * s.Count) - 1]; }
            Check(candidates[1] < candidates[0], $"{count} {names[kind]}: grid narrows candidates");
            rows.Add(FormattableString.Invariant($"{names[kind]} candidates {candidates[0] / 96.0:F2} -> {candidates[1] / 96.0:F2}, p95 {P95(samples[0]):F2} -> {P95(samples[1]):F2} us, hits {answers[0].Count(a => a is not ("" or "-"))}/96"));
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(all.ToString())))[..16];
        Console.WriteLine($"PASS capsule/edge spatial {count}: identical ordered answers, sha256 {hash}; " + string.Join("; ", rows));
    }
}
