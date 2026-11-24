using System.Text.Json;
using System.Text.Json.Nodes;
using Engine.Core;

// JsonElements writes GameData values, script references and prefab nodes without JsonSerializer (engine/tests/results/aot-size-2).
// Each element must have the raw text and kind JsonSerializer.SerializeToElement gives with GameJson, or throw the same error.
static class GameJsonWriterChecks
{
    public static void Run()
    {
        int compared = 0;
        void Same<T>(T value, Func<T, JsonElement> mine, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info)
        {
            string Text(Func<JsonElement> make) { try { var e = make(); return $"{e.ValueKind} {e.GetRawText()}"; } catch (Exception ex) { return ex.GetType().Name; } }
            string expected = Text(() => JsonSerializer.SerializeToElement(value, info)), actual = Text(() => mine(value));
            if (expected != actual) throw new Exception($"JsonElements.Of({value}): {actual} instead of {expected}.");
            compared++;
        }
        foreach (var b in new[] { true, false }) Same(b, JsonElements.Of, GameJson.Default.Boolean);
        foreach (var d in new[] { 0, -0.0, 1, -2.5, 0.1, 1e-7, 1e21, 123456789012345678, double.MaxValue, double.MinValue, double.Epsilon, double.NaN, double.PositiveInfinity })
            Same(d, JsonElements.Of, GameJson.Default.Double);
        foreach (var s in new[] { "", "plain", "quote\" back\\slash", "<tag>&amp;'", "line\nbreak\ttab", "콘솔 키", "emoji 🎮", "\u0001\u007f", null })
            Same(s!, JsonElements.Of, GameJson.Default.String);
        foreach (var g in new[] { Guid.Empty, Guid.NewGuid() }) Same(g, JsonElements.Of, GameJson.Default.Guid);
        foreach (var ids in new[] { [], [Guid.NewGuid()], new[] { Guid.NewGuid(), Guid.Empty, Guid.NewGuid() }, null })
            Same(ids!, JsonElements.Of, GameJson.Default.GuidArray);
        foreach (var json in new[] { "{}", "[]", "1", "\"s\"", "null", "{\"a\":1,\"b\":[true,null,{\"c\":\"<x>\"}],\"키\":-0.5e3}", "[[[]],{\"d\":{}}]" })
            Same(JsonNode.Parse(json)!, JsonElements.Of, GameJson.Default.JsonNode);
        var built = new JsonObject { ["n"] = 1.5, ["s"] = "t", ["g"] = Guid.Empty, ["list"] = new JsonArray(1, "two", false), ["copy"] = JsonNode.Parse("{\"x\":[1,2]}") };
        Same<JsonNode>(built, JsonElements.Of, GameJson.Default.JsonNode);
        JsonNode deep = new JsonArray();
        for (int i = 1; i < 64; i++) deep = new JsonArray(deep);
        Same(deep, JsonElements.Of, GameJson.Default.JsonNode);
        // One level deeper than the writers allow: JsonSerializer reports JsonException, the writer InvalidOperationException.
        // Parsed nodes stop at depth 64 already (JsonDocument's limit), so only hand-built nodes get here.
        deep = new JsonArray(deep);
        bool Throws(Action write) { try { write(); return false; } catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return true; } }
        if (!Throws(() => JsonSerializer.SerializeToElement(deep, GameJson.Default.JsonNode)) || !Throws(() => JsonElements.Of(deep)))
            throw new Exception("JsonElements.Of must reject a node deeper than 64 levels like JsonSerializer.");
        Console.WriteLine($"PASS JSON element writers match JsonSerializer.SerializeToElement for {compared} values");

        // ReadOverride reads asset references by hand; the result must be what JsonSerializer gave, JsonException meaning "keep".
        var r0 = Guid.NewGuid();
        string[] references = ["null", "1", "\"x\"", "[]", "{}", $"{{\"Id\":\"{r0}\"}}", $"{{\"id\":\"{r0}\"}}", $"{{\"ID\":\"{r0}\"}}", $"{{\"iD\":\"{r0}\",\"extra\":[1,{{}}]}}",
            "{\"Id\":null}", "{\"Id\":1}", "{\"Id\":\"not-a-guid\"}", $"{{\"Id\":\"{r0:N}\"}}", $"{{\"Id\":\"{{{r0}}}\"}}", $"{{\"Id\":\"{Guid.Empty}\",\"Id\":\"{r0}\"}}",
            $"{{\"Id\":\"{r0}\",\"Id\":\"bad\"}}", $"{{\"\\u0049d\":\"{r0}\"}}", $"{{\"Other\":\"{r0}\"}}", $"{{\"Id\":\"{r0.ToString().ToUpperInvariant()}\"}}", $"{{\"Id\":\" {r0}\"}}"];
        int read = 0;
        void Reference<T>(System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info, Func<T, Guid> id)
        {
            foreach (var json in references)
            {
                var element = JsonDocument.Parse(json).RootElement;
                string expected; try { expected = id(element.Deserialize(info)).ToString(); } catch (JsonException) { expected = "keep"; }
                string actual = JsonElements.ReadReference(element, out var got) ? got.ToString() : "keep";
                if (expected != actual) throw new Exception($"{typeof(T).Name} from {json}: {actual} instead of JsonSerializer's {expected}.");
                read++;
            }
        }
        Reference(GameJson.Default.SceneReference, r => r.Id);
        Reference(GameJson.Default.SpriteReference, r => r.Id);
        Reference(GameJson.Default.PrefabReference, r => r.Id);
        Reference(GameJson.Default.DataReference, r => r.Id);
        Reference(GameJson.Default.TableReference, r => r.Id);
        foreach (var t in new[] { typeof(int), typeof(float), typeof(double), typeof(bool), typeof(string), typeof(Guid) })
            if (typeof(GameJson).GetMethod(nameof(GameJson.Field), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(t).Invoke(null, null) != GameJson.Default.GetTypeInfo(t))
                throw new Exception($"GameJson.Field<{t.Name}> is not GetTypeInfo's.");
        Console.WriteLine($"PASS script reference reads match JsonSerializer for {read} values; GameJson.Field matches GetTypeInfo for 6 field types");
        PrimitiveFields();
        WorldCheckpoints();
    }

    // Values a hand-edited or older save could hold where JSON expects a number, bool, string or GUID.
    static readonly string[] Odd = ["null", "true", "false", "0", "-0", "-1", "7", "1.5", "-2.5e3", "1e39", "1e400", "2147483648", "-2147483649",
        "123456789012345678901234567890", "\"\"", "\"x\"", "\"7\"", "\"-7\"", "\"+7\"", "\" 7\"", "\"7 \"", "\"07\"", "\"7.0\"", "\"1.5\"", "\"1e3\"", "\"1E+3\"",
        "\".5\"", "\"5.\"", "\"1e39\"", "\"1e400\"", "\"2147483648\"", "\"NaN\"", "\"nan\"", "\"Infinity\"", "\"-Infinity\"", "\"infinity\"", "\"∞\"", "\"0x10\"",
        "\"1,000\"", "\"\\u0037\"", "\"\\ud800\"", "\"true\"", "\"True\"", "[]", "[1]", "{}", "{\"Id\":1}",
        $"\"{Guid.Empty}\"", "\"6f9619ff-8b86-d011-b42d-00c04fc964ff\"", "\"6F9619FF-8B86-D011-B42D-00C04FC964FF\"", "\"6f9619ff8b86d011b42d00c04fc964ff\"",
        "\"{6f9619ff-8b86-d011-b42d-00c04fc964ff}\"", "\" 6f9619ff-8b86-d011-b42d-00c04fc964ff\"", "\"6f9619ff-8b86-d011-b42d-00c04fc964f\""];

    // ReadOverride's primitive fields: JsonElements.ReadField must give JsonSerializer's value, or "keep" where it threw JsonException.
    static void PrimitiveFields()
    {
        int compared = 0;
        void Field<T>()
        {
            foreach (var json in Odd)
            {
                var element = JsonDocument.Parse(json).RootElement;
                string expected; try { expected = Show(element.Deserialize(GameJson.Field<T>())); } catch (JsonException) { expected = "keep"; }
                string actual = JsonElements.ReadField<T>(element, out var value) ? Show(value) : "keep";
                if (expected != actual) throw new Exception($"{typeof(T).Name} field from {json}: {actual} instead of JsonSerializer's {expected}.");
                compared++;
            }
        }
        static string Show(object? value) => value switch
        {
            null => "null",
            float f => BitConverter.SingleToInt32Bits(f).ToString("X8"),
            double d => BitConverter.DoubleToInt64Bits(d).ToString("X16"),
            _ => value.ToString()!
        };
        Field<int>(); Field<float>(); Field<double>(); Field<bool>(); Field<string>(); Field<Guid>();
        Console.WriteLine($"PASS script primitive field reads match JsonSerializer for {compared} values");
    }

    // KeyRoom.WorldCheckpoints saves: the hand-written codec must write the bytes JsonSerializer writes with WorldCheckpointJson
    // and read every document (written ones and each one-edit variant of them) to the same value or the same rejection.
    static void WorldCheckpoints()
    {
        Guid G(int n) => new($"6f9619ff-8b86-d011-b42d-{n:x12}");
        var plain = new SavedObject(G(1), WorldSaveFields.All, false, new(1.5f, -2, 0), true, false);
        var basis = new SavedObject(G(2), WorldSaveFields.Transform, false, new(-0f, 1e-7f, 3.1415927f, new(0.5f, -1, float.Epsilon, 2, true)), null, null, new(G(3)));
        var orphan = new SavedObject(G(4), WorldSaveFields.Transform | WorldSaveFields.Active, false, new(1e30f, 123456.79f, -0.1f), false, null, new(null));
        var gone = new SavedObject(G(5), WorldSaveFields.Existence, true, null, null, null);
        var prefab = new SavedPrefab(G(6), G(1), 10.25f, -3, new() { [G(7)] = G(8), [G(9)] = G(10) },
            [new(G(8), WorldSaveFields.Collider, false, null, null, true), new(G(10), WorldSaveFields.Collider, true, null, null, null)]);
        var checkpoints = new[]
        {
            new WorldCheckpoint(1, new() { [G(20)] = new([plain, gone], []) }),
            new WorldCheckpoint(2, new() { [G(20)] = new([plain, basis, orphan], [prefab]), [G(21)] = new([], [prefab with { Parent = null }]) }),
            new WorldCheckpoint(1, []),
        };
        string Written(Func<JsonElement> write) { try { return write().GetRawText(); } catch (Exception ex) { return ex.GetType().Name; } }
        int written = 0;
        foreach (var checkpoint in checkpoints.Append(new WorldCheckpoint(1, new() { [G(22)] = new([plain with { Pose = new(float.NaN, 0, 0) }], []) })))
        {
            string expected = Written(() => JsonSerializer.SerializeToElement(checkpoint, WorldCheckpointJson.Default.WorldCheckpoint));
            string actual = Written(() => WorldCheckpointJsonCodec.Write(checkpoint));
            if (expected != actual) throw new Exception($"World checkpoint written by hand differs.\n--- JsonSerializer\n{expected}\n--- codec\n{actual}");
            written++;
        }
        string Read(Func<WorldCheckpoint?> read)
        {
            WorldCheckpoint? value;
            try { value = read(); } catch (JsonException) { return "JsonException"; }
            return Written(() => JsonSerializer.SerializeToElement(value!, WorldCheckpointJson.Default.WorldCheckpoint));
        }
        int read = 0;
        void Compare(string json)
        {
            var element = JsonDocument.Parse(json).RootElement;
            string expected = Read(() => element.Deserialize(WorldCheckpointJson.Default.WorldCheckpoint)), actual = Read(() => WorldCheckpointJsonCodec.Read(element));
            if (expected != actual) throw new Exception($"World checkpoint read differs for {json}\n--- JsonSerializer\n{expected}\n--- codec\n{actual}");
            read++;
        }
        foreach (var json in Odd) Compare(json);
        string[] keys = ["6F9619FF-8B86-D011-B42D-000000000014", "{6f9619ff-8b86-d011-b42d-000000000014}", "6f9619ff8b86d011b42d000000000014",
            " 6f9619ff-8b86-d011-b42d-000000000014", "6f9619ff-8b86-d011-b42d-00000000001", "x"];
        foreach (var checkpoint in checkpoints)
        {
            var text = WorldCheckpointJsonCodec.Write(checkpoint).GetRawText();
            Compare(text);
            var root = JsonNode.Parse(text)!;
            foreach (var edit in Edits(root, keys)) Compare(edit);
        }
        Console.WriteLine($"PASS world checkpoint codec matches JsonSerializer: {written} written, {read} read (one-edit variants)");
    }

    // Every document one edit away from 'root': each value replaced by each odd value, each property removed, renamed
    // (upper case, or for dictionary keys another spelling) or repeated after itself with an odd value.
    static IEnumerable<string> Edits(JsonNode root, string[] keys)
    {
        var nodes = new List<JsonNode>();
        void Walk(JsonNode? node)
        {
            if (node == null) return;
            nodes.Add(node);
            if (node is JsonObject o) foreach (var (_, v) in o) Walk(v);
            else if (node is JsonArray a) foreach (var v in a) Walk(v);
        }
        Walk(root);
        string Edited(JsonNode node, Action<JsonNode> change) { var copy = root.DeepClone(); change(Find(copy, node.GetPath())); return copy.ToJsonString(); }
        static JsonNode Find(JsonNode copy, string path)
        {
            foreach (var node in Descend(copy)) if (node.GetPath() == path) return node;
            throw new Exception(path);
        }
        static IEnumerable<JsonNode> Descend(JsonNode node)
        {
            yield return node;
            IEnumerable<JsonNode?> children = node is JsonObject o ? o.Select(p => p.Value) : node is JsonArray a ? a : [];
            foreach (var child in children) if (child != null) foreach (var d in Descend(child)) yield return d;
        }
        // Odd values go in as text: some ("\ud800") cannot pass through JsonNode.
        static JsonNode Hole() => JsonValue.Create("\u0002")!;
        static string Splice(string text, string odd) => text.Replace("\"\\u0002\"", odd);
        foreach (var node in nodes)
        {
            if (node.Parent is JsonObject)
                foreach (var odd in Odd) yield return Splice(Edited(node, n => n.Parent!.AsObject()[n.GetPropertyName()] = Hole()), odd);
            else if (node.Parent is JsonArray)
                foreach (var odd in Odd) yield return Splice(Edited(node, n => n.Parent!.AsArray()[n.GetElementIndex()] = Hole()), odd);
            if (node is not JsonObject obj) continue;
            foreach (var name in obj.Select(p => p.Key).ToArray())
            {
                bool guidKey = Guid.TryParse(name, out _);
                string Rewritten(Func<JsonObject, JsonObject> change)
                {
                    var copy = root.DeepClone();
                    var target = (JsonObject)Find(copy, node.GetPath());
                    var replaced = change(target);
                    if (target.Parent is JsonObject p) p[target.GetPropertyName()] = replaced;
                    else if (target.Parent is JsonArray a) a[target.GetElementIndex()] = replaced;
                    else return replaced.ToJsonString();
                    return copy.ToJsonString();
                }
                JsonObject Without(JsonObject o, Func<string, JsonNode?, IEnumerable<(string, JsonNode?)>> map)
                {
                    var result = new JsonObject();
                    foreach (var (k, v) in o.ToArray()) foreach (var (nk, nv) in map(k, v)) result.Add(nk, nv?.DeepClone());
                    return result;
                }
                yield return Rewritten(o => Without(o, (k, v) => k == name ? [] : [(k, v)]));
                foreach (var spelling in guidKey ? keys : [name.ToUpperInvariant()])
                    if (spelling != name) yield return Rewritten(o => Without(o, (k, v) => k == name ? [(spelling, v)] : [(k, v)]));
                foreach (var odd in guidKey ? ["null", "\"x\""] : new[] { "null", "1", "\"7\"", "{}", "[]", "\"x\"" })
                {
                    // JsonObject refuses duplicate names, so the repeat is spliced in as text.
                    var text = Rewritten(o => Without(o, (k, v) => k == name ? [(k, v), ("\u0001dup", Hole())] : [(k, v)]));
                    yield return Splice(text.Replace("\"\\u0001dup\"", JsonSerializer.Serialize(name, GameJson.Default.String)), odd);
                }
            }
        }
    }
}
