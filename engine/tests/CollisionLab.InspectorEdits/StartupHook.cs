using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

// Inspector edit cost (I-1) on CollisionLab: per keystroke and per commit on Field10000's 10,010 objects, the undo history
// after 100 committed edits, and the undo/redo/prefab-override result of one typed number edit in the small lab scene.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_EDITOR_PERF")!;
    // The base editor records every keystroke; only checks that hold on both editors run without strict mode.
    private static readonly bool Strict = Environment.GetEnvironmentVariable("MONOON_I1_STRICT") == "1";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life.MainWindow!));

    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static object? Call(object target, string name, params object?[] args) =>
        target.GetType().GetMethod(name, Any)!.Invoke(target, args);
    private static object? Field(object target, string name) => target.GetType().GetField(name, Any)!.GetValue(target);

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

    private static void Key(Control target, Key key)
    {
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });
        Dispatcher.UIThread.RunJobs();
    }
    // TextBox raises TextChanged through the dispatcher, so a keystroke's cost includes the jobs it queues.
    private static void Char(TextBox box, char c)
    {
        box.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = c.ToString() });
        Dispatcher.UIThread.RunJobs();
    }
    private static void Focus(Control target)
    {
        target.Focus();
        Dispatcher.UIThread.RunJobs();
    }
    private static TextBox Input(Control field) => field as TextBox ?? field.GetVisualDescendants().OfType<TextBox>().First();
    private static void Begin(TextBox box)
    {
        Focus(box);
        Require(box.IsKeyboardFocusWithin, "field did not take focus");
        box.SelectAll();
    }

    // The Inspector row whose label is `label`: the editor next to it (the label and its field share a Grid row).
    private static Control? Row(Window main, string label) => main.FindControl<ItemsControl>("AttachedComponents")!.GetVisualDescendants().OfType<TextBlock>()
        .Where(t => t.Text == label && t.Parent is Grid && t.IsEffectivelyVisible)
        .Select(t => ((Grid)t.Parent!).Children.FirstOrDefault(c => Grid.GetColumn(c) == 1 && Grid.GetRow(c) == Grid.GetRow(t))).FirstOrDefault();

    private static async Task Test(Window main)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var type = view.GetType();
        SceneDocument Scene() => (SceneDocument)type.GetProperty("Scene")!.GetValue(view)!;
        await Until(() => Scene().Objects.Count > 0, "scene open", 120, 20);
        main.Width = 1400; main.Height = 900;
        main.Activate();
        type.GetField("KeepSimulationActive", Any)!.SetValue(view, (Func<bool>)(() => true));
        var history = Field(main, "history")!;
        int Position() => (int)Field(history, "position")!;
        int Count() => ((System.Collections.ICollection)Field(history, "edits")!).Count;
        // Every capture makes a new state object in both history shapes (whole text, or cut object entries).
        object Current() => Field(history, "current")!;
        var caption = main.FindControl<TextBlock>("SceneCaption")!;
        async Task Show(SceneObject obj)
        {
            Call(main, "Select", obj);
            main.UpdateLayout();
            await Task.Delay(100);
            main.UpdateLayout();
        }

        // KeyRoom's showcase (MONOON_I1_SCENE=Showcase) skips the lab checks and measures its own small scene instead of Field10000.
        string sceneName = Environment.GetEnvironmentVariable("MONOON_I1_SCENE") ?? "Field10000";
        bool field = sceneName == "Field10000";
        // 1. Lab scene, new editor only (the base editor records each Sprite Order keystroke as its own undo step).
        if (Strict && field) await LabChecks();
        async Task LabChecks()
        {
            // A prefab instance's Sprite Order, typed "12" then "3" without leaving the field.
            var lab = Scene();
            var probe = lab.Objects.Single(o => o.Name == "Probe");
            await Show(probe);
            await (Task)Call(main, "SavePrefab", false)!;
            var root = lab.Objects.Single(o => o.Id == probe.Id);
            Require(root.Prefab != null, "Probe became a prefab instance");
            await Show(root);
            // The Inspector rebuilds after the prefab change; under load its rows can take longer than Show's short wait.
            await Until(() => { main.UpdateLayout(); return Row(main, "Order") is NumericUpDown; }, "Sprite Order field", 20);
            var order = (NumericUpDown)Row(main, "Order")!;
            int originalOrder = root.GetComponent<SpriteRendererComponent>()!.Order;
            var overridesBefore = lab.PrefabOverrides(root).ToArray();
            int position = Position(), count = Count();
            var box = Input(order);
            Begin(box);
            Char(box, '1'); Char(box, '2');
            Require(root.GetComponent<SpriteRendererComponent>()!.Order == 12, "typed value applies at once");
            Char(box, '3');
            Require(root.GetComponent<SpriteRendererComponent>()!.Order == 123, "typed 123");
            Require(caption.Text?.EndsWith(" *") == true, $"scene marked unsaved while typing: '{caption.Text}'");
            Require(Position() == position, "no undo record before commit");
            Key(box, Avalonia.Input.Key.Enter);
            Require(Position() == position + 1 && Count() == count + 1, $"one undo record after Enter ({position}->{Position()}, {count}->{Count()})");
            var overridesTyped = lab.PrefabOverrides(root).ToArray();
            Require(overridesTyped.Length == overridesBefore.Length + 1 && overridesTyped.Except(overridesBefore).Single().EndsWith("spriteRenderer.order"),
                "Order shows as a prefab override: " + string.Join(" | ", overridesTyped));
            await (Task)Call(main, "RestoreEdit", false)!;
            root = Scene().Objects.Single(o => o.Id == probe.Id);
            Require(root.GetComponent<SpriteRendererComponent>()!.Order == originalOrder, "undo restores the original Order in one step");
            Require(Scene().PrefabOverrides(root).SequenceEqual(overridesBefore), "undo clears the override");
            await (Task)Call(main, "RestoreEdit", true)!;
            root = Scene().Objects.Single(o => o.Id == probe.Id);
            Require(root.GetComponent<SpriteRendererComponent>()!.Order == 123, "redo restores 123");
            Require(Scene().PrefabOverrides(root).SequenceEqual(overridesTyped), "redo restores the override");

            // A Save while the field still has focus keeps the typed edit as one undo step and leaves the scene clean.
            await Show(root);
            var posX = Input(main.FindControl<NumericUpDown>("PosX")!);
            float x = root.X;
            position = Position();
            Begin(posX);
            Char(posX, '4'); Char(posX, '5');
            Require(Scene().Objects.Single(o => o.Id == probe.Id).X == 45, "Position X typed");
            Require(await (Task<bool>)Call(main, "Save")!, "save cancelled");
            Require(Position() == position + 1, "save keeps the typed edit as one undo record");
            Require(caption.Text?.EndsWith(" *") == false, $"saved scene is clean: '{caption.Text}'");
            Focus(view);
            await (Task)Call(main, "RestoreEdit", false)!;
            Require(Scene().Objects.Single(o => o.Id == probe.Id).X == x, "undo after save restores X");
            await (Task)Call(main, "RestoreEdit", true)!;
            Require(await (Task<bool>)Call(main, "Save")!, "save cancelled");

        }

        // 2. Typing cost. Commit by moving focus, which both editors treat as the end of the edit.
        await (Task)Call(main, "SwitchScene", sceneName)!;
        await Until(() => Scene().Objects.Count > (field ? 10000 : 0) && Path.GetFileNameWithoutExtension((string)Field(main, "sceneName")!) == sceneName, sceneName + " open", 120, 20);
        main.UpdateLayout();
        await Task.Delay(500);
        var scene = Scene();
        async Task<(List<(double, long)> Keys, List<(double, long)> Commits, List<(double, long)> Rounds, List<int> Captures)>
            Typing(SceneObject obj, Func<TextBox> field, Func<int, string> text, int rounds)
        {
            List<(double, long)> keys = [], commits = [], totals = [];
            List<int> captures = [];
            await Show(obj);
            for (int r = 0; r < rounds; r++)
            {
                var input = field();
                Focus(view);
                Begin(input);
                var seen = new HashSet<object>(ReferenceEqualityComparer.Instance) { Current() };
                List<(double, long)> round = [];
                foreach (char c in text(r)) { round.Add(Measure(() => Char(input, c))); seen.Add(Current()); }
                var commit = Measure(() => Focus(view));
                Require(view.IsKeyboardFocusWithin, "Scene view took focus");
                seen.Add(Current());
                Require(input.Text == text(r), $"typed '{input.Text}'");
                if (r == 0) continue; // warm-up
                keys.AddRange(round);
                commits.Add(commit);
                totals.Add((round.Sum(k => k.Item1) + commit.Ms, round.Sum(k => k.Item2) + commit.Bytes));
                captures.Add(seen.Count - 1);
            }
            return (keys, commits, totals, captures);
        }
        var collider = field ? scene.Objects[^1] : scene.Objects.Single(o => o.Name == "Player");
        var number = await Typing(collider, () => Input(main.FindControl<NumericUpDown>("PosX")!), r => (12345 + r).ToString(), 9);
        Require(collider.X == 12345 + 8, "Position X after typing");
        var title = field ? scene.Objects.Single(o => o.Name == "Title") : scene.Objects.First(o => o.GetComponent<UiTextComponent>() is { TextKey: null });
        var text = await Typing(title, () => Input(Row(main, "Text") ?? throw new InvalidDataException("no Text row")), r => $"Title {r:0000}", 6);
        Require(title.GetComponent<UiTextComponent>()!.Text == "Title 0005", "Title text after typing");
        if (Strict) Require(number.Captures.All(c => c == 1) && text.Captures.All(c => c == 1), $"one scene capture per committed edit: {string.Join(",", number.Captures)} / {string.Join(",", text.Captures)}");

        // 3. Undo history after 100 committed edits: one digit each, alternating X and Y so that the base editor, which
        //    merges a field's edits until another field is edited, also keeps 100 undo steps.
        await Show(collider);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long heapBefore = GC.GetTotalMemory(true);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        for (int i = 0; i < 100; i++)
        {
            var input = Input(main.FindControl<NumericUpDown>(i % 2 == 0 ? "PosX" : "PosY")!);
            Focus(view);
            Begin(input);
            Char(input, (char)('1' + i % 9));
            Focus(view);
        }
        double editsMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        long heapAfter = GC.GetTotalMemory(true);
        // What the records and the current state hold: whole-scene strings (UTF-16), or cut entries shared between states
        // (each distinct byte array once, plus one 8-byte reference per object per distinct state).
        var held = new HashSet<object>(ReferenceEqualityComparer.Instance);
        long heldBytes = 0;
        int states = 0;
        void Hold(object? state)
        {
            if (state == null || !held.Add(state)) return;
            states++;
            foreach (var member in state.GetType().GetFields(Any).Select(f => f.GetValue(state))
                         .Concat(state.GetType().GetProperties(Any).Where(p => p.GetIndexParameters().Length == 0).Select(p => p.GetValue(state))))
                switch (member)
                {
                    case string text when held.Add(text): heldBytes += 2L * text.Length; break;
                    case byte[] part when held.Add(part): heldBytes += part.Length; break;
                    case byte[][] parts when held.Add(parts):
                        heldBytes += 8L * parts.Length;
                        foreach (var part in parts) if (held.Add(part)) heldBytes += part.Length;
                        break;
                }
        }
        Hold(Current());
        foreach (var edit in (System.Collections.IEnumerable)Field(history, "edits")!)
            foreach (var name in new[] { "Before", "After" })
                Hold(edit.GetType().GetProperty(name)!.GetValue(edit));
        Require(Count() == 100 && Position() == 100, $"100 undo steps ({Count()}, {Position()})");
        Require(collider.X == 1 + 98 % 9 && collider.Y == 1 + 99 % 9, "last edits applied");

        // 100 undos then 100 redos through the history alone (RestoreEdit would also rebuild the 10k-row Hierarchy each time).
        string edited = scene.CaptureObjects();
        var restore = history.GetType().GetMethod("Restore", Any)!;
        (double Ms, long Bytes) Steps(bool redo) => Measure(() => { for (int i = 0; i < 100; i++) restore.Invoke(history, [Scene(), redo]); });
        var undo = Steps(false);
        Require(Position() == 0, "undo reached the start of the 100 records");
        string undone = Scene().CaptureObjects();
        var redo = Steps(true);
        Require(Position() == 100 && Scene().CaptureObjects() == edited, "redo all gives back the edited scene text");
        Require(undone != edited, "undo all changed the scene");
        Call(main, "RebuildHierarchy", Scene().Objects.FirstOrDefault(o => o.Id == collider.Id));
        Require(await (Task<bool>)Call(main, "Save")!, "save cancelled");

        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(new
        {
            scene = sceneName, objects = Scene().Objects.Count,
            strict = Strict,
            number_key = Median(number.Keys), number_commit = Median(number.Commits), number_edit = Median(number.Rounds),
            number_captures = number.Captures,
            text_key = Median(text.Keys), text_commit = Median(text.Commits), text_edit = Median(text.Rounds),
            text_captures = text.Captures,
            history_100 = new
            {
                ms = editsMs, allocated_bytes = allocated, retained_bytes = heapAfter - heapBefore,
                snapshot_strings = states, snapshot_bytes = heldBytes,
                undo_ms = undo.Ms, undo_bytes = undo.Bytes, redo_ms = redo.Ms, redo_bytes = redo.Bytes,
            },
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
