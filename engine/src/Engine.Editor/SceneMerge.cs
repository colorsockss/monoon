using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Engine.Core;

namespace Engine.Editor;

internal enum ConflictChoice { Local, Disk }
internal sealed record MergedScene(string Snapshot, List<string> Conflicts);

internal static class SceneMerge
{
    // Normalize supported older scenes with the same reader used for editing before comparing fields.
    internal static string Normalize(string yaml)
    {
        var doc = new SceneDocument();
        doc.RestoreObjects(yaml);
        return doc.CaptureObjects();
    }

    public static MergedScene Combine(string baseline, string local, string disk, ConflictChoice choice)
    {
        JsonNode Read(string yaml) => JsonNode.Parse(ProjectYaml.Decode<JsonElement>(Encoding.UTF8.GetBytes(yaml)).GetRawText())!;
        var conflicts = new List<string>();

        JsonNode? Resolve(JsonNode? before, JsonNode? mine, JsonNode? theirs, string path, string field)
        {
            if (JsonNode.DeepEquals(mine, theirs)) return mine?.DeepClone();
            if (JsonNode.DeepEquals(before, mine)) return theirs?.DeepClone();
            if (JsonNode.DeepEquals(before, theirs)) return mine?.DeepClone();

            if (before is JsonObject b && mine is JsonObject m && theirs is JsonObject t)
            {
                var result = new JsonObject();
                foreach (var name in t.Select(p => p.Key).Union(m.Select(p => p.Key)).Union(b.Select(p => p.Key)))
                {
                    // A YAML null is a value. Removing a key is a distinct edit.
                    var value = MergeEntry(b.ContainsKey(name), b[name], m.ContainsKey(name), m[name],
                        t.ContainsKey(name), t[name], path + "/" + name, name);
                    if (value.Exists) result[name] = value.Node;
                }
                return result;
            }
            if ((field == "objects" || field == "components") && before is JsonArray ba && mine is JsonArray ma && theirs is JsonArray ta)
            {
                string Key(JsonNode? n) => field == "objects" ? n!["id"]!.ToString() :
                    n!["type"]!.ToString() + (n["script"] == null ? "" : "/" + n["script"]);
                var old = ba.ToDictionary(Key);
                var left = ma.ToDictionary(Key);
                var right = ta.ToDictionary(Key);
                var common = old.Keys.Where(k => left.ContainsKey(k) && right.ContainsKey(k)).ToHashSet();
                var originalOrder = old.Keys.Where(common.Contains);
                var localOrder = left.Keys.Where(common.Contains);
                var diskOrder = right.Keys.Where(common.Contains);
                bool localMoved = !localOrder.SequenceEqual(originalOrder);
                bool diskMoved = !diskOrder.SequenceEqual(originalOrder);
                bool diverged = localMoved && diskMoved && !localOrder.SequenceEqual(diskOrder);
                if (diverged) conflicts.Add(path + "/order");
                bool useLocal = localMoved && (!diverged || choice == ConflictChoice.Local);
                var first = (useLocal ? left : right).Keys.ToList();
                var second = (useLocal ? right : left).Keys;
                // Insert other-side additions at their original neighbour, preserving sibling order.
                string? previous = null;
                foreach (var id in second)
                {
                    if (!first.Contains(id)) first.Insert(previous == null ? 0 : first.IndexOf(previous) + 1, id);
                    previous = id;
                }
                var result = new JsonArray();
                foreach (var id in first)
                {
                    var label = field == "objects"
                        ? (left.GetValueOrDefault(id) ?? right.GetValueOrDefault(id))?["name"]?.ToString() + " [" + id + "]"
                        : id;
                    var v = MergeEntry(old.TryGetValue(id, out var o), o, left.TryGetValue(id, out var l), l,
                        right.TryGetValue(id, out var r), r, path + "/" + label, "");
                    if (v.Exists) result.Add(v.Node);
                }
                return result;
            }
            conflicts.Add(path);
            return (choice == ConflictChoice.Local ? mine : theirs)?.DeepClone();
        }

        (bool Exists, JsonNode? Node) MergeEntry(bool b, JsonNode? old, bool l, JsonNode? mine,
            bool r, JsonNode? theirs, string path, string field)
        {
            if (b && l && r) return (true, Resolve(old, mine, theirs, path, field));
            if (l == r && JsonNode.DeepEquals(mine, theirs)) return (l, mine?.DeepClone());
            if (b == l && JsonNode.DeepEquals(old, mine)) return (r, theirs?.DeepClone());
            if (b == r && JsonNode.DeepEquals(old, theirs)) return (l, mine?.DeepClone());
            conflicts.Add(path);
            return choice == ConflictChoice.Local ? (l, mine?.DeepClone()) : (r, theirs?.DeepClone());
        }

        var merged = Resolve(Read(baseline), Read(local), Read(disk), "Scene", "")!;
        var element = JsonSerializer.SerializeToElement(merged, GameJson.Default.JsonNode);
        return new(Encoding.UTF8.GetString(ProjectYaml.Encode(element)), conflicts);
    }
}
