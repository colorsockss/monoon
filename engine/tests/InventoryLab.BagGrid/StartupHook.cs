using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS. Records the UI Layout Inspector rows (Axis, Columns, Gap,
// Padding) at three window widths, then replays the bag recording in Scene Play and draws the chosen frames with the Scene
// panel's SDL canvas and SDL_ttf text on an offscreen surface (the player's path).
public static class StartupHook
{
    private static readonly string[] Rows = ["Axis", "Columns", "Gap", "Padding"];

    public static void Initialize() => Start(Path.Combine(Environment.GetEnvironmentVariable("MONOON_BAG_OUT")!, "error.txt"), async life =>
    {
        string output = Environment.GetEnvironmentVariable("MONOON_BAG_OUT")!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scene = viewport.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(viewport)!).Objects.Any(o => o.Name == "Bag trace"), "Bag trace");
        await Delay(500);
        var doc = (SceneDocument)scene.GetValue(viewport)!;
        main.GetType().GetMethod("Select", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, [typeof(SceneObject)])!
            .Invoke(main, [doc.Objects.Single(o => o.Name == "Bag")]);
        var report = new List<string>();
        foreach (var width in new[] { 1280, 1600, 1920 })
        {
            (main.Width, main.Height) = (width, 900);
            await Delay(700);
            var panel = main.FindControl<Control>("InspectorPanel")!;
            var labels = panel.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => Rows.Contains(t.Text) && t.IsEffectivelyVisible && t.Bounds.Width > 0 && t.GetVisualParent() is Grid).ToList();
            var placed = labels.Select(label =>
            {
                var grid = (Grid)label.GetVisualParent()!;
                var field = grid.Children.First(c => Grid.GetColumn(c) == 1);
                return (label.Text, LabelX: label.TranslatePoint(default, main)!.Value.X, FieldX: field.TranslatePoint(default, main)!.Value.X,
                    field.Bounds.Width, field.Bounds.Height);
            }).ToList();
            if (!Rows.All(r => placed.Any(p => p.Text == r)) || placed.Select(p => (p.LabelX, p.FieldX, p.Width)).Distinct().Count() != 1)
                throw new InvalidDataException($"UI Layout rows are not on one grid at {width}px: " + string.Join(" | ", placed));
            report.Add($"window {width}: " + string.Join(" | ", placed.Select(p => $"{p.Text}: label x={p.LabelX:F1} field x={p.FieldX:F1} w={p.Width:F1} h={p.Height:F1}")));
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)panel.Bounds.Width, (int)panel.Bounds.Height));
            bitmap.Render(panel);
            bitmap.Save(Path.Combine(output, $"inspector-{width}.png"));
        }
        File.WriteAllLines(Path.Combine(output, "inspector.txt"), report);

        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!);
        var frames = File.ReadAllText(Path.Combine(output, "captures.txt")).Split(',').Select(int.Parse).ToHashSet();
        var captures = Directory.CreateDirectory(Path.Combine(output, "captures")).FullName;
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        Replay(game, project.ProjectId!.Value, output, frames, captures);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        await Delay(200);
    });

    private static unsafe void Replay(GameSession game, Guid project, string output, HashSet<int> frames, string captures)
    {
        var size = game.LogicalSize;
        var surface = Sdl.PInvoke.CreateSurface(size.Width, size.Height, SdlPixelFormat.Rgba32);
        var renderer = Sdl.Render.CreateSoftwareRenderer(surface);
        using (var canvas = new SdlCanvas(renderer))
        {
            InputReplay.Load(Path.Combine(output, "bag.inputreplay")).Play(game, project, frame =>
            {
                if (!frames.Contains(frame)) return;
                game.Draw(canvas, 1);
                canvas.FlushSprites();
                var pixels = Sdl.PInvoke.RenderReadPixels(renderer, null);
                if (pixels == null || Sdl.PInvoke.SavePNG(pixels, Path.Combine(captures, $"{frame:D4}.png")) == 0)
                    throw new IOException(Sdl.PInvoke.GetError());
                Sdl.PInvoke.DestroySurface(pixels);
            });
        }
        renderer.Dispose();
        Sdl.PInvoke.DestroySurface(surface);
    }
}
