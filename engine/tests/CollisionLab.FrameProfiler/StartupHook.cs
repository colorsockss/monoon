using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Engine.Core;

// Scene Play in the packaged editor: open the 10,000-collider field, repeat its queries with R, stop,
// then select the slowest frame in the profiler graph and read the work table.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_PROFILER_EDITOR")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Guid Field10000 = Guid.Parse("ae245142-c47c-52aa-8b4f-5c867bdef887");
    private static readonly string[] Queries = ["Physics.Overlap", "Physics.Raycast", "Physics.Sweep"];

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life.MainWindow!));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static async Task Test(Window main)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var type = view.GetType();
        await Until(() => ((SceneDocument)type.GetProperty("Scene")!.GetValue(view)!).Objects.Count > 0, "scene open");
        main.Width = 1400; main.Height = 900;
        main.Activate();
        // The harness may run without focus; Scene Play otherwise pauses in an inactive window.
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
        await Until(() => work.Count > 30, "frames before input");
        async Task Press(string key)
        {
            Require(game.Actions.Press(key), $"{key} is not bound");
            await Delay(250);
            game.Actions.Release(key);
        }
        await Press("2");
        await Until(() => game.SceneId == Field10000, "Field10000 loaded");
        await Delay(1500);
        long before = work.Sequence;
        await Press("R");
        await Until(() => work.Sequence > before + 40, "frames after R");
        Click(play);
        Require(work.Count == Math.Min(work.Sequence, work.Capacity), $"ring holds {work.Count} of {work.Sequence} frames");

        var frames = Enumerable.Range(0, work.Count).Select(work.At).ToList();
        // Entering the field also stalls the editor; the user picks the spike that follows R.
        var load = frames.MaxBy(f => f.Milliseconds)!;
        var slowest = frames.Where(f => f.Sequence > before).MaxBy(f => f.Milliseconds)!;
        var heavy = frames.Where(f => f.Total("Physics.Overlap").Calls > 0).Select(f => new
        {
            frame = f.Sequence, frame_ms = f.Milliseconds, alloc_bytes = f.AllocatedBytes, gen0 = f.Gen0, dropped = f.Dropped,
            calls = Queries.ToDictionary(q => q, q => f.Total(q).Calls),
            query_ms = Queries.Sum(q => f.Total(q).Milliseconds), script_ms = f.Total("QueryField.FixedUpdate").Milliseconds,
        }).ToList();
        var times = frames.Select(f => f.Milliseconds).Order().ToList();

        // Select the slowest frame as a click on the graph does, then read the table the user sees.
        var chart = main.FindControl<Control>("FrameGraph")!;
        var selected = (Delegate)chart.GetType().GetField("FrameSelected", Any)!.GetValue(chart)!;
        selected.DynamicInvoke(slowest.Sequence);
        var title = main.FindControl<TextBlock>("WorkTitle")!;
        var rows = main.FindControl<StackPanel>("WorkRows")!;
        var table = rows.Children.Cast<Grid>().Select(r => r.Children.Cast<TextBlock>().Select(t => t.Text).ToArray()).ToList();
        Require(title.Text!.StartsWith($"Selected frame {slowest.Sequence:N0}"), title.Text);
        foreach (var q in Queries)
            Require(table.Any(r => r[0] == q && r[1] == slowest.Total(q).Calls.ToString("N0")),
                $"{q} row missing: {title.Text}\n{string.Join("\n", table.Select(r => string.Join(" | ", r)))}\n{JsonSerializer.Serialize(heavy)}");

        var alignment = new List<object>();
        var header = ((Grid)title.Parent!).Children.OfType<Grid>().First();
        foreach (int width in new[] { 1400, 820 })
        {
            main.Width = width;
            await Delay(400);
            double Right(Control c) => c.TranslatePoint(new(c.Bounds.Width, 0), main)!.Value.X;
            double Left(Control c) => c.TranslatePoint(default, main)!.Value.X;
            var head = header.Children.Cast<TextBlock>().ToArray();
            var first = rows.Children.Cast<Grid>().First().Children.Cast<TextBlock>().ToArray();
            var last = rows.Children.Cast<Grid>().Last().Children.Cast<TextBlock>().ToArray();
            double drift = Math.Abs(Left(head[0]) - Left(first[0]));
            for (int c = 1; c < 4; c++) drift = Math.Max(drift, Math.Max(Math.Abs(Right(head[c]) - Right(first[c])), Math.Abs(Right(head[c]) - Right(last[c]))));
            Require(drift < 1 && Right(head[3]) <= main.Bounds.Width, $"work columns drift {drift:F2}px at {width}px");
            alignment.Add(new { width, drift, right = Right(head[3]) });
            // The window, not just the dock, so the panel keeps its theme background. The native Scene view stays blank.
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)main.Bounds.Width, (int)main.Bounds.Height));
            bitmap.Render(main);
            bitmap.Save(Path.Combine(Output, $"profiler-{width}.png"));
        }
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(new
        {
            frames = work.Count, median_ms = times[times.Count / 2], slowest = slowest.Sequence, slowest_ms = slowest.Milliseconds,
            load = new { frame = load.Sequence, frame_ms = load.Milliseconds, alloc_bytes = load.AllocatedBytes,
                top = Enumerable.Range(0, load.NodeCount).Where(n => load.Depth(n) <= 2 && load.InclusiveMilliseconds(n) >= 1)
                    .Select(n => new { name = load.Name(n), depth = load.Depth(n), total_ms = load.InclusiveMilliseconds(n), self_ms = load.SelfMilliseconds(n) }) },
            title = title.Text, table, query_frames = heavy, alignment,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
