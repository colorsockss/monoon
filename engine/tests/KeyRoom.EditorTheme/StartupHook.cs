using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

// Captures the packaged editor with KeyRoom open, then each former editor window (now a dock panel), and measures
// Inspector rows: every label and every field must start at the same x as the other rows.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_THEME_EDITOR")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string LongName = "Door to the courtyard with an intentionally long object name for trimming";

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Run(life.MainWindow!), allow: ["Select sheet cells"]);

    private static object? Call(object target, string method, params object?[] args) =>
        target.GetType().GetMethod(method, Any)!.Invoke(target, args);
    private static SceneDocument Scene(Window w) => (SceneDocument)w.GetType().GetProperty("Scene", Any)!.GetValue(w)!;
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
    private static void Capture(Visual area, string name)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(area.Bounds.Width), (int)Math.Ceiling(area.Bounds.Height)));
        bitmap.Render(area);
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    private static async Task Resize(Window main, int width, int height)
    {
        main.Width = width; main.Height = height;
        await Delay(700);
    }

    // Top-level Inspector rows: a Grid at the content's left edge whose column 0 is a field label.
    private static object Rows(Window main, string capture)
    {
        var panel = main.FindControl<Border>("InspectorPanel")!;
        var inspector = main.FindControl<StackPanel>("Inspector")!;
        double X(Visual v) => v.TranslatePoint(default, panel)!.Value.X;
        double Mid(Control c) => c.TranslatePoint(new(0, c.Bounds.Height / 2), panel)!.Value.Y;
        double left = X(inspector);
        var rows = inspector.GetVisualDescendants().OfType<Grid>()
            .Where(g => g.IsEffectivelyVisible && Math.Abs(X(g) - left) < .5)
            .Select(g => (grid: g,
                label: g.Children.OfType<TextBlock>().FirstOrDefault(t => Grid.GetColumn(t) == 0 && t.Classes.Contains("fieldLabel")),
                field: g.Children.Where(c => Grid.GetColumn(c) >= 1 && c.IsVisible).OrderBy(Grid.GetColumn).FirstOrDefault()))
            .Where(r => r.label != null && r.field != null).ToList();
        var labels = new List<object>();
        foreach (var (grid, label, field) in rows)
        {
            var natural = new TextBlock { Text = label!.Text, FontSize = label.FontSize, FontFamily = label.FontFamily, FontWeight = label.FontWeight };
            natural.Measure(Size.Infinity);
            labels.Add(new
            {
                text = label.Text, font = label.FontSize, label_x = Math.Round(X(label), 2), label_width = Math.Round(label.Bounds.Width, 2),
                natural = Math.Round(natural.DesiredSize.Width, 2), field_x = Math.Round(X(field!), 2), field_height = Math.Round(field!.Bounds.Height, 2),
                field_type = field.GetType().Name, center_offset = Math.Round(Math.Abs(Mid(label) - Mid(field)), 2),
                trimmed = label.TextTrimming != TextTrimming.None, tip = ToolTip.GetTip(label) != null,
            });
        }
        return new { capture, window_width = main.Bounds.Width, inspector_width = panel.Bounds.Width, rows = labels };
    }

    private static object Hierarchy(Window main, string name)
    {
        var tree = main.FindControl<ListBox>("Hierarchy")!;
        var item = tree.GetVisualDescendants().OfType<ListBoxItem>().First(i => (i.Tag as Engine.Core.SceneObject)?.Name == name && i.IsEffectivelyVisible);
        var text = item.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == name);
        var natural = new TextBlock { Text = name, FontSize = text.FontSize };
        natural.Measure(Size.Infinity);
        double right = text.TranslatePoint(new(text.Bounds.Width, 0), tree)!.Value.X;
        return new
        {
            trimmed = text.TextTrimming != TextTrimming.None, tip = ToolTip.GetTip(text) as string == name,
            natural = Math.Round(natural.DesiredSize.Width, 1), shown = Math.Round(text.Bounds.Width, 1),
            inside = right <= tree.Bounds.Width + .5,
        };
    }

    private static async Task<Window> Open(Window owner, Func<object?> open, string name, List<object> windows)
    {
        var before = owner.OwnedWindows.ToHashSet();
        open();
        await Until(() => owner.OwnedWindows.Any(w => !before.Contains(w) && w.IsVisible), name);
        var window = owner.OwnedWindows.First(w => !before.Contains(w));
        await Delay(700);
        Capture(window, name);
        windows.Add(new { name, title = window.Title, width = window.Bounds.Width, height = window.Bounds.Height,
            font = window.FontSize, background = (window.Background as ISolidColorBrush)?.Color.ToString() });
        return window;
    }

    private static async Task Close(Window window, string name)
    {
        window.Close();
        await Until(() => !window.IsVisible, "closing " + name);
    }

    // Former windows open as dock panels (roadmap 12.4): capture the panel and record the same facts as for a window.
    private static async Task OpenPanel(Window main, Func<object?> open, string id, string name, List<object> windows)
    {
        var dock = main.FindControl<Engine.Editor.DockHost>("Dock")!;
        open();
        await Until(() => dock.IsOpen(id) && dock.Panels.Any(p => Engine.Editor.DockHost.GetPanel(p) == id && p.IsVisible), name);
        await Delay(700);
        var panel = dock.Panels.Single(p => Engine.Editor.DockHost.GetPanel(p) == id);
        Capture(panel, name);
        var content = panel.GetVisualDescendants().OfType<TemplatedControl>().First();
        var title = (string)typeof(Engine.Editor.DockHost).GetMethod("Label", Any)!.Invoke(dock, [id])!;
        windows.Add(new { name, title, width = panel.Bounds.Width, height = panel.Bounds.Height,
            font = content.FontSize, background = ((panel as Border)?.Background ?? (panel as Panel)?.Background) is ISolidColorBrush b ? b.Color.ToString() : null });
    }

    // The tab's close button, as a user closes the panel.
    private static async Task ClosePanel(Window main, string id)
    {
        var dock = main.FindControl<Engine.Editor.DockHost>("Dock")!;
        var tab = dock.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("dockTab") && (string?)b.Tag == id);
        Click(tab.GetVisualDescendants().OfType<Button>().Single());
        await Until(() => !dock.IsOpen(id), "closing " + id);
    }

    private static async Task Run(Window main)
    {
        string root = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        await Until(() => Scene(main).Objects.Count > 1, "KeyRoom");
        main.Activate();
        var door = Scene(main).Objects.First(o => o.Name == "Door");
        Call(main, "Select", door);
        var inspections = new List<object>();
        foreach (var (w, h) in new[] { (1280, 800), (1920, 1080) })
        {
            await Resize(main, w, h);
            Capture(main, $"main-{w}");
            Capture(main.FindControl<Border>("InspectorPanel")!, $"inspector-{w}");
            inspections.Add(Rows(main, $"main-{w}"));
        }

        await Resize(main, 1280, 800);
        double inspectorWidth = main.FindControl<Border>("InspectorPanel")!.Bounds.Width;
        door.Name = LongName;
        Call(main, "FinishObjectChange", door, null);
        Call(main, "Select", door);
        await Delay(700);
        Capture(main, "main-1280-long-name");
        inspections.Add(Rows(main, "main-1280-long-name"));
        var longName = new
        {
            hierarchy = Hierarchy(main, LongName),
            inspector_width_before = inspectorWidth,
            inspector_width_after = main.FindControl<Border>("InspectorPanel")!.Bounds.Width,
        };

        var log = main.GetType().GetMethod("WriteLog", Any)!;
        log.Invoke(main, [new GameLogEntry(GameLogLevel.Info, "Theme probe: info line", null, 0)]);
        log.Invoke(main, [new GameLogEntry(GameLogLevel.Warning, "Theme probe: warning line", null, 0)]);
        log.Invoke(main, [new GameLogEntry(GameLogLevel.Error, "Theme probe: error line", null, 0)]);
        var windows = new List<object>();
        await OpenPanel(main, () => Call(main, "ShowConsole"), "Console", "console", windows); await ClosePanel(main, "Console");
        await OpenPanel(main, () => Call(main, "EditAnimator", (string?)null), "Animator", "animator", windows); await ClosePanel(main, "Animator");
        await OpenPanel(main, () => Call(main, "EditClip", "Assets/Showcase/DoorOpen.clip"), "Clip", "clip", windows);
        var clip = main.GetVisualDescendants().First(c => c.GetType().Name == "ClipEditorWindow");
        // The cell picker stays a dialog, now owned by the window that shows the clip panel.
        await Close(await Open(main, () => Call(clip, "ChooseCells"), "sheet-picker", windows), "sheet-picker");
        await ClosePanel(main, "Clip");
        await OpenPanel(main, () => Call(main, "EditInput"), "Input Actions", "input-actions", windows); await ClosePanel(main, "Input Actions");
        await OpenPanel(main, () => Call(main, "EditValues", "Assets/KeyRoomRules.data", false, false), "Data", "data", windows); await ClosePanel(main, "Data");
        await OpenPanel(main, () => Call(main, "EditValues", "Assets/Loot.table", true, false), "Data", "table", windows); await ClosePanel(main, "Data");
        await OpenPanel(main, () => Call(main, "EditValues", null, false, true), "Dialogue", "dialogue", windows); await ClosePanel(main, "Dialogue");
        await OpenPanel(main, () => Call(main, "EditTileset", (string?)null), "Tileset", "tileset", windows); await ClosePanel(main, "Tileset");

        // Scene Play with the frame profiler; the native Scene view itself stays blank in a RenderTargetBitmap.
        var view = main.FindControl<Control>("Viewport")!;
        view.GetType().GetField("KeepSimulationActive", Any)!.SetValue(view, (Func<bool>)(() => true));
        var play = main.FindControl<Button>("PlayButton")!;
        Click(play);
        await Until(() => (bool)view.GetType().GetProperty("IsPlaying", Any)!.GetValue(view)!, "Play");
        main.FindControl<ToggleButton>("DebugButton")!.IsChecked = true;
        var profiler = main.FindControl<CheckBox>("FrameDebug")!;
        profiler.IsChecked = true;
        profiler.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, profiler));
        await Delay(2500);
        Capture(main, "profiler-1280");
        Click(play);
        await Until(() => !(bool)view.GetType().GetProperty("IsPlaying", Any)!.GetValue(view)!, "Stop");

        var tokens = new Dictionary<string, string?>();
        foreach (var key in new[] { "EditorFontSize", "EditorFontSizeSmall", "EditorFieldLabelWidth", "EditorRowHeight", "EditorInputHeight", "EditorPanelPadding" })
            tokens[key] = Application.Current!.TryGetResource(key, null, out var value) ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : null;
        string[] iconKeys = ["IconTransform", "IconSpriteRenderer", "IconBoxCollider", "IconCircleCollider", "IconRigidbody", "IconCamera",
            "IconAudio", "IconTilemap", "IconUi", "IconScript", "IconAnimator", "IconParticle", "IconLight", "IconFolder", "IconScene",
            "IconPrefab", "IconImage", "IconCSharp", "IconData", "IconTileset", "IconAtlas", "IconAnimation", "IconFile", "IconPlay",
            "IconStop", "IconPause", "IconStep", "IconMove", "IconRotate", "IconScale", "IconRect", "IconSnap", "IconGear", "IconAdd",
            "IconSearch", "IconClose", "IconArrowUp", "IconArrowDown"];
        var missingIcons = iconKeys.Where(k => !Application.Current!.TryGetResource(k, null, out var g) || g is not Geometry).ToArray();
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(new
        {
            os = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier, root = Path.GetFileName(root), inspections, long_name = longName, windows, tokens,
            window_font = main.FontSize, icons = iconKeys.Length - missingIcons.Length, missing_icons = missingIcons,
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (!await (Task<bool>)Call(main, "Save")!) throw new InvalidOperationException("Scene save cancelled");
    }
}
