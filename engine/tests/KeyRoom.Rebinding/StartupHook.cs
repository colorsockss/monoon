using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Engine.Core;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS. Each run starts a new Scene Play on the same profile.
public static class StartupHook
{
    public static void Initialize() => Start(Path.Combine(Environment.GetEnvironmentVariable("MONOON_REBIND_EDITOR")!, "error.txt"), async life =>
    {
        string output = Environment.GetEnvironmentVariable("MONOON_REBIND_EDITOR")!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scene = viewport.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(viewport)!).Objects.Any(o => o.Name == "Rebinding trace"), "Rebinding trace");
        var play = main.FindControl<Button>("PlayButton")!;
        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!);
        foreach (var run in new[] { "rebind", "restart", "restored" })
        {
            Environment.SetEnvironmentVariable("MONOON_REBIND_TRACE", Path.Combine(output, run + ".trace"));
            play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
            var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
            game.LoadFailed += message => throw new InvalidDataException(message);
            InputReplay.Load(Path.Combine(output, run + ".inputreplay")).Play(game, project.ProjectId!.Value);
            play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
            var settings = Path.Combine(Environment.GetEnvironmentVariable("MONOON_USER_DATA")!, "Play", project.ProjectId!.Value.ToString("N"), "player.settings");
            File.Copy(settings, Path.Combine(output, run + ".player.settings"));
            await Delay(200);
        }
    });
}
