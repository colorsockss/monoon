using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Engine.Core;

namespace MoNoon.Quests;

public enum QuestStage { Available, Active, Ready, Claimed }

public sealed class CollectionQuest
{
    public string Title { get; }
    public string Item { get; }
    public int Goal { get; }
    public string Reward { get; }
    public int RewardCount { get; }
    public QuestStage Stage { get; private set; }
    public int Collected { get; private set; }

    internal CollectionQuest(GameData row, string key)
    {
        Title = row.GetText("title");
        Item = row.GetText("item");
        Reward = row.GetText("reward");
        Goal = ReadCount("goal");
        RewardCount = ReadCount("rewardCount");
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Item) || string.IsNullOrWhiteSpace(Reward))
            throw new InvalidDataException($"Quest '{key}' needs a title, item and reward.");

        int ReadCount(string field)
        {
            double n = row.GetNumber(field);
            if (n < 1 || n > int.MaxValue || n != Math.Truncate(n))
                throw new InvalidDataException($"Quest '{key}' needs a positive integer {field}.");
            return (int)n;
        }
    }

    public bool TryAccept()
    {
        if (Stage != QuestStage.Available) return false;
        Stage = QuestStage.Active;
        return true;
    }

    internal void Record(string item, int count)
    {
        if (Stage != QuestStage.Active || Item != item) return;
        Collected += Math.Min(count, Goal - Collected);
        if (Collected == Goal) Stage = QuestStage.Ready;
    }

    // The grant must add the whole reward or leave its destination untouched.
    public bool TryClaim(Func<string, int, bool> grant)
    {
        if (Stage != QuestStage.Ready) return false;
        ArgumentNullException.ThrowIfNull(grant);
        Stage = QuestStage.Claimed;
        bool delivered = false;
        try { delivered = grant(Reward, RewardCount); return delivered; }
        finally { if (!delivered) Stage = QuestStage.Ready; }
    }

    internal void Restore(QuestStage stage, int collected) => (Stage, Collected) = (stage, collected);
}

public sealed class QuestJournal
{
    private readonly Dictionary<string, CollectionQuest> entries = new(StringComparer.Ordinal);
    public CollectionQuest this[string id] => entries[id];

    public QuestJournal(GameTable catalog)
    {
        catalog.Validate();
        foreach (var row in catalog.Rows)
            entries.Add(row.Key, new CollectionQuest(catalog.Row(row.Key), row.Key));
    }

    // Call after a successful pickup. More than one accepted quest may track the same item.
    public void RecordCollection(string item, int count = 1)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        foreach (var quest in entries.Values) quest.Record(item, count);
    }

    // Return a mapping for a GameData save. Use explicit primitives so this works in Native AOT.
    public JsonElement Capture()
    {
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject();
            foreach (var entry in entries)
            {
                var quest = entry.Value;
                if (quest.Stage == QuestStage.Available) continue;
                writer.WriteStartObject(entry.Key);
                writer.WriteNumber("collected"u8, quest.Collected);
                writer.WriteBoolean("claimed"u8, quest.Stage == QuestStage.Claimed);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        bytes.Position = 0;
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }

    public void Restore(JsonElement saved)
    {
        if (saved.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected a quest journal mapping.");
        var states = new Dictionary<string, (QuestStage Stage, int Count)>(StringComparer.Ordinal);
        foreach (var entry in saved.EnumerateObject())
        {
            var value = entry.Value;
            if (!entries.TryGetValue(entry.Name, out var quest) || value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty("collected"u8, out var n) || n.ValueKind != JsonValueKind.Number ||
                !n.TryGetInt32(out int count) || count < 0 || count > quest.Goal ||
                !value.TryGetProperty("claimed"u8, out var flag) || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                flag.GetBoolean() && count != quest.Goal)
                throw new InvalidDataException($"Invalid saved quest '{entry.Name}'.");
            var stage = flag.GetBoolean() ? QuestStage.Claimed : count == quest.Goal ? QuestStage.Ready : QuestStage.Active;
            if (!states.TryAdd(entry.Name, (stage, count))) throw new InvalidDataException($"Duplicate saved quest '{entry.Name}'.");
        }
        foreach (var entry in entries)
        {
            states.TryGetValue(entry.Key, out var state);
            entry.Value.Restore(state.Stage, state.Count);
        }
    }
}
