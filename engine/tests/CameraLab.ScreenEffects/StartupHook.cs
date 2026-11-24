using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Engine.Core;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS: starts Scene Play once CameraLab is open and writes a ticket.
// verify.py then sends the same keys to the editor window as to the exported players.
public static class StartupHook
{
    // No shutdown: verify.py drives the open editor and closes it. Errors (and unexpected windows) go to the ticket.
    public static void Initialize() => Start(Environment.GetEnvironmentVariable("MONOON_QA_TICKET")!, async life =>
    {
        var ticket = Environment.GetEnvironmentVariable("MONOON_QA_TICKET")!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scene = viewport.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(viewport)!).Objects.Any(o => o.Name == "Screen Effects"), "Screen Effects");
        await Delay(500);
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        viewport.Focus();
        File.WriteAllText(ticket, "playing");
    }, shutdown: false);
}
