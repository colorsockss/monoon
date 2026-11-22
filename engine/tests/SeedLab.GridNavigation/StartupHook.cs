using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Engine.Core;

public static class StartupHook
{
    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            string output = Environment.GetEnvironmentVariable("MONOON_ROUTE_EDITOR")!;
            var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try
            {
                var main = life.MainWindow!;
                var viewport = main.FindControl<Control>("Viewport")!;
                var scene = viewport.GetType().GetProperty("Scene")!;
                while (((SceneDocument)scene.GetValue(viewport)!).Objects.All(o => o.Name != "Board")) await Task.Delay(100);
                var play = main.FindControl<Button>("PlayButton")!;
                play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
                var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
                game.LoadFailed += message => throw new InvalidDataException(message);
                var project = GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!);
                InputReplay.Load(Path.Combine(output, "Routes.inputreplay")).Play(game, project.ProjectId!.Value);
                play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output,"error.txt"), ex.ToString()); }
            finally { life.Shutdown(); }
        });
    });
}
