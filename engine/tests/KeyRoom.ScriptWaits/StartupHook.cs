using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Engine.Core;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS. Starts Scene Play, replays the wait recording and stops.
public static class StartupHook
{
    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            string output = Environment.GetEnvironmentVariable("MONOON_WAIT_EDITOR")!;
            var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try
            {
                var main = life.MainWindow!;
                var viewport = main.FindControl<Control>("Viewport")!;
                var scene = viewport.GetType().GetProperty("Scene")!;
                while (((SceneDocument)scene.GetValue(viewport)!).Objects.All(o => o.Name != "Wait trace")) await Task.Delay(100);
                var play = main.FindControl<Button>("PlayButton")!;
                var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!);
                play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
                var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
                game.LoadFailed += message => throw new InvalidDataException(message);
                InputReplay.Load(Path.Combine(output, "wait.inputreplay")).Play(game, project.ProjectId!.Value);
                play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
                await Task.Delay(200);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); }
            finally { life.Shutdown(); }
        });
    });
}
