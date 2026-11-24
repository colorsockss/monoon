using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

// Scene Play probe for ParticleLab: waits for the start scene, presses Play, replays the recording into the editor's game
// session (the ParticleTrace script writes the trace) and draws the chosen frames with the player's SDL canvas.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_PARTICLES_EDITOR")!;

    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try { await Test(life.MainWindow!); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Output, "error.txt"), ex.ToString()); }
            finally { life.Shutdown(); }
        });
    });

    private static async Task Test(Window main)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        string wait = Environment.GetEnvironmentVariable("MONOON_PARTICLES_WAIT")!;
        while (((SceneDocument)scene.GetValue(view)!).Objects.All(o => o.Name != wait)) await Task.Delay(100);
        await Task.Delay(500);
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var game = (GameSession)view.GetType().GetField("game", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!).ProjectId!.Value;
        var frames = File.ReadAllText(Path.Combine(Output, "captures.txt")).Split(',').Select(int.Parse).ToHashSet();
        Replay(game, project, Directory.GetFiles(Output, "*.inputreplay").Single(), frames, Directory.CreateDirectory(Path.Combine(Output, "captures")).FullName);
        // The live Scene panel keeps ticking until Stop; those frames are not part of the recording.
        Environment.SetEnvironmentVariable("MONOON_PARTICLE_TRACE", null);
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
