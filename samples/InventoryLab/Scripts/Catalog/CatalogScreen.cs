using System;
using System.Collections.Generic;
using Engine.Core;

// Shows 1,000 generated items in a data-bound list. Attach to the Scroll View; its first child is the row template.
public sealed class CatalogScreen : GameBehaviour, IUiListSource
{
    private sealed record Item(string Id, string Name, int Count);
    private static readonly string[] Adjectives = ["낡은", "빛나는", "작은", "단단한", "향긋한", "차가운", "무거운", "가벼운"];
    private static readonly string[] Nouns = ["약초", "수정", "열쇠", "밧줄", "등불", "지도", "물병", "붕대", "화살", "방패"];
    private static readonly string[] Orders = ["번호순", "이름순", "수량순"];

    public Guid Status;
    public SceneReference Inventory;
    public int ItemCount = 1000;
    private readonly List<Item> items = [];
    private readonly List<string> used = [];
    private Guid list;
    private int order;

    public int Count => items.Count;
    public string ItemId(int index) => items[index].Id;
    public string Field(int index, string name) => name switch
    {
        "id" => items[index].Id,
        "name" => items[index].Name,
        "count" => items[index].Count.ToString(),
        _ => ""
    };

    public override void OnEnable(ScriptLife life)
    {
        list = life.ObjectId;
        items.Clear();
        for (int i = 1; i <= ItemCount; i++)
            items.Add(new($"item-{i:0000}", $"{Adjectives[i * 5 % Adjectives.Length]} {Nouns[i * 3 % Nouns.Length]}", i * 37 % 99 + 1));
        life.Ui.BindList(list, this);
        life.Ui.FocusWidget(list);
        Show(life.Ui, "방향키나 패드로 고르고 Enter·A 버튼으로 사용합니다.");
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("Sort")) Sort((order + 1) % Orders.Length, tick.Ui);
        if (tick.Input.Pressed("Back")) tick.Session.LoadScene(Inventory.Id);
    }

    // Row activation reports the list object; the selected item ID is the use target.
    public override void OnUiClick(UiClick click)
    {
        if (click.ObjectId != list || click.Ui.SelectedItem(list) is not { } id) return;
        int index = items.FindIndex(item => item.Id == id);
        var item = items[index];
        if (item.Count == 0) { Show(click.Ui, $"{item.Name}  ({id}) 수량이 없습니다."); return; }
        items[index] = item with { Count = item.Count - 1 };
        used.Add(id);
        var data = new GameData();
        data.Set("used", string.Join(",", used));
        data.Set("order", Orders[order]);
        data.Set("rows", click.Ui.ListRowCount(list));
        data.Set("items", items.Count);
        click.Session.Storage.SaveSlot(2, data);
        click.Ui.RefreshList(list);
        Show(click.Ui, $"{item.Name} 1개를 사용했습니다.  ({id})");
    }

    public void Sort(int mode, IGameUi ui)
    {
        order = mode;
        Comparison<Item> compare = order switch
        {
            1 => (a, b) => string.CompareOrdinal(a.Name, b.Name),
            2 => (a, b) => b.Count.CompareTo(a.Count),
            _ => (a, b) => 0
        };
        // Equal keys fall back to the ID so every order is deterministic.
        items.Sort((a, b) => compare(a, b) is var c && c != 0 ? c : string.CompareOrdinal(a.Id, b.Id));
        ui.RefreshList(list);
        Show(ui, $"{Orders[order]}으로 정렬했습니다.");
    }

    private void Show(IGameUi ui, string message) =>
        ui.SetText(Status, $"{message}\n\n정렬   {Orders[order]}\n선택   {ui.SelectedItem(list) ?? "없음"}\n행 객체   {ui.ListRowCount(list)}개 / 아이템 {items.Count}개");
}
