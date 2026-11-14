using System;
using System.Collections.Generic;
using System.IO;
using Engine.Core;

namespace MoNoon.Inventory;

public readonly record struct ItemStack(string Item, int Count);
public readonly record struct InventoryItem(string Label, int StackLimit);

public sealed class InventoryBag
{
    private readonly Dictionary<string, InventoryItem> items = new(StringComparer.Ordinal);
    private readonly ItemStack[] slots;

    public InventoryBag(GameTable catalog, int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        catalog.Validate();
        foreach (var row in catalog.Rows)
        {
            var data = catalog.Row(row.Key);
            string label = data.GetText("label");
            double limit = data.GetNumber("stackLimit");
            if (string.IsNullOrWhiteSpace(label) || limit < 1 || limit > int.MaxValue || limit != Math.Truncate(limit))
                throw new InvalidDataException($"Inventory item '{row.Key}' needs a label and a positive integer stackLimit.");
            items.Add(row.Key, new(label, (int)limit));
        }
        slots = new ItemStack[capacity];
    }

    public int Capacity => slots.Length;
    public ItemStack this[int index] => slots[index];
    public InventoryItem Definition(string item) => items[item];

    // A pickup either fits in full or stays in the game world.
    public bool TryAdd(string item, int count = 1)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        int limit = Definition(item).StackLimit;
        long room = 0;
        foreach (var slot in slots)
            if (slot.Count == 0 || slot.Item == item) room += limit - slot.Count;
        if (room < count) return false;

        // Fill existing stacks first, even when a lower-index slot is empty.
        for (int pass = 0; pass < 2 && count > 0; pass++)
            for (int i = 0; i < slots.Length && count > 0; i++)
            {
                var slot = slots[i];
                if (pass == 0 ? slot.Item != item : slot.Count != 0) continue;
                int added = Math.Min(limit - slot.Count, count);
                slots[i] = new(item, slot.Count + added);
                count -= added;
            }
        return true;
    }

    public bool TryTake(int index, int count = 1)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        var slot = slots[index];
        if (slot.Count < count) return false;
        slots[index] = slot.Count == count ? default : new(slot.Item, slot.Count - count);
        return true;
    }

    public GameData Capture()
    {
        var data = new GameData();
        data.Set("capacity", Capacity);
        for (int i = 0; i < Capacity; i++)
        {
            if (slots[i].Count == 0) continue;
            data.Set($"item{i}", slots[i].Item);
            data.Set($"count{i}", slots[i].Count);
        }
        return data;
    }

    public void Restore(GameData data)
    {
        if (data.Version != 1 || data.GetNumber("capacity") != Capacity)
            throw new InvalidDataException("Inventory save version or capacity does not match.");
        var restored = new ItemStack[Capacity];
        int fields = 1;
        for (int i = 0; i < Capacity; i++)
        {
            if (!data.Values.ContainsKey($"item{i}")) continue;
            string item = data.GetText($"item{i}");
            double count = data.GetNumber($"count{i}");
            if (!items.TryGetValue(item, out var definition) || count < 1 || count > definition.StackLimit || count != Math.Truncate(count))
                throw new InvalidDataException($"Invalid inventory save at slot {i + 1}.");
            restored[i] = new(item, (int)count);
            fields += 2;
        }
        if (data.Values.Count != fields) throw new InvalidDataException("Unexpected inventory save fields.");
        restored.CopyTo(slots, 0);
    }
}
