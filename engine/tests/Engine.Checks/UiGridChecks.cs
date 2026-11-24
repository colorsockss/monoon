using System.Drawing;
using Engine.Core;

// Roadmap 16.13: Grid and Wrap axes of UI Layout, and draggable slots (pointer drag, activation pick/drop, keys and pad).
internal static class UiGridChecks
{
    [Check(5000, "ui-grid")] static void Registered(CheckRun c) => Run(c.Scratch);
    private static void Check(bool pass, string text)
    {
        if (!pass) throw new Exception("UI grid: " + text);
        Console.WriteLine("PASS UI grid: " + text);
    }

    internal static void Run(string scratch)
    {
        var doc = SceneDocument.Empty(new());
        var canvas = doc.CreateUi("Canvas", null);
        var bag = doc.CreateUi("Layout", canvas);
        (bag.X, bag.Y, bag.Width, bag.Height) = (40, 76, 360, 330);
        var layout = bag.GetComponent<UiLayoutComponent>()!;
        (layout.Axis, layout.Columns) = ("Grid", 4);
        var slots = new SceneObject[20];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = doc.CreateUi("Button", bag);
            (slots[i].Width, slots[i].Height) = (80, 58);
            slots[i].GetComponent<UiButtonComponent>()!.Draggable = true;
            slots[i].GetComponent<UiTextComponent>()!.Text = i < 3 ? new[] { "약초", "보석", "열쇠" }[i] : "";
        }
        var plain = doc.CreateUi("Button", canvas);
        (plain.X, plain.Y) = (500, 300);
        var ui = new ScreenUi(doc, null);
        // Inner width 360 − 2×6 = 348; four cells of (348 − 3×6) / 4 = 82.5; rows of 58 with gap 6.
        Check(ui.Bounds(slots[0].Id) == new RectangleF(46, 82, 82.5f, 58) && ui.Bounds(slots[5].Id) == new RectangleF(46 + 88.5f, 82 + 64, 82.5f, 58) &&
              ui.Bounds(slots[19].Id) == new RectangleF(46 + 3 * 88.5f, 82 + 4 * 64, 82.5f, 58), "Grid 4 places 20 slots in five rows of equal stretched cells");
        ui.SetVisible(slots[1].Id, false);
        Check(ui.Bounds(slots[2].Id)!.Value.X == 46 + 88.5f, "a hidden slot leaves no gap in the grid");
        ui.SetVisible(slots[1].Id, true);
        layout.Axis = "Wrap"; layout.Stretch = false;
        ui = new ScreenUi(doc, null);
        // Wrap fits floor((348 + 6) / (80 + 6)) = 4 cells of the widest child (80) per row.
        Check(ui.Bounds(slots[4].Id) == new RectangleF(46, 82 + 64, 80, 58) && ui.Bounds(slots[3].Id)!.Value.X == 46 + 3 * 86, "Wrap fits as many cells as the width allows");
        bag.Width = 200;
        ui = new ScreenUi(doc, null);
        Check(ui.Bounds(slots[2].Id)!.Value.Y == 82 + 64, "a narrower Wrap layout moves to two cells per row");
        (bag.Width, layout.Axis, layout.Stretch) = (360, "Grid", true);
        ui = new ScreenUi(doc, null);

        var drops = new List<(Guid, Guid)>();
        var clicks = new List<Guid>();
        void Dispatch() => ui.Dispatch(clicks.Add, null, (a, b) => drops.Add((a, b)));
        PointF Center(SceneObject o) { var b = ui.Bounds(o.Id)!.Value; return new(b.X + b.Width / 2, b.Y + b.Height / 2); }
        void Drag(SceneObject from, SceneObject to)
        {
            var a = Center(from); var b = Center(to);
            ui.Drag(a.X, a.Y); ui.Pointer(true, a.X, a.Y);
            for (int s = 1; s <= 4; s++) ui.Drag(a.X + (b.X - a.X) * s / 4, a.Y + (b.Y - a.Y) * s / 4);
            ui.Pointer(false, b.X, b.Y);
        }
        Drag(slots[0], slots[5]);
        Dispatch();
        Check(drops.SequenceEqual([(slots[0].Id, slots[5].Id)]) && clicks.Count == 0 && ui.Carried == null, "a pointer drag drops one slot on another without a click");
        drops.Clear();
        var c1 = Center(slots[1]);
        ui.Drag(c1.X, c1.Y); ui.Pointer(true, c1.X, c1.Y); ui.Drag(c1.X + 3, c1.Y + 2); ui.Pointer(false, c1.X + 3, c1.Y + 2);
        Check(ui.Carried == slots[1].Id, "a press that moves 4 px or less is a click and picks the slot up");
        var c2 = Center(slots[2]);
        ui.Drag(c2.X, c2.Y); ui.Pointer(true, c2.X, c2.Y); ui.Pointer(false, c2.X, c2.Y);
        Dispatch();
        Check(drops.SequenceEqual([(slots[1].Id, slots[2].Id)]) && ui.Carried == null, "clicking a second slot drops the picked one on it");
        drops.Clear();
        Drag(slots[3], plain);
        Dispatch();
        Check(drops.Count == 0 && clicks.Count == 0, "releasing over a plain button or empty space drops nothing");
        var cp = Center(plain);
        ui.Drag(cp.X, cp.Y); ui.Pointer(true, cp.X, cp.Y); ui.Pointer(false, cp.X, cp.Y);
        Dispatch();
        Check(clicks.SequenceEqual([plain.Id]), "a plain button still clicks");

        // Keyboard and pad: arrows move focus on the grid, activation picks and drops, Escape / pad East cancel.
        ui.ResetInput();
        void Key(string name) { ui.Key(name, true); ui.Key(name, false); }
        Check(ui.FocusWidget(slots[0].Id), "focus the first slot");
        Key("Return"); Key("Right"); Key("Down");
        Check(ui.Carried == slots[0].Id && ui.Focused == slots[5].Id, "Enter picks the focused slot; arrows move focus right and down the grid");
        Key("Return"); Dispatch();
        Check(drops.SequenceEqual([(slots[0].Id, slots[5].Id)]), "Enter on another slot drops on it");
        drops.Clear();
        Key("Space"); Key("Escape"); Dispatch();
        Check(ui.Carried == null && drops.Count == 0 && ui.Focused == slots[5].Id, "Escape cancels a pick without leaving focus");
        ui.PadKey("Pad.South"); ui.PadKey("Pad.DpadDown"); ui.PadKey("Pad.DpadLeft");
        Check(ui.Carried == slots[5].Id && ui.Focused == slots[8].Id, "pad South picks; D-pad moves down and left");
        ui.PadKey("Pad.East");
        Check(ui.Carried == null && ui.Focused == slots[8].Id, "pad East cancels the pick first");
        ui.PadKey("Pad.South"); ui.PadKey("Pad.DpadUp"); ui.PadKey("Pad.South"); Dispatch();
        Check(drops.SequenceEqual([(slots[8].Id, slots[4].Id)]), "pad South on another slot drops on it");
        drops.Clear();
        ui.PadKey("Pad.South"); ui.PadKey("Pad.South"); Dispatch();
        Check(ui.Carried == null && drops.Count == 0, "activating the picked slot again puts it back");

        // YAML: only grid layouts and draggable buttons raise the schema; defaults are not written.
        var file = Path.Combine(Directory.CreateDirectory(Path.Combine(scratch, "UiGrid")).FullName, "Bag.scene");
        doc.Save(file);
        var yaml = File.ReadAllText(file);
        var reopened = SceneDocument.Load(file);
        Check(reopened.SchemaVersion == 41 && yaml.Contains("axis: Grid") && yaml.Contains("columns: 4") && yaml.Contains("draggable: true") &&
              reopened.Objects.Single(o => o.Id == slots[7].Id).GetComponent<UiButtonComponent>()!.Draggable &&
              !reopened.Objects.Single(o => o.Id == plain.Id).GetComponent<UiButtonComponent>()!.Draggable,
            "grid layout and draggable slots round-trip as scene version 41");
        foreach (var slot in slots) slot.GetComponent<UiButtonComponent>()!.Draggable = false;
        (layout.Axis, layout.Columns) = ("Vertical", null);
        doc.Save(file);
        yaml = File.ReadAllText(file);
        Check(SceneDocument.Load(file).SchemaVersion < 40 && !yaml.Contains("draggable") && !yaml.Contains("columns"),
            "without them the scene keeps its older version and writes neither key");
        layout.Columns = 0;
        Check(Throws(() => doc.Save(file)), "Save rejects a grid of 0 columns (use no value)");
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (InvalidDataException) { return true; }
    }
}
