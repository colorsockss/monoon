using System.Numerics;
using Engine.Core;
using Engine.Editor;
using MoNoon.Inventory;
using MoNoon.Dialogue;
using MoNoon.Quests;

internal static class QuestChecks
{
    internal static void Run(string repo, string scratch)
    {
        var sample = Path.Combine(repo, "samples", "InventoryLab");
        var target = Directory.CreateDirectory(Path.Combine(scratch, "QuestImport")).FullName;
        var package = Path.Combine(repo, "packages", "Quests.monopackage");
        var installed = WorkspaceArchives.Install(package, target, WorkspaceArchives.Package);
        var expected = new[] { "Assets/Quests", "Scripts/Quests" }
            .SelectMany(dir => Directory.GetFiles(Path.Combine(sample, dir)))
            .Select(path => Path.GetRelativePath(sample, path)).Order();
        Check(installed.Select(f => Path.GetRelativePath(target, f)).Order().SequenceEqual(expected) &&
            installed.All(f => File.ReadAllBytes(f).SequenceEqual(File.ReadAllBytes(Path.Combine(sample, Path.GetRelativePath(target, f))))) &&
            WorkspaceArchives.Install(package, target, WorkspaceArchives.Package).Length == 0, "standalone package contains matching sources, assets and stable IDs");
        var dir = Directory.CreateDirectory(Path.Combine(target, "Scenes")).FullName;
        var path = Path.Combine(dir, "Camp.scene");
        var id = Guid.NewGuid();
        File.WriteAllText(path, $"schemaVersion: 14\nobjects:\n- id: {id}\n  name: Camera\n  width: 1\n  height: 1\n  components:\n  - type: camera2D\ndefaultCameraId: {id}\n");
        var scene = SceneDocument.OpenForEditing(path);
        var journalObject = scene.PlacePrefab("Assets/Quests/Journal.prefab", Vector2.Zero);
        scene.Save(path);
        using (var game = new GameSession(SceneDocument.Load(path)))
        {
            game.FixedUpdate(Vector2.Zero, 1f / 60);
            Check(game.Current.HasScript(journalObject.Id, "Quests/QuestLog") && !ProjectCatalog.Inspect(target).Problems.Any(),
                "imported journal prefab plays without the inventory package or sample scripts");
        }
        var table = GameTable.Load(Path.Combine(target, "Assets", "Quests", "Collection.table"));
        var journal = new QuestJournal(table);
        var talk = new DialogueSession(DialogueGraph.Read(GameData.Load(Path.Combine(sample, "Assets", "Dialogue", "Herbalist.data"))));
        var quest = journal["herbalist"];
        var bag = new InventoryBag(GameTable.Load(Path.Combine(sample, "Assets", "Inventory", "Items.table")), 3);
        var store = new PlayerStorage(Path.Combine(scratch, "QuestProfile"));
        journal.RecordCollection("herb", 9);
        Check(quest.TryAccept() && quest.Collected == 0 && !quest.TryAccept(), "no retroactive credit or repeated acceptance");
        journal.RecordCollection("gem");
        journal.RecordCollection("herb", 2);
        Check(quest.Stage == QuestStage.Active && quest.Collected == 2 && !quest.TryClaim(bag.TryAdd), "other items and early claims cannot advance a quest");
        journal.RecordCollection("herb", int.MaxValue);
        bag.TryAdd("key", 3);
        Check(quest.Collected == 3 && !quest.TryClaim(bag.TryAdd) && quest.Stage == QuestStage.Ready, "progress is capped and a full bag keeps the reward available");
        bag.TryTake(1);
        Check(quest.TryClaim((item, count) => !quest.TryClaim(bag.TryAdd) && bag.TryAdd(item, count)) &&
            !quest.TryClaim(bag.TryAdd) && bag[1] == new ItemStack("gem", 1), "retry with space grants exactly one reward including re-entrant and repeated requests");
        talk.Restore("paid");
        WorkbenchSave.Write(store, bag, journal, talk);
        bag.TryTake(1);
        talk.Close();
        journal.Restore(GameData.ParseValue("{}"));
        Check(WorkbenchSave.Read(store, bag, journal, talk) && quest.Stage == QuestStage.Claimed && bag[1] == new ItemStack("gem",1)
            && talk.Position == "paid" && !quest.TryClaim(bag.TryAdd), "one checkpoint restores the reward and its receipt together");
        bag.TryTake(1);
        var bad = store.LoadSlot(1, 3)!;
        bad.Set("dialogue", "offer");
        bad.Values["quests"] = GameData.ParseValue("{herbalist: {collected: 1, claimed: true}}");
        store.SaveSlot(1,bad);
        try { WorkbenchSave.Read(store, bag, journal, talk); throw new Exception("Invalid receipt accepted"); }
        catch (InvalidDataException) { Check(bag[1] == default && quest.Stage == QuestStage.Claimed && talk.Position == "paid", "invalid receipt preserves both live bag and journal"); }

        var legacyStore = new PlayerStorage(Path.Combine(scratch, "QuestLegacy"));
        legacyStore.SaveSlot(1, bag.Capture());
        Check(WorkbenchSave.Read(legacyStore, bag, journal, talk) && talk.Current == null && quest.Stage == QuestStage.Available &&
            bag[0] == new ItemStack("key", 1) && bag[1] == default && bag[2] == bag[0] &&
            legacyStore.LoadSlot(1, 1) != null, "v1 inventory migrates in memory with its original slot order and a fresh journal");
        quest.TryAccept();
        journal.RecordCollection("herb", 2);
        var v2 = bag.Capture();
        v2.Version = 2;
        v2.Values["quests"] = journal.Capture();
        legacyStore.SaveSlot(1, v2);
        journal.Restore(GameData.ParseValue("{}"));
        talk.Begin();
        Check(WorkbenchSave.Read(legacyStore, bag, journal, talk) && quest.Collected == 2 && talk.Current == null,
            "v2 checkpoint restores the existing quest and starts outside the conversation");
        WorkbenchSave.Write(legacyStore, bag, journal, talk);
        Check(legacyStore.LoadSlot(1, 3)!.GetText("dialogue", "missing") == "", "explicit save upgrades the legacy checkpoint");
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("Quest: " + message);
        Console.WriteLine("PASS quest: " + message);
    }
}
