using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Engine.Core;

// Scene Play in the packaged editor: add the 10,000-collider field (4), return to the lab (0), add it again and
// cancel halfway through the uploads (Backspace). Records the loading frames and the resources before and after cancel.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_SPLIT_LOADING_EDITOR")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Guid Field = Guid.Parse("ae245142-c47c-52aa-8b4f-5c867bdef887"), Lab = Guid.Parse("5750e5bb-1700-5a5b-94c2-b6bd02a0356c");

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life.MainWindow!));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    // Frame-counting waits: keep the 10 ms poll so input lands on the same frames as before.
    private static Task Until(Func<bool> done, string what, int seconds = 30) => Probe.Until(done, what, seconds, poll: 10);

    private static async Task Test(Window main)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var type = view.GetType();
        await Until(() => ((SceneDocument)type.GetProperty("Scene")!.GetValue(view)!).Objects.Count > 0, "scene open");
        main.Width = 1400; main.Height = 900;
        main.Activate();
        type.GetField("KeepSimulationActive", Any)!.SetValue(view, (Func<bool>)(() => true));
        var play = main.FindControl<Button>("PlayButton")!;
        Click(play);
        var game = (GameSession)type.GetField("game", Any)!.GetValue(view)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        main.FindControl<ToggleButton>("DebugButton")!.IsChecked = true;
        var profiler = main.FindControl<CheckBox>("FrameDebug")!;
        profiler.IsChecked = true;
        profiler.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, profiler));
        var history = type.GetField("Profile", Any)!.GetValue(view)!;
        var work = (FrameProfiler)history.GetType().GetField("Work")!.GetValue(history)!;
        var resources = (SceneResources)typeof(GameSession).GetField("resources", Any)!.GetValue(game)!;
        object Counts()
        {
            var native = type.GetField("native", Any)!.GetValue(view);
            var canvas = native?.GetType().GetField("canvas", Any)!.GetValue(native) as Engine.Desktop.SdlCanvas;
            return new { resident = resources.ResidentCount, decoded = resources.DecodedCount, textures = canvas?.SpriteTextures };
        }
        async Task Frames(int count) { long at = work.Sequence; await Until(() => work.Sequence >= at + count, $"{count} frames"); }
        async Task Press(string key)
        {
            Require(game.Actions.Press(key), $"{key} is not bound");
            await Frames(2);
            game.Actions.Release(key);
        }
        object Window(long after, long until)
        {
            var frames = Enumerable.Range(0, work.Count).Select(work.At).Where(f => f.Sequence > after && f.Sequence <= until).ToList();
            var times = frames.Select(f => f.Milliseconds).Order().ToList();
            var slowest = frames.MaxBy(f => f.Milliseconds)!;
            return new
            {
                frames = frames.Count, total_ms = frames.Sum(f => f.Milliseconds), max_ms = times[^1],
                p95_ms = times[Math.Min(times.Count - 1, (int)Math.Round(.95 * (times.Count - 1)))],
                loading_ms = frames.Sum(f => f.Total("Scene.Loading").Milliseconds),
                slowest = new { frame = slowest.Sequence, alloc_bytes = slowest.AllocatedBytes, gen0 = slowest.Gen0,
                    top = Enumerable.Range(0, slowest.NodeCount).Where(n => slowest.Depth(n) <= 3 && slowest.InclusiveMilliseconds(n) >= 1)
                        .Select(n => new { name = slowest.Name(n), depth = slowest.Depth(n), total_ms = slowest.InclusiveMilliseconds(n), self_ms = slowest.SelfMilliseconds(n) }) },
            };
        }
        long? Commit(long after) => Enumerable.Range(0, work.Count).Select(work.At)
            .FirstOrDefault(f => f.Sequence > after && f.Total("Scene.Commit").Calls > 0)?.Sequence;

        await Until(() => work.Count > 30, "frames before input");
        var baseline = Counts();
        long before = work.Sequence;
        await Press("4");
        await Until(() => game.State.IsSceneLoaded(Field), "field added");
        await Frames(5);
        long commit = Commit(before) ?? throw new InvalidDataException("no commit frame");
        // The commit frame's Editor sample includes the Play Hierarchy rebuild for 10,000 objects; it is reported separately.
        var add = new { loading = Window(before, commit - 1), commit = Window(commit - 1, commit), after_commit = Window(commit, commit + 2), counts = Counts() };

        await Press("0");
        await Until(() => game.SceneId == Lab && !game.State.IsSceneLoaded(Field) && game.State.PendingScene == null, "lab reloaded");
        await Frames(30);
        var start = Counts();
        long cancelFrom = work.Sequence;
        await Press("4");
        await Until(() => game.State.PendingScene is { Progress: > .55f }, "uploads started");
        await Press("Backspace");
        await Until(() => game.State.PendingScene == null, "load canceled");
        await Until(() => resources.DecodedCount == 0, "decoded buffers released", 5);
        await Frames(5);
        var end = Counts();
        Require(!game.State.IsSceneLoaded(Field) && JsonSerializer.Serialize(start) == JsonSerializer.Serialize(end), $"after cancel {JsonSerializer.Serialize(end)} != {JsonSerializer.Serialize(start)}");
        Click(play);
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(new
        {
            budget_ms = game.State.SceneLoadBudgetMilliseconds, baseline, add, cancel = new { start, end, window = Window(cancelFrom, work.Sequence) },
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
