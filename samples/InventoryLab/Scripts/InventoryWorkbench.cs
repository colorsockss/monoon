using System;
using System.IO;
using Engine.Core;
using MoNoon.Inventory;
using MoNoon.Quests;
using MoNoon.Dialogue;

public sealed class InventoryWorkbench : GameBehaviour
{
    public Guid[] Choices = [];
    public ComponentReference<InventoryContainer> Container;
    public ComponentReference<QuestLog> Quests;
    public ComponentReference<DialoguePlayer> Merchant;
    public Guid InventoryPanel, DialoguePanel, Speaker, Line, MoreChoices;
    public string QuestId = "herbalist";
    public Guid QuestTitle, QuestProgress, QuestButton;
    public Guid FirstSlot, SecondSlot, ThirdSlot, Selection, Status;
    public SceneReference CatalogScene;
    private Guid[] labels;
    private InventoryBag bag;
    private QuestJournal journal;
    private CollectionQuest quest;
    private DialogueSession talk;
    private int page;
    private int selected;

    public override void OnEnable(ScriptLife life)
    {
        bag = Container.Resolve(life.Objects).Bag;
        journal = Quests.Resolve(life.Objects).Journal;
        quest = journal[QuestId];
        talk = Merchant.Resolve(life.Objects).Session;
        labels = new[] { FirstSlot, SecondSlot, ThirdSlot };
        if (bag.Capacity != labels.Length) throw new InvalidDataException("The workbench displays three inventory slots.");
        Show(life.Ui, "보급품을 골라 가방에 담으세요.");
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("Catalog") && CatalogScene.Id != Guid.Empty) { tick.Session.LoadScene(CatalogScene.Id); return; }
        if (tick.Input.Pressed("Save")) { Store(tick.Session.Storage, tick.Ui, true); return; }
        if (tick.Input.Pressed("Load")) { Store(tick.Session.Storage, tick.Ui, false); return; }
        if (tick.Input.Pressed("Talk")) { ToggleTalk(tick.Ui); return; }
        if (talk.Current != null)
        {
            if (tick.Input.Pressed("Skip")) tick.Ui.SkipReveal(Line);
            if (tick.Input.Pressed("AddHerb")) Choose(0, tick.Ui);
            else if (tick.Input.Pressed("AddGem")) Choose(1, tick.Ui);
            else if (tick.Input.Pressed("AddKey")) Choose(2, tick.Ui);
            else if (tick.Input.Pressed("FourthAnswer")) Choose(3, tick.Ui);
            else if (tick.Input.Pressed("Next")) NextChoices(tick.Ui);
            return;
        }
        if (tick.Input.Pressed("AddHerb")) Add("herb", tick.Ui);
        if (tick.Input.Pressed("AddGem")) Add("gem", tick.Ui);
        if (tick.Input.Pressed("AddKey")) Add("key", tick.Ui);
        if (tick.Input.Pressed("Previous")) Select((selected + bag.Capacity - 1) % bag.Capacity, tick.Ui);
        if (tick.Input.Pressed("Next")) Select((selected + 1) % bag.Capacity, tick.Ui);
        if (tick.Input.Pressed("Use")) Use(tick.Ui);
        if (tick.Input.Pressed("Quest")) AdvanceQuest(tick.Ui);
    }

    public void ToggleTalk(IGameUi ui)
    {
        if (talk.Current != null) talk.Close();
        else talk.Begin();
        page = 0;
        Show(ui, talk.Current == null ? "대화를 마쳤습니다." : "답변을 선택하세요.");
    }

    public void NextChoices(IGameUi ui)
    {
        if (talk.Current == null || Choices.Length == 0 || talk.Current.Choices.Count <= Choices.Length) return;
        page = (page + 1) % ((talk.Current.Choices.Count + Choices.Length - 1) / Choices.Length);
        Show(ui,"답변을 선택하세요.");
    }

    public void ChooseButton(Guid button, IGameUi ui) => Choose(Array.IndexOf(Choices, button), ui);

    private void Choose(int slot, IGameUi ui)
    {
        if (talk.Current == null || (uint)slot >= (uint)Choices.Length) return;
        int index = page * Choices.Length + slot;
        if (index >= talk.Current.Choices.Count) return;
        string message = "";
        bool ok = talk.Choose(index, action =>
        {
            switch (action)
            {
                case "accept":
                    if (quest.TryAccept()) return true;
                    message = "이미 받은 의뢰입니다."; return false;
                case "claim":
                    if (quest.TryClaim(bag.TryAdd)) return true;
                    message = quest.Stage switch {
                        QuestStage.Ready => "보상을 담을 공간이 부족합니다.",
                        QuestStage.Claimed => "이미 받은 보상입니다.",
                        QuestStage.Active => $"약초를 더 모아 주세요.  {quest.Collected} / {quest.Goal}",
                        _ => "먼저 의뢰를 받아 주세요."
                    };
                    return false;
                default: message = $"등록되지 않은 행동입니다.  {action}"; return false;
            }
        });
        if (ok) page = 0;
        Show(ui, ok ? (talk.Current == null ? "대화를 마쳤습니다." : "답변을 선택하세요.") : message);
    }

    public void Add(string item, IGameUi ui)
    {
        if (!bag.TryAdd(item)) { Show(ui, "가방이 가득 찼습니다."); return; }
        journal.RecordCollection(item);
        Show(ui, bag.Definition(item).Label + " 1개를 담았습니다.");
    }

    public void AdvanceQuest(IGameUi ui)
    {
        if (quest.TryAccept()) Show(ui, "의뢰를 받았습니다. 지금부터 수집한 수량을 셉니다.");
        else if (quest.Stage == QuestStage.Ready)
        {
            Show(ui, quest.TryClaim(bag.TryAdd) ? "보상을 가방에 담았습니다." : "보상을 담을 공간이 부족합니다.");
        }
        else Show(ui, quest.Stage == QuestStage.Claimed ? "이미 보상을 받았습니다." : "목표 수량을 모아 주세요.");
    }

    public void Select(int slot, IGameUi ui)
    {
        selected = slot;
        Show(ui, $"{slot + 1}번 칸을 선택했습니다.");
    }

    public void Use(IGameUi ui)
    {
        var stack = bag[selected];
        Show(ui, bag.TryTake(selected) ? bag.Definition(stack.Item).Label + " 1개를 사용했습니다." : "사용할 아이템이 없습니다.");
    }

    public void Store(PlayerStorage storage, IGameUi ui, bool save)
    {
        try
        {
            if (save)
            {
                WorkbenchSave.Write(storage, bag, journal, talk);
                Show(ui, "진행 상황을 저장했습니다.");
                return;
            }
            if (!WorkbenchSave.Read(storage, bag, journal, talk, out bool recovered)) { Show(ui, "저장 기록이 없습니다."); return; }
            page = 0;
            Show(ui, recovered ? "직전 저장을 복구했습니다." : "저장한 진행 상황을 불러왔습니다.");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        { Show(ui, "가방 저장 파일을 확인하세요."); }
    }

    private void Show(IGameUi ui, string message)
    {
        var node = talk.Current;
        ui.SetVisible(InventoryPanel, node == null);
        ui.SetVisible(DialoguePanel, node != null);
        if (node != null)
        {
            ui.SetText(Speaker, node.Speaker);
            // A line may name a translation table key; other lines are shown as written.
            ui.SetText(Line, ui.Translate(node.Text, node.Text));
            for (int i = 0; i < Choices.Length; i++)
            {
                int index = page * Choices.Length + i;
                ui.SetVisible(Choices[i], index < node.Choices.Count);
                if (index < node.Choices.Count) ui.SetText(Choices[i], $"[{i+1}]   {node.Choices[index].Text}");
            }
            ui.SetVisible(MoreChoices, Choices.Length > 0 && node.Choices.Count > Choices.Length);
        }
        for (int i = 0; i < labels.Length; i++)
        {
            var stack = bag[i];
            string text = stack.Count == 0 ? "빈 칸" : $"{bag.Definition(stack.Item).Label}   {stack.Count} / {bag.Definition(stack.Item).StackLimit}";
            ui.SetText(labels[i], $"{(i == selected ? "●" : "○")}   {i + 1}     {text}");
        }
        ui.SetText(Selection, $"{selected + 1}번 칸 사용   [U]");
        ui.SetText(QuestTitle, quest.Title);
        string objective = $"{bag.Definition(quest.Item).Label}   {quest.Collected} / {quest.Goal}";
        string reward = $"보상   {bag.Definition(quest.Reward).Label} {quest.RewardCount}개";
        ui.SetText(QuestProgress, $"{objective}     ·     {reward}");
        ui.SetText(QuestButton, quest.Stage switch {
            QuestStage.Available => "의뢰 받기   [J]",
            QuestStage.Active => "수집 중",
            QuestStage.Ready => "보상 받기   [J]",
            _ => "보상 수령 완료"
        });
        ui.SetInteractable(QuestButton, quest.Stage is QuestStage.Available or QuestStage.Ready);
        ui.SetText(Status, message);
    }
}
