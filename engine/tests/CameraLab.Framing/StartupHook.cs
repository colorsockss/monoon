using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Engine.Core;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS: one Scene Play replays the CameraLab framing recording.
public static class StartupHook
{
    public static void Initialize() => Start(Path.Combine(Environment.GetEnvironmentVariable("MONOON_FRAMING_OUT")!, "error.txt"), async life =>
    {
        string output = Environment.GetEnvironmentVariable("MONOON_FRAMING_OUT")!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scene = viewport.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(viewport)!).Objects.Any(o => o.Name == "Camera trace"), "Camera trace");
        // Inspector rows of the framing camera at three window widths: the new rows share the label and field columns.
        var doc = (SceneDocument)scene.GetValue(viewport)!;
        main.GetType().GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(main, [doc.Objects.Single(o => o.Name == "Player Camera")]);
        var report = new List<string>();
        foreach (var width in new[] { 1280, 1600, 1920 })
        {
            main.Width = width; main.Height = 900;
            await Delay(600);
            var panel = main.FindControl<Control>("InspectorPanel")!;
            var labels = panel.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.Text is "Half-life" or "Deadzone" or "Lead time" or "Frame" or "Padding" or "X / Y").ToList();
            string Column(TextBlock label)
            {
                var grid = (Grid)label.GetVisualParent()!;
                var field = grid.Children.First(c => Grid.GetColumn(c) == 1);
                var at = field.TranslatePoint(default, main)!.Value;
                return $"{label.Text}: label x={label.TranslatePoint(default, main)!.Value.X:F1} field x={at.X:F1} w={field.Bounds.Width:F1} h={field.Bounds.Height:F1}";
            }
            report.Add($"window {width}: " + string.Join(" | ", labels.Select(Column)));
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)panel.Bounds.Width, (int)panel.Bounds.Height));
            bitmap.Render(panel);
            bitmap.Save(Path.Combine(output, $"inspector-{width}.png"));
        }
        File.WriteAllLines(Path.Combine(output, "inspector.txt"), report);
        var play = main.FindControl<Button>("PlayButton")!;
        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        InputReplay.Load(Path.Combine(output, "framing.inputreplay")).Play(game, project.ProjectId!.Value);
        await Delay(300);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        await Delay(200);
    });
}
