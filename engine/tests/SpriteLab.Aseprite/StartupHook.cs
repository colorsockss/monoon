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
    // MONOON_QA_SLOW scales waits and time limits for emulated x64 guests (TCG); 1 on a normal desktop.
    private static readonly double Slow = double.TryParse(Environment.GetEnvironmentVariable("MONOON_QA_SLOW"), System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1;
    private static Task Delay(int ms) => Task.Delay((int)(ms * Slow));
    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            string output = Environment.GetEnvironmentVariable("MONOON_ASE_EDITOR")!;
            string root = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
            var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try
            {
                var main = life.MainWindow!;
                var viewport = main.FindControl<Control>("Viewport")!;
                var scene = viewport.GetType().GetProperty("Scene")!;
                while (((SceneDocument)scene.GetValue(viewport)!).Objects.All(o => o.Name != "Animated copy")) await Delay(100);
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
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); }
            finally { life.Shutdown(); }
        });
    });
}
