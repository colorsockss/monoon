global using static Probe;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.VisualTree;

// Shared by the editor StartupHook probes: each probe .csproj links this file (<Compile Include="../Shared/Probe.cs"/>).
// It is test code only; the editor and game builds never contain it.
internal static class Probe
{
    // MONOON_QA_SLOW scales waits and time limits for emulated x64 guests (TCG); 1 on a normal desktop.
    public static readonly double Slow = double.TryParse(Environment.GetEnvironmentVariable("MONOON_QA_SLOW"), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 1;

    public static Task Delay(int ms) => Task.Delay((int)(ms * Slow));

    // Polls until done() is true; throws TimeoutException(what) after `seconds` (scaled by MONOON_QA_SLOW).
    public static async Task Until(Func<bool> done, string what, double seconds = 30, int poll = 50)
    {
        var limit = DateTime.UtcNow.AddSeconds(seconds * Slow);
        while (!done())
        {
            if (DateTime.UtcNow > limit) throw new TimeoutException(what);
            await Delay(poll);
        }
    }

    public static async Task<IClassicDesktopStyleApplicationLifetime> WaitMainWindow(double seconds = 60)
    {
        var limit = DateTime.UtcNow.AddSeconds(seconds * Slow);
        IClassicDesktopStyleApplicationLifetime? life;
        while ((life = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow == null)
        {
            if (DateTime.UtcNow > limit) throw new TimeoutException("editor main window");
            await Task.Delay(50);
        }
        return life!;
    }

    // Waits for the main window, then runs test on the UI thread with the window guard. An exception is written to
    // errorFile. shutdown: false leaves the editor open for an outside driver (it closes or kills the editor itself).
    public static void Start(string errorFile, Func<IClassicDesktopStyleApplicationLifetime, Task> test, string[]? allow = null, bool shutdown = true) =>
        Task.Run(async () =>
        {
            IClassicDesktopStyleApplicationLifetime life;
            try { life = await WaitMainWindow(); }
            catch (Exception ex) { Fail(errorFile, ex.ToString()); return; }
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Guard(life, errorFile, allow ?? []);
                try { await test(life); }
                catch (Exception ex) { File.WriteAllText(errorFile, ex.ToString()); }
                finally { if (shutdown) life.Shutdown(); }
            });
        });

    // A window the probe did not open on purpose (unsaved-scene prompt, Choice() question, error box, second dialog) waits for
    // a click forever. Fail within ~250 ms instead, naming the window. The guard keeps running through Shutdown, where the
    // unsaved prompt appears. Windows whose titles are in `allow` are expected; "Prefab · *" matches a title prefix.
    public static void Guard(IClassicDesktopStyleApplicationLifetime life, string errorFile, params string[] allow)
    {
        bool Allowed(string? title) => allow.Any(a => a.EndsWith('*') ? title?.StartsWith(a[..^1], StringComparison.Ordinal) == true : title == a);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) =>
        {
            if (life.Windows.FirstOrDefault(w => w != life.MainWindow && w.IsVisible && !Allowed(w.Title)) is not { } window) return;
            timer.Stop();
            var text = string.Join(" | ", window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t)));
            Fail(errorFile, $"unexpected window '{window.Title}': {text}");
        };
        timer.Start();
    }

    private static void Fail(string errorFile, string message)
    {
        File.AppendAllText(errorFile, message);
        Console.Error.WriteLine("probe: " + message);
        Environment.Exit(3);
    }
}
