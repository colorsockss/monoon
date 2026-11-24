using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_TILE_EDITOR")!;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life, life.MainWindow!));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,button));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }

    private static async Task Test(IClassicDesktopStyleApplicationLifetime life, Window main)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(view)!).Objects.Any(o => o.Name == "Runner"), "Runner");
        async Task Edit(string asset, SpriteRegion cell, int mode, string? input, string expected, bool capture)
        {
            var closing = (Task)main.GetType().GetMethod("EditTileset",Private)!.Invoke(main,[asset])!;
            // The tileset editor is a dock panel in the main window (roadmap 12.4); it appears after the panel's layout pass.
            await closing;
            Visual? found = null;
            for (int i = 0; i < 200 && (found = main.GetVisualDescendants().SingleOrDefault(c => c.GetType().Name == "TilesetWindow")) == null; i++) await Delay(50);
            var window = (ContentControl)(found ?? throw new InvalidDataException("Tileset panel did not open"));
            var grid = window.FindControl<Control>("Cells")!;
            grid.GetType().GetMethod("RestoreSelection",Private)!.Invoke(grid,[new[]{cell}]);
            var select = window.FindControl<ComboBox>("ShapeMode")!;
            var points = window.FindControl<TextBox>("PointsBox")!;
            Require(select.SelectedIndex == mode && points.Text == expected, $"{asset} reopened with {points.Text}");
            if (input != null)
            {
                // Exercise creation from the dropdown as well as replacement of the existing points.
                select.SelectedIndex = 0;
                select.SelectedIndex = mode;
                points.Text = input;
            }
            if (capture)
            {
                foreach (int width in new[]{700,380})
                {
                    window.Width=width;
                    await Delay(180);
                    var field=points.TranslatePoint(default,window)!.Value;
                    var chooser=select.TranslatePoint(default,window)!.Value;
                    var save=window.FindControl<Button>("SaveAsset")!;
                    var action=save.TranslatePoint(default,window)!.Value;
                    Require(Math.Abs(field.X-chooser.X)<1 && action.X+save.Bounds.Width<=window.Bounds.Width+1, "Tile fields clipped or misaligned");
                }
                var panel=(Control)window.Content!;
                using var bitmap=new RenderTargetBitmap(new((int)panel.Bounds.Width,(int)panel.Bounds.Height));
                bitmap.Render(panel); bitmap.Save(Path.Combine(Output, mode==3 ? "face.png" : "polygon.png"));
            }
            Click(window.FindControl<Button>("SaveAsset")!);
            // Saving closes the editor and empties its panel, as it closed the window before.
            Require(window.GetVisualRoot() == null, window.FindControl<TextBlock>("Info")!.Text ?? "Save failed");
            await closing;
        }
        await Edit("Assets/Incline.tileset",new(76,0,38,90),2,"0,0.65  1,0.4  1,1  0,1","0,0.6  1,0.4  1,1  0,1",false);
        await Edit("Assets/Incline.tileset",new(76,0,38,90),2,"0,0.6  1,0.4  1,1  0,1","0,0.65  1,0.4  1,1  0,1",true);
        await Edit("Assets/Ridge.tileset",new(24,24,8,8),3,"1,0  0,0","0,0  1,0",false);
        await Edit("Assets/Ridge.tileset",new(24,24,8,8),3,"0,0  1,0","1,0  0,0",true);
        Click(main.FindControl<Button>("PlayButton")!);
        var game=(GameSession)view.GetType().GetField("game",Private)!.GetValue(view)!;
        InputReplay.Load(Path.Combine(Output,"Traverse.inputreplay")).Play(game,GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!).ProjectId!.Value);
        Click(main.FindControl<Button>("PlayButton")!);
    }
}
