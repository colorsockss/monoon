using System;
using System.Collections.Generic;
using Engine.Core;

// Showcase: the herbalist's stock ledger, 1,000 generated items in a data-bound list. Range shows which rows are on screen
// and how few row objects exist; R / pad Y (Sort) cycles the order and shows it: the active tab gets its gold bar (Marks)
// and ▲ / ▼, its column turns gold in every row and Sort names the order. The list takes the keyboard and pad
// focus on entry; Return / pad A on a row uses one of that item and records the used item numbers, the order and the row
// object count in save slot 2. B / pad Back returns to the yard. Raises the "scrolled", "sorted" and "used" HUD tag flags. Attach to the Scroll View; its first child is the row template.
public sealed class LedgerList : GameBehaviour, IUiListSource
{
    private sealed record Item(int Number, string Name, int Count);
    private static readonly string[] Adjectives = ["Worn", "Shiny", "Small", "Sturdy", "Fragrant", "Cold", "Heavy", "Light"];
    private static readonly string[] Nouns = ["Herb", "Crystal", "Key", "Rope", "Lantern", "Map", "Flask", "Bandage", "Arrow", "Shield"];

    public Guid Range;
    public Guid Sort;                 // "Sorted by …" caption
    public Guid[] Tabs = [];          // No. / Name / Count, in sort order
    public Guid[] Marks = [];         // the bar behind each tab, shown for the active one
    public float RowStep = 34;        // row height + layout gap
    public int ItemCount = 1000;
    public SceneReference Yard;
    private static readonly string[] Orders = ["No.", "Name", "Count"];
    private static readonly string[] Arrows = ["▲", "▲", "▼"];
    private static readonly string[] Captions = ["Sorted by No., lowest first", "Sorted by Name, A → Z", "Sorted by Count, most first"];
    private readonly List<Item> items = [];
    private readonly List<string> used = [];
    private Guid list;
    private int order;
    private string shown = "";

    public int Count => items.Count;
    public string ItemId(int index) => items[index].Number.ToString();
    public string Field(int index, string name) => name switch
    {
        "name" => Lit(1, items[index].Name),
        "icon" => items[index].Name[(items[index].Name.IndexOf(' ') + 1)..].ToLowerInvariant(),
        "count" => Lit(2, $"× {items[index].Count}"),
        "number" => Lit(0, items[index].Number.ToString("N0")),
        _ => ""
    };

    // The column the list is sorted by is gold in every row.
    private string Lit(int column, string text) => column == order ? $"<color=#FFD84A>{text}</color>" : text;

    public override void OnEnable(ScriptLife life)
    {
        items.Clear();
        for (int i = 1; i <= ItemCount; i++)
            items.Add(new(i, $"{Adjectives[i * 5 % Adjectives.Length]} {Nouns[i * 3 % Nouns.Length]}", i * 37 % 99 + 1));
        list = life.ObjectId;
        life.Ui.BindList(list, this);
        life.Ui.FocusWidget(list);
        ShowTabs(life.Ui);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var ui = tick.Ui;
        if (tick.Input.Pressed("Back") && Yard.Id != Guid.Empty) { tick.Session.LoadScene(Yard.Id); return; }
        if (tick.Input.Pressed("Sort"))
        {
            order = (order + 1) % 3;
            Comparison<Item> compare = order switch
            {
                1 => (a, b) => string.CompareOrdinal(a.Name, b.Name),
                2 => (a, b) => b.Count.CompareTo(a.Count),
                _ => (a, b) => 0
            };
            // Equal keys fall back to the number so every order is deterministic.
            items.Sort((a, b) => compare(a, b) is var c && c != 0 ? c : a.Number.CompareTo(b.Number));
            ui.RefreshList(tick.ObjectId);
            ShowTabs(ui);
            tick.Objects.Tween(Sort, TweenClock.Ui).UiRect(new(0, -4, 0, 0), .07f, TweenEase.EaseOut).UiRect(default, .14f, TweenEase.EaseIn);
            tick.Session.Set("sorted");
        }
        int first = (int)(ui.ScrollOffset(tick.ObjectId) / RowStep) + 1;
        if (first > 1) tick.Session.Set("scrolled");
        int last = Math.Min(items.Count, first + (int)MathF.Round((ui.Bounds(tick.ObjectId)?.Height ?? 0) / RowStep) - 1);
        string text = $"Rows {first:N0}–{last:N0} of {items.Count:N0}     <color=#FFD84A>{ui.ListRowCount(tick.ObjectId)} row objects</color>";
        if (text != shown) ui.SetText(Range, shown = text);
    }

    // Row activation reports the list object; the selected item is the one used.
    public override void OnUiClick(UiClick click)
    {
        if (click.ObjectId != list || click.Ui.SelectedItem(list) is not { } id) return;
        int index = items.FindIndex(item => item.Number.ToString() == id);
        if (index < 0 || items[index].Count == 0) return;
        items[index] = items[index] with { Count = items[index].Count - 1 };
        used.Add(id);
        var data = new GameData();
        data.Set("used", string.Join(",", used));
        data.Set("order", Orders[order]);
        data.Set("rows", click.Ui.ListRowCount(list));
        data.Set("items", items.Count);
        click.Session.Storage.SaveSlot(2, data);
        click.Ui.RefreshList(list);
        click.Session.Set("used");
    }

    private void ShowTabs(IGameUi ui)
    {
        for (int i = 0; i < Tabs.Length && i < Orders.Length; i++)
            ui.SetText(Tabs[i], i == order ? $"<b><color=#FFD84A>{Orders[i]} {Arrows[i]}</color></b>" : Orders[i]);
        for (int i = 0; i < Marks.Length; i++) ui.SetVisible(Marks[i], i == order);
        ui.SetText(Sort, Captions[order]);
    }
}
