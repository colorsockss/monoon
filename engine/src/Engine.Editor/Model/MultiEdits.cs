using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Engine.Core;

namespace Engine.Editor;

// Multi-object editing (roadmap 13.5) without UI, so Engine.Checks runs it through the Undo history. A field is the saved
// JSON path the prefab override list uses (PrefabProperty: component key, optional group such as a script's "values",
// field). Setting it rewrites that one key in each selected object's component and decodes the component again, so a
// prefab instance gets exactly the override a single edit would give it. The caller records one edit.
internal static class MultiEdits
{
    internal static JsonObject Json(SceneComponent component) =>
        JsonNode.Parse(ProjectYaml.Decode<JsonElement>(ProjectYaml.Encode<SceneComponent>(component)).GetRawText())!.AsObject();

    private static SceneComponent Decode(JsonObject node) =>
        ProjectYaml.Decode<SceneComponent>(ProjectYaml.Encode(JsonSerializer.SerializeToElement(node, GameJson.Default.JsonNode)));

    // The component's identity in prefab baselines and override lists ("boxCollider2D/", "script/PlayerMovement").
    internal static string Key(SceneComponent component) => PrefabMerge.ComponentKey(Json(component));

    internal static SceneComponent? Find(SceneObject owner, string key) => owner.Components.FirstOrDefault(c => Key(c) == key);

    // The saved value on each object (null: the key is absent, i.e. the default).
    internal static JsonNode?[] Values(IReadOnlyList<SceneObject> objects, string key, string? group, string field) =>
        objects.Select(o => Find(o, key) is { } c ? (group == null ? Json(c)[field] : Json(c)[group]?[field])?.DeepClone() : null).ToArray();

    // Sets (or, with null, removes) one saved key on every object that has the component. False when nothing changed.
    internal static bool Set(IEnumerable<SceneObject> objects, string key, string? group, string field, JsonNode? value)
    {
        bool changed = false;
        foreach (var owner in objects)
        {
            if (Find(owner, key) is not { } component) continue;
            var node = Json(component);
            var before = node.ToJsonString();
            var container = node;
            if (group != null)
            {
                if (node[group] is not JsonObject inner) node[group] = inner = new JsonObject();
                container = inner;
            }
            if (value == null) container.Remove(field);
            else container[field] = value.DeepClone();
            if (node.ToJsonString() == before) continue;
            var next = Decode(node);
            if (ComponentEdits.Copy(next) == ComponentEdits.Copy(component)) continue;
            owner.Components = owner.Components.SetItem(owner.Components.IndexOf(component), next);
            changed = true;
        }
        return changed;
    }

    internal sealed record BuiltInField(string Key, string Label, PropertyInfo Property)
    {
        public Type Type => Property.PropertyType;
        public object? Default => Property.GetValue(Activator.CreateInstance(Property.ReflectedType!));
        // The saved JSON for a value, exactly as the component writes it (null: the default, so the key is left out).
        public JsonNode? Node(Type component, object? value)
        {
            var probe = (SceneComponent)Activator.CreateInstance(component)!;
            Property.SetValue(probe, value);
            return Json(probe)[Key]?.DeepClone();
        }
    }

    private static readonly ConcurrentDictionary<Type, BuiltInField[]> fields = new();

    // The scalar fields of a built-in component (bool, number, enum, string) with their saved keys, found by changing each
    // property on a fresh instance and seeing which key the YAML gains. Fields at their default are not in a snapshot, so
    // this is what lets "Is Trigger" show for ten walls that all have it off.
    internal static BuiltInField[] Fields(Type type) => fields.GetOrAdd(type, static type =>
    {
        if (type == typeof(ScriptComponent) || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null) return [];
        var empty = Json((SceneComponent)Activator.CreateInstance(type)!);
        var found = new List<BuiltInField>();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetSetMethod() == null || property.GetGetMethod() == null || property.GetIndexParameters().Length > 0 ||
                property.Name == nameof(SceneComponent.Enabled)) continue;
            var t = property.PropertyType;
            if (!(t == typeof(bool) || t == typeof(int) || t == typeof(float) || t == typeof(double) || t == typeof(string) || t.IsEnum)) continue;
            var probe = (SceneComponent)Activator.CreateInstance(type)!;
            object? current = property.GetValue(probe);
            object? other = current switch
            {
                bool b => !b, int i => i + 1, float f => f + 1, double d => d + 1, string s => s + "x", null when t == typeof(string) => "x",
                Enum e => Enum.GetValues(t).Cast<object>().FirstOrDefault(v => !v.Equals(e)), _ => null
            };
            if (other == null) continue;
            string[] changed;
            try
            {
                property.SetValue(probe, other);
                changed = Json(probe).Where(p => !empty.ContainsKey(p.Key) || !JsonNode.DeepEquals(empty[p.Key], p.Value)).Select(p => p.Key).ToArray();
            }
            catch (Exception ex) when (ex is TargetInvocationException or ArgumentException or InvalidDataException or FormatException) { continue; }
            if (changed is [var key] && key != "type") found.Add(new(key, Label(property.Name), property));
        }
        return found.ToArray();
    });

    // "IsTrigger" -> "Is Trigger".
    internal static string Label(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]) ? " " + c : c.ToString()));
}
