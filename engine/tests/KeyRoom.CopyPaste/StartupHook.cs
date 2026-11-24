using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

// Drives the packaged editor: copy a parent with a child and a prefab instance, paste them into the same scene and
// into SecondRoom through the Edit menu, Undo and Redo each step, show the missing reference, save and Play.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_COPY_EDITOR")!;
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static bool noClipboard;
    private const string Missing = "Missing object or component";

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Run(life.MainWindow!));

    private static object? Call(Window w, string method, params object?[] args) => w.GetType().GetMethod(method, Members)!.Invoke(w, args);
    private static SceneDocument Scene(Window w) => (SceneDocument)w.GetType().GetProperty("Scene", Members)!.GetValue(w)!;
    private static void Require(bool ok, string why) { if (!ok) throw new InvalidDataException(why); }
    private static void Log(string line) => File.AppendAllText(Path.Combine(Output, "editor.txt"), line + "\n");
    private static JsonElement Ref(Guid id) => JsonDocument.Parse($"\"{id}\"").RootElement.Clone();
    private static Guid Field(SceneObject obj, string name) => obj.GetComponent<ScriptComponent>()!.ReadOverride(name, Guid.Empty);
    private static Rect In(Visual c, Visual root) => new(c.TranslatePoint(default, root)!.Value, c.Bounds.Size);

    private static NativeMenuItem Item(Window main, string menu, string header) =>
        NativeMenu.GetMenu(main)!.Items.OfType<NativeMenuItem>().Single(i => i.Header == menu).Menu!.Items.OfType<NativeMenuItem>().Single(i => i.Header == header);

    // The same entry point the native menu bar uses for a click or a key equivalent.
    private static void Click(NativeMenuItem item) =>
        typeof(NativeMenuItem).GetMethods(Members).First(m => m.Name.Contains("RaiseClick") && m.GetParameters().Length == 0).Invoke(item, null);

    private static async Task Choose(Window main, string header, string status)
    {
        var item = Item(main, "Edit", header);
        Require(item.IsEnabled, $"Edit > {header} is disabled");
        var status0 = main.FindControl<TextBlock>("Status")!;
        status0.Text = "";
        Click(item);
        await Until(() => status0.Text == status, $"{header}: status '{status}', got '{status0.Text}'");
    }

    private static async Task Run(Window main)
    {
        string root = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        string trace = Environment.GetEnvironmentVariable("MONOON_COPY_TRACE")!;
        await Until(() => Scene(main).Objects.Count > 1, "KeyRoom");
        main.Width = 1280; main.Height = 800;
        var edit = NativeMenu.GetMenu(main)!.Items.OfType<NativeMenuItem>().Single(i => i.Header == "Edit").Menu!.Items.OfType<NativeMenuItem>();
        Log("edit menu " + string.Join(" | ", edit.Select(i => $"{i.Header} {i.Gesture}")));
        foreach (var (header, key) in new[] { ("Cut", "X"), ("Copy", "C"), ("Paste", "V") })
            Require(Item(main, "Edit", header).Gesture?.ToString() == (OperatingSystem.IsMacOS() ? "Cmd+" : "Ctrl+") + key, header + " shortcut");

        await (Task)Call(main, "SwitchScene", "PrefabWorkshop")!;
        var scene = Scene(main);
        var chest = scene.Objects.Single(o => o.Name == "Chest 1");
        var floor = scene.Objects.Single(o => o.Name == "Floor");

        // A focused text field keeps its own Copy: the object name goes to the clipboard, not the object.
        Call(main, "Select", floor);
        // A desktop without a window manager (Xvfb) never activates the window by itself; the Edit menu reads focus from the active window.
        main.Activate();
        for (int i = 0; i < 20 && !main.IsActive; i++) await Delay(100);
        var name = main.FindControl<TextBox>("ObjectName")!;
        name.Focus();
        name.SelectAll();
        var unchanged = scene.CaptureObjects();
        Click(Item(main, "Edit", "Copy"));
        for (int i = 0; i < 50 && await main.Clipboard!.TryGetTextAsync() != "Floor"; i++) await Delay(100);
        var clip = await main.Clipboard!.TryGetTextAsync();
        if (clip != "Floor")
        {
            // Separate an OS clipboard that never hands back what this process wrote from a wrong Copy target.
            await main.Clipboard!.SetTextAsync("clipboard round trip");
            var back = await main.Clipboard!.TryGetTextAsync();
            // Diagnostic opt-in for desktops whose OS clipboard returns nothing (Xvfb guest): record the two OS clipboard checks as skipped.
            noClipboard = back == null && Environment.GetEnvironmentVariable("MONOON_QA_NO_OS_CLIPBOARD") == "1";
            if (!noClipboard) throw new InvalidDataException($"text field copy: clipboard {(clip == null ? "null" : clip.Length > 60 ? clip[..60] + "…" : clip)}, " +
                $"window active {main.IsActive}, focused {main.FocusManager?.GetFocusedElement()?.GetType().Name ?? "none"}, round trip {back ?? "null"}");
        }
        Require(scene.CaptureObjects() == unchanged, "Copy in a text field changed the scene");
        Log(noClipboard ? "text field copy -> SKIPPED (OS clipboard returns nothing)" : "text field copy -> clipboard 'Floor'");

        var rig = new SceneObject { Name = "Rig", X = 600, Y = 120 };
        rig.AddComponent(new SpriteRendererComponent { SquareColor = "#5FA8D3" });
        rig.AddComponent(new ScriptComponent { Script = "TraceCopies", Values = new() { ["Copied"] = Ref(chest.Id), ["Outside"] = Ref(floor.Id) } });
        var arm = new SceneObject { Name = "Rig Arm", X = 40, Y = 12, Width = 24, Height = 12 };
        scene.Objects.Add(rig);
        scene.Objects.Add(arm);
        scene.Reparent(arm, rig, keepWorld: false);
        Call(main, "FinishObjectChange", rig, null);
        Call(main, "ShowSelection", new List<SceneObject> { rig, chest });
        main.FindControl<Control>("Hierarchy")!.Focus();
        await Choose(main, "Copy", "Copied 2 objects.");
        var copied = await main.Clipboard!.TryGetTextAsync();
        if (noClipboard) Log("copied Rig + Chest 1 -> OS clipboard SKIPPED (returns nothing; paste uses the editor's own copy)");
        else
        {
            Require(SceneDocument.IsObjectClipboard(copied), "OS clipboard does not hold the objects");
            Log($"copied Rig + Chest 1 -> OS clipboard {copied!.Length} chars, first line '{copied.Split('\n')[0]}'");
        }

        var before = scene.CaptureObjects();
        var known = scene.Objects.Select(o => o.Id).ToHashSet();
        Call(main, "Select", chest);
        await Choose(main, "Paste", "Pasted 2 objects.");
        var rigCopy = scene.Objects.Single(o => o.Name == "Rig (1)");
        var chestCopy = scene.Objects.Single(o => o.Name == "Chest 1 (1)");
        var fresh = scene.Objects.Where(o => !known.Contains(o.Id)).ToArray();
        var armCopy = fresh.Single(o => o.Parent == rigCopy);
        var lidCopy = fresh.Single(o => o.Parent == chestCopy);
        Require(fresh.Length == 4 && scene.Objects.Select(o => o.Id).Distinct().Count() == scene.Objects.Count, "pasted IDs collide");
        Require(armCopy.Name == "Rig Arm" && rigCopy.Parent == null && chestCopy.Parent == null, "pasted hierarchy");
        Require(Field(rigCopy, "Copied") == chestCopy.Id && Field(rigCopy, "Outside") == floor.Id, "same-scene references");
        Require(chestCopy.Prefab?.AssetId == chest.Prefab!.AssetId && chestCopy.Prefab.Instances.Values.ToHashSet().SetEquals([chestCopy.Id, lidCopy.Id]), "prefab link");
        Log($"same scene: {string.Join(", ", fresh.Select(o => o.Name))}; new IDs {fresh.Length}/4 unique; Rig (1).Copied -> Chest 1 (1), Outside -> Floor; Chest 1 (1) prefab {chestCopy.Prefab.AssetId}");
        var pasted = scene.CaptureObjects();
        await (Task)Call(main, "RestoreEdit", false)!;
        Require(scene.CaptureObjects() == before, "one Undo did not remove the paste");
        await (Task)Call(main, "RestoreEdit", true)!;
        Require(scene.CaptureObjects() == pasted, "Redo did not restore the same paste");
        Log("same scene: Undo removes 4 objects, Redo restores the same IDs");

        Call(main, "Select", scene.Objects.Single(o => o.Id == rigCopy.Id));
        await Choose(main, "Cut", "Cut Rig (1).");
        Require(scene.Objects.All(o => o.Id != rigCopy.Id && o.Id != armCopy.Id) && scene.Objects.Count == fresh.Length + known.Count - 2, "Cut left part of the subtree");
        Require(noClipboard || SceneDocument.IsObjectClipboard(await main.Clipboard!.TryGetTextAsync()), "Cut did not fill the clipboard");
        await (Task)Call(main, "RestoreEdit", false)!;
        Require(scene.CaptureObjects() == pasted, "one Undo did not restore the cut subtree");
        Log("cut Rig (1): parent and child removed; one Undo restores both with their IDs");
        Require(await (Task<bool>)Call(main, "Save")!, "PrefabWorkshop save cancelled");

        // Copy again: the cut replaced the clipboard with Rig (1) alone.
        Call(main, "ShowSelection", new List<SceneObject> { scene.Objects.Single(o => o.Name == "Rig"), scene.Objects.Single(o => o.Name == "Chest 1") });
        await Choose(main, "Copy", "Copied 2 objects.");
        await (Task)Call(main, "SwitchScene", "SecondRoom")!;
        var second = Scene(main);
        var secondBefore = second.CaptureObjects();
        Call(main, "Select", (SceneObject?)null);
        await Choose(main, "Paste", "Pasted 2 objects.");
        var moved = second.Objects.Single(o => o.Name == "Rig");
        var movedChest = second.Objects.Single(o => o.Name == "Chest 1");
        Require(Field(moved, "Copied") == movedChest.Id && Field(moved, "Outside") == floor.Id && second.Objects.All(o => o.Id != floor.Id), "cross-scene references");
        Require(second.Objects.Single(o => o.Parent == moved).Name == "Rig Arm" && movedChest.Prefab?.AssetId == chest.Prefab.AssetId, "cross-scene hierarchy and prefab");
        Require(second.Objects.Select(o => o.Id).Distinct().Count() == second.Objects.Count && !known.Contains(moved.Id), "cross-scene IDs");
        Log("SecondRoom: Rig, Rig Arm, Chest 1, Chest Lid; Rig.Copied -> pasted Chest 1, Rig.Outside -> PrefabWorkshop Floor (missing)");

        Call(main, "Select", moved);
        await Delay(800);
        var panel = main.GetVisualDescendants().OfType<Border>().First(b => b.Name == "InspectorPanel");
        // Object fields (13.7): a ReferenceField per reference, its value in Shown.
        Control Picker(string field) => panel.GetVisualDescendants().OfType<Control>().Single(c => c.GetType().Name == "ReferenceField" && AutomationProperties.GetName(c) == field && c.IsEffectivelyVisible);
        object? Member(object target, string name) => target.GetType().GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target);
        var outside = Picker("Outside");
        var copiedPicker = Picker("Copied");
        Require(Member(outside, "Shown")?.ToString() == Missing, "Outside shows " + Member(outside, "Shown"));
        Require(Member(copiedPicker, "Shown")?.ToString() == "Chest 1", "Copied shows " + Member(copiedPicker, "Shown"));
        TextBlock Label(Control c) => ((Panel)c.Parent!).Children.OfType<TextBlock>().First();
        var (a, b) = (In(outside, main), In(copiedPicker, main));
        var (la, lb) = (In(Label(outside), main), In(Label(copiedPicker), main));
        Require(a.X == b.X && a.Width == b.Width && la.X == lb.X && Math.Abs((a.Center.Y - la.Center.Y) - (b.Center.Y - lb.Center.Y)) < .5,
            $"missing row {a}/{la} vs reference row {b}/{lb}");
        var sprite = panel.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && (c is ComboBox or NumericUpDown || c.GetType().Name == "ReferenceField") && c != outside && c != copiedPicker)
            .Select(c => Math.Round(In(c, main).X, 1)).Distinct().ToArray();
        Log($"Inspector 1280px: Outside '{Missing}' at x={a.X:F1} w={a.Width:F1}, label x={la.X:F1}; Copied 'Chest 1' at x={b.X:F1} w={b.Width:F1}, label x={lb.X:F1}; other value columns x={string.Join(',', sprite)}");
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)panel.Bounds.Width, (int)panel.Bounds.Height)))
        {
            bitmap.Render(panel);
            bitmap.Save(Path.Combine(Output, "inspector-missing-1280.png"));
        }
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)main.Bounds.Width, (int)main.Bounds.Height)))
        {
            bitmap.Render(main);
            bitmap.Save(Path.Combine(Output, "editor-1280.png"));
        }

        var secondPasted = second.CaptureObjects();
        await (Task)Call(main, "RestoreEdit", false)!;
        Require(second.CaptureObjects() == secondBefore, "one Undo did not remove the cross-scene paste");
        await (Task)Call(main, "RestoreEdit", true)!;
        Require(second.CaptureObjects() == secondPasted, "Redo did not restore the cross-scene paste");
        Log("SecondRoom: Undo removes the paste, Redo restores it");
        Require(await (Task<bool>)Call(main, "Save")!, "SecondRoom save cancelled");
        var reopened = SceneDocument.OpenForEditing(Path.Combine(root, "Scenes", "SecondRoom.scene"));
        var saved = reopened.Objects.Single(o => o.Name == "Rig");
        Require(Field(saved, "Outside") == floor.Id && Field(saved, "Copied") == reopened.Objects.Single(o => o.Name == "Chest 1").Id &&
                reopened.Objects.Single(o => o.Name == "Chest 1").Prefab?.AssetId == chest.Prefab.AssetId, "saved SecondRoom");
        var workshop = SceneDocument.OpenForEditing(Path.Combine(root, "Scenes", "PrefabWorkshop.scene"));
        Require(workshop.Objects.Count(o => o.Name is "Rig" or "Rig (1)") == 2 && workshop.Objects.Any(o => o.Name == "Chest 1 (1)" && o.Prefab != null), "saved PrefabWorkshop");
        Log("saved files: SecondRoom keeps the missing Outside reference and the prefab link; PrefabWorkshop has Rig, Rig (1), Chest 1 (1)");

        await Play(main, trace, "copied 1 chest 1 outside 0");

        // Game builds refuse a missing reference, so point it at an object of this room as an author would.
        var pastedRig = second.Objects.Single(o => o.Name == "Rig");
        var wall = second.Objects.Single(o => o.Name == "North Wall");
        Call(main, "Select", pastedRig);
        await Delay(500);
        var repoint = Picker("Outside");
        // The picker (13.7): search, then choose the one match.
        ((Avalonia.Controls.Primitives.FlyoutBase)Member(repoint, "Picker")!).ShowAt(repoint);
        await Delay(200);
        ((TextBox)Member(repoint, "Search")!).Text = "North Wall";
        var match = ((ListBox)Member(repoint, "List")!).Items.Cast<object>().Single(i => i.ToString() == "North Wall");
        repoint.GetType().GetMethod("Choose", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(repoint, [match]);
        await Until(() => Field(second.Objects.Single(o => o.Name == "Rig"), "Outside") == wall.Id, "repointing Outside");
        Require(await (Task<bool>)Call(main, "Save")!, "SecondRoom save cancelled");
        reopened = SceneDocument.OpenForEditing(Path.Combine(root, "Scenes", "SecondRoom.scene"));
        Require(Field(reopened.Objects.Single(o => o.Name == "Rig"), "Outside") == wall.Id, "saved repointed reference");
        Log("Outside repointed to North Wall in the Inspector and saved");
        await Play(main, trace, "copied 1 chest 1 outside 1");
    }

    private static async Task Play(Window main, string trace, string expected)
    {
        File.Delete(trace);
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        await Until(() => File.Exists(trace), "Scene Play trace");
        var row = File.ReadAllText(trace).Trim();
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        Require(row == expected, "Scene Play trace " + row);
        Log("Scene Play: " + row);
    }
}
