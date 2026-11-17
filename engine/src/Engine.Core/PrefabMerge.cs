using System.Text.Json;
using System.Text.Json.Nodes;

namespace Engine.Core;

// Use the scene's YAML representation so component discriminators, script fields and
// import settings have exactly the same meaning in a baseline as in the asset itself.
internal static class PrefabMerge
{
    internal static JsonElement Snapshot(SceneObject obj)
    {
        var node = JsonNode.Parse(ProjectYaml.Decode<JsonElement>(ProjectYaml.Encode(obj)).GetRawText())!.AsObject();
        node.Remove("prefab");
        return JsonSerializer.SerializeToElement(node, GameJson.Default.JsonNode);
    }

    internal static SceneObject Restore(JsonNode node) =>
        ProjectYaml.Decode<SceneObject>(ProjectYaml.Encode(JsonSerializer.SerializeToElement(node, GameJson.Default.JsonNode)));

    internal static JsonNode Merge(JsonElement baseline, SceneObject local, JsonElement incoming)
    {
        var before = JsonNode.Parse(baseline.GetRawText())!.AsObject();
        var edited = JsonNode.Parse(Snapshot(local).GetRawText())!.AsObject();
        var result = JsonNode.Parse(incoming.GetRawText())!.AsObject();
        MergeFields(before, edited, result);
        return result;
    }

    private static void MergeFields(JsonObject before, JsonObject edited, JsonObject result)
    {
        foreach (var key in before.Select(p => p.Key).Union(edited.Select(p => p.Key)))
        {
            bool present = edited.TryGetPropertyValue(key, out var value);
            if (before.ContainsKey(key) == present && JsonNode.DeepEquals(before[key], value)) continue;
            if (!present) { result.Remove(key); continue; }
            if (key == "components" && before[key] is JsonArray a && value is JsonArray b && result[key] is JsonArray c)
                MergeComponents(a,b,c);
            else if (before[key] is JsonObject original && value is JsonObject modified && result[key] is JsonObject inherited)
                MergeFields(original,modified,inherited);
            else result[key] = value?.DeepClone();
        }
    }


    internal static IEnumerable<string> StructuralChanges(JsonElement baseline, SceneObject obj)
    {
        var before = JsonNode.Parse(baseline.GetRawText())!["components"]!.AsArray().Select(ComponentKey).ToHashSet();
        var current = JsonNode.Parse(Snapshot(obj).GetRawText())!["components"]!.AsArray().Select(ComponentKey).ToHashSet();
        foreach (var removed in before.Except(current)) yield return removed.TrimEnd('/') + " (removed)";
        foreach (var added in current.Except(before)) yield return added.TrimEnd('/') + " (added)";
    }

    internal static string ComponentKey(JsonNode? node)=>node!["type"]!.ToString()+"/"+node["script"]?.ToString();

    private static void MergeComponents(JsonArray before, JsonArray edited, JsonArray result)
    {
        var old = before.ToDictionary(ComponentKey);
        var current = edited.ToDictionary(ComponentKey);
        foreach (var (key, component) in old)
        {
            var inherited = result.FirstOrDefault(n => ComponentKey(n) == key);
            if (!current.TryGetValue(key, out var modified))
            {
                if (inherited != null) result.Remove(inherited);
            }
            else if (!JsonNode.DeepEquals(component,modified))
            {
                if (inherited != null) MergeFields(component!.AsObject(),modified!.AsObject(),inherited.AsObject());
                else result.Add(modified!.DeepClone());
            }
        }
        foreach (var (key, component) in current.Where(p => !old.ContainsKey(p.Key)))
        {
            var inherited = result.FirstOrDefault(n => ComponentKey(n) == key);
            if (inherited != null) result.Remove(inherited);
            result.Add(component!.DeepClone());
        }
    }
}
