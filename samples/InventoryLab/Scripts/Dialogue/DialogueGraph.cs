using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Engine.Core;

namespace MoNoon.Dialogue;

public sealed class DialogueOption
{
    public string Text { get; set; } = "";
    public string Next { get; set; } = "";
    public string Action { get; set; } = "";
}

public sealed class DialogueNode
{
    public string Id { get; set; } = "";
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
    public List<DialogueOption> Choices { get; } = new();
}

public sealed class DialogueGraph
{
    public const string Format = "monoon.dialogue.v1";
    public string Start { get; set; } = "start";
    public List<DialogueNode> Nodes { get; } = new();

    public DialogueNode Find(string id) => Nodes.Find(n => n.Id == id)
        ?? throw new InvalidDataException($"Unknown dialogue node '{id}'.");

    public void Validate()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id))
                throw new InvalidDataException($"Empty or duplicate node ID '{node.Id}'.");
            if (string.IsNullOrWhiteSpace(node.Text)) throw new InvalidDataException($"Enter a line for '{node.Id}'.");
        }
        if (!ids.Contains(Start)) throw new InvalidDataException("Choose an existing start node.");
        foreach (var node in Nodes)
            foreach (var choice in node.Choices)
            {
                if (string.IsNullOrWhiteSpace(choice.Text)) throw new InvalidDataException($"Empty choice in '{node.Id}'.");
                if (choice.Next.Length != 0 && !ids.Contains(choice.Next))
                    throw new InvalidDataException($"'{node.Id}' points to missing node '{choice.Next}'.");
            }
    }

    public static DialogueGraph Read(GameData data)
    {
        if (data.Version != 1 || data.GetText("format") != Format || !data.Values.TryGetValue("nodes", out var nodes) ||
            nodes.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid dialogue asset.");
        var graph = new DialogueGraph { Start = data.GetText("start") };
        foreach (var element in nodes.EnumerateArray())
        {
            var node = new DialogueNode { Id = Field(element,"id"), Speaker = Field(element,"speaker"), Text = Field(element,"text") };
            if (!element.TryGetProperty("choices"u8, out var choices) || choices.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"Missing choices in '{node.Id}'.");
            foreach (var choice in choices.EnumerateArray())
                node.Choices.Add(new DialogueOption { Text = Field(choice,"text"), Next = Field(choice,"next"), Action = Field(choice,"action") });
            graph.Nodes.Add(node);
        }
        graph.Validate();
        return graph;
    }

    private static string Field(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Expected dialogue text field '{name}'.");
        return field.GetString()!;
    }

    public GameData Capture()
    {
        Validate();
        var data = new GameData();
        data.Set("format", Format);
        data.Set("start", Start);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var node in Nodes)
            {
                writer.WriteStartObject();
                writer.WriteString("id"u8, node.Id);
                writer.WriteString("speaker"u8, node.Speaker);
                writer.WriteString("text"u8, node.Text);
                writer.WriteStartArray("choices"u8);
                foreach (var choice in node.Choices)
                {
                    writer.WriteStartObject();
                    writer.WriteString("text"u8, choice.Text);
                    writer.WriteString("next"u8, choice.Next);
                    writer.WriteString("action"u8, choice.Action);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        data.Values["nodes"] = doc.RootElement.Clone();
        return data;
    }
}
