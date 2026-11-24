using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS. Replays the herbalist recording in Scene Play and draws the
// chosen frames with the Scene panel's text path (SDL canvas + SDL_ttf, the same as the player) on an offscreen SDL surface.
public static class StartupHook
{
    public static void Initialize() => Start(Path.Combine(Environment.GetEnvironmentVariable("MONOON_RICH_TEXT_EDITOR")!, "error.txt"), async life =>
    {
        string output = Environment.GetEnvironmentVariable("MONOON_RICH_TEXT_EDITOR")!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scene = viewport.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(viewport)!).Objects.Any(o => o.Name == "Reveal trace"), "Reveal trace");
        await Delay(500);
        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!);
        var frames = File.ReadAllText(Path.Combine(output, "captures.txt")).Split(',').Select(int.Parse).ToHashSet();
        var captures = Directory.CreateDirectory(Path.Combine(output, "captures")).FullName;
        // Inspector rows for the herbalist line at the three checked window widths.
        var line = ((SceneDocument)scene.GetValue(viewport)!).Objects.Single(o => o.Name == "Merchant line");
        main.GetType().GetMethod("Select", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, [typeof(SceneObject)])!
            .Invoke(main, [line]);
        foreach (var width in new[] { 1280, 1600, 1920 })
        {
            (main.Width, main.Height) = (width, 860);
            await Delay(800);
            WindowCapture(main, Path.Combine(output, $"inspector-{width}.png"));
        }
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        Replay(game, project.ProjectId!.Value, output, frames, captures);
        // Let the live Scene panel present the final line; later frames are not traced.
        Environment.SetEnvironmentVariable("MONOON_REVEAL_TRACE", null);
        await Delay(800);
        WindowCapture(main, Path.Combine(output, "scene-play-window.png"));
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
    });

    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint objc_msgSend(nint receiver, nint selector);
    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint sel_registerName(string name);
    // Only the editor window, including the native SDL Scene view.
    private static void WindowCapture(Window main, string path)
    {
        // Windows and Linux have no native Scene view (Avalonia draws it): capture the window with RenderTargetBitmap there.
        if (main.TryGetPlatformHandle() is not { HandleDescriptor: "NSWindow" } handle)
        {
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)main.Bounds.Width, (int)main.Bounds.Height));
            bitmap.Render(main);
            bitmap.Save(path);
            return;
        }
        var number = objc_msgSend(handle.Handle, sel_registerName("windowNumber"));
        using var capture = System.Diagnostics.Process.Start("/usr/sbin/screencapture", ["-x", "-o", "-l", number.ToString(), path])!;
        capture.WaitForExit();
    }

    private static unsafe void Replay(GameSession game, Guid project, string output, HashSet<int> frames, string captures)
    {
        var size = game.LogicalSize;
        var surface = Sdl.PInvoke.CreateSurface(size.Width, size.Height, SdlPixelFormat.Rgba32);
        var renderer = Sdl.Render.CreateSoftwareRenderer(surface);
        using (var canvas = new SdlCanvas(renderer))
        {
            InputReplay.Load(Path.Combine(output, "Herbalist-rich.inputreplay")).Play(game, project, frame =>
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
