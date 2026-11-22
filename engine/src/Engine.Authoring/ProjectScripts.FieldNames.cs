using System.Text.Json;
using System.Text.Json.Nodes;

namespace Engine.Core;

public sealed partial class ProjectScripts
{
    // Remove the alias even when a current key already exists. Current spelling wins,
    // then the first declared alias, preserving unknown fields and the original YAML value.
    internal static bool NormalizeValues<T>(IDictionary<string,T> values, ScriptDefinition definition)
    {
        bool changed = false;
        foreach (var field in definition.Fields)
        foreach (var old in field.FormerNames)
        {
            if (!values.Remove(old, out var value)) continue;
            values.TryAdd(field.Name, value);
            changed = true;
        }
        return changed;
    }

    internal override void NormalizeFields(SceneDocument scene)
    {
        if (!scene.Objects.Any(o => o.Prefab?.Baseline?.Count > 0 ||
            o.Components.OfType<ScriptComponent>().Any(s => s.Values.Count != 0))) return;
        Dictionary<string,ScriptDefinition> definitions;
        try { definitions = Current().Components; }
        catch (InvalidDataException)
        {
            // A project with compiler errors remains editable. Leave its stored keys intact;
            // the Inspector, Play and build surface the compiler diagnostic.
            return;
        }
        if (!definitions.Values.Any(d => d.Fields.Any(f => f.FormerNames.Length > 0))) return;
        foreach (var obj in scene.Objects)
        {
            foreach (var script in obj.Components.OfType<ScriptComponent>())
                if (definitions.TryGetValue(script.Script, out var def)) scene.MigratedScriptFields |= NormalizeValues(script.Values, def);

            // Baselines are serialized objects in instance ID space. Edit only their script
            // value keys so unknown members and all object reference IDs survive untouched.
            if (obj.Prefab?.Baseline is not {} baseline) continue;
            foreach (var (id, snapshot) in baseline.ToArray())
            {
                if (!snapshot.TryGetProperty("components"u8, out var components) ||
                    !components.EnumerateArray().Any(c => c.TryGetProperty("script"u8, out var script) &&
                        definitions.TryGetValue(script.GetString()!, out var def) && c.TryGetProperty("values"u8, out var values) &&
                        def.Fields.Any(f => f.FormerNames.Any(n => values.TryGetProperty(n, out _))))) continue;
                var node = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
                foreach (var component in node["components"]!.AsArray())
                    if (component?["script"]?.GetValue<string>() is {} script && definitions.TryGetValue(script, out var def) &&
                        component["values"] is JsonObject values) NormalizeValues(values, def);
                scene.MigratedScriptFields = true;
                baseline[id] = JsonSerializer.SerializeToElement(node, GameJson.Default.JsonNode);
            }
        }
    }
}
