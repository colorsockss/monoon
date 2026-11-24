using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Engine.Core;

// Editor cost on CollisionLab's 10,000-collider field: edit-mode Scene redraws (draw order), the Hierarchy rebuild in edit
// mode and Play, and the Play Hierarchy refresh after an active toggle (the change that pool Rent/Return makes).
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_EDITOR_PERF")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life.MainWindow!));

    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static Task Until(Func<bool> done, string what, int seconds = 30) => Probe.Until(done, what, seconds, poll: 20);
    private static object? Call(object target, string name, params object?[] args) =>
        target.GetType().GetMethod(name, Any)!.Invoke(target, args);

    private static (double Ms, long Bytes) Measure(Action action)
    {
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        action();
        return (Stopwatch.GetElapsedTime(start).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - bytes);
    }
    private static object Median(List<(double Ms, long Bytes)> runs)
    {
        var ms = runs.Select(r => r.Ms).Order().ToList();
        var bytes = runs.Select(r => r.Bytes).Order().ToList();
        return new { runs = runs.Count, median_ms = ms[ms.Count / 2], min_ms = ms[0], max_ms = ms[^1], median_bytes = bytes[bytes.Count / 2] };
    }

    // The edit view's draw order must match a freshly built order after every kind of edit, and after a Sort-by-Y move.
    private static async Task DrawOrderEdits(Window main, Control view, SceneDocument scene)
    {
        string Items(IEnumerable<(SceneObject Owner, Tilemap2DComponent? Map, int Layer, int Order)> items) =>
            string.Join(";", items.Select(i => $"{i.Owner.Id}/{i.Map != null}/{i.Layer}/{i.Order}"));
        string Shown() => Items((IEnumerable<(SceneObject, Tilemap2DComponent?, int, int)>)Call(view, "EditItems")!);
        string Fresh() => Items(scene.WorldItems());
        void Same(string step) => Require(Shown() == Fresh(), $"draw order after {step}");
        int Rank(SceneObject obj) => scene.WorldItems().Select(i => i.Owner).ToList().IndexOf(obj);
        Same("open");
        Call(main, "AddObject", null, true);
        Call(main, "AddObject", null, true);
        var a = scene.Objects[^2];
        var b = scene.Objects[^1];
        Same("add");
        Require(Rank(a) < Rank(b), "new squares draw in creation order");
        a.GetComponent<SpriteRendererComponent>()!.Order = 5;
        Call(main, "MarkDirty", (object?)null);
        Same("sorting order");
        Require(Rank(a) > Rank(b), "higher order draws later");
        await (Task)Call(main, "RestoreEdit", false)!;
        Same("undo");
        await (Task)Call(main, "RestoreEdit", true)!;
        Same("redo");
        a = scene.Objects.Single(o => o.Id == a.Id);
        b = scene.Objects.Single(o => o.Id == b.Id);
        // A typed Inspector field (TextBox/NumericUpDown) keeps the order unless it changed a sorting input of the selection.
        Call(main, "Select", a);
        Shown();
        object? Kept() => view.GetType().GetField("editOrder", Any)!.GetValue(view);
        var built = Kept();
        var field = new NumericUpDown();
        a.Name = "Typed name";
        Call(main, "MarkDirty", field);
        Require(built != null && ReferenceEquals(Kept(), built), "a typed name keeps the draw order");
        Same("typed name");
        a.GetComponent<SpriteRendererComponent>()!.Order = -3;
        Call(main, "MarkDirty", field);
        Require(Kept() == null, "a typed sorting order rebuilds the draw order");
        Same("typed sorting order");
        Require(Rank(a) < Rank(b), "typed lower order draws first");
        a.GetComponent<SpriteRendererComponent>()!.Order = 0;
        a.GetComponent<SpriteRendererComponent>()!.SortByY = b.GetComponent<SpriteRendererComponent>()!.SortByY = true;
        a.Y = b.Y + 100;
        Call(main, "MarkDirty", (object?)null);
        Same("sort by y");
        Require(Rank(a) > Rank(b), "lower object draws later");
        a.Y = b.Y - 100; // a drag moves objects before the edit is recorded
        Same("sort by y move");
        Require(Rank(a) < Rank(b), "moved object re-sorted");
        Call(main, "ChangeParent", b, a);
        Same("reparent");
        Call(main, "Select", a);
        Call(main, "DeleteObject");
        Same("delete");
    }

    private static async Task Test(Window main)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var type = view.GetType();
        SceneDocument Scene() => (SceneDocument)type.GetProperty("Scene")!.GetValue(view)!;
        await Until(() => Scene().Objects.Count > 0, "scene open");
        main.Width = 1400; main.Height = 900;
        main.Activate();
        type.GetField("KeepSimulationActive", Any)!.SetValue(view, (Func<bool>)(() => true));
        await (Task)Call(main, "SwitchScene", "Field10000")!;
        await Until(() => Scene().Objects.Count > 10000, "Field10000 open");
        main.UpdateLayout();
        await Delay(500);
        var scene = Scene();
        var tree = (System.Collections.IDictionary)main.GetType().GetField("treeItems", Any)!.GetValue(main)!;

        // Edit-mode Scene redraw: the whole native frame and the draw-order part of it.
        var native = type.GetField("native", Any)!.GetValue(view) ?? throw new InvalidDataException("no native Scene panel");
        var canvas = (Engine.Desktop.SdlCanvas)native.GetType().GetField("canvas", Any)!.GetValue(native)!;
        var draw = native.GetType().GetMethod("DrawFrame", Any)!;
        List<(double, long)> frames = [], order = [], items = [];
        long calls = canvas.DrawSubmissions;
        for (int i = 0; i < 45; i++)
        {
            var frame = Measure(() => draw.Invoke(native, null));
            var sorted = Measure(() => scene.WorldItems().Count());
            var edit = Measure(() => ((IEnumerable<(SceneObject, Tilemap2DComponent?, int, int)>)Call(view, "EditItems")!).Count());
            if (i < 5) continue;
            frames.Add(frame); order.Add(sorted); items.Add(edit);
        }
        Require(canvas.DrawSubmissions > calls, "edit frames drew nothing");

        // Edit-mode Hierarchy rebuild (rows with eye/lock toggles) and the layout that realizes the rows.
        var selected = scene.Objects[^1];
        List<(double, long)> editBuild = [], editLayout = [];
        for (int i = 0; i < 3; i++)
        {
            editBuild.Add(Measure(() => Call(main, "RebuildHierarchy", selected)));
            editLayout.Add(Measure(main.UpdateLayout));
        }

        // Play: entering Play rebuilds the Hierarchy for the play world.
        var play = main.FindControl<Button>("PlayButton")!;
        var enter = Measure(() => play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play)));
        var enterLayout = Measure(main.UpdateLayout);
        var world = (SceneGame)type.GetProperty("PlayWorld", Any)!.GetValue(view)!;
        Require(world.Objects.Count > 10000, "play world");
        await Delay(1000);
        main.UpdateLayout();

        var target = world.Objects[^1];
        var canvasRow = world.Objects.First(o => o.GetComponent<UiCanvasComponent>() != null);
        Call(main, "Select", target);
        TreeViewItem Row(SceneObject obj) => (TreeViewItem)tree[obj]!;
        Row(canvasRow).IsExpanded = false;
        var hierarchy = main.FindControl<TreeView>("Hierarchy")!;

        // Active toggles bump HierarchyRevision without changing the object set.
        List<(double, long)> toggle = [], toggleLayout = [];
        bool reused = true;
        for (int i = 0; i < 6; i++)
        {
            bool active = i % 2 == 1;
            var before = Row(target);
            Require(world.SetActive(target.Id, active), "SetActive");
            toggle.Add(Measure(() => Call(main, "SyncPlayInspector")));
            toggleLayout.Add(Measure(main.UpdateLayout));
            reused &= ReferenceEquals(before, Row(target));
            Require(Row(target).Opacity == (active ? 1 : 0.55), $"row opacity {Row(target).Opacity} after SetActive({active})");
            Require((hierarchy.SelectedItem as TreeViewItem)?.Tag == target, "selection kept");
            Require(!Row(canvasRow).IsExpanded, "collapsed row kept");
            Require(Row(world.Objects[^2]).Opacity == 1, "other rows unchanged");
        }

        // A full Play rebuild (what a structural change still costs).
        List<(double, long)> playBuild = [], playLayout = [];
        for (int i = 0; i < 3; i++)
        {
            playBuild.Add(Measure(() => Call(main, "RebuildHierarchy", target)));
            playLayout.Add(Measure(main.UpdateLayout));
        }
        Require((hierarchy.SelectedItem as TreeViewItem)?.Tag == target && !Row(canvasRow).IsExpanded, "full rebuild keeps selection and expansion");
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));

        // Correctness only, after every timed step: edit the small lab scene, then save the scratch copy so nothing prompts.
        await (Task)Call(main, "SwitchScene", "CollisionLab")!;
        await Until(() => Scene().Objects.Count < 10000, "lab scene open");
        await DrawOrderEdits(main, view, Scene());
        Require(await (Task<bool>)Call(main, "Save")!, "save cancelled");

        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(new
        {
            objects = scene.Objects.Count,
            edit_frame = Median(frames), world_items = Median(order), edit_items = Median(items),
            edit_rebuild = Median(editBuild), edit_rebuild_layout = Median(editLayout),
            play_enter = new { ms = enter.Ms, bytes = enter.Bytes, layout_ms = enterLayout.Ms, layout_bytes = enterLayout.Bytes },
            toggle = Median(toggle), toggle_layout = Median(toggleLayout), toggle_reused_rows = reused,
            play_rebuild = Median(playBuild), play_rebuild_layout = Median(playLayout),
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
