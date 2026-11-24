using System.Numerics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

// Scene Play probe for 16.4: drags the capsule radius handle and an edge point in the Scene view, adds and deletes an edge
// point, undoes every step back to the authored scene, checks the Capsule/Edge Inspector rows at three widths, then replays
// RoundTrip.inputreplay in Play.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_COLLIDER_EDITOR")!;
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
        await Until(() => Scene().Objects.Any(o => o.Name == "Slope"), "Slope");
        await Delay(500);
        main.Width = 1280; main.Height = 900;
        var report = new List<string>();
        SceneObject Object(string name) => Scene().Objects.Single(o => o.Name == name);
        void Select(string name) => main.GetType().GetMethod("Select", Any, [typeof(SceneObject)])!.Invoke(main, [Object(name)]);
        var vt = view.GetType();
        Point Screen(Vector2 world)
        {
            double scale = (double)vt.GetProperty("Scale", Any)!.GetValue(view)!;
            var origin = (Point)vt.GetProperty("Origin", Any)!.GetValue(view)!;
            return new(world.X * scale + origin.X, world.Y * scale + origin.Y);
        }
        async Task Drag(Vector2 from, Vector2 to)
        {
            vt.GetMethod("BeginDrag", Any)!.Invoke(view, [Screen(from), false]);
            for (int i = 1; i <= 4; i++) vt.GetMethod("DragTo", Any)!.Invoke(view, [Screen(from + (to - from) * (i / 4f))]);
            vt.GetMethod("ReleaseAt", Any)!.Invoke(view, [Screen(to)]);
            await Delay(300);
        }
        Button InspectorButton(string label) => main.FindControl<StackPanel>("Inspector")!.GetVisualDescendants().OfType<Button>()
            .First(b => b.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetName(b) == label);
        CapsuleCollider2DComponent Capsule() => Object("Runner").GetComponent<CapsuleCollider2DComponent>()!;
        ColliderVertex[] Points() => Object("Slope").GetComponent<EdgeCollider2DComponent>()!.Points;
        var original = Points();

        // 1. Capsule radius handle: the Runner (22×30 at 40,270) has radius 11; its handle is on the left side at (40, 285).
        Select("Runner"); await Delay(400);
        await Drag(new(40, 285), new(44, 285));
        Require(Capsule().Radius == 7, $"radius drag: {Capsule().Radius}");
        // 2. Edge point 2 (480, 240) up by 10 px, 3. add a point after it, 4. delete it again.
        Select("Slope"); await Delay(400);
        await Drag(new(480, 240), new(480, 230));
        var moved = Points();
        Require(moved.Length == 3 && Math.Abs(moved[1].X - original[1].X) < 1e-5 && Math.Abs(moved[1].Y - .3f) < 1e-5, $"edge drag: {string.Join(" ", moved)}");
        Click(InspectorButton("Add point")); await Delay(400);
        var added = Points();
        Require(added.Length == 4, $"edge add: {string.Join(" ", added)}");
        CaptureWindow(main, Path.Combine(Output, "edge-edit.png"));
        foreach (int width in new[] { 1280, 1600, 1920 })
            foreach (var (name, title) in new[] { ("Slope", "Edge Collider 2D"), ("Runner", "Capsule Collider 2D") })
            {
                Select(name);
                main.Width = width;
                await Delay(700);
                report.Add(Aligned(main, title, width));
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Panel(main).Bounds.Width, (int)Panel(main).Bounds.Height));
                bitmap.Render(Panel(main));
                bitmap.Save(Path.Combine(Output, $"inspector-{(name == "Slope" ? "edge" : "capsule")}-{width}.png"));
            }
        main.Width = 1280; Select("Slope"); await Delay(500);
        Click(InspectorButton("Delete point")); await Delay(400);
        Require(Points().Length == 3, $"edge delete: {string.Join(" ", Points())}");
        // Each edit is one undo step: delete, add, edge drag, radius drag.
        var restore = main.GetType().GetMethod("RestoreEdit", Any)!;
        for (int i = 0; i < 4; i++) { await (Task)restore.Invoke(main, [false])!; await Delay(250); }
        Require(Points().SequenceEqual(original) && Capsule().Radius == 0, $"undo all: radius {Capsule().Radius}, {string.Join(" ", Points())}");
        report.Add("gizmos: capsule radius drag 11 -> 7, edge point drag, add, delete; 4 undo steps restore the authored scene");
        File.WriteAllLines(Path.Combine(Output, "editor.txt"), report);

        Click(main.FindControl<Button>("PlayButton")!);
        var game = (GameSession)view.GetType().GetField("game", Any)!.GetValue(view)!;
        InputReplay.Load(Path.Combine(Output, "RoundTrip.inputreplay")).Play(game, GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!).ProjectId!.Value);
        Environment.SetEnvironmentVariable("MONOON_COLLIDER_TRACE", null);
        await Delay(800);
        CaptureWindow(main, Path.Combine(Output, "scene-play-window.png"));
        Click(main.FindControl<Button>("PlayButton")!);
    }

    private static Border Panel(Window main) => main.GetVisualDescendants().OfType<Border>().First(b => b.Name == "InspectorPanel");

    // Labels in one column, values (number boxes, combo boxes, check boxes, the point button row) in one column, nothing clipped.
    private static string Aligned(Window main, string title, int width)
    {
        var inspector = main.FindControl<StackPanel>("Inspector")!;
        var section = inspector.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.HeaderedContentControl>()
            .First(s => s.GetType().Name == "ComponentSection" && s.IsEffectivelyVisible && s.Header as string == title);
        section.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        var fields = section.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.Name != "EnabledCheck" && (c is NumericUpDown or ComboBox or CheckBox ||
                c is Grid g && g.Children.OfType<Button>().Count() == 2 && !g.Children.Any(h => h.Name == "HeaderToggle")) &&
                        c.GetVisualAncestors().OfType<NumericUpDown>().FirstOrDefault() == null).ToList();
        var labels = section.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Classes.Contains("fieldLabel")).ToList();
        var columns = fields.Select(f => Math.Round(In(f, main).X, 1)).Distinct().ToArray();
        var labelColumns = labels.Select(l => Math.Round(In(l, main).X, 1)).Distinct().ToArray();
        var clipped = section.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c is NumericUpDown or ComboBox or Button &&
            In(c, main).Right > In(section, main).Right + .5).Select(c => c.GetType().Name).ToArray();
        Require(columns.Length == 1 && labelColumns.Length == 1 && clipped.Length == 0,
            $"{title} at {width}px: value columns {string.Join(',', columns)}, label columns {string.Join(',', labelColumns)}, clipped {string.Join(',', clipped)}");
        return $"{title}@{width}: {fields.Count} fields at x={columns[0]}, {labels.Count} labels at x={labelColumns[0]}";
    }

    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint objc_msgSend(nint receiver, nint selector);
    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint sel_registerName(string name);
    // The editor window including the native SDL Scene view.
    private static void CaptureWindow(Window main, string path)
    {
        if (main.TryGetPlatformHandle() is not { HandleDescriptor: "NSWindow" } handle) return;
        var number = objc_msgSend(handle.Handle, sel_registerName("windowNumber"));
        using var capture = System.Diagnostics.Process.Start("/usr/sbin/screencapture", ["-x", "-o", "-l", number.ToString(), path])!;
        capture.WaitForExit();
    }
}
