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
    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            string output = Environment.GetEnvironmentVariable("MONOON_REBIND_EDITOR")!;
            var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try
            {
                var main = life.MainWindow!;
                var viewport = main.FindControl<Control>("Viewport")!;
                var scene = viewport.GetType().GetProperty("Scene")!;
                while (((SceneDocument)scene.GetValue(viewport)!).Objects.All(o => o.Name != "Rebinding trace")) await Task.Delay(100);
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
                    await Task.Delay(200);
                }
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); }
            finally { life.Shutdown(); }
        });
    });
}
