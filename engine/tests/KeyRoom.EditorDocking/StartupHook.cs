using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;
using Engine.Editor;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS. MONOON_DOCKING_PHASE selects one editor launch:
// first (empty profile), restore (same profile), fallback (oversized saved values),
// panels (former windows as panels, floating, Scene/Game tabs, tools) and panels-restore (floating windows after a restart).
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_DOCKING_OUTPUT")!;
    private static readonly string Phase = Environment.GetEnvironmentVariable("MONOON_DOCKING_PHASE")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, object?> Result = [];
    private static readonly Avalonia.Input.Pointer Mouse = new(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, true);
    private static Window main = null!;
    private static DockHost dock = null!;
    // How each real-window capture was taken: screencapture -l on macOS, RenderTargetBitmap (no native Scene view) elsewhere.
    private static readonly Dictionary<string, string> Captures = [];

    public static void Initialize() => Start(Path.Combine(Output, $"{Phase}-error.txt"), async life =>
    {
        main = life.MainWindow!;
        dock = main.FindControl<DockHost>("Dock")!;
        await Until(() => main.IsVisible && main.FindControl<ListBox>("Hierarchy")!.Items.Count > 1, "KeyRoom");
        await Delay(500);
        Result["os"] = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
        Result["captures"] = Captures;
        Result["screen"] = main.Screens.All.Select(s => new { s.WorkingArea.Width, s.WorkingArea.Height, s.Scaling }).ToList();
        await (Phase switch
        {
            "first" => First(), "restore" => Restore(), "fallback" => Fallback(), "panels" => Panels(), "panels-restore" => PanelsRestore(),
            _ => throw new ArgumentException(Phase)
        });
        File.WriteAllText(Path.Combine(Output, $"{Phase}.json"), JsonSerializer.Serialize(Result, new JsonSerializerOptions { WriteIndented = true }));
    }, allow: ["Console*", "Animator*", "Inspector*", "Scene*", "Game*"]); // floating panel windows

    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static T Find<T>(string name) where T : Control => main.FindControl<T>(name) ?? throw new InvalidDataException($"{name} not found");
    private static Rect In(Visual c) => new(c.TranslatePoint(default, main)!.Value, c.Bounds.Size);
    private static Control Panel(string id) => dock.Panels.Single(p => DockHost.GetPanel(p) == id);
    private static Border Tab(string id) => dock.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("dockTab") && (string?)b.Tag == id);
    private static IEnumerable<DockNode> Nodes(DockNode n) => (n.Children ?? []).SelectMany(Nodes).Prepend(n);
    private static bool Active(string id) => Nodes(dock.Layout).Any(n => n.Active == id) || dock.IsFloating(id);
    private static DockNode Preset(string name) => (DockNode)typeof(MainWindow).GetMethod("LayoutPreset", Any)!.Invoke(null, [name])!;
    private static async Task Settle() { await Delay(250); }

    // Structure only: Row[...], Column[...] and (tabs) with * on the active tab.
    private static string Describe(DockNode n) => n.Panels != null
        ? "(" + string.Join(",", n.Panels.Select(p => p == n.Active ? p + "*" : p)) + ")"
        : n.Split + "[" + string.Join(" ", n.Children!.Select(Describe)) + "]";
    private static string Layout() => Describe(dock.Layout);
    private static Dictionary<string, double[]> Slots() => dock.Panels.Where(p => p.IsVisible)
        .ToDictionary(p => DockHost.GetPanel(p)!, p => { var r = In(p); return new[] { Math.Round(r.X, 1), Math.Round(r.Y, 1), Math.Round(r.Width, 1), Math.Round(r.Height, 1) }; });

    // Every open panel is inside the window and at least its minimum size.
    private static void OnScreen(string when)
    {
        var host = In(dock);
        foreach (var p in dock.Panels.Where(p => p.IsVisible))
        {
            var r = In(p);
            Require(r.X >= host.X - .5 && r.Y >= host.Y - .5 && r.Right <= host.Right + .5 && r.Bottom <= host.Bottom + .5 &&
                r.Width >= p.MinWidth - .5 && r.Height >= p.MinHeight - .5, $"{when}: {DockHost.GetPanel(p)} at {r} outside {host} or below its minimum");
        }
    }

    private static void Capture(string name)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)main.Bounds.Width, (int)main.Bounds.Height));
        bitmap.Render(main);
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint objc_msgSend(nint receiver, nint selector);
    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint sel_registerName(string name);
    // The OS capture shows the native Scene view, which RenderTargetBitmap leaves blank.
    // Windows and Linux have no native Scene view (Avalonia draws it): capture the window with RenderTargetBitmap there.
    private static void ScreenCapture(Window window, string name)
    {
        if (window.TryGetPlatformHandle() is not { HandleDescriptor: "NSWindow" } handle)
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
            bitmap.Render(window);
            bitmap.Save(Path.Combine(Output, name + ".png"));
            Captures[name] = "RenderTargetBitmap";
            return;
        }
        Captures[name] = "screencapture -l";
        var number = objc_msgSend(handle.Handle, sel_registerName("windowNumber"));
        using var capture = System.Diagnostics.Process.Start("/usr/sbin/screencapture", ["-x", "-o", "-l", number.ToString(), Path.Combine(Output, name + ".png")]);
        capture.WaitForExit();
    }

    private static void Step(string name, object? value) { Result[name] = value; File.AppendAllText(Path.Combine(Output, $"{Phase}.log"), $"{name}: {JsonSerializer.Serialize(value)}\n"); }

    private static NativeMenuItem MenuItem(params string[] path)
    {
        var items = NativeMenu.GetMenu(main)!.Items;
        NativeMenuItem? item = null;
        foreach (var header in path)
        {
            item = items.OfType<NativeMenuItem>().Single(i => i.Header == header);
            items = item.Menu?.Items ?? [];
        }
        return item!;
    }
    // The same entry point the native menu bar uses for a click.
    private static void Choose(params string[] path)
    {
        var item = MenuItem(path);
        Require(item.IsEnabled, string.Join(" > ", path) + " is disabled");
        var click = typeof(NativeMenuItem).GetMethods(Any).FirstOrDefault(m => m.Name.Contains("RaiseClick") && m.GetParameters().Length == 0)
            ?? throw new MissingMethodException(string.Join(", ", typeof(NativeMenuItem).GetMethods(Any).Select(m => m.Name)));
        click.Invoke(item, null);
    }

    // Inspector rows: label column, input column and vertical centres. AGENTS.md: rows keep one baseline.
    private static object Baseline(string label)
    {
        var name = Find<TextBox>("ObjectName");
        var nameLabel = ((Grid)name.Parent!).Children.OfType<TextBlock>().First();
        var position = Find<TextBlock>("PositionRowLabel");
        var posX = Find<NumericUpDown>("PosX");
        var rows = new List<(TextBlock Label, Control Input)> { (nameLabel, name), (position, (Control)posX.Parent!) };
        var tag = Find<TextBox>("ObjectTag");
        if (tag.IsEffectivelyVisible) rows.Add((((Grid)tag.Parent!).Children.OfType<TextBlock>().First(), tag));
        var labels = rows.Select(r => In(r.Label).X).ToList();
        var inputs = rows.Select(r => In(r.Input).X).ToList();
        double centre(Control c) => In(c).Y + In(c).Height / 2;
        double drift = Math.Max(labels.Max() - labels.Min(), inputs.Max() - inputs.Min());
        double column = inputs[0] - labels[0];
        double vertical = Math.Max(Math.Abs(centre(nameLabel) - centre(name)), Math.Abs(centre(position) - centre(posX)));
        var inspector = In(Panel("Inspector"));
        var value = new
        {
            label, window = Math.Round(main.Bounds.Width), inspector = Math.Round(inspector.Width, 1), rows = rows.Count,
            label_x = Math.Round(labels[0], 1), input_x = Math.Round(inputs[0], 1), column = Math.Round(column, 2),
            drift = Math.Round(drift, 2), vertical = Math.Round(vertical, 2), name_width = Math.Round(name.Bounds.Width, 1),
            inspector_right = Math.Round(inspector.Right, 1)
        };
        OnScreen(label);
        Require(drift < 1 && Math.Abs(column - 60) < 1 && vertical < 1 && inspector.Right <= main.Bounds.Width + .5,
            $"Inspector rows out of line: {JsonSerializer.Serialize(value)}");
        return value;
    }

    private static PointerPointProperties Props(PointerUpdateKind kind) =>
        new(kind == PointerUpdateKind.LeftButtonReleased ? RawInputModifiers.None : RawInputModifiers.LeftMouseButton, kind);

    // Press, move and release on the tab itself: the dock's own pointer handlers run, as with the mouse.
    private static async Task<object> DragTab(string id, Point to, string capture)
    {
        var tab = Tab(id);
        var start = In(tab).Center;
        ulong time = (ulong)Environment.TickCount64;
        tab.RaiseEvent(new PointerPressedEventArgs(tab, Mouse, main, start, time, Props(PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        var before = Slots();
        for (int i = 1; i <= 8; i++)
        {
            var p = start + (to - start) * (i / 8.0);
            tab.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, tab, Mouse, main, p, time + (ulong)i * 16, Props(PointerUpdateKind.Other), KeyModifiers.None));
            await Delay(30);
        }
        await Settle();
        var preview = (Rect?)typeof(DockHost).GetProperty("PreviewRect", Any)!.GetValue(dock);
        var during = Slots();
        Require(preview != null, $"no drop preview while dragging {id}");
        Require(JsonSerializer.Serialize(before) == JsonSerializer.Serialize(during), "the drop preview moved panels");
        CaptureWithPreview(capture);
        tab.RaiseEvent(new PointerReleasedEventArgs(tab, Mouse, main, to, time + 200, Props(PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        await Settle();
        var origin = dock.TranslatePoint(default, main)!.Value;
        return new { panel = id, preview = new[] { preview!.Value.X + origin.X, preview.Value.Y + origin.Y, preview.Value.Width, preview.Value.Height }, unchanged = true };
    }

    // The preview is a popup window; draw its rendered box where it sits over the main window.
    private static void CaptureWithPreview(string name)
    {
        var popup = (Popup)typeof(DockHost).GetField("preview", Any)!.GetValue(dock)!;
        var box = (Control)popup.Child!;
        var size = new PixelSize((int)main.Bounds.Width, (int)main.Bounds.Height);
        using var window = new RenderTargetBitmap(size);
        window.Render(main);
        using var overlay = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)box.Bounds.Width), Math.Max(1, (int)box.Bounds.Height)));
        overlay.Render(box);
        using var composed = new RenderTargetBitmap(size);
        using (var ctx = composed.CreateDrawingContext())
        {
            ctx.DrawImage(window, new Rect(0, 0, size.Width, size.Height));
            var at = dock.TranslatePoint(new Point(popup.HorizontalOffset, popup.VerticalOffset), main)!.Value;
            ctx.DrawImage(overlay, new Rect(at, box.Bounds.Size));
        }
        composed.Save(Path.Combine(Output, name + ".png"));
    }

    private static GridSplitter SplitterLeftOf(string id)
    {
        double left = In(Panel(id)).X;
        return dock.GetVisualDescendants().OfType<GridSplitter>().Where(s => s.IsEffectivelyVisible)
            .OrderBy(s => Math.Abs(In(s).Right - left) + (In(s).Height < 50 ? 1e6 : 0)).First();
    }
    private static async Task DragSplitter(GridSplitter splitter, Vector delta)
    {
        splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragStartedEvent, Vector = default });
        splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = delta });
        splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent, Vector = default });
        await Settle();
    }

    private static Control Viewport => Find<Control>("Viewport");
    private static GameSession? Game => (GameSession?)Viewport.GetType().GetField("game", Any)!.GetValue(Viewport);
    private static Control? Native => (Viewport as Decorator)?.Child is { } c && c.GetType().Name == "MacSceneHost" ? c : null;
    private static long Draws()
    {
        var canvas = Native?.GetType().GetField("canvas", Any)!.GetValue(Native);
        return canvas == null ? -1 : (long)canvas.GetType().GetProperty("DrawSubmissions")!.GetValue(canvas)!;
    }
    private static nint View() => Native == null ? 0 : (nint)Native.GetType().GetField("view", Any)!.GetValue(Native)!;
    // The control that shows the Scene or game: the native SDL view on macOS, the Avalonia viewport itself elsewhere.
    private static Control SceneView => Native ?? Viewport;
    private static bool SameView(nint view) => Native == null || (View() == view && view != 0);
    private static async Task<object> Running(string label, GameSession game, bool visible = true)
    {
        long ticks = game.Time.TickCount, draws = Draws();
        var view = View();
        await Delay(1200);
        if (visible) OnScreen(label);
        var value = new { label, same_session = Game == game, ticks = game.Time.TickCount - ticks, draws = Draws() - draws, view_kept = View() == view && view != 0 };
        Require(value.same_session && value.ticks > 30, $"Play stopped: {JsonSerializer.Serialize(value)}");
        if (visible && Native != null) Require(value.draws > 0 && value.view_kept, $"Scene view stopped drawing: {JsonSerializer.Serialize(value)}");
        return value;
    }

    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));

    private static async Task First()
    {
        Viewport.GetType().GetField("KeepSimulationActive", Any)!.SetValue(Viewport, (Func<bool>)(() => true));
        Require(Layout() == Describe(Preset("Default")), "first launch layout: " + Layout());
        main.Width = 1280; main.Height = 800;
        await Settle();
        var slots = Slots();
        Step("default_slots", slots);
        Require(Math.Abs(slots["Hierarchy"][2] - 176) < 1 && Math.Abs(slots["Inspector"][2] - 218) < 1,
            "default widths: " + JsonSerializer.Serialize(slots));
        Capture("default-1280");

        var baselines = new List<object>();
        foreach (var width in new[] { 1280, 1600, 1920 })
        {
            main.Width = width;
            await Settle();
            baselines.Add(Baseline("default"));
            Capture($"default-{width}");
        }
        main.Width = 1280;
        await Settle();
        var name = Find<TextBox>("ObjectName");
        string original = name.Text ?? "";
        double nameWidth = name.Bounds.Width;
        name.Text = "Player with a very long object name that must stay inside the Inspector input column";
        await Settle();
        baselines.Add(Baseline("long name"));
        Require(Math.Abs(name.Bounds.Width - nameWidth) < .5, $"long name widened the field {nameWidth} -> {name.Bounds.Width}");
        Capture("long-name-1280");
        name.Text = original;
        Step("baselines", baselines);

        // Drag the Project tab onto the Inspector body: it becomes a second tab there.
        var inspector = In(Panel("Inspector"));
        Step("drag_tab", await DragTab("Project", inspector.Center, "drag-preview-tab"));
        Require(Layout() == "Row[(Hierarchy*) (Scene*,Game) (Inspector,Project*)]", "after tab drop: " + Layout());
        OnScreen("tab drop");
        Capture("project-tabbed");
        // Drag Hierarchy to the lower quarter of the Scene: split Scene top/bottom.
        var scene = In(Panel("Scene"));
        Step("drag_split", await DragTab("Hierarchy", new Point(scene.Center.X, scene.Bottom - 20), "drag-preview-split"));
        Require(Layout() == "Row[Column[(Scene*,Game) (Hierarchy*)] (Inspector,Project*)]", "after split drop: " + Layout());
        OnScreen("split drop");
        Capture("hierarchy-below-scene");

        // The four split directions and tab grouping through the same Move() the drop uses.
        var moves = new List<object>();
        foreach (var (panel, target, zone) in new (string, string?, DockZone)[]
                 {
                     ("Project", "Scene", DockZone.Left), ("Hierarchy", "Inspector", DockZone.Top), ("Hierarchy", "Inspector", DockZone.Bottom),
                     ("Project", "Inspector", DockZone.Right), ("Hierarchy", null, DockZone.Left), ("Project", "Hierarchy", DockZone.Center),
                 })
        {
            dock.Move(panel, target, zone);
            await Settle();
            moves.Add(new { panel, target, zone = zone.ToString(), layout = Layout() });
            OnScreen($"after {panel} {zone}");
            Baseline($"after {panel} {zone}");
        }
        Step("moves", moves);
        Require(Layout() == "Row[(Hierarchy,Project*) (Scene*,Game) (Inspector*)]", "after moves: " + Layout());
        Capture("moved");

        // Splitter: widen the Inspector by 80px, then try to make it narrower than its minimum.
        double before = In(Panel("Inspector")).Width;
        await DragSplitter(SplitterLeftOf("Inspector"), new Vector(-80, 0));
        double wider = In(Panel("Inspector")).Width;
        await DragSplitter(SplitterLeftOf("Inspector"), new Vector(400, 0));
        double clamped = In(Panel("Inspector")).Width;
        await DragSplitter(SplitterLeftOf("Inspector"), new Vector(-60, 0));
        double final = In(Panel("Inspector")).Width;
        Step("splitter", new { before, wider, clamped, final });
        Require(Math.Abs(wider - before - 80) < 1 && Math.Abs(clamped - 218) < 1 && Math.Abs(final - 278) < 1, "splitter widths");
        Step("baseline_wide", Baseline("inspector 278"));
        Capture("splitter-278");

        // Play after the Game panel moved away from Scene, then keep moving the panel that shows the game while it runs.
        dock.Move("Game", "Inspector", DockZone.Left);
        await Settle();
        Step("game_moved", Layout());
        var play = Find<Button>("PlayButton");
        Click(play);
        await Until(() => Game != null, "Play");
        await Settle();
        var game = Game!;
        game.LoadFailed += message => File.AppendAllText(Path.Combine(Output, "play-errors.txt"), message + "\n");
        Require(Active("Game") && Viewport.Parent == Find<Decorator>("GameHost"), "Play must bring the Game tab forward with the game view: " + Layout());
        var running = new List<object> { await Running("after move", game) };
        dock.Move("Game", "Hierarchy", DockZone.Bottom);
        await Settle();
        running.Add(await Running("split during Play: " + Layout(), game));
        dock.Move("Game", "Inspector", DockZone.Center);
        await Settle();
        dock.Activate("Inspector");
        await Settle();
        running.Add(await Running("hidden tab during Play", game, visible: false));
        dock.Activate("Game");
        await Settle();
        running.Add(await Running("tab shown again", game));
        dock.Move("Game", null, DockZone.Right);
        await Settle();
        running.Add(await Running("window edge: " + Layout(), game));
        ScreenCapture(main, "play-game-moved");
        Step("play", running);
        Click(play);
        await Until(() => Game == null, "Stop");
        await Settle();
        Require(Active("Scene") && Viewport.Parent == Find<Decorator>("SceneHost"), "Stop must return to the Scene tab: " + Layout());

        // Window menu presets, then Save as Custom and a different current layout for the restart.
        foreach (var preset in new[] { "Editing", "Animation", "Default" })
        {
            Choose("Window", "Layout", preset);
            await Settle();
            Require(Layout() == Describe(Preset(preset)), $"{preset}: {Layout()}");
            OnScreen(preset);
            Capture($"layout-{preset.ToLowerInvariant()}");
        }
        dock.Move("Project", "Inspector", DockZone.Bottom);
        await Settle();
        Choose("Window", "Save Layout as Custom");
        Step("custom", Layout());
        Choose("Window", "Layout", "Editing");
        await Settle();
        await DragSplitter(SplitterLeftOf("Inspector"), new Vector(-60, 0));
        main.Width = 1500; main.Height = 860;
        await Settle();
        Step("current", Layout());
        Step("current_inspector", In(Panel("Inspector")).Width);
        Step("window", new[] { main.Width, main.Height });
        Require(File.Exists(Path.Combine(Environment.GetEnvironmentVariable("MONOON_USER_DATA")!, "editor-layout.json")), "layout file");
    }

    private static async Task Restore()
    {
        var first = JsonDocument.Parse(File.ReadAllText(Path.Combine(Output, "first.json"))).RootElement;
        Step("restored", Layout());
        Require(Layout() == first.GetProperty("current").GetString(), "restored layout: " + Layout());
        double inspector = In(Panel("Inspector")).Width;
        Step("restored_inspector", inspector);
        Require(Math.Abs(inspector - first.GetProperty("current_inspector").GetDouble()) < 1, "restored Inspector width");
        OnScreen("restored");
        Step("restored_window", new[] { main.Width, main.Height });
        Require(main.Width == 1500 && main.Height == 860, "restored window size");
        Capture("restored");

        Require(MenuItem("Window", "Layout", "Custom").IsEnabled, "Custom layout menu");
        Choose("Window", "Layout", "Custom");
        await Settle();
        Require(Layout() == first.GetProperty("custom").GetString(), "custom: " + Layout());

        Choose("Window", "Reset Layout");
        await Settle();
        Require(Layout() == Describe(Preset("Default")), "reset: " + Layout());
        var slots = Slots();
        Require(Math.Abs(slots["Inspector"][2] - 218) < 1, "reset Inspector width");
        Step("reset", new { layout = Layout(), slots });
        Capture("reset");

        // Close a panel with its tab button, then bring it back from the Window menu.
        var close = Tab("Hierarchy").GetVisualDescendants().OfType<Button>().Single();
        Click(close);
        await Settle();
        Require(!dock.IsOpen("Hierarchy") && MenuItem("Window", "Hierarchy").IsChecked == false, "closed Hierarchy");
        Step("closed", Layout());
        Choose("Window", "Hierarchy");
        await Settle();
        Require(dock.IsOpen("Hierarchy") && MenuItem("Window", "Hierarchy").IsChecked == true, "reopened Hierarchy");
        Require(Layout() == Describe(Preset("Default")), "reopened above Project: " + Layout());
        Step("reopened", Layout());
        Step("baseline", Baseline("after reset"));
    }

    private static async Task Fallback()
    {
        await Task.CompletedTask;
        Step("layout", Layout());
        Require(Layout() == Describe(Preset("Default")), "oversized layout must fall back: " + Layout());
        var screen = main.Screens.ScreenFromWindow(main)!;
        var bounds = new PixelRect(main.Position, PixelSize.FromSize(main.ClientSize, screen.Scaling));
        Step("window", new { main.Width, main.Height, main.Position.X, main.Position.Y });
        Require(main.Width == 1220 && main.Height == 740 && screen.WorkingArea.Contains(bounds.TopLeft), "window fallback");
        Require(!MenuItem("Window", "Layout", "Custom").IsEnabled, "oversized Custom must be dropped");
        Step("slots", Slots());
        OnScreen("fallback");
    }

    private static async Task Call(string method, params object?[] args)
    {
        if (typeof(MainWindow).GetMethod(method, Any)!.Invoke(main, args) is Task task) await task;
        await Settle();
    }
    private static ContentControl Host(string id) => (ContentControl)Panel(id).GetVisualDescendants().OfType<ScrollViewer>().First();
    private static string PanelSet() => string.Join(",", Nodes(dock.Layout).SelectMany(n => n.Panels ?? []).Order());
    private static UserControl? Editor(string id) => id is "Console" or "Profiler" || !dock.IsOpen(id) ? null : (dock.IsFloating(id)
        ? dock.FloatingWindow(id)!.GetVisualDescendants().OfType<ScrollViewer>().First().Content : Host(id).Content) as UserControl;
    private static Control PanelAnywhere(string id) => dock.IsFloating(id)
        ? ((Grid)dock.FloatingWindow(id)!.Content!).Children[1] : Panel(id);
    private static string Title(string id) => (string)typeof(DockHost).GetMethod("Label", Any)!.Invoke(dock, [id])!;

    private static void CapturePanel(string id, string name)
    {
        var panel = Panel(id);
        using var bitmap = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)panel.Bounds.Width), Math.Max(1, (int)panel.Bounds.Height)));
        bitmap.Render(panel);
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    private static async Task<object> FloatAndDock(string id)
    {
        var panel = PanelAnywhere(id);
        dock.Float(id);
        await Delay(600);
        var window = dock.FloatingWindow(id) ?? throw new InvalidDataException($"{id} did not float");
        Require(window.IsVisible && panel.GetVisualRoot() == window && !dock.Panels.Contains(panel), $"{id} is not shown in its window");
        ScreenCapture(window, $"float-{id.ToLowerInvariant().Replace(' ', '-')}");
        var floated = new { window = new[] { window.Position.X, window.Position.Y, (int)window.Width, (int)window.Height }, title = window.Title };
        // The Dock button in the window's title strip.
        var button = window.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == $"Dock {id}");
        Click(button);
        await Delay(600);
        Require(!dock.IsFloating(id) && dock.IsOpen(id) && panel.GetVisualRoot() == main && !window.IsVisible, $"{id} did not dock back: {Layout()}");
        OnScreen($"{id} docked again");
        return new { panel = id, floated, docked = Layout() };
    }

    // Former windows open as panels in the area below the Scene; asking again focuses the open tab.
    private static async Task Panels()
    {
        Viewport.GetType().GetField("KeepSimulationActive", Any)!.SetValue(Viewport, (Func<bool>)(() => true));
        main.Width = 1280; main.Height = 800;
        await Settle();
        var openers = new (string Panel, string Method, object?[] Args)[]
        {
            ("Console", "ShowConsole", []), ("Profiler", "SetProfiler", [true]), ("Animator", "EditAnimator", [null]),
            ("Clip", "EditClip", ["Assets/Showcase/DoorOpen.clip"]), ("Tileset", "EditTileset", [null]), ("Input Actions", "EditInput", []),
            ("Dialogue", "EditValues", [null, false, true]), ("Data", "EditValues", ["Assets/KeyRoomRules.data", false, false]),
        };
        var opened = new List<object>();
        foreach (var (id, method, args) in openers)
        {
            await Call(method, args);
            Require(dock.IsOpen(id) && Active(id), $"{id} did not open as a panel: {Layout()}");
            Require(main.OwnedWindows.All(w => !w.IsVisible), $"{id} opened a separate window");
            var editor = Editor(id);
            // Another tab in front of it, then ask for it again.
            var group = Nodes(dock.Layout).Single(n => n.Panels?.Contains(id) == true);
            if (group.Panels!.FirstOrDefault(p => p != id) is { } other) dock.Activate(other);
            await Settle();
            string before = PanelSet();
            await Call(method, args);
            Require(Active(id) && Editor(id) == editor && PanelSet() == before, $"opening {id} again: {before} -> {Layout()}");
            Require(Nodes(dock.Layout).Count(n => n.Panels?.Contains(id) == true) == 1, $"{id} opened twice");
            OnScreen(id);
            CapturePanel(id, "panel-" + id.ToLowerInvariant().Replace(' ', '-'));
            opened.Add(new { panel = id, title = Title(id), editor = editor?.GetType().Name, layout = Layout() });
        }
        Step("opened", opened);
        var tools = Nodes(dock.Layout).Single(n => n.Panels?.Contains("Console") == true);
        Require(openers.All(o => tools.Panels!.Contains(o.Panel)), "former windows share the tool area: " + Layout());
        Capture("panels-1280");
        // A data table replaces the data asset in the same panel.
        var data = Editor("Data");
        await Call("EditValues", "Assets/Loot.table", true, false);
        Require(Editor("Data") != data && Title("Data").Contains("Data Table"), "the table did not replace the data asset: " + Title("Data"));
        Step("replaced", Title("Data"));
        foreach (var id in DockPanelNames()) Require(MenuItem("Window", id) != null && MenuItem("Window", id).IsChecked == dock.IsOpen(id), $"Window menu {id}");
        Step("window_menu", DockPanelNames());

        // Tear off and dock back: Console, Animator, then Scene.
        var floats = new List<object> { await FloatAndDock("Console"), await FloatAndDock("Animator") };
        // A floating Inspector keeps the main window's panel styles and component templates.
        dock.Float("Inspector");
        await Delay(600);
        var inspectorWindow = dock.FloatingWindow("Inspector")!;
        var sections = inspectorWindow.GetVisualDescendants().OfType<TemplatedControl>().Where(c => c.GetType().Name == "ComponentSection" && c.IsEffectivelyVisible).ToList();
        // Each section keeps its header toggle (the main window's ComponentSection theme).
        bool headers = sections.All(c => c.GetVisualDescendants().OfType<ToggleButton>().Any(t => t.Name == "HeaderToggle"));
        // 12.7 removed the Inspector title row; its slim action row (hidden for one selection) still takes the panel header style.
        var header = inspectorWindow.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("panelHeader"));
        Require(sections.Count > 0 && headers && Math.Abs(header.Height - 28) < .5,
            $"floating Inspector: {sections.Count} sections, header {header.Height}px");
        ScreenCapture(inspectorWindow, "float-inspector");
        Click(inspectorWindow.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Dock Inspector"));
        await Delay(600);
        Require(!dock.IsFloating("Inspector"), "Inspector did not dock back");
        floats.Add(new { panel = "Inspector", sections = sections.Count, header = header.Height, docked = Layout(), baseline = Baseline("Inspector docked again") });
        var view = View();
        dock.Float("Scene");
        await Delay(800);
        var sceneWindow = dock.FloatingWindow("Scene")!;
        Require(SceneView.GetVisualRoot() == sceneWindow && SameView(view), "floating Scene lost its native view");
        Viewport.GetType().GetMethod("RefreshScene")!.Invoke(Viewport, null);
        await Delay(400);
        ScreenCapture(sceneWindow, "float-scene");
        // Play with Scene floating: the game moves to the Game tab in the main window and back to the floating Scene on Stop.
        var play = Find<Button>("PlayButton");
        Click(play);
        await Until(() => Game != null, "Play");
        await Settle();
        var game = Game!;
        Require(Active("Game") && SceneView.GetVisualRoot() == main, "Play with a floating Scene must show the game in the Game tab");
        var running = new List<object> { await Running("Scene floating, game in Game tab", game) };
        // The Game panel floats during Play and comes back with the session and NSView.
        dock.Float("Game");
        await Delay(800);
        var gameWindow = dock.FloatingWindow("Game")!;
        running.Add(await Running("Game floating", game));
        ScreenCapture(gameWindow, "float-game-play");
        Require(SceneView.GetVisualRoot() == gameWindow, "the game view did not move with the Game panel");
        Click(gameWindow.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Dock Game"));
        await Delay(800);
        running.Add(await Running("Game docked again", game));
        // Stats and scale on the Game panel.
        var stats = Find<ToggleButton>("StatsButton");
        stats.IsChecked = true;
        await Delay(700);
        var statsText = Find<TextBlock>("GameStats").Text ?? "";
        Require(Find<TextBlock>("GameStats").IsVisible && statsText.Contains("fps") && statsText.Contains("draws"), "stats: " + statsText);
        Find<ComboBox>("GameScale").SelectedIndex = 2;
        await Settle();
        double scale = (double)Viewport.GetType().GetProperty("Scale", Any)!.GetValue(Viewport)!;
        Require(scale == 2, "Game scale 2× gave " + scale);
        running.Add(await Running("Game at 2×", game));
        ScreenCapture(main, "play-game-tab");
        Find<ComboBox>("GameScale").SelectedIndex = 0;
        var resolution = Find<ComboBox>("GameResolution");
        var settings = ((SceneDocument)Viewport.GetType().GetProperty("Scene")!.GetValue(Viewport)!).Settings;
        Require(!resolution.IsEnabled && (resolution.SelectedItem as string) == $"{settings.Width} × {settings.Height}", "resolution during Play: " + resolution.SelectedItem);
        Click(play);
        await Until(() => Game == null, "Stop");
        await Settle();
        Require(SceneView.GetVisualRoot() == sceneWindow && SameView(view), "Stop must return the view to the floating Scene");
        Require(resolution.IsEnabled, "resolution after Stop");
        Step("game", new { running, stats = statsText, scale, resolution = resolution.SelectedItem });
        Click(sceneWindow.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Dock Scene"));
        await Delay(800);
        Require(!dock.IsFloating("Scene") && SceneView.GetVisualRoot() == main && SameView(view), "Scene did not dock back with its view");
        floats.Add(new { panel = "Scene", docked = Layout(), view_kept = Native == null ? (bool?)null : View() == view });
        Step("floats", floats);
        OnScreen("Scene docked again");

        // Scene tools: each toolbar button switches the gizmo; Rect changes Width.
        var tools2 = new List<object>();
        foreach (var (button, mode) in new[] { ("RotateTool", "Rotate"), ("ScaleTool", "Scale"), ("RectTool", "Rect"), ("MoveTool", "Move") })
        {
            var toggle = Find<ToggleButton>(button);
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggle));
            await Settle();
            string gizmo = Viewport.GetType().GetProperty("Gizmo", Any)!.GetValue(Viewport)!.ToString()!;
            var checkedTools = new[] { "MoveTool", "RotateTool", "ScaleTool", "RectTool" }.Where(n => Find<ToggleButton>(n).IsChecked == true).ToArray();
            Require(gizmo == mode && checkedTools.SequenceEqual([button]), $"{button}: gizmo {gizmo}, checked {string.Join(",", checkedTools)}");
            tools2.Add(new { button, gizmo });
        }
        var scene = (SceneDocument)Viewport.GetType().GetProperty("Scene")!.GetValue(Viewport)!;
        var player = scene.Objects.First(o => o.Name == "Player");
        typeof(MainWindow).GetMethod("Select", Any)!.Invoke(main, [player]);
        Click(Find<ToggleButton>("RectTool"));
        float width = player.Width;
        var grip = Viewport.GetType().GetNestedType("Grip", Any)!;
        var center = SelectionTransform.Center([player]);
        Viewport.GetType().GetMethod("BeginGesture", Any)!.Invoke(Viewport, [center, Enum.Parse(grip, "X")]);
        Viewport.GetType().GetMethod("UpdateGesture", Any)!.Invoke(Viewport, [center + new System.Numerics.Vector2(24, 0)]);
        float widened = player.Width;
        Viewport.GetType().GetMethod("CancelGesture", Any)!.Invoke(Viewport, null);
        Require(Math.Abs(widened - width - 24) < .01 && player.Width == width, $"Rect handle: {width} -> {widened} -> {player.Width}");
        Click(Find<ToggleButton>("MoveTool"));
        Step("tools", new { tools = tools2, rect = new { width, widened, cancelled = player.Width } });
        Capture("scene-tools");

        // Close the Animator with its tab button; the restart check gives it a floating entry on a missing monitor.
        Click(Tab("Animator").GetVisualDescendants().OfType<Button>().Single());
        await Settle();
        Require(!dock.IsOpen("Animator") && Editor("Animator") == null, "Animator tab close");
        // Float the Console at a known place for the restart check.
        var at = main.Position + new PixelPoint(120, 140);
        dock.Float("Console", at, new Size(520, 300));
        await Delay(600);
        var console = dock.FloatingWindow("Console")!;
        Step("console_floating", new[] { console.Position.X, console.Position.Y, (int)console.Width, (int)console.Height });
        Step("layout_before_exit", Layout());
        typeof(MainWindow).GetMethod("SaveLayout", Any)!.Invoke(main, null);
    }

    private static string[] DockPanelNames() => (string[])typeof(MainWindow).GetField("DockPanels", Any)!.GetValue(null)!;
    private static void Click(ToggleButton b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));

    private static async Task PanelsRestore()
    {
        var panels = JsonDocument.Parse(File.ReadAllText(Path.Combine(Output, "panels.json"))).RootElement;
        await Until(() => dock.IsFloating("Console") && dock.IsFloating("Animator"), "floating windows restored", 20);
        await Delay(600);
        var console = dock.FloatingWindow("Console")!;
        var saved = panels.GetProperty("console_floating").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        var now = new[] { console.Position.X, console.Position.Y, (int)console.Width, (int)console.Height };
        Step("console_restored", now);
        Require(Math.Abs(now[0] - saved[0]) <= 2 && Math.Abs(now[1] - saved[1]) <= 2 && now[2] == saved[2] && now[3] == saved[3],
            $"Console window moved: saved {string.Join(",", saved)} restored {string.Join(",", now)}");
        Require(Layout() == panels.GetProperty("layout_before_exit").GetString(), "restored layout: " + Layout());
        // The Animator entry was saved on a monitor that no longer exists: it opens on screen at the default size.
        var animator = dock.FloatingWindow("Animator")!;
        var screen = main.Screens.All.FirstOrDefault(s => s.WorkingArea.Contains(animator.Position));
        Step("animator_fallback", new { animator.Position.X, animator.Position.Y, animator.Width, animator.Height });
        Require(screen != null && animator.Width < 9000, $"Animator window off screen at {animator.Position}");
        ScreenCapture(console, "restored-float-console");
        Capture("panels-restored");
    }
}
