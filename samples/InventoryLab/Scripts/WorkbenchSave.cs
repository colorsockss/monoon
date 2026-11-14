using System.IO;
using Engine.Core;
using MoNoon.Inventory;
using MoNoon.Quests;
using MoNoon.Dialogue;
using System.Text.Json;

internal static class WorkbenchSave
{
    private const int Version = 3;

    public static void Write(PlayerStorage storage, InventoryBag bag, QuestJournal journal, DialogueSession talk)
    {
        var data = bag.Capture();
        data.Version = Version;
        data.Values.Add("quests", journal.Capture());
        data.Set("dialogue", talk.Position);
        storage.SaveSlot(1, data);
    }

    public static bool Read(PlayerStorage storage, InventoryBag bag, QuestJournal journal, DialogueSession talk)
    {
        var data = storage.LoadSlot(1, Version, (from, old) =>
        {
            if (from == 1 && !old.Values.TryAdd("quests", GameData.ParseValue("{}")))
                throw new InvalidDataException("Invalid legacy inventory save.");
            if (from == 2) old.Set("dialogue", "");
        });
        if (data == null) return false;
        if (!data.Values.Remove("quests", out var quests))
            throw new InvalidDataException("Missing quest journal.");
        if (!data.Values.Remove("dialogue", out var position) || position.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Missing dialogue position.");
        data.Version = 1; // The remaining fields belong to the inventory package.
        var previous = bag.Capture();
        string previousNode = talk.Position;
        talk.Restore(position.GetString()!);
        try { bag.Restore(data); journal.Restore(quests); }
        catch (InvalidDataException) { bag.Restore(previous); talk.Restore(previousNode); throw; }
        return true;
    }
}
