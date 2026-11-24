using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

// Scene Play probe for PlatformerLab effects: presses Play, replays the recording into the editor's game session and
// draws the chosen frames with the player's SDL canvas. The editor project is not baked, so shader sheets come from SkSL
// like the Mac Scene panel.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_EFFECTS_EDITOR")!;

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life.MainWindow!));

    private static async Task Test(Window main)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(view)!).Objects.Any(o => o.Name == "Torch flame"), "Torch flame");
        await Task.Delay(500);
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var game = (GameSession)view.GetType().GetField("game", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!).ProjectId!.Value;
        var frames = File.ReadAllText(Path.Combine(Output, "captures.txt")).Split(',').Select(int.Parse).ToHashSet();
        Replay(game, project, Path.Combine(Output, "Jump.inputreplay"), frames, Directory.CreateDirectory(Path.Combine(Output, "captures")).FullName);
        await Task.Delay(500);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
    }

    private static unsafe void Replay(GameSession game, Guid project, string recording, HashSet<int> frames, string captures)
    {
        var size = game.LogicalSize;
        var surface = Sdl.PInvoke.CreateSurface(size.Width, size.Height, SdlPixelFormat.Rgba32);
        var renderer = Sdl.Render.CreateSoftwareRenderer(surface);
        using (var canvas = new SdlCanvas(renderer, previewShader: ShaderPixels.Preview))
        {
            InputReplay.Load(recording).Play(game, project, frame =>
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
