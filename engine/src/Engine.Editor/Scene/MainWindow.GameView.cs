using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private static readonly (int Width, int Height)[] Resolutions = [(320, 180), (640, 360), (800, 450), (1280, 720), (1920, 1080)];
    private static readonly double[] GameScales = [0, 1, 2, 3, 4];
    private const string CustomResolution = "Custom…";
    private bool syncingResolution;
    private long nextStats;

    private (ToggleButton Button, GizmoTool Tool)[] Tools => [(MoveTool, GizmoTool.Move), (RotateTool, GizmoTool.Rotate), (ScaleTool, GizmoTool.Scale), (RectTool, GizmoTool.Rect)];

    private void InitializeGameView()
    {
        foreach (var (button, tool) in Tools) button.Click += (_, _) => SetTool(tool);
        SetTool(GizmoTool.Move);
        GameScale.ItemsSource = new[] { "Fit", "1×", "2×", "3×", "4×" };
        GameScale.SelectedIndex = 0;
        GameScale.SelectionChanged += (_, _) => { Viewport.GameZoom = GameScales[Math.Max(0, GameScale.SelectedIndex)]; Viewport.RefreshScene(); };
        GameResolution.SelectionChanged += (_, _) => { if (!syncingResolution) PickResolution(); };
        StatsButton.IsCheckedChanged += (_, _) => { GameStats.IsVisible = StatsButton.IsChecked == true; nextStats = 0; RefreshGameStats(); };
        Viewport.RuntimeTick += RefreshGameStats;
    }

    // The toolbar buttons and W/E/R/T choose the Scene gizmo; only one stays checked.
    private void SetTool(GizmoTool tool)
    {
        foreach (var (button, kind) in Tools) button.IsChecked = kind == tool;
        if (Viewport.Gizmo == tool) return;
        Viewport.EndDrag();
        Viewport.Gizmo = tool;
        Viewport.RefreshScene();
    }

    // Scene and Game share the one SDL view: the Play session lives in this viewport, and a second NSView renderer would upload
    // every sprite again. It moves between the two panels like a tab or floating-window move, which keeps the native view.
    private void ShowGameView(bool playing)
    {
        var target = playing ? GameHost : SceneHost;
        if (Viewport.Parent == target) return;
        // The panels may be in different windows, each with its own layout queue (see DockHost.Float).
        Viewport.UpdateLayout();
        ((Decorator)Viewport.Parent!).Child = null;
        target.Child = Viewport;
        Viewport.UpdateLayout();
        SceneAway.IsVisible = playing;
        GameIdle.IsVisible = !playing;
        GameResolution.IsEnabled = !playing;
        if (prefabSource == null) OpenPanel(playing ? "Game" : "Scene");
    }

    // Presets and the project's own size; changing it saves the project setting like Project → Game Resolution….
    private void SyncResolution()
    {
        var current = (Scene.Settings.Width, Scene.Settings.Height);
        var sizes = Resolutions.Contains(current) ? Resolutions : [current, .. Resolutions];
        syncingResolution = true;
        try
        {
            GameResolution.ItemsSource = sizes.Select(s => $"{s.Width} × {s.Height}").Append(CustomResolution).ToArray();
            GameResolution.SelectedIndex = Array.IndexOf(sizes, current);
        }
        finally { syncingResolution = false; }
    }

    private void PickResolution()
    {
        int i = GameResolution.SelectedIndex;
        var items = (string[]?)GameResolution.ItemsSource;
        if (items == null || i < 0) return;
        if (items[i] == CustomResolution) { RunAction(async () => { try { await EditGameSettings(); } finally { SyncResolution(); } }); return; }
        var size = items[i].Split(" × ").Select(int.Parse).ToArray();
        if (size[0] == Scene.Settings.Width && size[1] == Scene.Settings.Height) return;
        RunAction(async () =>
        {
            try
            {
                if (project == null && !await Save()) return;
                ApplyGameSettings(Scene.Settings with { Width = size[0], Height = size[1] });
            }
            finally { SyncResolution(); }
        });
    }

    private void RefreshGameStats()
    {
        if (StatsButton.IsChecked != true || Environment.TickCount64 < nextStats) return;
        nextStats = Environment.TickCount64 + 250;
        var history = Viewport.Profile;
        if (!Viewport.IsPlaying || history.Count == 0) { GameStats.Text = "Play to measure frames"; return; }
        int count = Math.Min(60, history.Count);
        double total = 0;
        for (int i = history.Count - count; i < history.Count; i++) total += history.At(i).Interval;
        double average = total / count;
        GameStats.Text = $"{1000 / Math.Max(average, .001):0} fps · {average:F1} ms · {history.At(history.Count - 1).Calls} draws";
    }
}
