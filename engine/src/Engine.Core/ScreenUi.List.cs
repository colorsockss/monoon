using System.Text;

namespace Engine.Core;

/// <summary>Game data shown by a bound UI list. Indices follow the game's current order; IDs stay stable across sorting.</summary>
public interface IUiListSource
{
    int Count { get; }
    string ItemId(int index);
    /// <summary>Text for a <c>{name}</c> placeholder in the row template.</summary>
    string Field(int index, string name);
}

// A bound Scroll View shows its first child as the row template. Only enough copies of that row to cover
// the viewport are created; item i always occupies pooled row i % rows, so a focused row keeps its item while scrolling.
public sealed partial class ScreenUi
{
    private sealed class ListBinding(IUiListSource source, SceneObject[] template)
    {
        internal IUiListSource Source = source;
        internal readonly SceneObject[] Template = template; // template subtree, parent first
        internal readonly List<SceneObject[]> Rows = [];
        internal int[] Items = [];
        internal string? Selected;
        internal int SelectedIndex = -1;
        internal float Stride = 1;
        internal int Needed;
    }
    private readonly Dictionary<Guid, ListBinding> lists = [];
    private readonly Dictionary<Guid, (Guid List, int Row)> rowOf = [];

    public bool BindList(Guid id, IUiListSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!byId.TryGetValue(id, out var owner) || !owner.Scroll || Horizontal(owner)) return false;
        if (lists.TryGetValue(id, out var bound)) bound.Source = source;
        else
        {
            var first = owner.Children.FirstOrDefault(c => !rowOf.ContainsKey(c.Object.Id));
            if (first == null) return false;
            var template = treeOrder.Where(n => n.Object.IsWithin(first.Object)).Select(n => n.Object).ToArray();
            lists.Add(id, new(source, template));
            hidden.Add(first.Object.Id);
        }
        return RefreshList(id);
    }

    public bool RefreshList(Guid id)
    {
        if (!lists.TryGetValue(id, out var l)) return false;
        bool focused = FocusedRow(id) >= 0;
        int previous = l.SelectedIndex, count = l.Source.Count;
        l.SelectedIndex = l.Selected == null ? -1 : IndexOf(l, l.Selected);
        if (l.SelectedIndex < 0 && l.Selected != null && count > 0) l.SelectedIndex = Math.Clamp(previous, 0, count - 1);
        l.Selected = l.SelectedIndex < 0 ? null : l.Source.ItemId(l.SelectedIndex);
        Array.Fill(l.Items, -2);
        Arrange();
        if (l.SelectedIndex >= 0) ShowItem(id, l, focused);
        return true;
    }

    public string? SelectedItem(Guid id) => lists.GetValueOrDefault(id)?.Selected;

    public bool SelectItem(Guid id, string itemId)
    {
        if (!lists.TryGetValue(id, out var l)) return false;
        int index = IndexOf(l, itemId);
        if (index < 0) return false;
        bool focused = FocusedRow(id) >= 0;
        (l.Selected, l.SelectedIndex) = (itemId, index);
        ShowItem(id, l, focused);
        return true;
    }

    /// <summary>Pooled row objects for a bound list; stays near the number of visible rows regardless of item count.</summary>
    public int ListRowCount(Guid id) { Arrange(); return lists.GetValueOrDefault(id)?.Rows.Count ?? 0; }

    private static int IndexOf(ListBinding l, string itemId)
    {
        for (int i = 0, count = l.Source.Count; i < count; i++) if (l.Source.ItemId(i) == itemId) return i;
        return -1;
    }

    private int FocusedRow(Guid list) =>
        focus is Guid f && rowOf.TryGetValue(f, out var row) && row.List == list && lists[list].Rows[row.Row][0].Id == f ? row.Row : -1;

    // Scrolls the selected item into view and, when the list had focus, moves focus to the row now showing it.
    private void ShowItem(Guid id, ListBinding l, bool focusRow)
    {
        var owner = byId[id];
        float pad = owner.Layout?.Padding ?? 0, gap = owner.Layout?.Gap ?? 0;
        float top = pad + l.SelectedIndex * l.Stride, bottom = top + l.Stride - gap;
        owner.Offset = Math.Max(0, Math.Min(Math.Max(owner.Offset, bottom - owner.Box.Height), top));
        Arrange();
        if (focusRow && l.Rows.Count > 0) SetFocus(l.Rows[l.SelectedIndex % l.Rows.Count][0].Id);
    }

    private bool FocusList(Guid id)
    {
        var l = lists[id];
        if (l.SelectedIndex < 0 && l.Source.Count > 0) (l.Selected, l.SelectedIndex) = (l.Source.ItemId(0), 0);
        if (l.SelectedIndex < 0) return false;
        ShowItem(id, l, true);
        return focus is Guid f && rowOf.ContainsKey(f);
    }

    // Up/Down inside a list walks the data, not the pooled rows; at either end normal navigation leaves the list.
    private bool ListStep(string key)
    {
        if (key is not ("Up" or "Down") || focus is not Guid f || !rowOf.TryGetValue(f, out var row) || FocusedRow(row.List) < 0) return false;
        var l = lists[row.List];
        int target = l.SelectedIndex + (key == "Down" ? 1 : -1);
        if (target < 0 || target >= l.Source.Count) return false;
        (l.Selected, l.SelectedIndex) = (l.Source.ItemId(target), target);
        ShowItem(row.List, l, true);
        return true;
    }

    // Entering a list from another widget returns to the item selected there before.
    private Node EnterList(Node next)
    {
        if (!rowOf.TryGetValue(next.Object.Id, out var row) || focus is Guid f && rowOf.TryGetValue(f, out var from) && from.List == row.List) return next;
        var l = lists[row.List];
        if (l.SelectedIndex < 0) return next;
        ShowItem(row.List, l, false);
        return byId[l.Rows[l.SelectedIndex % l.Rows.Count][0].Id];
    }

    private void RememberRow(Guid? id)
    {
        if (id is not Guid f || !rowOf.TryGetValue(f, out var row)) return;
        var l = lists[row.List];
        int item = l.Items[row.Row];
        if (item >= 0 && l.Rows[row.Row][0].Id == f) (l.Selected, l.SelectedIndex) = (l.Source.ItemId(item), item);
    }

    private Guid ClickTarget(Guid id) => rowOf.TryGetValue(id, out var row) ? row.List : id;

    private void SizeLists()
    {
        foreach (var (id, l) in lists)
        {
            var owner = byId[id];
            var row = byId[(l.Rows.Count > 0 ? l.Rows[0] : l.Template)[0].Id];
            float pad = owner.Layout?.Padding ?? 0, gap = owner.Layout?.Gap ?? 0;
            l.Stride = Math.Max(1, row.Measured) + gap;
            int count = l.Source.Count;
            owner.Content = 2 * pad + Math.Max(0, count * l.Stride - gap);
        }
    }

    // Called for every node in parent-first order once its own box is final.
    private void PlaceListRow(Node n)
    {
        if (lists.TryGetValue(n.Object.Id, out var l)) { BindRows(n, l); return; }
        if (!rowOf.TryGetValue(n.Object.Id, out var row) || n.Parent is not { } owner || owner.Object.Id != row.List) return;
        var list = lists[row.List];
        var box = n.Box;
        box.Y = owner.Box.Y + (owner.Layout?.Padding ?? 0) + Math.Max(0, list.Items[row.Row]) * list.Stride - owner.Offset;
        n.Box = box;
    }

    private void BindRows(Node owner, ListBinding l)
    {
        l.Needed = (int)Math.Ceiling(owner.Box.Height / l.Stride) + 1;
        int rows = l.Rows.Count, count = l.Source.Count;
        if (rows == 0) return;
        float pad = owner.Layout?.Padding ?? 0;
        int first = Math.Max(0, (int)((owner.Offset - pad) / l.Stride));
        for (int i = first; i < first + rows; i++)
        {
            int r = i % rows, item = i < count ? i : -1;
            if (l.Items[r] == item) continue;
            l.Items[r] = item;
            var objects = l.Rows[r];
            if (item < 0) { hidden.Add(objects[0].Id); continue; }
            hidden.Remove(objects[0].Id);
            if (focus == objects[0].Id && item != l.SelectedIndex) SetFocus(null);
            for (int k = 0; k < objects.Length; k++)
                if (byId.GetValueOrDefault(l.Template[k].Id) is { Label: not null } source && Label(source) is var text && text.Contains('{'))
                    textOverrides[objects[k].Id] = Fill(text, l.Source, item);
        }
    }

    private static string Fill(string template, IUiListSource source, int index)
    {
        var text = new StringBuilder();
        int at = 0;
        for (int open; (open = template.IndexOf('{', at)) >= 0;)
        {
            int close = template.IndexOf('}', open + 1);
            if (close < 0) break;
            text.Append(template, at, open - at).Append(source.Field(index, template[(open + 1)..close]));
            at = close + 1;
        }
        return text.Append(template, at, template.Length - at).ToString();
    }

    private bool GrowLists()
    {
        bool grew = false;
        foreach (var l in lists.Values)
            while (l.Rows.Count < l.Needed)
            {
                var copies = new SceneObject[l.Template.Length];
                for (int k = 0; k < copies.Length; k++)
                {
                    var source = l.Template[k];
                    copies[k] = source.CopyTransform(Guid.NewGuid());
                    copies[k].Components = source.Components;
                    var parent = NearestTemplateParent(l.Template, source);
                    copies[k].SetParent(parent == null ? source.Parent : copies[Array.IndexOf(l.Template, parent)]);
                }
                l.Rows.Add(copies);
                grew = true;
            }
        if (!grew) return false;
        var ordered = treeOrder.ToList();
        InsertListRows(ordered, byId);
        treeOrder = ordered.ToArray();
        nodes = ordered.OrderBy(n => n.Order).ToArray();
        OrderModeLayers();
        return true;
    }

    private static SceneObject? NearestTemplateParent(SceneObject[] template, SceneObject obj)
    {
        for (var p = obj.Parent; p != null; p = p.Parent) if (Array.IndexOf(template, p) >= 0) return p;
        return null;
    }

    // Recreates nodes for every pooled row right after its list's subtree. Safe to call on a fresh or an existing tree.
    private void InsertListRows(List<Node> ordered, Dictionary<Guid, Node> tree)
    {
        ordered.RemoveAll(n => rowOf.ContainsKey(n.Object.Id));
        foreach (var n in ordered) n.Children.RemoveAll(c => rowOf.ContainsKey(c.Object.Id));
        foreach (var id in rowOf.Keys) { tree.Remove(id); modeOwners.Remove(id); }
        rowOf.Clear();
        foreach (var (id, l) in lists.ToArray())
        {
            if (!tree.TryGetValue(id, out var owner) || !tree.ContainsKey(l.Template[0].Id)) { lists.Remove(id); continue; }
            int at = ordered.IndexOf(owner) + 1;
            while (at < ordered.Count && ordered[at].Object.IsWithin(owner.Object)) at++;
            l.Items = Enumerable.Repeat(-2, l.Rows.Count).ToArray();
            for (int r = 0; r < l.Rows.Count; r++)
            {
                var made = new Node[l.Template.Length];
                for (int k = 0; k < made.Length; k++)
                {
                    var obj = l.Rows[r][k];
                    var template = tree[l.Template[k].Id];
                    var parent = k == 0 ? owner : made[Array.IndexOf(l.Rows[r], obj.Parent)];
                    var node = made[k] = new Node(obj, parent, parent.Order, template.Image)
                        { Source = template.Object.Id, Palette = template.Palette, Slice = template.Slice, ControlValue = template.ControlValue,
                          Icons = template.Icons }; // shared with the template: no dictionary per row
                    parent.Children.Add(node);
                    tree.Add(obj.Id, node);
                    ordered.Insert(at++, node);
                    rowOf.Add(obj.Id, (id, r));
                    if (modeOwners.GetValueOrDefault(template.Object.Id) is Guid mode) modeOwners[obj.Id] = mode;
                }
            }
        }
    }
}
