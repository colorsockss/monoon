using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Engine.Core;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS. Starts Scene Play of an integer-scale DisplayLab copy and
// resizes the editor window three times; the Game panel must show the game at whole device pixels per game pixel.
public static class StartupHook
{
    public static void Initialize() => Start(Path.Combine(Environment.GetEnvironmentVariable("MONOON_INTEGER_EDITOR")!, "error.txt"), async life =>
    {
        string output = Environment.GetEnvironmentVariable("MONOON_INTEGER_EDITOR")!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scale = viewport.GetType().GetProperty("Scale", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var settings = (GameSettings)viewport.GetType().GetProperty("LayoutSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
        if (settings.IntegerScale != true) throw new InvalidDataException("The DisplayLab copy does not enable integer scale.");
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var playing = viewport.GetType().GetProperty("IsPlaying", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        await Until(() => (bool)playing.GetValue(viewport)!, "Scene Play");
        double pixels = main.RenderScaling;
        var rows = new List<object>();
        foreach (var (w, h) in new[] { (1280, 800), (1600, 1000), (1920, 1080) })
        {
            main.WindowState = WindowState.Normal;
            main.Width = w; main.Height = h;
            await Delay(700);
            var panel = viewport.Bounds.Size;
            double fit = Math.Min(panel.Width * pixels / 800, panel.Height * pixels / 450);
            double device = (double)scale.GetValue(viewport)! * pixels;
            if (Math.Abs(device - Math.Max(1, Math.Floor(fit + 1e-9))) > 1e-9)
                throw new InvalidDataException($"Window {w}x{h}: panel {panel} at {pixels}x shows {device} device pixels per game pixel (fit {fit}).");
            rows.Add(new { window = new[] { w, h }, panel = new[] { panel.Width, panel.Height }, renderScaling = pixels, fit, devicePixelsPerGamePixel = device });
        }
        File.WriteAllText(Path.Combine(output, "scene-play.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        await Delay(200);
    });
}
