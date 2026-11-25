using System;
using System.Collections.Generic;
using Engine.Core;

// Showcase copy of Catalog/CatalogScreen without the internal IDs: the herbalist's stock ledger, 1,000 generated items in a
// data-bound list. Range shows which rows are on screen and how few row objects exist; R (Sort) cycles the order and
// lights its tab. Attach to the Scroll View; its first child is the row template.
public sealed class LedgerList : GameBehaviour, IUiListSource
{
    private sealed record Item(int Number, string Name, int Count);
    private static readonly string[] Adjectives = ["Worn", "Shiny", "Small", "Sturdy", "Fragrant", "Cold", "Heavy", "Light"];
    private static readonly string[] Nouns = ["Herb", "Crystal", "Key", "Rope", "Lantern", "Map", "Flask", "Bandage", "Arrow", "Shield"];

    public Guid Range;
    public Guid[] Tabs = [];          // No. / Name / Count, in sort order
    public float RowStep = 34;        // row height + layout gap
    public int ItemCount = 1000;
    private readonly List<Item> items = [];
    private int order;
    private string shown = "";

    public int Count => items.Count;
    public string ItemId(int index) => items[index].Number.ToString();
    public string Field(int index, string name) => name switch
    {
        "name" => items[index].Name,
        "icon" => items[index].Name[(items[index].Name.IndexOf(' ') + 1)..].ToLowerInvariant(),
        "count" => items[index].Count.ToString(),
        "number" => items[index].Number.ToString("N0"),
        _ => ""
    };

    public override void OnEnable(ScriptLife life)
    {
        items.Clear();
        for (int i = 1; i <= ItemCount; i++)
            items.Add(new(i, $"{Adjectives[i * 5 % Adjectives.Length]} {Nouns[i * 3 % Nouns.Length]}", i * 37 % 99 + 1));
        life.Ui.BindList(life.ObjectId, this);
        ShowTabs(life.Ui);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var ui = tick.Ui;
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
            tick.Session.Set("sorted");
        }
        int first = (int)(ui.ScrollOffset(tick.ObjectId) / RowStep) + 1;
        int last = Math.Min(items.Count, first + (int)MathF.Round((ui.Bounds(tick.ObjectId)?.Height ?? 0) / RowStep) - 1);
        string text = $"Rows {first:N0}–{last:N0} of {items.Count:N0}     <color=#FFD84A>{ui.ListRowCount(tick.ObjectId)} row objects</color>";
        if (text != shown) ui.SetText(Range, shown = text);
    }

    private void ShowTabs(IGameUi ui)
    {
        string[] names = ["No.", "Name", "Count"];
        for (int i = 0; i < Tabs.Length && i < names.Length; i++)
            ui.SetText(Tabs[i], i == order ? $"<b><color=#FFD84A>{names[i]}</color></b>" : names[i]);
    }
}
