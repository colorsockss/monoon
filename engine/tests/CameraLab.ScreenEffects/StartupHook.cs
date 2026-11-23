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
    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var ticket = Environment.GetEnvironmentVariable("MONOON_QA_TICKET")!;
            try
            {
                var main = ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
                var viewport = main.FindControl<Control>("Viewport")!;
                var scene = viewport.GetType().GetProperty("Scene")!;
                while (((SceneDocument)scene.GetValue(viewport)!).Objects.All(o => o.Name != "Screen Effects")) await Task.Delay(100);
                await Task.Delay(500);
                var play = main.FindControl<Button>("PlayButton")!;
                play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
                viewport.Focus();
                File.WriteAllText(ticket, "playing");
            }
            catch (Exception ex) { File.WriteAllText(ticket, "error " + ex); }
        });
    });
}
