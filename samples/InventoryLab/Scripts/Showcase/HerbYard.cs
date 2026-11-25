using System;
using System.IO;
using System.Numerics;
using Engine.Core;
using MoNoon.Dialogue;
using MoNoon.Inventory;
using MoNoon.Quests;

// Showcase: the herb yard's HUD on the sample's own packages (InventoryBag, QuestJournal, DialogueSession, WorkbenchSave).
// Return talks to the herbalist when the player stands near; 1–4 answer; F5 saves, F7 simulates a crash that cuts the save
// file in half, F9 loads (the damaged file falls back to the previous copy). Raises the HUD tag flags.
public sealed class HerbYard : GameBehaviour
{
    public ComponentReference<InventoryContainer> Container;
    public ComponentReference<QuestLog> Quests;
    public ComponentReference<DialoguePlayer> Merchant;
    public Guid Player, Herbalist;
    public float TalkRange = 85;
    public string QuestId = "herbalist";
    public Guid Dialogue, Line, Prompt, Tracker, TrackerTitle, TrackerProgress, Status;
    public Guid[] Choices = [], Slots = [];
    // Where editor Play keeps this game's saves (F7 only). A string, not a Guid: a Guid field is checked as an object reference.
    public string ProjectId = "";
    private InventoryBag bag = null!;
    private QuestJournal journal = null!;
    private CollectionQuest quest = null!;
    private DialogueSession talk = null!;
    private SessionState session = null!;
    private QuestStage shownStage;
    private int shownCollected;

    public override void OnEnable(ScriptLife life)
    {
        bag = Container.Resolve(life.Objects)!.Bag;
        journal = Quests.Resolve(life.Objects)!.Journal;
        quest = journal[QuestId];
        talk = Merchant.Resolve(life.Objects)!.Session;
        session = life.Session;
        Show(life.Ui, life.Objects, "");
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var (input, ui, objects) = (tick.Input, tick.Ui, tick.Objects);
        if (input.Pressed("Save")) { Store(tick, true); return; }
        if (input.Pressed("Load")) { Store(tick, false); return; }
        if (input.Pressed("Crash")) { Crash(ui, objects); return; }
        bool near = objects.TryGetPosition(Player, out var p) && objects.TryGetPosition(Herbalist, out var h) && Vector2.Distance(p, h) < TalkRange;
        if (talk.Current == null)
        {
            ui.SetVisible(Prompt, near);
            if (near && input.Pressed("Talk"))
            {
                talk.Begin();
                tick.Session.Set("talk");
                Show(ui, objects, "");
            }
            return;
        }
        if (input.Pressed("Talk") || input.Pressed("Skip")) ui.SkipReveal(Line);
        string[] keys = ["AddHerb", "AddGem", "AddKey", "FourthAnswer"];
        for (int i = 0; i < keys.Length; i++)
            if (input.Pressed(keys[i])) { Choose(i, ui, objects); break; }
    }

    // Called by HerbPickup; false when the bag has no room (the herb stays in the yard).
    public bool Collect(string item, IGameUi ui, IGameObjects objects)
    {
        if (!bag.TryAdd(item)) { Show(ui, objects, "The bag is full."); return false; }
        journal.RecordCollection(item);
        for (int i = 0; i < bag.Capacity && i < Slots.Length; i++)
            if (bag[i].Item == item) Pop(objects, Slots[i]);
        Show(ui, objects, "+1 " + bag.Definition(item).Label);
        return true;
    }

    private void Choose(int index, IGameUi ui, IGameObjects objects)
    {
        if (talk.Current == null || index >= talk.Current.Choices.Count) return;
        string message = "";
        talk.Choose(index, action => action switch
        {
            "accept" => quest.TryAccept() || Fail("You already accepted this request."),
            "claim" => quest.TryClaim(bag.TryAdd) || Fail(quest.Stage == QuestStage.Active ? "Gather more herbs first." : "Nothing to claim."),
            _ => Fail("Unknown action.")
        });
        Show(ui, objects, message);
        bool Fail(string why) { message = why; return false; }
    }

    private void Store(ScriptTick tick, bool save)
    {
        try
        {
            if (save) { WorkbenchSave.Write(tick.Session.Storage, bag, journal, talk); Show(tick.Ui, tick.Objects, "Saved."); return; }
            if (!WorkbenchSave.Read(tick.Session.Storage, bag, journal, talk, out bool recovered)) { Show(tick.Ui, tick.Objects, "No save yet."); return; }
            if (recovered) tick.Session.Set("recovered");
            Show(tick.Ui, tick.Objects, recovered ? "Damaged save: loaded the last good copy." : "Loaded.");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        { Show(tick.Ui, tick.Objects, "The save could not be read."); }
    }

    // shortcut: cuts the editor Play save file in half to stand in for a crash during a write; showcase only.
    private void Crash(IGameUi ui, IGameObjects objects)
    {
        var root = Environment.GetEnvironmentVariable("MONOON_USER_DATA");
        if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoNoon");
        if (!Guid.TryParse(ProjectId, out var project)) { Show(ui, objects, "No project id."); return; }
        var path = Path.Combine(root, "Play", project.ToString("N"), "slot-001.save");
        if (!File.Exists(path)) { Show(ui, objects, "Save first."); return; }
        var bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes[..(bytes.Length / 2)]);
        Show(ui, objects, "Crash mid-save: the file is cut in half.");
    }

    private static void Pop(IGameObjects objects, Guid widget) =>
        objects.Tween(widget, TweenClock.Ui).UiRect(new(-4, -4, 8, 8), .07f, TweenEase.EaseOut).UiRect(default, .12f, TweenEase.EaseIn);

    private void Show(IGameUi ui, IGameObjects objects, string message)
    {
        var node = talk.Current;
        ui.SetVisible(Dialogue, node != null);
        if (node != null)
        {
            ui.SetVisible(Prompt, false);
            ui.SetText(Line, ui.Translate(node.Text, node.Text));
            for (int i = 0; i < Choices.Length; i++)
            {
                ui.SetVisible(Choices[i], i < node.Choices.Count);
                if (i < node.Choices.Count) ui.SetText(Choices[i], $"<b>{i + 1}</b>   {node.Choices[i].Text}");
            }
        }
        // The session flag stops YardWalker while the dialogue box is open.
        if (node == null) session.Clear("talking"); else session.Set("talking");

        for (int i = 0; i < Slots.Length; i++)
        {
            var stack = i < bag.Capacity ? bag[i] : default;
            ui.SetText(Slots[i], stack.Count == 0 ? "" : $"<size=26><icon={stack.Item}></size><b>{stack.Count}</b>");
        }

        ui.SetVisible(Tracker, quest.Stage != QuestStage.Available);
        ui.SetText(TrackerTitle, quest.Stage == QuestStage.Claimed ? $"{quest.Title}   <color=#9BE27A><b>Complete</b></color>" : quest.Title);
        ui.SetText(TrackerProgress, quest.Stage switch
        {
            QuestStage.Active => $"<icon={quest.Item}>  {bag.Definition(quest.Item).Label}   {quest.Collected} / {quest.Goal}",
            QuestStage.Ready => $"<icon={quest.Item}>  {quest.Goal} / {quest.Goal}   <color=#FFD84A>Back to the herbalist</color>",
            QuestStage.Claimed => $"<icon={quest.Reward}>  Reward received",
            _ => ""
        });
        if (quest.Stage != shownStage || quest.Collected != shownCollected)
        {
            if (quest.Stage != QuestStage.Available) Pop(objects, Tracker);
            if (quest.Stage == QuestStage.Active) session.Set("quest");
            if (quest.Collected > 0) session.Set("bag");
            (shownStage, shownCollected) = (quest.Stage, quest.Collected);
        }
        if (message.Length > 0 && message.StartsWith("Saved")) session.Set("save");
        ui.SetText(Status, message);
        ui.SetVisible(Status, message.Length > 0);
    }
}
