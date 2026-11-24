using System.Text;
using System.Text.Json;
using Engine.Core;

// Player-written documents use hand-written emitters (ProjectYamlWriters.cs) so Native AOT can drop VYaml's generated
// Serialize code. They must produce the same bytes as the generated formatters (Encode<T>) for every value shape.
static class PlayerYamlChecks
{
    [Check(400, StopAfter = "player-yaml")] static void Registered(CheckRun c) => Run();
    static readonly string?[] Texts = ["plain", "", " lead", "trail ", "true", "null", "~", "123", "1.5", "a: b", "#hash", "- dash",
        "multi\nline", "tab\tin", "quote\"s", "'single'", "콘솔 키", "emoji 🎮", "C:\\Games\\x", "{flow}", "[seq]", "@at", "*ref", "&anchor", "!tag",
        "%pct", "|pipe", ">gt", "yes", "No", "0x1F", "1e3", ".inf", "-", "?", ":", null];

    public static void Run()
    {
        ProjectYamlResolverChecks.Run();
        GameJsonWriterChecks.Run();
        int compared = 0;
        void Same<T>(T value, Func<T, byte[]> writer, string what)
        {
            byte[] expected = ProjectYaml.Encode<T>(value), actual = writer(value);
            if (!expected.AsSpan().SequenceEqual(actual))
                throw new Exception($"{what}: hand-written YAML differs.\n--- VYaml\n{Encoding.UTF8.GetString(expected)}\n--- writer\n{Encoding.UTF8.GetString(actual)}");
            compared++;
        }
        JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
        var values = new[] { "1", "-2.5", "1e-7", "true", "false", "null", "\"text\"", "\"\"", "\"true\"", "\"line\\nbreak\"", "[]", "{}",
            "[1, \"a\", [true, null], {\"k\": {\"n\": []}}]", "{\"nested key\": {\"x\": 1.25, \"list\": [\"a: b\", \"#c\"]}}", "12345678901234567890" }.Select(Json).ToArray();

        var data = new GameData { Version = 3 };
        for (int i = 0; i < Texts.Length; i++) if (Texts[i] is { Length: > 0 } key) data.Values[key] = values[i % values.Length];
        foreach (var d in new[] { data, new GameData(), new GameData { Version = 7, Values = null! }, null! })
            Same(d, ProjectYaml.Encode, "GameData");

        Same(Texts.ToArray()!, ProjectYaml.Encode, "string[]");
        Same(Array.Empty<string>(), ProjectYaml.Encode, "string[] empty");
        Same((string[])null!, ProjectYaml.Encode, "string[] null");

        var entries = Texts.Select((t, i) => new AssetEntry(i == 0 ? Guid.Empty : Guid.NewGuid(), t!, i % 3 == 0 ? null! : new string('A', i))).ToArray();
        Same(entries.Append(null!).ToArray(), ProjectYaml.Encode, "AssetEntry[]");

        var table = new GameTable(1, [new("Name", "Text"), new("Cost", "Number"), new(null!, null!), null!],
            [new("sword", [values[6], values[1], values[3]]), new("empty", []), new(null!, null!), null!]);
        foreach (var t in new[] { table, new GameTable(0, null!, null!), null! }) Same(t, ProjectYaml.Encode, "GameTable");

        var inputs = new List<RecordedInput>();
        foreach (var kind in Enum.GetValues<ReplayInputKind>())
            foreach (var text in new[] { "Left", null, "true", " gap" })
                inputs.Add(new(inputs.Count, inputs.Count * 166667L, new(kind, text, text == null ? null : true, text == "true" ? false : null,
                    text == null ? null : 0.1f * inputs.Count, text == null ? null : -1e-7f, text == "Left" ? float.MaxValue : null, text)));
        inputs.Add(null!);
        foreach (var r in new[] { new InputReplay(1, Guid.NewGuid(), Guid.Empty, [0, 166667, long.MaxValue, -1], inputs.ToArray()),
                     new InputReplay(1, Guid.NewGuid(), Guid.NewGuid(), [], []), new InputReplay(0, default, default, null!, null!), null! })
            Same(r, ProjectYaml.Encode, "InputReplay");

        var turns = Enum.GetValues<GridCommand>().Select((c, i) => new RecordedTurn(c, i, -i)).Append(null!).ToArray();
        foreach (var g in new[] { new GridReplay(1, new GridRunRules("maze: v1", 2, ulong.MaxValue, 9, 7, -1, 0), turns),
                     new GridReplay(1, new GridRunRules(null!, 0, 0, 0, 0, 0, 0), []), new GridReplay(0, null!, null!), null! })
            Same(g, ProjectYaml.Encode, "GridReplay");

        var frames = new[] { new CrashFrame("Game.Update()", "Game", "mvid", 0x06000001, 12, "0x1f0"), new CrashFrame("?", null, null, null, null, null),
            new CrashFrame(null!, "a: b", "", -1, 0, "#"), null! };
        foreach (var c in new[] { new PlayerCrashReport(1, "2026-11-24T00:00:00Z", "macOS 27", "Arm64", "release", Guid.NewGuid(), data,
                         [new("System.Exception", "multi\nline: msg", frames), new("x", null!, null!), null!]),
                     new PlayerCrashReport(0, null!, null!, null!, null!, null, null!, null!), null! })
            Same(c, ProjectYaml.Encode, "PlayerCrashReport");

        Console.WriteLine($"PASS player YAML writers match the generated VYaml formatters for {compared} documents");
    }
}
