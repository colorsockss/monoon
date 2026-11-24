using System.Drawing;
using System.Numerics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Engine.Core;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS. Replays CameraLab without and with G in Scene Play, then draws
// the live game into a recording canvas: Scene Play compiles the scripts' tick.Debug calls (roadmap 16.14).
public static class StartupHook
{
    public static void Initialize() => Start(Path.Combine(Environment.GetEnvironmentVariable("MONOON_DEBUG_DRAW_OUT")!, "error.txt"), async life =>
    {
        string output = Environment.GetEnvironmentVariable("MONOON_DEBUG_DRAW_OUT")!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scene = viewport.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(viewport)!).Objects.Any(o => o.Name == "Player"), "CameraLab scene");
        var play = main.FindControl<Button>("PlayButton")!;
        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!);
        var lines = new List<string>();
        foreach (var run in new[] { "plain", "gizmos" })
        {
            play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
            var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
            game.LoadFailed += message => throw new InvalidDataException(message);
            InputReplay.Load(Path.Combine(output, run + ".inputreplay")).Play(game, project.ProjectId!.Value);
            var canvas = new Recorder();
            game.Draw(canvas, 1);
            lines.Add($"{run} gold={canvas.Gold} cyan={canvas.Cyan} text={string.Join(";", canvas.Texts)}");
            await Delay(300);
            play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
            await Delay(200);
        }
        File.WriteAllLines(Path.Combine(output, "scene-play.txt"), lines);
    });

    private sealed class Recorder : ICanvas2D
    {
        public int Gold, Cyan;
        public readonly List<string> Texts = [];
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF rectangle, Color color) { }
        public bool DrawSprite(string path, RectangleF destination) => true;
        public IDisposable PushClip(RectangleF area) => new Scope();
        public void BeginColorAdjust() { }
        public void FillStrip(ReadOnlySpan<Vector2> edges, ReadOnlySpan<Color> colors, Matrix3x2 matrix)
        {
            if (colors[0].ToArgb() == Color.Gold.ToArgb()) Gold++;
            if (colors[0].ToArgb() == Color.Cyan.ToArgb()) Cyan++;
        }
        public void DrawText(string text, float x, float y, float size, Color color) { if (color.ToArgb() == Color.White.ToArgb() && text.Contains(", ")) Texts.Add(text); }
        private sealed class Scope : IDisposable { public void Dispose() { } }
    }
}
