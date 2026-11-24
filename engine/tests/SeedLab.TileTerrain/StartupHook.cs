using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

// Edits the mud tile's terrain in the Tileset window, restores it, then runs Scene Play with the replay.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_TERRAIN_EDITOR")!;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life, life.MainWindow!));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }

    private static async Task Test(IClassicDesktopStyleApplicationLifetime life, Window main)
    {
        string project = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        var view = main.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(view)!).Objects.Any(o => o.Name == "Board"), "Board");
        SpriteRegion mud = new(16, 0, 8, 8);
        async Task Edit(string expectName, int expectCost, string name, int cost, bool capture)
        {
            var closing = (Task)main.GetType().GetMethod("EditTileset", Private)!.Invoke(main, ["Assets/Route.tileset"])!;
            // The tileset editor is a dock panel in the main window (roadmap 12.4); it appears after the panel's layout pass.
            await closing;
            Visual? found = null;
            for (int i = 0; i < 200 && (found = main.GetVisualDescendants().SingleOrDefault(c => c.GetType().Name == "TilesetWindow")) == null; i++) await Delay(50);
            var window = (ContentControl)(found ?? throw new InvalidDataException("Tileset panel did not open"));
            var grid = window.FindControl<Control>("Cells")!;
            grid.GetType().GetMethod("RestoreSelection", Private)!.Invoke(grid, [new[] { mud }]);
            var terrain = window.FindControl<TextBox>("TerrainName")!;
            var price = window.FindControl<NumericUpDown>("MoveCostBox")!;
            Require(terrain.Text == expectName && price.Value == expectCost, $"mud reopened as {terrain.Text} x{price.Value}");
            terrain.Text = name;
            price.Value = cost;
            if (capture)
            {
                foreach (int width in new[] { 700, 380 })
                {
                    window.Width = width;
                    await Delay(180);
                    var field = terrain.TranslatePoint(default, window)!.Value;
                    var chooser = window.FindControl<ComboBox>("ShapeMode")!.TranslatePoint(default, window)!.Value;
                    var number = price.TranslatePoint(default, window)!.Value;
                    var save = window.FindControl<Button>("SaveAsset")!;
                    var action = save.TranslatePoint(default, window)!.Value;
                    Require(Math.Abs(field.X - chooser.X) < 1 && number.X + price.Bounds.Width <= window.Bounds.Width + 1 &&
                            action.X + save.Bounds.Width <= window.Bounds.Width + 1, $"terrain fields clipped or misaligned at {width}px");
                    var panel = (Control)window.Content!;
                    using var bitmap = new RenderTargetBitmap(new((int)panel.Bounds.Width, (int)panel.Bounds.Height));
                    bitmap.Render(panel); bitmap.Save(Path.Combine(Output, $"tileset-{width}.png"));
                }
            }
            Click(window.FindControl<Button>("SaveAsset")!);
            // Saving closes the editor and empties its panel, as it closed the window before.
            Require(window.GetVisualRoot() == null, window.FindControl<TextBlock>("Info")!.Text ?? "Save failed");
            await closing;
            var saved = Tileset.Load(Path.Combine(project, "Assets/Route.tileset")).Tiles.Single(t => t.Source == mud);
            Require(saved.Terrain == name && saved.MoveCost == cost, $"saved {saved.Terrain} x{saved.MoveCost}");
        }
        await Edit("mud", 12, "bog", 9, true);
        await Edit("bog", 9, "mud", 12, false);
        File.WriteAllText(Path.Combine(Output, "tileset-edit.txt"), "mud -> bog x9 -> mud x12 saved and reopened\n");
        var play = main.FindControl<Button>("PlayButton")!;
        Click(play);
        var game = (GameSession)view.GetType().GetField("game", Private)!.GetValue(view)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        InputReplay.Load(Path.Combine(Output, "Terrain.inputreplay")).Play(game, GameSettings.Load(project).ProjectId!.Value);
        Click(play);
    }
}
