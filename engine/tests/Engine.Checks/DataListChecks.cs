using System.Drawing;
using Engine.Core;

internal static class DataListChecks
{
    internal sealed class Items(int count) : IUiListSource
    {
        internal readonly List<(string Id, string Name, int Count)> Rows = Enumerable.Range(1, count)
            .Select(i => ($"item-{i:0000}", $"{(char)('A' + i * 7 % 26)}{i * 37 % 1000:000}", i * 13 % 50 + 1)).ToList();
        public int Count => Rows.Count;
        public string ItemId(int index) => Rows[index].Id;
        public string Field(int index, string name) => name switch
        {
            "id" => Rows[index].Id, "name" => Rows[index].Name, "count" => Rows[index].Count.ToString(), _ => ""
        };
    }

    internal static void Run()
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception("Data list: " + label); }
        var scene = SceneDocument.Empty(new());
        var canvas = scene.CreateUi("Canvas", null);
        var list = scene.CreateUi("Scroll View", canvas);
        list.Y = 10; list.Height = 200;
        list.GetComponent<UiLayoutComponent>()!.Gap = 2;
        list.GetComponent<UiLayoutComponent>()!.Padding = 4;
        var row = scene.CreateUi("Button", list);
        row.Height = 30; // stride 32 with the 2-unit gap
        row.GetComponent<UiTextComponent>()!.Text = "{name}  x{count}";
        var sort = scene.CreateUi("Button", canvas);
        sort.Y = 230;
        scene.Validate();
        var ui = new ScreenUi(scene, null);
        var data = new Items(1000);
        var clicks = new List<Guid>();
        void Flush() => ui.Dispatch(clicks.Add);
        void Pad(string key, int times = 1) { for (int i = 0; i < times; i++) ui.PadKey(key); }
        List<(string Text, float Y)> Drawn()
        {
            var probe = new Probe();
            ui.Draw(probe);
            return probe.Labels;
        }
        string Label(int index) => $"{data.Rows[index].Name}  x{data.Rows[index].Count}";
        RectangleF Box() => ui.Bounds(list.Id)!.Value;
        bool FocusShows(string id)
        {
            var b = ui.Bounds(ui.Focused ?? Guid.Empty);
            int index = data.Rows.FindIndex(r => r.Id == id);
            return b is { } box && box.Top >= Box().Top && box.Bottom <= Box().Bottom &&
                Drawn().Any(l => l.Text == Label(index) && l.Y >= box.Top && l.Y < box.Bottom);
        }

        Check(ui.BindList(list.Id, data) && !ui.BindList(sort.Id, data), "binds only a vertical Scroll View with a row template");
        int rows = ui.ListRowCount(list.Id);
        Check(rows is >= 8 and <= 10, $"1,000 items create {rows} pooled rows for a 200-unit viewport");
        Check(!Drawn().Any(l => l.Text.Contains('{')) && Drawn().Count(l => l.Text.Contains(" x")) <= rows,
            "template is hidden and only pooled rows are drawn");
        Console.WriteLine($"PASS data list recycling: 1,000 items use {rows} row objects");

        Check(ui.FocusWidget(list.Id) && ui.SelectedItem(list.Id) == "item-0001", "focusing the list selects its first item");
        Pad("Pad.DpadDown", 41);
        Check(ui.SelectedItem(list.Id) == "item-0042" && ui.ScrollOffset(list.Id) >= 41 * 32 + 4 + 30 - 200 && FocusShows("item-0042"),
            "pad down walks data items and auto-scrolls the focused row into view");
        Check(ui.ListRowCount(list.Id) == rows, "scrolling recycles rows instead of creating more");
        Pad("Pad.DpadUp", 2);
        Check(ui.SelectedItem(list.Id) == "item-0040" && FocusShows("item-0040"), "pad up walks back");
        Console.WriteLine("PASS data list navigation: pad focus follows data items and scrolls with recycled rows");

        var chosen = ui.SelectedItem(list.Id)!;
        data.Rows.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name) is var c && c != 0 ? c : string.CompareOrdinal(a.Id, b.Id));
        int moved = data.Rows.FindIndex(r => r.Id == chosen);
        Check(moved != 39 && ui.RefreshList(list.Id) && ui.SelectedItem(list.Id) == chosen && FocusShows(chosen),
            $"sorting keeps {chosen} selected and focused at its new index {moved}");
        Pad("Pad.South"); Flush();
        Check(clicks.SequenceEqual([list.Id]) && ui.SelectedItem(list.Id) == chosen, "use reports the list with the selected item ID");
        Pad("Pad.DpadDown", 3);
        Check(ui.SelectedItem(list.Id) == data.Rows[moved + 3].Id, "navigation continues in the new order");
        Console.WriteLine($"PASS data list sorting: {chosen} stays selected after sorting and is the use target");

        ui.Wheel(Box().X + 20, Box().Y + 20, 200); ui.Draw(new Probe()); // the next frame rebinds rows
        Check(ui.Focused == null && ui.SelectedItem(list.Id) == data.Rows[moved + 3].Id, "wheel scrolling away keeps the selected ID");
        Pad("Pad.DpadDown");
        Check(FocusShows(data.Rows[moved + 3].Id), "the next pad input returns focus to the selected item");
        var target = ui.Pick(Box().X + 20, Box().Y + 46)!;
        ui.Pointer(true, Box().X + 20, Box().Y + 46); ui.Pointer(false, Box().X + 20, Box().Y + 46);
        clicks.Clear(); Flush();
        Check(target.Id != row.Id && clicks.SequenceEqual([list.Id]) && ui.SelectedItem(list.Id) == data.Rows[(int)((ui.ScrollOffset(list.Id) + 42) / 32)].Id,
            "clicking a recycled row selects and uses its item");

        var removed = ui.SelectedItem(list.Id)!;
        int at = data.Rows.FindIndex(r => r.Id == removed);
        data.Rows.RemoveAt(at);
        Check(ui.RefreshList(list.Id) && ui.SelectedItem(list.Id) == data.Rows[at].Id, "filtering out the selected item selects its neighbour");
        Check(ui.SelectItem(list.Id, "item-1000") && ui.FocusWidget(list.Id) && FocusShows("item-1000"), "script selection reveals the item");
        int last = data.Rows.FindIndex(r => r.Id == "item-1000");
        if (last != data.Count - 1) { data.Rows.Add(data.Rows[last]); data.Rows.RemoveAt(last); ui.RefreshList(list.Id); }
        Pad("Pad.DpadDown");
        Check(ui.Focused == sort.Id, "pad down from the last item leaves the list");
        Pad("Pad.DpadUp");
        Check(ui.SelectedItem(list.Id) == "item-1000" && FocusShows("item-1000"), "re-entering the list returns to the selected item");
        ui.Rebuild(scene, null);
        Check(ui.SelectedItem(list.Id) == "item-1000" && ui.ListRowCount(list.Id) == rows, "scene rebuild keeps binding and pool");
        Console.WriteLine("PASS data list: wheel, pointer use, filtering, script selection, leaving and re-entering keep item IDs");
    }

    private sealed class Probe : ICanvas2D, IDisposable
    {
        internal readonly List<(string, float)> Labels = [];
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF r, Color color) { }
        public bool DrawSprite(string path, RectangleF r) => true;
        public void DrawText(string text, float x, float y, float size, Color color) => Labels.Add((text, y));
        public IDisposable PushClip(RectangleF area) => this;
        public void Dispose() { }
    }
}
