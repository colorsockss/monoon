using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Engine.Core;

// Scene Play of the imported clip, then an external save of the .aseprite while the editor stays open and focused.
public static class StartupHook
{
    public static void Initialize() => Start(Path.Combine(Environment.GetEnvironmentVariable("MONOON_ASE_EDITOR")!, "error.txt"), async life =>
    {
        string output = Environment.GetEnvironmentVariable("MONOON_ASE_EDITOR")!;
        string root = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<Control>("Viewport")!;
        var scene = viewport.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(viewport)!).Objects.Any(o => o.Name == "Animated copy"), "Animated copy");
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var game = (GameSession)viewport.GetType().GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        InputReplay.Load(Path.Combine(output, "Frames.inputreplay")).Play(game, GameSettings.Load(root).ProjectId!.Value);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));

        var applied = viewport.GetType().GetField("tileAssets", BindingFlags.Instance | BindingFlags.NonPublic)!;
        string png = Path.Combine(root, "Assets", "ArrowCycle.png");
        string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        string before = Hash(png);
        var variant = File.ReadAllBytes(Path.Combine(output, "variant.aseprite"));
        string expectedSource = Convert.ToHexString(SHA256.HashData(variant));
        var clock = Stopwatch.StartNew();
        File.WriteAllBytes(Path.Combine(root, "Assets", "ArrowCycle.aseprite"), variant);
        // Done when the sheet on disk was generated from the new file and the Scene panel holds that sheet's hash.
        while (true)
        {
            await Delay(50);
            if (clock.Elapsed > TimeSpan.FromSeconds(20 * Slow)) throw new TimeoutException("No reimport after the external save.");
            if (!File.ReadAllText(png + ".meta").Contains(expectedSource)) continue;
            var now = Hash(png);
            if (((AssetEntry[])applied.GetValue(viewport)!).Any(e => e.Path == "Assets/ArrowCycle.png" && e.Hash == now)) break;
        }
        File.WriteAllText(Path.Combine(output, "reimport.json"),
            $"{{\"secondsUntilSceneUpdated\": {clock.Elapsed.TotalSeconds:F2}, \"windowActive\": {main.IsActive.ToString().ToLowerInvariant()}, " +
            $"\"sheetBefore\": \"{before}\", \"sheetAfter\": \"{Hash(png)}\", \"source\": \"{expectedSource}\"}}\n");
    });
}
