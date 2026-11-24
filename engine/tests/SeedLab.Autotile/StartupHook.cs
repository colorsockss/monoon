using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Engine.Core;

// Changes the hedge brush seed and the water FPS in the Tileset panel and restores them, checks row alignment, then runs Scene
// Play with the replay.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_AUTOTILE_EDITOR")!;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life.MainWindow!));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }

    private static async Task Test(Window main)
    {
        string project = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        var view = main.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(view)!).Objects.Any(o => o.Name == "Autotile trace"), "Autotile trace");
        SpriteRegion hedge = new(0, 0, 16, 16), water = new(0, 112, 16, 16);
        async Task Edit(int expectSeed, int seed, int expectFps, int fps, bool capture)
        {
            var closing = (Task)main.GetType().GetMethod("EditTileset", Private)!.Invoke(main, ["Assets/Meadow.tileset"])!;
            Visual? found = null;
            for (int i = 0; i < 200 && (found = main.GetVisualDescendants().SingleOrDefault(c => c.GetType().Name == "TilesetWindow")) == null; i++) await Delay(50);
            var window = (ContentControl)(found ?? throw new InvalidDataException("Tileset panel did not open"));
            var grid = window.FindControl<Control>("Cells")!;
            grid.GetType().GetMethod("RestoreSelection", Private)!.Invoke(grid, [new[] { hedge }]);
            var brush = window.FindControl<ComboBox>("TerrainMode")!;
            var count = window.FindControl<NumericUpDown>("RandomCount")!;
            var seedBox = window.FindControl<NumericUpDown>("RandomSeedBox")!;
            Require(brush.SelectedIndex == 3 && count.Value == 4 && seedBox.Value == expectSeed,
                $"hedge reopened as brush {brush.SelectedIndex}, random {count.Value}, seed {seedBox.Value}");
            seedBox.Value = seed;
            var frames = window.FindControl<NumericUpDown>("FrameCount")!;
            var fpsBox = window.FindControl<NumericUpDown>("FrameFps")!;
            grid.GetType().GetMethod("RestoreSelection", Private)!.Invoke(grid, [new[] { water }]);
            Require(frames.Value == 3 && fpsBox.Value == expectFps && count.Value == null, $"water reopened as {frames.Value} frames at {fpsBox.Value} FPS");
            fpsBox.Value = fps;
            if (capture)
            {
                foreach (int width in new[] { 700, 380 })
                {
                    window.Width = width;
                    await Delay(180);
                    double X(Control c) => c.TranslatePoint(default, window)!.Value.X;
                    var cost = window.FindControl<NumericUpDown>("MoveCostBox")!;
                    var name = window.FindControl<TextBox>("TerrainName")!;
                    var save = window.FindControl<Button>("SaveAsset")!;
                    Require(Math.Abs(X(count) - X(brush)) < 1 && Math.Abs(X(count) - X(name)) < 1 && Math.Abs(X(seedBox) - X(cost)) < 1 &&
                            Math.Abs(seedBox.Bounds.Width - cost.Bounds.Width) < 1 && X(seedBox) + seedBox.Bounds.Width <= window.Bounds.Width + 1 &&
                            X(save) + save.Bounds.Width <= window.Bounds.Width + 1, $"random row misaligned or clipped at {width}px");
                    Require(Math.Abs(X(frames) - X(count)) < 1 && Math.Abs(X(fpsBox) - X(seedBox)) < 1 && Math.Abs(fpsBox.Bounds.Width - seedBox.Bounds.Width) < 1 &&
                            Math.Abs(frames.Bounds.Width - count.Bounds.Width) < 1, $"frames row misaligned at {width}px");
                    var panel = (Control)window.Content!;
                    using var bitmap = new RenderTargetBitmap(new((int)panel.Bounds.Width, (int)panel.Bounds.Height));
                    bitmap.Render(panel); bitmap.Save(Path.Combine(Output, $"tileset-{width}.png"));
                }
            }
            Click(window.FindControl<Button>("SaveAsset")!);
            Require(window.GetVisualRoot() == null, window.FindControl<TextBlock>("Info")!.Text ?? "Save failed");
            await closing;
            var saved = Tileset.Load(Path.Combine(project, "Assets/Meadow.tileset")).Tiles.Single(t => t.Source == hedge);
            Require(saved.AutoEdges == 8 && saved.RandomTiles == 4 && saved.RandomSeed == seed, $"saved {saved.AutoEdges}/{saved.RandomTiles}/{saved.RandomSeed}");
            var pond = Tileset.Load(Path.Combine(project, "Assets/Meadow.tileset")).Tiles.Single(t => t.Source == water);
            Require(pond.Frames == 3 && pond.Fps == fps, $"saved water {pond.Frames} frames at {pond.Fps} FPS");
        }
        await Edit(7, 8, 4, 6, true);
        await Edit(8, 7, 6, 4, false);
        File.WriteAllText(Path.Combine(Output, "tileset-edit.txt"),
            "hedge seed 7 -> 8 -> 7 and water FPS 4 -> 6 -> 4 saved and reopened (8 neighbours, 4 random tiles, 3 frames)\n");
        var play = main.FindControl<Button>("PlayButton")!;
        Click(play);
        var game = (GameSession)view.GetType().GetField("game", Private)!.GetValue(view)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        InputReplay.Load(Path.Combine(Output, "autotile.inputreplay")).Play(game, GameSettings.Load(project).ProjectId!.Value);
        Click(play);
        await Delay(200);
    }
}
