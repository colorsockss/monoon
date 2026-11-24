using System.Numerics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

// Scene Play probe for lines, trails and paths (PlatformerLab.Paths and BarrageLab.Lines). With MONOON_LINES_EDIT=1 it first
// edits the Path lift's curve in the Scene view (drag a point, add, smooth, drag a handle, delete), checks undo/redo and the
// Inspector rows at three widths. Then it replays the recording in Play and draws the chosen frames with the SDL canvas.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_LINES_EDITOR")!;
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life.MainWindow!));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static Rect In(Visual c, Visual root) => new(c.TranslatePoint(default, root)!.Value, c.Bounds.Size);

    private static async Task Test(Window main)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        SceneDocument Scene() => (SceneDocument)scene.GetValue(view)!;
        string wait = Environment.GetEnvironmentVariable("MONOON_LINES_WAIT")!;
        await Until(() => Scene().Objects.Any(o => o.Name == wait), wait);
        await Delay(500);
        main.Width = 1280; main.Height = 900;
        if (Environment.GetEnvironmentVariable("MONOON_LINES_EDIT") == "1") await EditPath(main, view, Scene);
        main.Width = 1280;
        var play = main.FindControl<Button>("PlayButton")!;
        Click(play);
        var game = (GameSession)view.GetType().GetField("game", Any)!.GetValue(view)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!).ProjectId!.Value;
        var frames = File.ReadAllText(Path.Combine(Output, "captures.txt")).Split(',').Select(int.Parse).ToHashSet();
        Replay(game, project, Directory.GetFiles(Output, "*.inputreplay").Single(), frames, Directory.CreateDirectory(Path.Combine(Output, "captures")).FullName);
        // The live Scene panel (SDL in the editor window) presents the final frame; later frames are not traced.
        Environment.SetEnvironmentVariable("MONOON_LINE_TRACE", null);
        await Delay(800);
        CaptureWindow(main, Path.Combine(Output, "scene-play-window.png"));
        Click(play);
    }

    private static async Task EditPath(Window main, Control view, Func<SceneDocument> scene)
    {
        var report = new List<string>();
        SceneObject Lift() => scene().Objects.Single(o => o.Name.StartsWith("Path lift", StringComparison.Ordinal));
        PathPoint[] Points() => Lift().GetComponent<PathFollower2DComponent>()!.Points;
        var original = Points();
        Lift().Name = "Path lift — the curved ferry deck whose long name must not push the Inspector columns";
        main.GetType().GetMethod("Select", Any, [typeof(SceneObject)])!.Invoke(main, [Lift()]);
        await Delay(500);
        var vt = view.GetType();
        double Scale() => (double)vt.GetProperty("Scale", Any)!.GetValue(view)!;
        Point Screen(Vector2 world)
        {
            var origin = (Point)vt.GetProperty("Origin", Any)!.GetValue(view)!;
            return new(world.X * Scale() + origin.X, world.Y * Scale() + origin.Y);
        }
        async Task Drag(Vector2 from, Vector2 to)
        {
            vt.GetMethod("BeginDrag", Any)!.Invoke(view, [Screen(from), false]);
            for (int i = 1; i <= 4; i++) vt.GetMethod("DragTo", Any)!.Invoke(view, [Screen(Vector2.Lerp(from, to, i / 4f))]);
            vt.GetMethod("ReleaseAt", Any)!.Invoke(view, [Screen(to)]);
            await Delay(300);
        }
        Button PathButton(string label) => main.FindControl<StackPanel>("Inspector")!.GetVisualDescendants().OfType<Button>()
            .First(b => b.IsEffectivelyVisible && (b.Content as string == label || Avalonia.Automation.AutomationProperties.GetName(b) == label));
        var start = Lift().WorldPosition - new Vector2(original[0].X, original[0].Y);
        Vector2 World(PathPoint p, Vector2 offset = default) => start + new Vector2(p.X, p.Y) + offset;

        // 1. Drag point 2 in the Scene view.
        await Drag(World(original[1]), World(original[1]) + new Vector2(20, -10));
        var moved = Points();
        Require(moved.Length == 3 && moved[1] == original[1] with { X = original[1].X + 20, Y = original[1].Y - 10 }, $"drag point: {string.Join(" ", moved)}");
        // 2. Add a point after it (halfway to the next), 3. make it smooth, 4. drag its out handle, 5. delete it.
        Click(PathButton("Add point")); await Delay(400);
        var added = Points();
        Require(added.Length == 4 && added[2] == new PathPoint((moved[1].X + moved[2].X) / 2, (moved[1].Y + moved[2].Y) / 2), $"add: {string.Join(" ", added)}");
        Click(PathButton("Smooth")); await Delay(400);
        var smooth = Points();
        var handle = new Vector2(added[3].X - added[1].X, added[3].Y - added[1].Y) / 6;
        Require(smooth[2] == added[2] with { InX = -handle.X, InY = -handle.Y, OutX = handle.X, OutY = handle.Y }, $"smooth: {smooth[2]}");
        await Drag(World(smooth[2], handle), World(smooth[2], handle) + new Vector2(15, 0));
        var bent = Points();
        Require(bent[2] == smooth[2] with { OutX = handle.X + 15, InX = -handle.X - 15 }, $"handle: {bent[2]}");
        await Delay(400);
        CaptureWindow(main, Path.Combine(Output, "path-edit.png"));
        foreach (int width in new[] { 1280, 1600, 1920 })
        {
            main.Width = width;
            await Delay(700);
            report.Add(Aligned(main, "Path Follower 2D", width));
            await Delay(300);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Panel(main).Bounds.Width, (int)Panel(main).Bounds.Height));
            bitmap.Render(Panel(main));
            bitmap.Save(Path.Combine(Output, $"inspector-path-{width}.png"));
        }
        main.Width = 1280; await Delay(500);
        Click(PathButton("Delete point")); await Delay(400);
        var deleted = Points();
        Require(deleted.SequenceEqual(moved), $"delete: {string.Join(" ", deleted)}");

        // Each edit is one undo step; redo replays them; undoing all restores the authored curve.
        var restore = main.GetType().GetMethod("RestoreEdit", Any)!;
        async Task History(bool redo) { await (Task)restore.Invoke(main, [redo])!; await Delay(250); }
        var states = new[] { bent, smooth, added, moved, original };
        foreach (var expected in states) { await History(false); Require(Points().SequenceEqual(expected), $"undo -> {string.Join(" ", Points())}"); }
        foreach (var expected in states.Reverse().Skip(1).Append(deleted)) { await History(true); Require(Points().SequenceEqual(expected), $"redo -> {string.Join(" ", Points())}"); }
        for (int i = 0; i < 5; i++) await History(false);
        Require(Points().SequenceEqual(original) && Lift().Name == "Path lift", $"undo all -> {Lift().Name}: {string.Join(" ", Points())}");
        report.Add("path edit: drag point, add, smooth, drag handle, delete; 5 undo, 5 redo, 5 undo restore the authored path");
        File.WriteAllLines(Path.Combine(Output, "editor.txt"), report);
    }

    private static Border Panel(Window main) => main.GetVisualDescendants().OfType<Border>().First(b => b.Name == "InspectorPanel");

    // Labels in one column, values (number boxes, combo boxes, check boxes, the button row) in one column, nothing clipped.
    private static string Aligned(Window main, string title, int width)
    {
        var inspector = main.FindControl<StackPanel>("Inspector")!;
        var section = inspector.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.HeaderedContentControl>()
            .First(s => s.GetType().Name == "ComponentSection" && s.IsEffectivelyVisible && s.Header as string == title);
        section.BringIntoView(); // The panel capture then shows the path rows.
        Dispatcher.UIThread.RunJobs();
        var fields = section.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.Name != "EnabledCheck" && (c is NumericUpDown or ComboBox or CheckBox ||
                c is Grid g && g.Children.OfType<Button>().Count() == 3 && !g.Children.Any(h => h.Name == "HeaderToggle")) && // not the section header
                        c.GetVisualAncestors().OfType<NumericUpDown>().FirstOrDefault() == null).ToList();
        var labels = section.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Classes.Contains("fieldLabel")).ToList();
        var columns = fields.Select(f => Math.Round(In(f, main).X, 1)).Distinct().ToArray();
        var labelColumns = labels.Select(l => Math.Round(In(l, main).X, 1)).Distinct().ToArray();
        var clipped = section.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c is NumericUpDown or ComboBox or Button &&
            In(c, main).Right > In(section, main).Right + .5).Select(c => c.GetType().Name).ToArray();
        var header = main.FindControl<TextBox>("ObjectName") is { } name ? In(name, main) : default;
        Require(columns.Length == 1 && labelColumns.Length == 1 && clipped.Length == 0 && header.Right <= In(Panel(main), main).Right + .5,
            $"{title} at {width}px: value columns {string.Join(',', columns)}, label columns {string.Join(',', labelColumns)}, clipped {string.Join(',', clipped)}, name right {header.Right}");
        return $"{title}@{width}: {fields.Count} fields at x={columns[0]}, {labels.Count} labels at x={labelColumns[0]}, name box right {header.Right:F0} <= panel {In(Panel(main), main).Right:F0}";
    }

    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint objc_msgSend(nint receiver, nint selector);
    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint sel_registerName(string name);
    // The editor window including the native SDL Scene view.
    private static void CaptureWindow(Window main, string path)
    {
        // Windows and Linux have no native Scene view (Avalonia draws it): capture the window with RenderTargetBitmap there.
        if (main.TryGetPlatformHandle() is not { HandleDescriptor: "NSWindow" } handle)
        {
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)main.Bounds.Width, (int)main.Bounds.Height));
            bitmap.Render(main);
            bitmap.Save(path);
            return;
        }
        var number = objc_msgSend(handle.Handle, sel_registerName("windowNumber"));
        using var capture = System.Diagnostics.Process.Start("/usr/sbin/screencapture", ["-x", "-o", "-l", number.ToString(), path])!;
        capture.WaitForExit();
    }

    private static unsafe void Replay(GameSession game, Guid project, string recording, HashSet<int> frames, string captures)
    {
        var size = game.LogicalSize;
        var surface = Sdl.PInvoke.CreateSurface(size.Width, size.Height, SdlPixelFormat.Rgba32);
        var renderer = Sdl.Render.CreateSoftwareRenderer(surface);
        using (var canvas = new SdlCanvas(renderer)) // The player's canvas and text rasteriser
        {
            InputReplay.Load(recording).Play(game, project, frame =>
            {
                if (!frames.Contains(frame)) return;
                game.Draw(canvas, 1);
                canvas.FlushSprites();
                var pixels = Sdl.PInvoke.RenderReadPixels(renderer, null);
                if (pixels == null || Sdl.PInvoke.SavePNG(pixels, Path.Combine(captures, $"{frame:D4}.png")) == 0)
                    throw new IOException(Sdl.PInvoke.GetError());
                Sdl.PInvoke.DestroySurface(pixels);
            });
        }
        renderer.Dispose();
        Sdl.PInvoke.DestroySurface(surface);
    }
}
