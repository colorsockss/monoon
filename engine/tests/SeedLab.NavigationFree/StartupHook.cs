using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Engine.Core;

// Scene Play: replays Crowd.inputreplay from the start scene (the crowd yard in the prepared copy). With MONOON_CROWD_GIZMO set (a copy whose start scene is the
// Crowd yard), selects the Yard region instead, records the Inspector's Top-down row and captures the window
// (MONOON_QA_WINDOW is the clip-qa window tool).
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_CROWD_EDITOR")!;

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), async life =>
    {
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scene = viewport.GetType().GetProperty("Scene")!;
        SceneDocument Document() => (SceneDocument)scene.GetValue(viewport)!;
        if (Environment.GetEnvironmentVariable("MONOON_CROWD_GIZMO") is { Length: > 0 })
        {
            await Until(() => Document().Objects.Any(o => o.Name == "Yard"), "Yard");
            main.GetType().GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, [Document().Objects.Single(o => o.Name == "Yard")]);
            await Delay(1500);
            var row = main.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Text?.Contains("cells open") == true).Select(t => t.Text).FirstOrDefault();
            File.WriteAllText(Path.Combine(Output, "inspector.txt"), (row ?? "missing") + "\n");
            var info = Process.Start(new ProcessStartInfo(Environment.GetEnvironmentVariable("MONOON_QA_WINDOW")!, $"info {Environment.ProcessId}") { RedirectStandardOutput = true })!;
            string window = info.StandardOutput.ReadToEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            info.WaitForExit();
            Process.Start("screencapture", ["-x", "-o", "-l", window, Path.Combine(Output, "editor-gizmo.png")])!.WaitForExit();
            return;
        }
        await Until(() => Document().Objects.Any(o => o.Name == "Yard"), "Yard");
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!);
        InputReplay.Load(Path.Combine(Output, "Crowd.inputreplay")).Play(game, project.ProjectId!.Value);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
    });
}
