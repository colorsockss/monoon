using System;
using System.Collections.Generic;
using System.Drawing;
using Engine.Core;

[Flags] public enum SupplyTags { Food = 1, Gem = 2, Key = 4, Quest = 8 }
public enum CrateRarity { Common, Rare, Epic }

/// <summary>One restock wave of the supply crate, edited as a fold row in the Inspector.</summary>
[Serializable]
public struct RestockWave
{
    public int Count;
    public float Delay;
    public CrateRarity Rarity;
    public string Label;
}

// Supply crate tuning with one field of each Inspector field kind that 13.8 added: long, uint, Color, a [ColorUsage] string,
// a flag enum, primitive arrays, a List<T>, a dictionary and a [Serializable] struct. Attach it to an object to edit the values;
// the generated factory reads them without reflection in Scene Play, Release and Native AOT alike.
public sealed class SupplyTuning : GameBehaviour
{
    [Header("Crate")]
    public long Seed = 20261124;
    [Tooltip("Bag slots the crate may fill (bit 0 = first slot).")]
    public uint SlotMask = 0b111;
    public Color CrateTint = Color.FromArgb(214, 168, 92);
    [ColorUsage(false)] public string LabelColor = "#F4E9D8";
    public SupplyTags Tags = SupplyTags.Food | SupplyTags.Gem;

    [Header("Items")]
    public int[] StackSizes = { 5, 3, 1 };
    public float[] Weights = { 0.5f, 0.3f, 0.2f };
    public string[] Names = { "herb", "gem", "key" };
    public bool[] Unlocked = { true, true, false };
    public List<int> Restock = new() { 2, 4 };
    public Dictionary<string, int> Prices = new() { ["herb"] = 3, ["gem"] = 10, ["key"] = 25 };
    public RestockWave FirstWave = new RestockWave { Count = 3, Delay = 1.5f, Rarity = CrateRarity.Common, Label = "Morning" };

    /// <summary>The price of one item, or 0 when the crate does not sell it.</summary>
    public int PriceOf(string item) => Prices.TryGetValue(item, out var price) ? price : 0;
}
