using System.Drawing;
using System.Numerics;
using Engine.Core;

// Merging split screen (Camera 2D SplitWith) and per-camera screen effects (Session.Screen.For).
internal static class CameraSplitChecks
{
    [Check(4750, "camera-split", "screen-effects")] static void Registered(CheckRun c) => Run(c.Scratch);
    private static void Check(bool ok, string why) { if (!ok) throw new Exception("Camera split: " + why); }
    private static void Near(float a, float b, string why) => Check(Math.Abs(a - b) < .002f, $"{why}: {a} vs {b}");
    private const float Tick = 1f / 60;

    private sealed record Rig(SceneDocument Scene, SceneObject Main, SceneObject Partner, SceneObject Map, SceneObject P, SceneObject Q);

    // 800×450. Main (left half) follows P, Partner (right half) follows Q; merge within 200 px, split past 300, 0.5 s blend.
    private static Rig Build()
    {
        var scene = SceneDocument.Empty(new());
        var p = new SceneObject { Name = "P", X = 495, Y = 295, Width = 10, Height = 10 };
        var q = new SceneObject { Name = "Q", X = 1495, Y = 295, Width = 10, Height = 10 };
        p.AddComponent(new SpriteRendererComponent()); q.AddComponent(new SpriteRendererComponent());
        scene.Objects.AddRange([p, q]);
        var main = scene.Camera!;
        var partner = scene.CreateCamera(new(1500, 300));
        var map = scene.CreateCamera(new(1000, 300));
        map.GetComponent<Camera2DComponent>()!.Output = Camera2DComponent.Minimap;
        map.GetComponent<Camera2DComponent>()!.Zoom = .125f;
        var bounds = new CameraRect(0, 0, 2000, 600);
        var a = main.GetComponent<Camera2DComponent>()!;
        a.Viewport = new(0, 0, .5f, 1); a.Follow = p.Id; a.Bounds = bounds;
        a.SplitWith = partner.Id; a.MergeDistance = 200; a.SplitDistance = 300; a.BlendSeconds = .5f;
        var b = partner.GetComponent<Camera2DComponent>()!;
        b.Output = Camera2DComponent.Extra; b.Viewport = new(.5f, 0, .5f, 1); b.Follow = q.Id; b.Bounds = bounds;
        return new(scene, main, partner, map, p, q);
    }
    private static CameraView View(SceneGame g, Guid id) => g.TryGetView(id, out var v) ? v : throw new Exception("Camera split: missing view");
    private static void Step(SceneGame g, Guid id, Vector2 move) { Check(g.TryMoveStep(id, move), "move"); g.FixedUpdate(Vector2.Zero, Tick); }

    internal static void Run(string scratch)
    {
        var rig = Build();
        rig.Scene.Validate();
        Check(rig.Scene.SchemaVersion == 43, $"a split camera raises the schema to 43 ({rig.Scene.SchemaVersion})");
        var file = Path.Combine(Directory.CreateDirectory(Path.Combine(scratch, "CameraSplit")).FullName, "Split.scene");
        rig.Scene.Save(file);
        var yaml = File.ReadAllText(file);
        var reopened = SceneDocument.Load(file);
        Check(yaml.Contains($"splitWith: {rig.Partner.Id}") && yaml.Contains("mergeDistance: 200") && yaml.Contains("splitDistance: 300") &&
              yaml.Contains("blendSeconds: 0.5") && reopened.Camera!.GetComponent<Camera2DComponent>() == rig.Main.GetComponent<Camera2DComponent>(),
            "YAML round trip");
        var plain = SceneDocument.Empty(new()); plain.Validate();
        var plainFile = Path.Combine(scratch, "CameraSplit", "Plain.scene"); plain.Save(plainFile);
        Check(plain.SchemaVersion < 43 && !File.ReadAllText(plainFile).Contains("splitWith"), $"cameras without a split keep the older schema ({plain.SchemaVersion})");

        Rectangle[] Ports(SceneGame g) => [View(g, rig.Main.Id).Viewport, View(g, rig.Partner.Id).Viewport, View(g, rig.Map.Id).Viewport];
        var inset = new Rectangle(584, 14, 200, 112);
        using (var g = new SceneGame(rig.Scene))
        {
            g.FixedUpdate(Vector2.Zero, Tick);
            var ports = Ports(g);
            Check(ports[0] == new Rectangle(0, 0, 400, 450) && ports[1] == new Rectangle(400, 0, 400, 450) && ports[2] == inset,
                $"targets 1000 px apart start split at once, P on the left ({string.Join(" ", ports)})");
            Near(View(g, rig.Main.Id).Center.X, 500, "split: main follows P"); Near(View(g, rig.Partner.Id).Center.X, 1500, "split: partner follows Q");

            Step(g, rig.Q.Id, new(-750, 0));
            Check(Ports(g)[0].Width == 400, "250 px is between the merge and split distances: still split");
            Step(g, rig.Q.Id, new(-100, 0));
            var first = Ports(g);
            Check(first[0].Width is > 400 and < 800 && first[1].X == first[0].Right && first[1].Right == 800, $"150 px starts merging ({first[0]} {first[1]})");
            int widest = 0, widthJump = 0; float centerJump = 0;
            var last = View(g, rig.Main.Id);
            for (int i = 0; i < 40; i++)
            {
                g.FixedUpdate(Vector2.Zero, Tick);
                var v = View(g, rig.Main.Id);
                widthJump = Math.Max(widthJump, v.Viewport.Width - last.Viewport.Width);
                centerJump = Math.Max(centerJump, Vector2.Distance(v.Center, last.Center));
                widest = Math.Max(widest, v.Viewport.Width);
                Check(v.World.Left >= 0 && v.World.Right <= 2000.001f && v.World.Top >= 0 && v.World.Bottom <= 600.001f, "Bounds hold while blending");
                last = v;
            }
            Check(widthJump <= 21 && centerJump < 20, $"the blend is smooth (max {widthJump} px width, {centerJump:F1} px centre per tick)");
            var merged = View(g, rig.Main.Id);
            Check(merged.Viewport == new Rectangle(0, 0, 800, 450) && Ports(g)[2] == inset, "merged: the main view fills the union; the minimap stays");
            Near(merged.Center.X, 575, "merged: the view follows the midpoint of P (500) and Q (650)");
            Check(View(g, rig.Partner.Id) == merged, "a merged partner reports the shared view");
            var drawn = new DrawCapture(); g.Draw(drawn, 1);
            Check(drawn.Clips.SequenceEqual([inset]), $"merged: only the full main view and the minimap draw ({string.Join(" ", drawn.Clips)})");

            // Hysteresis: anywhere between 200 and 300 px keeps the merged view.
            Step(g, rig.Q.Id, new(140, 0));
            for (int i = 0; i < 60; i++) { Step(g, rig.Q.Id, new(i % 2 == 0 ? -80 : 80, 0)); Check(Ports(g)[0].Width == 800, $"no flicker between the distances (tick {i})"); }
            Step(g, rig.Q.Id, new(20, 0));
            Check(Ports(g)[0].Width < 800, "310 px splits again");

            // Sides are chosen when the views part: Q now left of P gives P the right half.
            Step(g, rig.Q.Id, new(-310, 0));
            for (int i = 0; i < 40; i++) g.FixedUpdate(Vector2.Zero, Tick);
            Check(Ports(g)[0].Width == 800, "together again: merged");
            Step(g, rig.Q.Id, new(-400, 0));
            for (int i = 0; i < 40; i++) g.FixedUpdate(Vector2.Zero, Tick);
            ports = Ports(g);
            Check(ports[0] == new Rectangle(400, 0, 400, 450) && ports[1] == new Rectangle(0, 0, 400, 450), $"Q on the left takes the left half ({ports[0]} {ports[1]})");
            Near(View(g, rig.Partner.Id).Center.X, 200, "split again: partner follows Q to x 100, held at 200 by Bounds");

            // Without the partner the main view takes the whole union and follows P; back on, the distance rule splits again.
            g.SetActive(rig.Partner.Id, false);
            for (int i = 0; i < 40; i++) g.FixedUpdate(Vector2.Zero, Tick);
            Check(View(g, rig.Main.Id).Viewport == new Rectangle(0, 0, 800, 450), "an inactive partner merges the main view");
            Near(View(g, rig.Main.Id).Center.X, 500, "alone, the main view follows P");
            g.SetActive(rig.Partner.Id, true);
            for (int i = 0; i < 40; i++) g.FixedUpdate(Vector2.Zero, Tick);
            Check(Ports(g)[0] == new Rectangle(400, 0, 400, 450), "the partner back on splits again on the same sides");

            Check(Allocated(g) <= Allocated(null), "a split pair and per-camera effects add no allocation per frame");
        }
        Effects();
        Validation();
        Duplication();
        Console.WriteLine("PASS camera split: start state, hysteresis band, smooth merge/split blend, midpoint, sides, Bounds, minimap untouched, " +
                          "no allocation, schema 43, YAML, validation, duplication; per-camera shake/flash/fade/adjust stay in their viewport, comfort scaling, determinism");
    }

    // Bytes 60 fixed ticks and drawn frames allocate after warming up; null measures the same scene without the split.
    private static long Allocated(SceneGame? split)
    {
        SceneGame? own = null;
        if (split == null) { var rig = Build(); rig.Main.GetComponent<Camera2DComponent>()!.SplitWith = null; own = split = new(rig.Scene); }
        else
        {
            // Running per-camera effects sample without allocating too.
            var effects = split.State.Screen.For(split.ActiveCameraId!.Value);
            effects.Shake(4, 10); effects.Flash(Color.Red, 10); effects.Adjust(Color.Gray, .1f, 5);
        }
        var canvas = new Quiet();
        for (int i = 0; i < 10; i++) { split.FixedUpdate(Vector2.Zero, Tick); split.Draw(canvas, .5f); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 60; i++) { split.FixedUpdate(Vector2.Zero, Tick); split.Draw(canvas, .5f); }
        long used = GC.GetAllocatedBytesForCurrentThread() - before;
        own?.Dispose();
        Console.WriteLine($"camera split: {(own == null ? "split" : "plain")} frames allocate {used} bytes per 60");
        return used;
    }

    private static void Effects()
    {
        // Same scene, same camera IDs: per-camera shake phases come from the camera ID.
        var shared = Build();
        Vector2[] Run(out ScreenFrame global)
        {
            var rig = shared;
            using var g = new SceneGame(rig.Scene);
            g.FixedUpdate(Vector2.Zero, Tick);
            g.State.Screen.For(rig.Main.Id).Shake(8, 1);
            g.State.Screen.For(rig.Partner.Id).Shake(8, 1);
            var shakes = new Vector2[20];
            for (int i = 0; i < shakes.Length; i++) { g.FixedUpdate(Vector2.Zero, Tick); shakes[i] = g.State.Screen.For(rig.Main.Id).Current.Shake; }
            global = g.State.Screen.Current;
            Check(g.State.Screen.For(rig.Partner.Id).Current.Shake != shakes[^1], "each camera has its own seeded shake phase");
            return shakes;
        }
        var once = Run(out var untouched);
        Check(once.SequenceEqual(Run(out _)) && once[0] != Vector2.Zero, "the same calls on the same ticks shake the same way");
        Check(!untouched.Moves && untouched.Flash.A == 0, "per-camera calls leave the screen-wide effects alone");

        var rig = Build();
        using var game = new SceneGame(rig.Scene);
        game.FixedUpdate(Vector2.Zero, Tick);
        var main = game.State.Screen.For(rig.Main.Id);
        var partner = game.State.Screen.For(rig.Partner.Id);
        Check(ReferenceEquals(main, game.State.Screen.For(rig.Main.Id)), "For returns the same effects for a camera");
        try { main.For(rig.Partner.Id); throw new Exception("Camera split: For on one camera's effects accepted"); }
        catch (InvalidOperationException) { }

        var still = new DrawCapture(); game.Draw(still, 1);
        main.Shake(8, 1);
        partner.Flash(Color.Red, 1);
        main.Adjust(Color.FromArgb(200, 100, 50), -.2f);
        game.FixedUpdate(Vector2.Zero, Tick);
        var drawn = new DrawCapture(); game.Draw(drawn, 1);
        var offset = main.Current.Shake;
        Check(offset != Vector2.Zero && Vector2.Distance(still.Square(0) - drawn.Square(0), offset) < .01f, "main shake moves the main view");
        Check(still.Square(1) == drawn.Square(1), "main shake leaves the partner view");
        static bool Red(Color c) => (c.ToArgb() & 0xffffff) == 0xff0000 && c.A > 0;
        Check(drawn.Fills.Count(f => Red(f.Color)) == 1 && drawn.Fills.Single(f => Red(f.Color)) is { Clip: 1, Rect: var flash } && flash == new RectangleF(400, 0, 400, 450),
            "partner flash covers the partner viewport only");
        var grade = main.Current;
        Check(drawn.Adjusts.SequenceEqual([(new RectangleF(0, 0, 400, 450), grade.Multiply, grade.Lift)]) && !drawn.Log.Contains("begin"),
            "main colour adjustment covers the main viewport only, without the screen layer");
        partner.Fade(Color.Black, 0);
        game.FixedUpdate(Vector2.Zero, Tick);
        drawn = new(); game.Draw(drawn, 1);
        Check(drawn.Fills.Any(f => f.Color.ToArgb() == Color.Black.ToArgb() && f.Rect == new RectangleF(400, 0, 400, 450)) &&
              !drawn.Fills.Any(f => f.Color.ToArgb() == Color.Black.ToArgb() && f.Rect.Width == 800), "partner fade stays in the partner viewport");

        // Comfort settings scale per-camera effects the same way.
        var full = main.Current; int flashFull = partner.Current.Flash.A;
        game.Ui.Preferences.ShakeScale = .25f; game.Ui.Preferences.FlashScale = .25f;
        var reduced = main.Current;
        Check(Vector2.Distance(reduced.Shake, full.Shake * .25f) < 1e-4f && flashFull > 200 && Math.Abs(partner.Current.Flash.A - flashFull * .25f) <= 1,
            "ShakeScale and FlashScale apply to per-camera effects");
        partner.Clear(); main.Clear();
        game.State.Screen.Flash(Color.White, 1);
        game.FixedUpdate(Vector2.Zero, Tick);
        drawn = new(); game.Draw(drawn, 1);
        Check(drawn.Fills.Any(f => f.Clip < 0 && f.Rect == new RectangleF(0, 0, 800, 450) && f.Color.R == 255 && f.Color.A > 0) &&
              drawn.Adjusts.Count == 0, "the screen-wide flash still covers the whole screen");
    }

    private static void Validation()
    {
        var reopened = SceneDocument.Empty(new());
        foreach (var bad in new Camera2DComponent[] { new() { SplitWith = Guid.NewGuid() }, new() { SplitWith = Guid.NewGuid(), MergeDistance = 100, SplitDistance = 50 },
            new() { Output = Camera2DComponent.Extra, SplitWith = Guid.NewGuid(), MergeDistance = 1, SplitDistance = 2 }, new() { BlendSeconds = 11 },
            new() { MergeDistance = float.NaN } })
        {
            var doc = SceneDocument.Empty(new()); var cam = doc.Camera!; cam.RemoveComponent<Camera2DComponent>(); cam.AddComponent(bad);
            try { reopened.RestoreObjects(doc.CaptureObjects()); throw new Exception("Camera split: invalid split camera accepted"); }
            catch (InvalidDataException) { }
        }
    }

    private static void Duplication()
    {
        var rig = Build();
        var group = new SceneObject { Name = "Cameras" };
        rig.Scene.Objects.Add(group);
        rig.Scene.Reparent(rig.Main, group); rig.Scene.Reparent(rig.Partner, group);
        var copy = rig.Scene.DuplicateObject(group);
        var cams = rig.Scene.Objects.Where(o => o.Parent == copy).ToArray();
        var lens = cams.Single(o => o.GetComponent<Camera2DComponent>()!.Output == Camera2DComponent.Primary).GetComponent<Camera2DComponent>()!;
        var second = cams.Single(o => o.GetComponent<Camera2DComponent>()!.Output == Camera2DComponent.Extra);
        Check(lens.SplitWith == second.Id && lens.Follow == rig.P.Id, "a duplicated pair splits with its own copy and keeps an outside Follow");
    }

    private sealed class Quiet : ICanvas2D
    {
        private sealed class Pop : IDisposable { public void Dispose() { } }
        private readonly Pop pop = new();
        public IDisposable PushClip(RectangleF r) => pop;
        public void AdjustRectangle(RectangleF area, Color multiply, byte lift) { }
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF rect, Color color) { }
        public bool DrawSprite(string path, RectangleF rect) => true;
    }

    private sealed class DrawCapture : ICanvas2D
    {
        public List<RectangleF> Clips = [];
        public List<(int Clip, RectangleF Rect, Color Color)> Fills = [];
        public List<(RectangleF, Color, byte)> Adjusts = [];
        public List<string> Log = [];
        private int clip = -1;
        public IDisposable PushClip(RectangleF r) { Clips.Add(r); clip = Clips.Count - 1; return new Pop(this); }
        private sealed class Pop(DrawCapture owner) : IDisposable { public void Dispose() => owner.clip = -1; }
        public void BeginColorAdjust() => Log.Add("begin");
        public void EndColorAdjust(Color multiply, byte lift) => Log.Add("end");
        public void AdjustRectangle(RectangleF area, Color multiply, byte lift) => Adjusts.Add((area, multiply, lift));
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF rect, Color color) => Fills.Add((clip, rect, color));
        public bool DrawSprite(string path, RectangleF rect) => true;
        // The white target square drawn inside clip n, as its top-left corner.
        public Vector2 Square(int n) => Fills.Where(f => f.Clip == n && f.Color.ToArgb() == Color.White.ToArgb()).Select(f => new Vector2(f.Rect.X, f.Rect.Y)).Single();
    }
}
