using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

// Roadmap 16.6: Text 2D. YAML and schema, placement under the camera, sorting with sprites, script text, the Text build
// feature, and 100 rising damage numbers stepped and drawn with the player's SDL canvas (SDL_ttf labels) without allocating.
internal static class WorldTextChecks
{
    [Check(4900, "world-text")] static void Registered(CheckRun c) => Run(c.Scratch);
    private const float Step = 1f / 60;
    private static void Check(bool value, string why)
    {
        if (!value) throw new Exception("Text 2D: " + why);
        Console.WriteLine("PASS Text 2D: " + why);
    }

    private sealed class Labels : ICanvas2D
    {
        public readonly List<(string Text, float X, float Y, float Size, Color Color)> Drawn = [];
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF rectangle, Color color) { }
        public bool DrawSprite(string path, RectangleF destination) => true;
        public bool DrawAffine(string? path, RectangleF r, Matrix3x2 matrix, Color tint, SpriteRegion? source = null, string layout = TileGrid.Orthogonal)
        {
            Drawn.Add(("[sprite]", matrix.M31, matrix.M32, 0, tint));
            return true;
        }
        public void DrawText(string text, float x, float y, float size, Color color) => Drawn.Add((text, x, y, size, color));
        public float MeasureText(string text, float size) => text.Length * size * .5f;
    }

    internal static void Run(string scratch)
    {
        var scene = SceneDocument.Empty(new());
        var camera = scene.Camera!;
        camera.GetComponent<Camera2DComponent>()!.Zoom = 2;
        var sign = new SceneObject { Name = "Sign", X = camera.X - 50, Y = camera.Y - 10, Width = 100, Height = 20 };
        sign.AddComponent(new Text2DComponent { Text = "상점", Size = 10, Color = "#FFD000", SortingLayer = 1 });
        var crate = new SceneObject { Name = "Crate", X = camera.X - 50, Y = camera.Y - 10, Width = 100, Height = 20 };
        crate.AddComponent(new SpriteRendererComponent());
        var tag = new SceneObject { Name = "Tag", X = camera.X, Y = camera.Y + 40, Width = 40, Height = 10 };
        tag.AddComponent(new Text2DComponent { Text = "<b>12</b> <color=#FF0000>HP</color>", RichText = true, Align = TextAlign2D.Left, Size = 8, TextKey = "missing.key" });
        scene.Objects.AddRange([sign, crate, tag]);
        var file = Path.Combine(Directory.CreateDirectory(Path.Combine(scratch, "WorldText")).FullName, "WorldText.scene");
        scene.Save(file);
        var yaml = File.ReadAllText(file);
        var reopened = SceneDocument.Load(file);
        Check(reopened.SchemaVersion == 40 && yaml.Contains("type: text2D") &&
              reopened.Objects.Single(o => o.Id == tag.Id).GetComponent<Text2DComponent>() == tag.GetComponent<Text2DComponent>(),
            "Text 2D round-trips through YAML and marks the scene as version 40");
        var plain = SceneDocument.Empty(new());
        plain.Objects.Add(new SceneObject { Name = "Box", Width = 4, Height = 4 });
        plain.Save(file + ".plain.scene");
        Check(SceneDocument.Load(file + ".plain.scene").SchemaVersion < 40, "scenes without Text 2D keep their older schema version");
        foreach (var bad in new[] { new Text2DComponent { Size = 0 }, new Text2DComponent { Color = "gold" }, new Text2DComponent { Align = (TextAlign2D)7 } })
        {
            var invalid = SceneDocument.Load(file);
            invalid.Objects.Single(o => o.Id == crate.Id).AddComponent(bad);
            Check(Throws(() => invalid.Save(file + ".bad.scene")), $"Save rejects size {bad.Size}, colour {bad.Color}, align {(int)bad.Align}");
        }
        Check(BuildFeatures.Of(new Text2DComponent()) == EngineFeature.Text, "a shipped Text 2D keeps SDL_ttf in the build (Text feature)");

        using var game = new SceneGame(reopened);
        game.FixedUpdate(Vector2.Zero, Step);
        var canvas = new Labels();
        game.Draw(canvas, 1);
        var shop = canvas.Drawn.Single(d => d.Text == "상점");
        // Zoom 2 about the view centre (400, 225): the 100×20 sign is 200×40 on screen; 2 chars × 10 × .5 = 10 world units wide.
        Check(shop.Size == 20 && shop.X == 400 - 10 && shop.Y == 225 - 14 && shop.Color == Color.FromArgb(255, 208, 0),
            $"a centred label follows the camera's zoom and position ({shop.X}, {shop.Y}, size {shop.Size})");
        int sprite = canvas.Drawn.FindIndex(d => d.Text == "[sprite]"), label = canvas.Drawn.FindIndex(d => d.Text == "상점");
        Check(sprite >= 0 && sprite < label, "sorting layer 1 draws the label over a layer-0 sprite at the same place");
        var rich = canvas.Drawn.Where(d => d.Text is "12" or "HP").ToArray();
        Check(rich.Length == 3 && rich.Count(d => d.Text == "12") == 2 && rich.Single(d => d.Text == "HP").Color == Color.FromArgb(255, 0, 0) &&
              rich[0].X == 400, "rich text tags draw bold twice and colour runs; a missing translation key falls back to Text");
        Check(game.SetWorldText(sign.Id, "닫힘") && !game.SetWorldText(crate.Id, "x") && !game.SetWorldText(Guid.NewGuid(), "x"),
            "SetWorldText accepts Text 2D objects only");
        canvas.Drawn.Clear();
        game.Draw(canvas, 1);
        Check(canvas.Drawn.Any(d => d.Text == "닫힘") && !canvas.Drawn.Any(d => d.Text == "상점"), "script text replaces the label on the next frame");
        DamageNumbers();
    }

    // 100 labels with distinct numbers rising on Position Tween 2D, as a hit-feedback burst. The player's SDL canvas keeps one
    // label texture per string (SdlText), so after the first frame neither stepping nor drawing allocates.
    private static unsafe void DamageNumbers()
    {
        var scene = SceneDocument.Empty(new());
        for (int i = 0; i < 100; i++)
        {
            var number = new SceneObject { Name = $"Damage {i}", X = 40 + i % 10 * 72, Y = 60 + i / 10 * 36, Width = 48, Height = 20 };
            number.AddComponent(new Text2DComponent { Text = (i * 7 + 3).ToString(System.Globalization.CultureInfo.InvariantCulture), Size = 18, Color = "#FFE070", SortByY = true });
            number.AddComponent(new PositionTween2DComponent { OffsetX = 0, OffsetY = -24, Duration = .75f, Repeat = TweenRepeat.PingPong, PlayOnStart = true });
            scene.Objects.Add(number);
        }
        using var game = new SceneGame(scene);
        var target = Sdl.PInvoke.CreateSurface(800, 450, SdlPixelFormat.Rgba32);
        if (target == null) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        try
        {
            using var renderer = Sdl.Render.CreateSoftwareRenderer(target);
            using var canvas = new SdlCanvas(renderer, batchSprites: true);
            // Step allocations belong to the existing tween and tick path (reported separately); the label path must be zero.
            long drawBytes = 0, stepBytes = 0;
            void Frame()
            {
                long a = GC.GetAllocatedBytesForCurrentThread();
                game.FixedUpdate(Vector2.Zero, Step);
                long b = GC.GetAllocatedBytesForCurrentThread();
                game.Draw(canvas, 1);
                canvas.FlushSprites();
                renderer.RenderPresent(); // the software renderer rasterises its queued commands here
                (stepBytes, drawBytes) = (stepBytes + b - a, drawBytes + GC.GetAllocatedBytesForCurrentThread() - b);
            }
            var warm = Stopwatch.StartNew();
            for (int i = 0; i < 120 || warm.ElapsedMilliseconds < 500; i++) Frame();
            (stepBytes, drawBytes) = (0, 0);
            var times = new double[600];
            long submissions = canvas.DrawSubmissions;
            for (int i = 0; i < times.Length; i++)
            {
                long start = Stopwatch.GetTimestamp();
                Frame();
                times[i] = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
            }
            submissions = (canvas.DrawSubmissions - submissions) / times.Length;
            Array.Sort(times);
            Check(drawBytes == 0 && submissions >= 100,
                FormattableString.Invariant($"100 rising damage numbers: step + draw + present (SDL software renderer, SDL_ttf labels) median {times[300]:F0} µs, p95 {times[570]:F0} µs; draw {drawBytes} B, step {stepBytes} B over 600 frames; {submissions} draw submissions per frame"));
        }
        finally { Sdl.PInvoke.DestroySurface(target); }
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (InvalidDataException) { return true; }
    }
}
