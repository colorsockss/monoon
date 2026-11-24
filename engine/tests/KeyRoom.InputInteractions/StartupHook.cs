using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Engine.Core;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS: one Scene Play replays the KeyRoom interaction recording.
public static class StartupHook
{
    public static void Initialize() => Start(Path.Combine(Environment.GetEnvironmentVariable("MONOON_INPUT_OUT")!, "error.txt"), async life =>
    {
        string output = Environment.GetEnvironmentVariable("MONOON_INPUT_OUT")!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scene = viewport.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(viewport)!).Objects.Any(o => o.Name == "Input trace"), "Input trace");
        var play = main.FindControl<Button>("PlayButton")!;
        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        InputReplay.Load(Path.Combine(output, "input.inputreplay")).Play(game, project.ProjectId!.Value);
        // Stop at once: the trace is compared up to the recording's last tick.
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        await Delay(200);
    });
}
