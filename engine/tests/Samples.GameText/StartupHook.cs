using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS. Presses Play, replays input.inputreplay in Scene Play and
// draws the chosen frames with the Scene panel's canvas (SdlCanvas + SDL_ttf, as in the player) on an offscreen surface.
public static class StartupHook
{
    public static void Initialize() => Start(Path.Combine(Environment.GetEnvironmentVariable("MONOON_GAME_TEXT_EDITOR")!, "error.txt"), async life =>
    {
        string output = Environment.GetEnvironmentVariable("MONOON_GAME_TEXT_EDITOR")!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scene = viewport.GetType().GetProperty("Scene")!;
        await Until(() => scene.GetValue(viewport) is SceneDocument { Objects.Count: > 0 }, "scene open");
        await Delay(1000);
        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!);
        var frames = File.ReadAllText(Path.Combine(output, "captures.txt")).Split(',').Select(int.Parse).ToHashSet();
        var captures = Directory.CreateDirectory(Path.Combine(output, "captures")).FullName;
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        Replay(game, project.ProjectId!.Value, output, frames, captures);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
    });

    private static unsafe void Replay(GameSession game, Guid project, string output, HashSet<int> frames, string captures)
    {
        var size = game.LogicalSize;
        var surface = Sdl.PInvoke.CreateSurface(size.Width, size.Height, SdlPixelFormat.Rgba32);
        var renderer = Sdl.Render.CreateSoftwareRenderer(surface);
        // Scenes with lights (the KeyRoom showcase torches) draw them from shader pixels, as the editor's Scene view does.
        using (var canvas = new SdlCanvas(renderer, previewShader: ShaderPixels.Preview))
        {
            InputReplay.Load(Path.Combine(output, "input.inputreplay")).Play(game, project, frame =>
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
