using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace Engine.Core;

// Roadmap 13.8: the generated script factory reads long, uint, colours, flag enums, primitive arrays, List<T>, dictionaries and
// one-level [Serializable] structs with these helpers: no JsonTypeInfo, no reflection. The generic ones are instantiated only by
// the factory code of a game that declares such a field, so a game without them carries none of it (Native AOT trims the rest).
// A value of the wrong JSON kind keeps the script's initial value, as ReadOverride does: the field type may have changed since.
public static class ScriptValues
{
    public static int Int32(JsonElement e, int initial) => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var v) ? v : initial;
    public static long Int64(JsonElement e, long initial) => e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var v) ? v : initial;
    public static uint UInt32(JsonElement e, uint initial) => e.ValueKind == JsonValueKind.Number && e.TryGetUInt32(out var v) ? v : initial;
    public static float Single(JsonElement e, float initial) =>
        e.ValueKind == JsonValueKind.Number && e.TryGetSingle(out var v) && float.IsFinite(v) ? v : initial;
    public static double Double(JsonElement e, double initial) =>
        e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out var v) && double.IsFinite(v) ? v : initial;
    public static bool Boolean(JsonElement e, bool initial) =>
        e.ValueKind == JsonValueKind.True || e.ValueKind != JsonValueKind.False && initial;
    public static string? String(JsonElement e, string? initial) =>
        e.ValueKind == JsonValueKind.String ? e.GetString() : e.ValueKind == JsonValueKind.Null ? null : initial;

    public static Vector2 Vector(JsonElement e, Vector2 initial) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty("x", out var x) && e.TryGetProperty("y", out var y)
            ? new(Single(x, initial.X), Single(y, initial.Y)) : initial;

    /// <summary>A colour saved as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.</summary>
    public static Color Color(JsonElement e, Color initial) =>
        e.ValueKind == JsonValueKind.String && TryColor(e.GetString(), out var c) ? c : initial;

    public static bool TryColor(string? hex, out Color color) => ParticleEffect.TryColor(hex, out color);

    /// <summary>One declared name (a flag enum also takes a comma list such as <c>"Fire, Ice"</c>; an empty text is no flag).</summary>
    public static T Enum<T>(JsonElement e, T initial, bool flags = false) where T : struct, System.Enum
    {
        if (e.ValueKind != JsonValueKind.String) return initial;
        var text = e.GetString()!;
        var names = System.Enum.GetNames<T>();
        if (flags)
        {
            var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return default;
            if (parts.All(names.Contains) && System.Enum.TryParse<T>(string.Join(',', parts), out var combined)) return combined;
        }
        else if (names.Contains(text) && System.Enum.TryParse<T>(text, out var value)) return value;
        throw new InvalidDataException($"'{text}' is not a declared {typeof(T).Name} value.");
    }

    // Arrays and lists keep their saved order; an element of the wrong kind becomes default(T). JSON null is a null field.
    public static T[]? Array<T>(JsonElement e, T[]? initial, Func<JsonElement, T, T> element)
    {
        if (e.ValueKind == JsonValueKind.Null) return null;
        if (e.ValueKind != JsonValueKind.Array) return initial;
        var items = new T[e.GetArrayLength()];
        int i = 0;
        foreach (var item in e.EnumerateArray()) items[i++] = element(item, default!);
        return items;
    }

    public static List<T>? List<T>(JsonElement e, List<T>? initial, Func<JsonElement, T, T> element) =>
        e.ValueKind == JsonValueKind.Array ? new(Array(e, null, element)!) : e.ValueKind == JsonValueKind.Null ? null : initial;

    // A dictionary is a JSON object in insertion order; keys are the property names (an int key in invariant digits).
    public static Dictionary<TKey, TValue>? Dictionary<TKey, TValue>(JsonElement e, Dictionary<TKey, TValue>? initial,
        Func<string, TKey> key, Func<JsonElement, TValue, TValue> value) where TKey : notnull
    {
        if (e.ValueKind == JsonValueKind.Null) return null;
        if (e.ValueKind != JsonValueKind.Object) return initial;
        var map = new Dictionary<TKey, TValue>();
        foreach (var p in e.EnumerateObject())
            if (!map.TryAdd(key(p.Name), value(p.Value, default!))) throw new InvalidDataException($"Duplicate dictionary key '{p.Name}'.");
        return map;
    }

    public static int Key32(string key) => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n :
        throw new InvalidDataException($"'{key}' is not an int dictionary key.");
}
