using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Engine.Core;

namespace MoNoon.Decks;

public sealed class CardDeck
{
    private readonly List<string> startingCards = new();
    private readonly Dictionary<string, int> copies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> labels = new(StringComparer.Ordinal);
    // The top of each pile is its last element. The hand keeps draw order.
    private readonly List<string> draw = new(), hand = new(), discard = new();

    public int Seed { get; private set; }
    public int ShuffleNumber { get; private set; }
    public int DrawCount => draw.Count;
    public int DiscardCount => discard.Count;
    public IReadOnlyList<string> Hand { get; }
    public string Label(string card) => labels[card];

    public CardDeck(GameTable catalog, int seed)
    {
        catalog.Validate();
        if (catalog.Rows.Length == 0) throw new InvalidDataException("The deck has no cards.");
        foreach (var row in catalog.Rows)
        {
            var data = catalog.Row(row.Key);
            string label = data.GetText("label");
            double count = data.GetNumber("copies");
            if (string.IsNullOrWhiteSpace(label) || count < 1 || count > Array.MaxLength - startingCards.Count || count != Math.Truncate(count))
                throw new InvalidDataException($"Card '{row.Key}' needs a label and a positive whole number of copies that fit in the deck.");
            labels.Add(row.Key, label);
            copies.Add(row.Key, (int)count);
            for (int i = 0; i < (int)count; i++) startingCards.Add(row.Key);
        }
        Hand = hand.AsReadOnly();
        Reset(seed);
    }

    public void Reset(int seed)
    {
        Seed = seed;
        ShuffleNumber = 0;
        draw.Clear();
        hand.Clear();
        discard.Clear();
        ShuffleIntoDraw(startingCards);
    }

    // Version 1: each shuffle uses its own numbered stream, independent of past draws.
    private void ShuffleIntoDraw(List<string> source)
    {
        int number = checked(ShuffleNumber + 1);
        var random = new SeededRandom(unchecked((uint)Seed)).Fork((uint)number);
        draw.AddRange(source);
        for (int i = draw.Count - 1; i > 0; i--)
        {
            int j = random.NextInt(i + 1);
            (draw[i], draw[j]) = (draw[j], draw[i]);
        }
        ShuffleNumber = number;
    }

    public bool TryDraw(out string card)
    {
        if (draw.Count == 0)
        {
            if (discard.Count == 0) { card = ""; return false; }
            ShuffleIntoDraw(discard);
            discard.Clear();
        }
        card = draw[^1];
        hand.Add(card);
        draw.RemoveAt(draw.Count - 1);
        return true;
    }

    public bool TryPlay(int handIndex, out string card)
    {
        if ((uint)handIndex >= (uint)hand.Count) { card = ""; return false; }
        card = hand[handIndex];
        discard.Add(card);
        hand.RemoveAt(handIndex);
        return true;
    }

    public GameData Capture()
    {
        var save = new GameData();
        save.Set("seed", Seed);
        save.Set("shuffle", ShuffleNumber);
        save.Values.Add("draw", ToArray(draw, topFirst: true));
        save.Values.Add("hand", ToArray(hand, topFirst: false));
        save.Values.Add("discard", ToArray(discard, topFirst: false));
        return save;
    }

    private static JsonElement ToArray(List<string> cards, bool topFirst)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            for (int i = 0; i < cards.Count; i++)
                writer.WriteStringValue(cards[topFirst ? cards.Count - 1 - i : i]);
            writer.WriteEndArray();
        }
        stream.Position = 0;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    public void Restore(GameData save)
    {
        if (save.Version != 1) throw new InvalidDataException("Unsupported deck save version.");
        int seed = ReadInteger("seed"), number = ReadInteger("shuffle");
        if (number < 1) throw new InvalidDataException("Invalid shuffle number.");
        var remaining = new Dictionary<string, int>(copies, StringComparer.Ordinal);
        var nextDraw = ReadPile("draw");
        var nextHand = ReadPile("hand");
        var nextDiscard = ReadPile("discard");
        if (nextDraw.Count + nextHand.Count + nextDiscard.Count != startingCards.Count)
            throw new InvalidDataException("Missing cards in the saved deck.");

        nextDraw.Reverse();
        draw.Clear(); draw.AddRange(nextDraw);
        hand.Clear(); hand.AddRange(nextHand);
        discard.Clear(); discard.AddRange(nextDiscard);
        Seed = seed;
        ShuffleNumber = number;

        int ReadInteger(string key)
        {
            if (!save.Values.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int n))
                throw new InvalidDataException($"Invalid deck field '{key}'.");
            return n;
        }

        List<string> ReadPile(string key)
        {
            if (!save.Values.TryGetValue(key, out var array) || array.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"Missing card list '{key}'.");
            var result = new List<string>();
            foreach (var value in array.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String || value.GetString() is not string id ||
                    !remaining.TryGetValue(id, out int left) || left == 0)
                    throw new InvalidDataException($"Unknown or excess card in '{key}'.");
                remaining[id] = left - 1;
                result.Add(id);
            }
            return result;
        }
    }
}
