using System.Collections;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Engine.Core;
using Engine.Editor;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS (roadmap 11.2). Starts Scene Play, saves version 2 of
// Scripts/ReloadProbe.cs, waits for the editor's own reload, then saves a syntax error and checks the old code keeps running.
public static class StartupHook
{
    // MONOON_QA_SLOW scales waits and time limits for emulated x64 guests (TCG); 1 on a normal desktop.
    private static readonly double Slow = double.TryParse(Environment.GetEnvironmentVariable("MONOON_QA_SLOW"), System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1;
    private static Task Delay(int ms) => Task.Delay((int)(ms * Slow));
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_RELOAD_OUTPUT")!;
    private static readonly string Here = Environment.GetEnvironmentVariable("MONOON_RELOAD_HARNESS")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, object?> Result = [];
    private static Window main = null!;
    private static SceneViewport view = null!;

    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try
            {
                main = life.MainWindow!;
                view = main.FindControl<SceneViewport>("Viewport")!;
                await Until(() => main.IsVisible && Scene.Objects.Any(o => o.Name == "Reload probe"), "KeyRoom");
                main.Width = 1280; main.Height = 800;
                await Delay(800);
                await Run();
                File.WriteAllText(Path.Combine(Output, "reload.json"), JsonSerializer.Serialize(Result, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Output, "error.txt"), ex.ToString()); }
            finally { life.Shutdown(); }
        });
    });

    private static async Task Run()
    {
        var probeId = Scene.Objects.First(o => o.Name == "Reload probe").Id;
        string file = Path.Combine(Scene.ProjectDirectory!, "Scripts", "ReloadProbe.cs");
        Call(main, "ShowConsole");
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        await Until(() => view.IsPlaying, "Play");
        var game = (GameSession)Get(view, "game")!;
        GameBehaviour Probe() => game.Current.GetComponent<GameBehaviour>(probeId)!;
        object? Field(string name) => Probe().GetType().GetField(name)?.GetValue(Probe());
        // Drive game ticks directly so the counts do not depend on window focus; all of this runs on the UI thread.
        void Tick(int n) { for (int i = 0; i < n; i++) game.FixedUpdate(default(GameInput), 1f / 60); }
        Tick(30);
        var before = Probe();
        int ticks = (int)Field("Ticks")!;
        Step("v1", new { ticks, speed = Field("Speed"), version = Field("Version"), routines = Routines(game) });

        File.WriteAllText(file, File.ReadAllText(Path.Combine(Here, "ReloadProbe.v2.source")));
        await Until(() => Console().Any(r => r.Message.StartsWith("Scripts reloaded")), "reload", 60);
        var after = Probe();
        int kept = (int)Field("Ticks")!;
        int t = kept; Tick(1);
        Step("v2", new { replaced = !ReferenceEquals(before, after), new_assembly = before.GetType().Assembly != after.GetType().Assembly,
            ticks_before = ticks, ticks_after_reload = kept, delta_one_tick = (int)Field("Ticks")! - t,
            speed = Field("Speed"), version = Field("Version"), label = Field("Label"), routines = Routines(game),
            console = Console().Where(r => r.Message.Contains("reload", StringComparison.OrdinalIgnoreCase) || r.Message.Contains("routine") || r.Level == "Warning").ToArray() });
        await Delay(600);
        ScreenCapture("reload-1280");

        string v2 = File.ReadAllText(file);
        File.WriteAllText(file, v2.Replace("Ticks += 1000;", "Ticks += 1000"));
        await Until(() => Console().Any(r => r.Message.StartsWith("Script reload failed")), "compile error", 60);
        var error = Console().Last(r => r.Level == "Error" && r.File != null);
        t = (int)Field("Ticks")!; Tick(1);
        Step("broken", new { error.Message, error.File, error.Line, same_instance = ReferenceEquals(Probe(), after),
            delta_one_tick = (int)Field("Ticks")! - t, playing = view.IsPlaying });
        await Delay(600);
        ScreenCapture("compile-error-1280");
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        await Until(() => !view.IsPlaying, "Stop");
    }

    private static int Routines(GameSession game) => ((IList)typeof(SceneGame).GetField("routines", Any)!.GetValue(game.Current)!).Count;
    private static async Task Until(Func<bool> done, string what, int seconds = 60)
    {
        var limit = DateTime.UtcNow.AddSeconds(seconds * Slow);
        while (!done()) { if (DateTime.UtcNow > limit) throw new TimeoutException(what); await Delay(50); }
    }
    private static object? Get(object target, string name) =>
        target.GetType().GetProperty(name, Any) is { } p ? p.GetValue(target) : target.GetType().GetField(name, Any)?.GetValue(target);
    private static object? Call(object target, string method, params object?[] args) =>
        target.GetType().GetMethods(Any).First(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static SceneDocument Scene => (SceneDocument)Get(main, "Scene")!;
    private static void Step(string name, object? value)
    {
        Result[name] = value;
        File.AppendAllText(Path.Combine(Output, "reload.log"), $"{name}: {JsonSerializer.Serialize(value)}\n");
    }

    private sealed record ConsoleLine(string Level, string Message, string? File, int Line);
    private static List<ConsoleLine> Console()
    {
        var rows = new List<ConsoleLine>();
        foreach (var row in (IEnumerable)Get(main, "consoleRows")!)
            rows.Add(new(Get(row, "Level")!.ToString()!, (string)Get(row, "Message")!, (string?)Get(row, "File"), (int)Get(row, "Line")!));
        return rows;
    }

    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint objc_msgSend(nint receiver, nint selector);
    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint sel_registerName(string name);
    private static void ScreenCapture(string name)
    {
        // Windows and Linux have no native Scene view (Avalonia draws it): capture the window with RenderTargetBitmap there.
        if (main.TryGetPlatformHandle() is not { HandleDescriptor: "NSWindow" } handle)
        {
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)main.Bounds.Width, (int)main.Bounds.Height));
            bitmap.Render(main);
            bitmap.Save(Path.Combine(Output, name + ".png"));
            return;
        }
        var number = objc_msgSend(handle.Handle, sel_registerName("windowNumber"));
        using var capture = System.Diagnostics.Process.Start("/usr/sbin/screencapture", ["-x", "-o", "-l", number.ToString(), Path.Combine(Output, name + ".png")])!;
        capture.WaitForExit();
    }
}
