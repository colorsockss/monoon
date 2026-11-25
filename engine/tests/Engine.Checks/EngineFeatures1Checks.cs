using System.Drawing;
using System.Numerics;
using System.Text;
using Engine.Core;

// Roadmap 16.8 (integer scale), 16.2 (parallax and tiled sprites) and 16.3 (script tweens).
internal static class EngineFeatures1Checks
{
    [Check(7600, "engine-features-1")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private static void Check(bool value, string why) { if (!value) throw new Exception("Engine features 1: " + why); }

    internal static void Run(string repository, string scratch)
    {
        IntegerScale(repository);
        Console.WriteLine("PASS integer scale settings and whole-pixel cameras");
        Parallax(repository);
        Console.WriteLine("PASS parallax layers following the camera, repeated strips, tiled and sliced fallbacks (no allocation)");
        Tweens();
        Console.WriteLine("PASS script tweens: sequences, delays, game and UI clocks, owner lifetime, no allocation");
    }

    private static void IntegerScale(string repository)
    {
        // AudioLab is a test-only sample whose settings predate the option (DisplayLab now turns it on).
        var display = GameSettings.Load(Path.Combine(repository, "samples", "AudioLab"));
        Check(display.IntegerScale == null && !Encoding.UTF8.GetString(ProjectYaml.Encode(display)).Contains("integerScale"),
            "an existing GameSettings file reads the option as off and saves without the key");
        Check(ProjectYaml.Decode<GameSettings>(ProjectYaml.Encode(display with { IntegerScale = true })).IntegerScale == true, "option round-trips");
        foreach (bool integer in new[] { false, true })
        {
            var scene = SceneDocument.Empty(new GameSettings(800, 450) { IntegerScale = integer ? true : null });
            var camera = scene.CreateCamera(new(100.3f, 50.7f));
            camera.GetComponent<Camera2DComponent>()!.Zoom = 2;
            using var game = new SceneGame(scene);
            Check(game.TryGetView(camera.Id, out var view) && game.IntegerScale == integer, "camera view");
            float x = view.World.X * view.Zoom, y = view.World.Y * view.Zoom;
            Check(integer ? x == MathF.Round(x) && y == MathF.Round(y) && Math.Abs(view.World.X - (100.3f - 200)) <= .25f
                : Math.Abs(view.World.X - (100.3f - 200)) < 1e-4f && Math.Abs(view.World.Y - (50.7f - 112.5f)) < 1e-4f,
                $"integer={integer}: camera frame {view.World} at zoom {view.Zoom}");
        }
    }

    // A canvas that knows image sizes and records grid draws, or (fallback) the pieces the shared tiling emits.
    private sealed class Grid(bool own) : ICanvas2D
    {
        public readonly List<(string Path, RectangleF Local, Matrix3x2 Matrix, bool Tiled, bool Borders)> Grids = [];
        public readonly List<(RectangleF Local, SpriteRegion? Source)> Pieces = [];
        public readonly List<(string? Path, RectangleF Local, Matrix3x2 Matrix)> Affine = [];
        public bool DrawGrid(string path, RectangleF local, Matrix3x2 matrix, Color tint, SpriteRegion? source, Size size, UiBorders? borders, bool tiled)
        {
            if (!own) return SpriteGrid.Draw(this, path, local, matrix, tint, source, size, borders, tiled);
            Grids.Add((path, local, matrix, tiled, borders != null));
            return true;
        }
        public bool DrawAffine(string? path, RectangleF r, Matrix3x2 matrix, Color tint, SpriteRegion? source = null, string layout = TileGrid.Orthogonal)
        { Pieces.Add((r, source)); Affine.Add((path, r, matrix)); return true; }
        public IDisposable PushClip(RectangleF area) => new Scope();
        private sealed class Scope : IDisposable { public void Dispose() { } }
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF rectangle, Color color) { }
        public bool DrawSprite(string path, RectangleF destination) => true;
        public void DrawText(string text, float x, float y, float size, Color color) { }
        public void DrawShader(ShaderClip shader, int frame, RectangleF local, Matrix3x2 matrix, Color tint, bool additive) { }
    }

    private static void Parallax(string repository)
    {
        // The shared fallback: 2.5 tiles across and 1 down give two whole tiles and one clipped half; a 9-slice gives nine pieces.
        var pieces = new Grid(own: false);
        Check(SpriteGrid.Draw(pieces, "t.png", new(0, 0, 40, 16), Matrix3x2.Identity, Color.White, null, new(16, 16), null, tiled: true) &&
            pieces.Pieces.Select(p => (p.Local.X, p.Local.Width, p.Source!.Width)).SequenceEqual([(0f, 16f, 16), (16f, 16f, 16), (32f, 8f, 8)]),
            "tiled fallback clips the last tile");
        pieces.Pieces.Clear();
        SpriteGrid.Draw(pieces, "s.png", new(0, 0, 140, 50), Matrix3x2.Identity, Color.White, new(24, 8, 12, 12), new(12, 12), new(4, 4, 4, 4), tiled: false);
        Check(pieces.Pieces.Count == 9 && pieces.Pieces[0] == (new RectangleF(0, 0, 4, 4), new SpriteRegion(24, 8, 4, 4)) &&
            pieces.Pieces[4] == (new RectangleF(4, 4, 132, 42), new SpriteRegion(28, 12, 4, 4)) && pieces.Pieces[8].Local == new RectangleF(136, 46, 4, 4),
            "sliced fallback keeps 4 px corners inside an atlas cell and stretches the centre");

        // The PlatformerLab showcase: Sky backing (scroll 0), Moon (0.05) and a repeating Sky strip (0.15) behind a camera that
        // follows the runner.
        var path = Path.Combine(repository, "samples", "PlatformerLab", "Scenes", "Showcase.scene");
        var scene = SceneDocument.Load(path);
        Check(scene.SchemaVersion == 33, "parallax scenes need schema 33");
        Guid Id(string name) => scene.Objects.Single(o => o.Name == name).Id;
        using var session = new GameSession(scene);
        var game = session.Current;
        var layers = new[] { ("Sky backing", 0f), ("Moon", .05f), ("Sky", .15f) };
        Check(!game.TryGetParallaxOffset(Id("Sign"), out _), "a plain sprite has no parallax offset");
        var canvas = new Grid(own: true);
        var camera = Id("Camera");
        float skyWidth = scene.Objects.Single(o => o.Name == "Sky").Width;
        Check(game.TryGetView(camera, out var first), "camera view");
        var home = first.Center;
        float farthest = 0;
        long allocated = 0;
        session.Actions.Press("Right");
        for (int tick = 0; tick <= 120; tick++)
        {
            if (tick > 0) session.FixedUpdate(session.Actions.Take(), 1 / 60f);
            Check(game.TryGetView(camera, out var view), "camera view");
            var travel = view.Center - home;
            farthest = Math.Max(farthest, travel.X);
            // PlatformerLab turns on integer scale: offsets snap to whole screen pixels at the camera's zoom.
            float Snap(float v) => game.IntegerScale ? MathF.Round(v * view.Zoom) / view.Zoom : v;
            foreach (var (name, scroll) in layers)
                Check(game.TryGetParallaxOffset(Id(name), out var offset) && Math.Abs(offset.X - Snap(travel.X * (1 - scroll))) < 1e-3f &&
                    Math.Abs(offset.Y - Snap(travel.Y)) < 1e-3f, $"tick {tick}: {name} follows {scroll} of the camera's {travel} travel ({offset})");
            canvas.Grids.Clear(); canvas.Pieces.Clear(); canvas.Affine.Clear();
            // The world pass only: this test canvas measures UI text with the allocating default.
            game.PrepareCameraDraw(1);
            long before = GC.GetAllocatedBytesForCurrentThread();
            game.DrawCamera(canvas, camera, 1);
            if (tick >= 60) allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            // The repeating Sky layer is drawn in whole strips from its origin until the camera frame is covered.
            var sky = canvas.Affine.Where(g => g.Path?.EndsWith("Showcase/Sky.png", StringComparison.Ordinal) == true).ToArray();
            var covered = sky.Select(g => Affine2D.Bounds(g.Local, g.Matrix)).ToArray();
            Check(sky.Length is >= 1 and <= 3 && sky.All(g => g.Local.X % skyWidth == 0) && covered.Min(r => r.Left) <= view.World.Left + .001f &&
                covered.Max(r => r.Right) >= view.World.Right - .001f, $"tick {tick}: the repeated Sky strip covers the view from a whole period ({sky.Length} draws)");
            Check(canvas.Affine.Count(g => g.Path?.EndsWith("Showcase/Moon.png", StringComparison.Ordinal) == true) <= 1, $"tick {tick}: one Moon draw");
        }
        session.Actions.Release("Right");
        Check(farthest > 50, $"the follow camera travels with the runner ({farthest:F1} px)");
        Check(allocated == 0, $"drawing parallax layers allocates nothing ({allocated} bytes over 60 frames)");
    }

    private static void Tweens()
    {
        var scene = SceneDocument.Empty(new());
        var box = new SceneObject { Name = "Box", Width = 10, Height = 10 };
        box.AddComponent(new SpriteRendererComponent { SquareColor = "#FF0000" });
        scene.Objects.Add(box);
        var button = scene.CreateUi("Button", scene.CreateUi("Canvas", null));
        scene.CreateCamera(new(0, 0));
        using var game = new SceneGame(scene);
        void Tick() => game.FixedUpdate(new GameInput(Vector2.Zero, false), 1f / 60);
        float value = 0;
        Action<float> apply = v => value = v; // cached: the tween itself must not allocate
        // Delay, then a linear move over half a second; a parallel colour and alpha; a Float on the same object.
        var move = game.Tween(box.Id).Delay(.25f).Move(new(30, 0), .5f, TweenEase.Linear);
        game.Tween(box.Id).Color(Color.FromArgb(0, 0, 255), .5f, TweenEase.Linear).Alpha(.5f, .5f, TweenEase.Linear);
        game.Tween(box.Id).Float(10, 20, 1, apply, TweenEase.Linear);
        Vector2 Position() => game.TryGetLocalTransform(box.Id, out var local) ? local.Position : throw new Exception("box");
        Color Drawn() => game.TryGetSpriteColor(box.Id, out var c) ? c : throw new Exception("color");
        long allocated = 0;
        for (int tick = 1; tick <= 75; tick++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Tick();
            if (tick > 2) allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            if (tick == 15) Check(Position().Length() < 1e-3f && Drawn().B is > 120 and < 135, $"tick 15: delay holds the move, colour half way ({Drawn()})");
            if (tick == 30) Check(Math.Abs(Position().X - 15) < 1e-3f && Drawn() == Color.FromArgb(255, 0, 0, 255) && Math.Abs(value - 15) < 1e-3f,
                $"tick 30: move half way {Position()}, colour done {Drawn()}, float {value}");
            if (tick == 45) Check(Position().X == 30 && !move.IsPlaying && Drawn().A == 191, $"tick 45: the sequence ends exactly at 0.75 s, alpha half way to 0.5 ({Drawn()})");
        }
        Check(Math.Abs(value - 20) < 1e-6f && Drawn() == Color.FromArgb(128, 0, 0, 255), "float and alpha reach their targets");
        // The same ticks without tweens: the scene's own UI pass allocates a little; tweens must add nothing to it.
        long idle = 0;
        for (int tick = 3; tick <= 75; tick++) { long before = GC.GetAllocatedBytesForCurrentThread(); Tick(); idle += GC.GetAllocatedBytesForCurrentThread() - before; }
        Check(allocated == idle, $"running tweens allocate nothing ({allocated} bytes with tweens, {idle} without, over 73 ticks)");

        // UI clock: a press tween on the button plays while the game is paused; the box tween on game time waits.
        var rest = game.Ui.Bounds(button.Id)!.Value;
        game.Time.IsPaused = true;
        game.Tween(box.Id).Move(new(0, 0), .1f);
        game.Tween(button.Id, TweenClock.Ui).UiRect(new(2, 2, -4, -4), .05f, TweenEase.Linear).UiRect(default, .05f, TweenEase.Linear);
        // A paused GameLoop runs only the UI pass each frame.
        void Frame() => game.UpdateUi(default, 1.0 / 60);
        for (int i = 0; i < 3; i++) Frame();
        var pressed = game.Ui.Bounds(button.Id)!.Value;
        Check(pressed == new RectangleF(rest.X + 2, rest.Y + 2, rest.Width - 4, rest.Height - 4) && Position().X == 30,
            $"paused: UI tween pressed the button {pressed} from {rest}, game tween waits at {Position()}");
        for (int i = 0; i < 3; i++) Frame();
        Check(game.Ui.Bounds(button.Id) == rest && game.Ui.BoxOffset(button.Id) == RectangleF.Empty, "the press returns to the laid-out box and clears its offset");
        game.Time.IsPaused = false;

        // A disabled owner stops its tweens; stale handles ignore calls.
        var stopped = game.Tween(box.Id).Scale(new(3, 3), 1);
        Tick();
        game.SetActive(box.Id, false);
        Tick();
        game.SetActive(box.Id, true);
        stopped.Scale(new(5, 5), 1);
        for (int i = 0; i < 10; i++) Tick();
        Check(!stopped.IsPlaying && game.TryGetLocalTransform(box.Id, out var after) && after.Scale.X < 1.1f, "disabling the owner ends its tweens");
        try { game.Tween(Guid.NewGuid()); throw new Exception("tween on a missing object"); } catch (InvalidOperationException) { }
    }
}
