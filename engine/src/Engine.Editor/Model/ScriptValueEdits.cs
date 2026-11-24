using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Engine.Core;

namespace Engine.Editor;

// 13.8: Inspector edits of the JSON-shaped script fields (arrays, List<T>, dictionaries, structs), UI-free so Engine.Checks runs
// them. A value equal to the field's default removes the saved key, as the scalar rows do.
internal static class ScriptValueEdits
{
    public static JsonNode? Current(ScriptComponent component, ScriptField field) =>
        JsonNode.Parse(((JsonElement)field.Value(component)!).GetRawText());

    public static bool Store(ScriptComponent component, ScriptField field, JsonNode? value)
    {
        var element = JsonSerializer.SerializeToElement(value, GameJson.Default.JsonNode);
        if (JsonElement.DeepEquals(element, (JsonElement)field.Value(component)!)) return false;
        if (JsonElement.DeepEquals(element, (JsonElement)field.Default!)) component.Values.Remove(field.Name);
        else component.Values[field.Name] = element;
        return true;
    }

    public static JsonNode? Blank(Type element) =>
        element == typeof(string) ? JsonValue.Create("") : element == typeof(bool) ? JsonValue.Create(false) : JsonValue.Create(0);

    // Typed text as the element's JSON: invariant numbers in range, finite floats; false when the text is not one.
    public static bool TryParse(Type type, string text, out JsonNode? value)
    {
        const NumberStyles integer = NumberStyles.Integer, real = NumberStyles.Float;
        var c = CultureInfo.InvariantCulture;
        value = type == typeof(string) ? JsonValue.Create(text)
            : type == typeof(int) && int.TryParse(text, integer, c, out var i) ? JsonValue.Create(i)
            : type == typeof(long) && long.TryParse(text, integer, c, out var l) ? JsonValue.Create(l)
            : type == typeof(uint) && uint.TryParse(text, integer, c, out var u) ? JsonValue.Create(u)
            : type == typeof(float) && float.TryParse(text, real, c, out var f) && float.IsFinite(f) ? JsonValue.Create(f)
            : type == typeof(double) && double.TryParse(text, real, c, out var d) && double.IsFinite(d) ? JsonValue.Create(d)
            : null;
        return value != null;
    }

    public static string Text(JsonNode? value) => value?.GetValueKind() switch
    {
        JsonValueKind.String => value.GetValue<string>(),
        null or JsonValueKind.Null => "",
        _ => value.ToJsonString()
    };

    public static string NewKey(JsonObject map, Type key)
    {
        if (key == typeof(int))
        {
            int n = 0;
            while (map.ContainsKey(n.ToString(CultureInfo.InvariantCulture))) n++;
            return n.ToString(CultureInfo.InvariantCulture);
        }
        string name = "key";
        for (int i = 2; map.ContainsKey(name); i++) name = "key" + i;
        return name;
    }

    // Renames one entry in place (the order is the dictionary's insertion order). False for a duplicate or a key that is not
    // an int of an int dictionary.
    public static bool RenameKey(JsonObject map, string from, string to, Type key)
    {
        if (from == to) return true;
        if (map.ContainsKey(to) || key == typeof(int) && (!int.TryParse(to, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ||
            n.ToString(CultureInfo.InvariantCulture) != to)) return false;
        var entries = map.ToArray();
        map.Clear();
        foreach (var (k, v) in entries) map[k == from ? to : k] = v;
        return true;
    }

    // Flag names checked, in the order they are declared; "" when none (ScriptField.EnumNames lists the non-zero members).
    public static string Flags(string[] names, ISet<string> on) => string.Join(", ", names.Where(on.Contains));

    public static HashSet<string> FlagSet(string? text) =>
        (text ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
}
