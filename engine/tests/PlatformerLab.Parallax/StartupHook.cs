using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Engine.Core;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS. Starts Scene Play of the parallax scene, replays the recording and stops.
public static class StartupHook
{
    public static void Initialize() => Start(Path.Combine(Environment.GetEnvironmentVariable("MONOON_PARALLAX_EDITOR")!, "error.txt"), async life =>
    {
        string output = Environment.GetEnvironmentVariable("MONOON_PARALLAX_EDITOR")!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scene = viewport.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(viewport)!).Objects.Any(o => o.Name == "Parallax trace"), "Parallax trace");
        var play = main.FindControl<Button>("PlayButton")!;
        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        InputReplay.Load(Path.Combine(output, "parallax.inputreplay")).Play(game, project.ProjectId!.Value);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        await Delay(200);
    });
}
