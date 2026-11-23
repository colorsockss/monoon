using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Engine.Editor;

public partial class MainWindow
{
    // Asset editors: one panel per kind. Opening the same asset focuses it; another asset replaces it after the unsaved-changes prompt.
    private static readonly string[] EditorKinds = ["Animator", "Clip", "Tileset", "Input Actions", "Dialogue", "Data"];
    // Tool panels, opened on demand. They share one area below the Scene unless the user moves them.
    private static readonly string[] ToolPanels = ["Console", "Profiler", .. EditorKinds];
    private static readonly string[] DockPanels = ["Hierarchy", "Project", "Scene", "Game", "Inspector", .. ToolPanels];
    internal static readonly string[] LayoutPresets = ["Default", "Editing", "Animation"];
    private readonly Dictionary<string, NativeMenuItem> panelItems = [];
    private readonly Dictionary<string, ScrollViewer> editorHosts = [];
    private readonly NativeMenuItem customLayoutItem = new("Custom");
    private EditorLayoutFile layoutFile = new();

    // Per-user editor settings, next to the script editor preference; MONOON_USER_DATA isolates test runs.
    internal static string LayoutPath => Path.Combine(Environment.GetEnvironmentVariable("MONOON_USER_DATA") is { Length: > 0 } root
        ? root : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoNoon"), "editor-layout.json");

    internal static DockNode LayoutPreset(string name) => name switch
    {
        "Editing" => DockNode.Row(0, DockNode.Tabs(220, "Hierarchy", "Project"), DockNode.Tabs(0, "Scene", "Game"), DockNode.Tabs(300, "Inspector")),
        // Animator and the clip timeline below the Scene, with Project as a third tab for dragging sprites in.
        "Animation" => DockNode.Column(0,
            DockNode.Row(0, DockNode.Tabs(200, "Hierarchy"), DockNode.Tabs(0, "Scene", "Game"), DockNode.Tabs(260, "Inspector")),
            DockNode.Tabs(260, "Clip", "Animator", "Project")),
        // Today's arrangement: Hierarchy over Project (1.25 : 1), Scene and Game tabs, then the Inspector.
        _ => DockNode.Row(0, DockNode.Column(176, DockNode.Tabs(400, "Hierarchy"), DockNode.Tabs(320, "Project")),
            DockNode.Tabs(0, "Scene", "Game"), DockNode.Tabs(218, "Inspector")),
    };

    private sealed class EditorLayoutFile
    {
        public int Version { get; set; } = 1;
        public DockNode? Layout { get; set; }
        public List<FloatingPanel>? Floating { get; set; }
        public DockNode? Custom { get; set; }
        public List<FloatingPanel>? CustomFloating { get; set; }
        // Position in screen pixels, size in window units.
        public int X { get; set; }
        public int Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool Maximized { get; set; }
    }

    private void InitializeDocking()
    {
        InitializeConsole();
        foreach (var kind in EditorKinds)
        {
            var host = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = Placeholder(kind) };
            // An editor fills the panel, and scrolls only when the panel is smaller than the editor's minimum size.
            host.SizeChanged += (_, _) => FitEditor(host);
            editorHosts[kind] = host;
            AddPanel(kind, host);
        }
        Dock.CloseRequested += id => { if (Editor(id) is { } editor) editor.Close(); else Dock.Close(id); };
        Dock.DockRequested += id => { Dock.Unfloat(id); OpenPanel(id); };
        Dock.WindowCreated += PrepareFloatingWindow;
        Dock.Changed += () => { if (FrameDebug.IsChecked == true && !Dock.IsOpen("Profiler")) SetProfiler(false); };
        Dock.Apply(LayoutPreset("Default"));
        if (prefabSource != null) { Dock.Close("Game"); return; }
        try { layoutFile = JsonSerializer.Deserialize<EditorLayoutFile>(File.ReadAllText(LayoutPath)) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { layoutFile = new(); }
        if (layoutFile.Custom is { } custom && !Fits(custom, layoutFile.CustomFloating)) layoutFile.Custom = null;
        if (layoutFile.Layout is { } layout && Fits(layout, layoutFile.Floating))
        {
            Dock.Apply(layout, Names(layoutFile.Floating));
            // Windows open once the main window is shown.
            var floating = layoutFile.Floating ?? [];
            Opened += (_, _) => { foreach (var f in floating) FloatAt(f); };
        }
        RestoreBounds();
        Dock.Changed += SaveLayout;
        Closing += (_, _) => SaveLayout();
    }

    private Border AddPanel(string id, Control content)
    {
        var panel = new Border { Background = EditorTokens.Brush("EditorPanel"), MinWidth = 200, MinHeight = 100, Child = content };
        DockHost.SetPanel(panel, id);
        Dock.Children.Add(panel);
        return panel;
    }

    private static TextBlock Placeholder(string kind) => new()
    {
        Classes = { "label" }, Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Top,
        Text = kind switch
        {
            "Input Actions" => "Choose Project → Input Actions… to edit the action map.",
            "Data" => "Double-click a data asset or table in the Project panel to edit it.",
            "Clip" => "Double-click an animation clip in the Project panel to edit its timeline.",
            _ => $"Double-click a {kind.ToLowerInvariant()} asset in the Project panel to edit it.",
        },
    };

    private EditorPanel? Editor(string id) => editorHosts.GetValueOrDefault(id)?.Content as EditorPanel;

    private static void FitEditor(ScrollViewer host)
    {
        if (host.Content is not EditorPanel editor || host.Bounds.Width <= 0) return;
        editor.Width = Math.Max(host.Bounds.Width, editor.MinWidth);
        editor.Height = Math.Max(host.Bounds.Height, editor.MinHeight);
    }

    // False when the request was handled by focusing the open editor or the open editor kept its unsaved changes.
    private async Task<bool> ReplaceEditor(string kind, string? asset)
    {
        if (Editor(kind) is not { } current) return true;
        if (current.Asset == asset) { OpenPanel(kind); return false; }
        return await current.RequestClose();
    }

    // Shows an asset editor in its panel. `saved` runs with the value the editor closes with, as after ShowDialog.
    private void ShowEditor<T>(string kind, EditorPanel editor, Action<T>? saved = null)
    {
        var host = editorHosts[kind];
        host.Content = editor;
        FitEditor(host);
        Dock.SetTitle(kind, editor.Title);
        editor.TitleChanged += title => { if (host.Content == editor) Dock.SetTitle(kind, title); };
        editor.ActivateRequested += () => OpenPanel(kind);
        editor.IsEnabled = !Viewport.IsPlaying || kind == "Clip";
        editor.Finished += result =>
        {
            if (host.Content == editor)
            {
                host.Content = Placeholder(kind);
                Dock.SetTitle(kind, null);
                Dock.Close(kind);
            }
            if (result is not T value || saved == null) return;
            try { saved(value); }
            catch (Exception ex) { LogError(ex.Message); Status.Text = ex.Message; }
        };
        OpenPanel(kind);
    }

    // Former modal editors cannot change the project while the game runs, as before; the clip timeline stays usable.
    private void LockEditors()
    {
        foreach (var kind in EditorKinds)
            if (Editor(kind) is { } editor) editor.IsEnabled = !Viewport.IsPlaying || kind == "Clip";
    }

    private static List<string> Names(List<FloatingPanel>? floating) => floating?.Select(f => f.Panel).ToList() ?? [];

    private double LargestScreen(Func<PixelRect, double> side) =>
        Screens.All.Count == 0 ? 16384 : Screens.All.Max(s => side(s.WorkingArea) / s.Scaling);

    // A layout saved on a larger display falls back to the default instead of pushing panels off screen.
    private bool Fits(DockNode layout, List<FloatingPanel>? floating) => Dock.CanApply(layout, Names(floating)) &&
        layout.Descendants().All(n => n.Size <= Math.Max(LargestScreen(r => r.Width), LargestScreen(r => r.Height)));

    // A floating window whose monitor is gone, or that is larger than it, opens at the default place and size.
    private void FloatAt(FloatingPanel f)
    {
        var title = new PixelRect(f.X, f.Y, 160, 32);
        var screen = Screens.All.FirstOrDefault(s => s.WorkingArea.Intersects(title));
        bool fits = screen != null && f.Width > 0 && f.Height > 0 &&
            f.Width * screen.Scaling <= screen.WorkingArea.Width && f.Height * screen.Scaling <= screen.WorkingArea.Height;
        Dock.Float(f.Panel, fits ? new PixelPoint(f.X, f.Y) : null, fits ? new Size(f.Width, f.Height) : null);
    }

    private void RestoreBounds()
    {
        var f = layoutFile;
        if (f.Width <= 0 || f.Height <= 0) return;
        var title = new PixelRect(f.X, f.Y, 160, 32);
        var screen = Screens.All.FirstOrDefault(s => s.WorkingArea.Intersects(title));
        // A missing monitor or a window larger than its screen keeps the default centred size.
        if (screen == null || f.Width * screen.Scaling > screen.WorkingArea.Width || f.Height * screen.Scaling > screen.WorkingArea.Height) return;
        Width = Math.Max(f.Width, MinWidth);
        Height = Math.Max(f.Height, MinHeight);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(f.X, f.Y);
        if (f.Maximized) WindowState = WindowState.Maximized;
    }

    private void SaveLayout()
    {
        if (prefabSource != null) return;
        layoutFile.Layout = Dock.Layout;
        layoutFile.Floating = Dock.Floating;
        layoutFile.Maximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal && IsVisible)
            (layoutFile.X, layoutFile.Y, layoutFile.Width, layoutFile.Height) = (Position.X, Position.Y, Width, Height);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
            var temp = LayoutPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(layoutFile, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, LayoutPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status.Text = "Could not save the editor layout: " + ex.Message; }
        UpdateWindowMenu();
    }

    private void ApplyLayout(DockNode layout, List<FloatingPanel>? floating = null)
    {
        Dock.Apply(layout, Names(floating));
        foreach (var f in floating ?? []) FloatAt(f);
    }

    private readonly NativeMenu windowMenu = new();
    private NativeMenu CreateWindowMenu()
    {
        var menu = windowMenu;
        foreach (var id in DockPanels)
        {
            var item = new NativeMenuItem(id) { ToggleType = NativeMenuItemToggleType.CheckBox };
            item.Click += (_, _) => { if (id == "Input Actions" && Editor(id) == null) RunAction(EditInput); else if (id == "Profiler") SetProfiler(true); else OpenPanel(id); };
            panelItems[id] = item;
            menu.Add(item);
            if (id == "Inspector") menu.Add(new NativeMenuItemSeparator());
        }
        menu.Add(new NativeMenuItemSeparator());
        var layouts = new NativeMenu();
        foreach (var name in LayoutPresets)
        {
            var item = new NativeMenuItem(name);
            item.Click += (_, _) => ApplyLayout(LayoutPreset(name));
            layouts.Add(item);
        }
        layouts.Add(new NativeMenuItemSeparator());
        customLayoutItem.Click += (_, _) => { if (layoutFile.Custom is { } custom && Fits(custom, layoutFile.CustomFloating)) ApplyLayout(custom, layoutFile.CustomFloating); };
        layouts.Add(customLayoutItem);
        menu.Add(new NativeMenuItem("Layout") { Menu = layouts });
        var save = new NativeMenuItem("Save Layout as Custom");
        save.Click += (_, _) =>
        {
            (layoutFile.Custom, layoutFile.CustomFloating) = (Dock.Layout, Dock.Floating);
            SaveLayout();
            Status.Text = "Saved the current layout as Custom.";
        };
        var reset = new NativeMenuItem("Reset Layout");
        reset.Click += (_, _) => { ApplyLayout(LayoutPreset("Default")); Status.Text = "Restored the default layout."; };
        menu.Add(save);
        menu.Add(reset);
        UpdateWindowMenu();
        return menu;
    }

    internal void OpenPanel(string id)
    {
        switch (id)
        {
            case "Hierarchy": Dock.Open(id, "Project", DockZone.Top, DockZone.Left); break;
            case "Project": Dock.Open(id, "Hierarchy", DockZone.Bottom, DockZone.Left); break;
            case "Scene": Dock.Open(id, "Game", DockZone.Center, DockZone.Right); break;
            case "Game": Dock.Open(id, "Scene", DockZone.Center, DockZone.Right); break;
            case "Inspector": Dock.Open(id, null, DockZone.Center, DockZone.Right); break;
            default:
                var near = ToolPanels.FirstOrDefault(p => p != id && Dock.IsOpen(p) && !Dock.IsFloating(p));
                if (near != null) Dock.Open(id, near, DockZone.Center, DockZone.Bottom);
                else Dock.Open(id, "Scene", DockZone.Bottom, DockZone.Bottom);
                break;
        }
    }

    // A floating panel window gets the main window's panel styles, Inspector templates, game input and shortcuts.
    private void PrepareFloatingWindow(Window window)
    {
        var styles = new Uri("avares://MoNoon/PanelStyles.axaml");
        window.Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(styles) { Source = styles });
        foreach (var (key, value) in Resources) window.Resources[key] = value;
        window.DataTemplates.AddRange(DataTemplates);
        window.AddHandler(KeyDownEvent, GameKeyDown, RoutingStrategies.Tunnel);
        window.AddHandler(KeyUpEvent, GameKeyUp, RoutingStrategies.Tunnel);
        window.AddHandler(KeyDownEvent, Shortcut, RoutingStrategies.Tunnel);
        window.Deactivated += (_, _) => Viewport.ResetInput();
    }

    // Handle game navigation before Avalonia moves Tab focus to the Play/Stop button.
    private void GameKeyDown(object? sender, KeyEventArgs e)
    {
        if (Viewport.IsKeyboardFocusWithin && !e.KeyModifiers.HasFlag(KeyModifiers.Meta) && Viewport.RouteUiKey(e, true)) { Viewport.Focus(); e.Handled = true; }
    }

    private void GameKeyUp(object? sender, KeyEventArgs e)
    {
        if (Viewport.RouteUiKey(e, false)) e.Handled = true;
    }

    private void UpdateWindowMenu()
    {
        foreach (var (id, item) in panelItems) item.IsChecked = Dock.IsOpen(id);
        customLayoutItem.IsEnabled = layoutFile.Custom != null;
    }
}
