using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Engine.Core;

// Edits the mud tile's terrain in the Tileset window, restores it, then runs Scene Play with the replay.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_TERRAIN_EDITOR")!;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try { await Test(life, life.MainWindow!); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Output, "error.txt"), ex.ToString()); }
            finally { life.Shutdown(); }
        });
    });

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }

    private static async Task Test(IClassicDesktopStyleApplicationLifetime life, Window main)
    {
        string project = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        var view = main.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        while (((SceneDocument)scene.GetValue(view)!).Objects.All(o => o.Name != "Board")) await Task.Delay(100);
        SpriteRegion mud = new(16, 0, 8, 8);
        async Task Edit(string expectName, int expectCost, string name, int cost, bool capture)
        {
            var closing = (Task)main.GetType().GetMethod("EditTileset", Private)!.Invoke(main, ["Assets/Route.tileset"])!;
            var window = life.Windows.Single(w => w.GetType().Name == "TilesetWindow");
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
                    await Task.Delay(180);
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
            Require(closing.IsCompleted || !window.IsVisible, window.FindControl<TextBlock>("Info")!.Text ?? "Save failed");
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
