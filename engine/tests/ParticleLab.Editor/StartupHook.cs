using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;
using RectangleF = System.Drawing.RectangleF;
using Matrix3x2 = System.Numerics.Matrix3x2;

// Particle panel probe (roadmap 14.6/14.7) on a ParticleLab copy that starts in Curves: selects Flame, opens the Particle
// panel, compares the editor preview with the committed Scene Play/Release/AOT trace, scrubs, plays (no file changes), then
// edits a curve key, a gradient key and a colour through the controls, checks the asset file and that Undo/Redo restore its
// bytes, and measures row alignment at several window sizes and with a long object name.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_PARTICLE_EDITOR")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string LongName = "Flame emitter with an intentionally long object name for trimming in the Particle panel";
    private static readonly Dictionary<string, object?> result = [];

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Run(life.MainWindow!));

    private static object? Get(object target, string name)
    {
        var type = target.GetType();
        return type.GetProperty(name, Any)?.GetValue(target) ?? type.GetField(name, Any)?.GetValue(target);
    }
    private static object? Call(object target, string method, params object?[] args) =>
        target.GetType().GetMethods(Any).Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static void Require(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }
    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static Dictionary<string, string> Files(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .Where(f => f.Contains("/Assets/") || f.Contains("/Scenes/")).ToDictionary(f => Path.GetRelativePath(root, f), f => Sha(File.ReadAllBytes(f)));

    private static void Capture(Visual area, string name)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(area.Bounds.Width), (int)Math.Ceiling(area.Bounds.Height)));
        bitmap.Render(area);
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    // The previews draw through the native SDL view, which a RenderTargetBitmap leaves blank: draw them with the player's
    // software SDL canvas instead.
    private static unsafe void Snap(object preview, int width, int height, RectangleF frame, Matrix3x2 view, string name)
    {
        var surface = Sdl.PInvoke.CreateSurface(width, height, SdlPixelFormat.Rgba32);
        var renderer = Sdl.Render.CreateSoftwareRenderer(surface);
        using (var canvas = new SdlCanvas(renderer, previewShader: ShaderPixels.Preview))
        {
            canvas.Clear(SceneGame.Background);
            Call(preview, "DrawAll", canvas, frame, view);
            canvas.FlushSprites();
            var pixels = Sdl.PInvoke.RenderReadPixels(renderer, null);
            if (pixels == null || Sdl.PInvoke.SavePNG(pixels, Path.Combine(Output, name + ".png")) == 0) throw new IOException(Sdl.PInvoke.GetError());
            Sdl.PInvoke.DestroySurface(pixels);
        }
        renderer.Dispose();
        Sdl.PInvoke.DestroySurface(surface);
    }

    private static Border Panel(Window main)
    {
        var dock = main.FindControl<Engine.Editor.DockHost>("Dock")!;
        return (Border)dock.Panels.Single(p => Engine.Editor.DockHost.GetPanel(p) == "Particle");
    }

    // Label-column rows in the panel's sections: every label and every field must start at one x.
    private static object Rows(Window main, string capture)
    {
        var area = Panel(main);
        var sections = (StackPanel)Get(Get(main, "particlePanel")!, "Sections")!;
        double X(Visual v) => v.TranslatePoint(default, area)!.Value.X;
        double Mid(Control c) => c.TranslatePoint(new(0, c.Bounds.Height / 2), area)!.Value.Y;
        var rows = new List<object>();
        foreach (var g in sections.GetVisualDescendants().OfType<Grid>().Where(g => g.IsEffectivelyVisible))
        {
            if (g.Children.OfType<TextBlock>().FirstOrDefault(t => Grid.GetColumn(t) == 0 && t.Classes.Contains("fieldLabel")) is not { } label) continue;
            if (g.Children.Where(c => Grid.GetColumn(c) >= 1 && c.IsVisible).OrderBy(Grid.GetColumn).FirstOrDefault() is not { } field) continue;
            var natural = new TextBlock { Text = label.Text, FontSize = label.FontSize, FontFamily = label.FontFamily };
            natural.Measure(Size.Infinity);
            rows.Add(new
            {
                text = label.Text, label_x = Math.Round(X(label), 2), field_x = Math.Round(X(field), 2), label_width = Math.Round(label.Bounds.Width, 2),
                natural = Math.Round(natural.DesiredSize.Width, 2), trimmed = label.TextTrimming != TextTrimming.None,
                tip = label.Text is not { Length: > 0 } || ToolTip.GetTip(label) != null, field_type = field.GetType().Name,
                field_height = Math.Round(field.Bounds.Height, 2), field_width = Math.Round(field.Bounds.Width, 2),
                center_offset = Math.Round(Math.Abs(Mid(label) - Mid(field)), 2),
                inside = X(field) + field.Bounds.Width <= area.Bounds.Width + .5,
            });
        }
        return new { capture, window_width = main.Bounds.Width, panel_width = Math.Round(area.Bounds.Width, 1), rows };
    }

    // Where each visible field sits; a key selection may swap the colour and alpha field in one cell but must not move others.
    private static Dictionary<Control, string> Layout(Window main) => ((StackPanel)Get(Get(main, "particlePanel")!, "Sections")!)
        .GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c is TextBlock or NumericUpDown or ComboBox or CheckBox or Button)
        .ToDictionary(c => c, c => c.TranslatePoint(default, Panel(main)) is { } p ? $"{p.X:0.#},{p.Y:0.#},{c.Bounds.Width:0.#},{c.Bounds.Height:0.#}" : "?");

    private static async Task Undo(Window main, bool redo)
    {
        main.FindControl<Control>("Viewport")!.Focus();
        await (Task)Call(main, "RestoreEdit", redo)!;
        await Delay(300);
    }

    private static int Edits(Window main) => (int)Get(Get(main, "history")!, "position")!;

    private static T Find<T>(object panel, string type, Func<Control, bool>? where = null) where T : class =>
        ((StackPanel)Get(panel, "Sections")!).GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == type && (where?.Invoke(c) ?? true)) as T
        ?? throw new InvalidOperationException(type);

    private static async Task Run(Window main)
    {
        string root = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        string asset = Path.Combine(root, "Assets", "Flame.particles"), scenePath = Path.Combine(root, "Scenes", "Curves.scene");
        var view = main.FindControl<Control>("Viewport")!;
        SceneDocument Scene() => (SceneDocument)Get(main, "Scene")!;
        await Until(() => Scene().Objects.Any(o => o.Name == "Flame"), "Curves scene");
        main.Activate();
        main.Width = 1600; main.Height = 1000;
        await Delay(700);
        var flame = Scene().Objects.First(o => o.Name == "Flame");
        Call(main, "Select", flame);
        Call(main, "OpenParticlePanel", (string?)null);
        var panel = Get(main, "particlePanel")!;
        await Until(() => (string?)Get(panel, "AssetPath") == "Assets/Flame.particles" && Panel(main).IsVisible, "Particle panel with Flame.particles");
        await Delay(700);
        var original = File.ReadAllBytes(asset);
        var sceneBytes = File.ReadAllBytes(scenePath);
        var files = Files(root);
        var inScene = Get(panel, "ScenePreview")!;
        var isolated = Get(panel, "Isolated")!;
        int Owners(object p) => ((System.Collections.ICollection)Get(p, "Owners")!).Count;
        ulong Hash(object p) => (ulong)Call(p, "Hash")!;
        int Count(object p) => (int)Get(p, "Count")!;
        result["selected_owners"] = Owners(inScene);
        result["viewport_preview"] = ReferenceEquals(Get(view, "Particles"), inScene);

        // Play all: the three Curves emitters, compared tick by tick with the trace Scene Play, Release and AOT recorded.
        ((CheckBox)Get(panel, "PlayAll")!).IsChecked = true;
        await Delay(300);
        Call(panel, "SetPlaying", false);
        result["all_owners"] = Owners(inScene);
        var reference = File.ReadAllLines(Environment.GetEnvironmentVariable("MONOON_PARTICLE_TRACE_REF")!);
        var ticks = new List<object>();
        foreach (int t in new[] { 0, 1, 60, 150, 299 })
        {
            Call(inScene, "Seek", t);
            var f = reference[t].Split(' ');
            ticks.Add(new { tick = t, count = Count(inScene), hash = Hash(inScene).ToString("x16"), trace_count = int.Parse(f[2]), trace_hash = f[3] });
        }
        result["trace"] = ticks;
        Snap(inScene, 800, 450, new(0, 0, 800, 450), Matrix3x2.Identity, "scene-preview-0299");

        // Scrubbing to t (the toolbar slider) equals playing t ticks from the start, in both previews.
        const int scrubTick = 90;
        Call(inScene, "Restart"); Call(isolated, "Restart");
        for (int i = 0; i < scrubTick; i++) { Call(inScene, "Advance"); Call(isolated, "Advance"); }
        var played = (Hash(inScene), Hash(isolated), Count(isolated));
        Call(panel, "Seek", 0, false);
        var slider = ((Control)panel).GetVisualDescendants().OfType<Slider>().Single();
        slider.Value = scrubTick * (double)(float)(1.0 / 60);
        await Delay(200);
        result["scrub"] = new { tick = (int)Get(isolated, "Tick")!, scene_same = Hash(inScene) == played.Item1, isolated_same = Hash(isolated) == played.Item2, isolated_count = played.Item3 };
        Snap(isolated, 400, 300, new(-200, -150, 400, 300), Matrix3x2.CreateTranslation(200, 150), "isolated-0090");

        // Two seconds of playback write nothing.
        Call(panel, "SetPlaying", true);
        int before = (int)Get(isolated, "Tick")!;
        await Delay(2000);
        int after = (int)Get(isolated, "Tick")!;
        Call(panel, "SetPlaying", false);
        ((CheckBox)Get(panel, "PlayAll")!).IsChecked = false;
        await Delay(300);
        result["playback"] = new { ticks_advanced = after - before, wrapped = after < before, files_unchanged = Files(root).SequenceEqual(files) };

        // A curve key drag: several moves, one Undo step, the file holds the new key, Undo/Redo restore the bytes.
        var history = new List<object>();
        int edits = Edits(main);
        var curve = Find<Control>(panel, "CurveField");
        var curveView = Get(curve, "View")!;
        Call(curveView, "BeginDrag", 1);
        foreach (var (t, v) in new[] { (.25f, 1.1f), (.28f, 1.2f), (.3f, 1.25f) }) Call(curveView, "DragTo", t, v);
        Call(curveView, "EndDrag");
        await Delay(300);
        var edited = ParticleEffect.Load(asset);
        var key = edited.SizeOverLifetime!.Keys[1];
        var lutType = typeof(ParticleEffect).Assembly.GetType("Engine.Core.ParticleLut")!;
        var bake = lutType.GetMethod("Bake", Any, [typeof(ParticleKey[]), typeof(bool)])!;
        var runtime = (float[])bake.Invoke(null, [edited.SizeOverLifetime.Keys, edited.SizeOverLifetime.Smooth])!;
        var shown = (float[])Get(Get(Find<Control>(panel, "CurveField"), "View")!, "Samples")!;
        var curveBytes = File.ReadAllBytes(asset);
        int curveEdits = Edits(main) - edits;
        await Undo(main, false);
        bool undo = File.ReadAllBytes(asset).AsSpan().SequenceEqual(original);
        await Undo(main, true);
        bool redo = File.ReadAllBytes(asset).AsSpan().SequenceEqual(curveBytes);
        await Undo(main, false);
        bool undoAgain = File.ReadAllBytes(asset).AsSpan().SequenceEqual(original);
        history.Add(new { edit = "size curve key 2 drag", records = curveEdits, key_time = key.Time, key_value = key.Value, lut_same = shown.SequenceEqual(runtime),
            undo_bytes = undo, redo_bytes = redo, undo_again_bytes = undoAgain, panel_reloaded = ((ParticleEffect)Get(panel, "Edited")!).SizeOverLifetime!.Keys[1] == ParticleEffect.Load(asset).SizeOverLifetime!.Keys[1] });

        // A gradient colour key moved along the bar.
        edits = Edits(main);
        var gradientView = Get(Find<Control>(panel, "GradientField"), "View")!;
        Call(gradientView, "BeginDrag", false, 1);
        Call(gradientView, "DragTo", .45f);
        Call(gradientView, "DragTo", .5f);
        Call(gradientView, "EndDrag");
        await Delay(300);
        edited = ParticleEffect.Load(asset);
        var bakeGradient = lutType.GetMethod("Bake", Any, [typeof(ParticleGradient)])!;
        var gradientShown = (float[])Get(Get(Find<Control>(panel, "GradientField"), "View")!, "Samples")!;
        var gradientRuntime = (float[])bakeGradient.Invoke(null, [edited.ColorOverLifetime!])!;
        int gradientEdits = Edits(main) - edits;
        var gradientBytes = File.ReadAllBytes(asset);
        await Undo(main, false);
        undo = File.ReadAllBytes(asset).AsSpan().SequenceEqual(original);
        await Undo(main, true);
        redo = File.ReadAllBytes(asset).AsSpan().SequenceEqual(gradientBytes);
        await Undo(main, false);
        history.Add(new { edit = "gradient colour key 2 drag", records = gradientEdits, key_time = edited.ColorOverLifetime.Colors![1].Time,
            lut_same = gradientShown.SequenceEqual(gradientRuntime), undo_bytes = undo, redo_bytes = redo,
            undo_again_bytes = File.ReadAllBytes(asset).AsSpan().SequenceEqual(original) });

        // The start colour through the ColorPicker flyout: live while the picker moves, one record when it closes.
        edits = Edits(main);
        var color = Find<Grid>(panel, "ColorField");
        var flyout = (Avalonia.Controls.Flyout)Get(color, "Flyout")!;
        flyout.ShowAt((Control)Get(color, "Swatch")!);
        await Until(() => flyout.IsOpen, "colour flyout");
        await Delay(500);
        var picker = (ColorView)Get(color, "Picker")!;
        Capture((Visual)((Control)picker).GetVisualRoot()!, "color-flyout");
        picker.Color = Color.Parse("#2A70C8");
        await Delay(100);
        picker.Color = Color.Parse("#3366CC");
        await Delay(100);
        flyout.Hide();
        await Delay(300);
        edited = ParticleEffect.Load(asset);
        int colorEdits = Edits(main) - edits;
        var colorBytes = File.ReadAllBytes(asset);
        await Undo(main, false);
        undo = File.ReadAllBytes(asset).AsSpan().SequenceEqual(original);
        await Undo(main, true);
        redo = File.ReadAllBytes(asset).AsSpan().SequenceEqual(colorBytes);
        await Undo(main, false);
        history.Add(new { edit = "start colour via ColorView", records = colorEdits, color = edited.Color.Min, undo_bytes = undo, redo_bytes = redo,
            undo_again_bytes = File.ReadAllBytes(asset).AsSpan().SequenceEqual(original) });
        result["edits"] = history;
        result["scene_unchanged"] = File.ReadAllBytes(scenePath).AsSpan().SequenceEqual(sceneBytes);
        result["scene_dirty"] = (bool)Get(Get(main, "history")!, "IsDirty")!;
        result["files_after_undo"] = Files(root).SequenceEqual(files);

        // Alignment: the same label and field columns at each window size; a key selection only repaints.
        var inspections = new List<object>();
        foreach (var (w, h) in new[] { (1280, 800), (1600, 1000), (1920, 1080), (880, 660) })
        {
            main.Width = w; main.Height = h;
            await Delay(800);
            Capture(Panel(main), $"panel-{w}");
            Capture(main, $"main-{w}");
            inspections.Add(Rows(main, $"panel-{w}"));
        }
        main.Width = 1280; main.Height = 800;
        await Delay(800);
        Find<Control>(panel, "GradientField").BringIntoView();
        await Delay(500);
        Capture(Panel(main), "panel-curves-1280");
        var layout = Layout(main);
        Call(Get(Find<Control>(panel, "CurveField"), "View")!, "Select", 2);
        Call(Get(Find<Control>(panel, "GradientField"), "View")!, "Select", true, 2);
        await Delay(300);
        var moved = Layout(main).Where(c => layout.TryGetValue(c.Key, out var was) && was != c.Value).Select(c => c.Key.GetType().Name).ToList();
        result["selection_keeps_layout"] = moved.Count == 0 && layout.Count > 100;
        result["selection_moved"] = moved;
        flame = Scene().Objects.First(o => o.Name == "Flame");
        flame.Name = LongName;
        Call(main, "FinishObjectChange", flame, null);
        Call(main, "Select", flame);
        await Delay(800);
        Capture(Panel(main), "panel-1280-long-name");
        inspections.Add(Rows(main, "panel-1280-long-name"));
        var title = ((Control)panel).GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.Contains(LongName) == true);
        result["long_title"] = new { trimmed = title.TextTrimming != TextTrimming.None, tip = ToolTip.GetTip(title) as string == title.Text,
            inside = title.TranslatePoint(new(title.Bounds.Width, 0), Panel(main))!.Value.X <= Panel(main).Bounds.Width + .5 };
        result["inspections"] = inspections;
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
}
