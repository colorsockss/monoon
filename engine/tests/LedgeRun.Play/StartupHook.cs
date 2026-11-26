using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_SAMPLE_ROUTE_EDITOR")!;

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Check(life.MainWindow!));

    private static async Task Check(Window window)
    {
        var view = window.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        string start = Environment.GetEnvironmentVariable("MONOON_SAMPLE_ROUTE_START")!;
        string logPrefix = Environment.GetEnvironmentVariable("MONOON_SAMPLE_ROUTE_LOG")!;
        await Until(() => scene.GetValue(view) is SceneDocument document && document.Objects.Any(o => o.Name == start),
            "first scene of sample route");
        var play = window.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var game = (GameSession?)view.GetType().GetField("game", flags)?.GetValue(view)
            ?? throw new InvalidOperationException("Scene Play did not start.");
        game.LoadFailed += message => throw new InvalidDataException(message);
        File.WriteAllText(Path.Combine(Output, "route.txt"), "");
        game.State.Logged += entry =>
        {
            if (entry.Message.StartsWith(logPrefix, StringComparison.Ordinal))
                File.AppendAllText(Path.Combine(Output, "route.txt"), entry.Message + "\n");
        };
        var replay = InputReplay.Load(Path.Combine(Output, "Trail.inputreplay"));
        var captures = File.ReadAllText(Path.Combine(Output, "frames.txt")).Split(',').Select(int.Parse).ToHashSet();
        if (game.SceneId != replay.StartScene) throw new InvalidDataException("Scene Play started from the wrong scene.");
        DrawReplay(game, replay, captures);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
    }

    private static unsafe void DrawReplay(GameSession game, InputReplay replay, HashSet<int> captures)
    {
        var size = game.LogicalSize;
        var folder = Directory.CreateDirectory(Path.Combine(Output, "captures")).FullName;
        var surface = Sdl.PInvoke.CreateSurface(size.Width, size.Height, SdlPixelFormat.Rgba32);
        var renderer = Sdl.Render.CreateSoftwareRenderer(surface);
        try
        {
            using var canvas = new SdlCanvas(renderer, previewShader: ShaderPixels.Preview);
            replay.Play(game, replay.Project, frame =>
            {
                if (!captures.Contains(frame)) return;
                game.Draw(canvas, 1);
                canvas.FlushSprites();
                var pixels = Sdl.PInvoke.RenderReadPixels(renderer, null);
                if (pixels == null) throw new IOException(Sdl.PInvoke.GetError());
                try
                {
                    if (Sdl.PInvoke.SavePNG(pixels, Path.Combine(folder, $"{frame:D4}.png")) == 0)
                        throw new IOException(Sdl.PInvoke.GetError());
                }
                finally { Sdl.PInvoke.DestroySurface(pixels); }
            });
        }
        finally
        {
            renderer.Dispose();
            Sdl.PInvoke.DestroySurface(surface);
        }
    }
}
