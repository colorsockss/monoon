using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Engine.Core;

// Scene Play probe: plays AudioLab until MONOON_AUDIO_CAPTURE holds four music loops, then stops the music
// with its Inspector fade (what F does in the game) and leaves Play once the fade has drained.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_AUDIO_EDITOR")!;
    private static readonly string Capture = Environment.GetEnvironmentVariable("MONOON_AUDIO_CAPTURE")!;
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life.MainWindow!));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    private static long Frames() => File.Exists(Capture) ? new FileInfo(Capture).Length / 8 : 0;

    private static async Task Test(Window main)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        SceneDocument Scene() => (SceneDocument)scene.GetValue(view)!;
        await Until(() => Scene().Objects.Any(o => o.Name == "Tide"), "Tide");
        var music = Scene().Objects.Single(o => o.Name == "Tide").Id;
        Click(main.FindControl<Button>("PlayButton")!);
        var game = (GameSession)view.GetType().GetField("game", Any)!.GetValue(view)!;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (Frames() < 4 * 57600 + 4 * 512)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("four music loops");
            await Delay(50);
        }
        long before = Frames();
        File.WriteAllText(Path.Combine(Output, "before-f.txt"), before.ToString());
        if (!game.Current.Stop(music)) throw new InvalidDataException("Tide was not playing");
        // The capture only grows while a voice plays; wait for the 0.6 s fade to end and the file to settle.
        long last = -1;
        while (Frames() != last || Frames() < before + 24000) { last = Frames(); await Delay(500); }
        Click(main.FindControl<Button>("PlayButton")!);
    }
}
