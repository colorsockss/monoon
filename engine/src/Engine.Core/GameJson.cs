using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Engine.Core;

// Runtime JSON: script field values (ReadOverride: int, float, double, bool, string, Guid and asset references),
// prefab merge nodes and GameData values. Legacy project documents are read by Engine.Authoring's LegacyJson.
[JsonSourceGenerationOptions(System.Text.Json.JsonSerializerDefaults.Web, WriteIndented = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(System.Text.Json.Nodes.JsonNode))]
[JsonSerializable(typeof(SceneReference))]
[JsonSerializable(typeof(SpriteReference))]
[JsonSerializable(typeof(PrefabReference))]
[JsonSerializable(typeof(DataReference))]
[JsonSerializable(typeof(TableReference))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(Guid[]))]
internal partial class GameJson : JsonSerializerContext
{
    // The primitive script field types ReadOverride deserializes. GetTypeInfo(Type) would keep every listed type's converter.
    // Another T gets null, which Deserialize rejects as GetTypeInfo's null did.
    internal static System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> Field<T>()
    {
        System.Text.Json.Serialization.Metadata.JsonTypeInfo? info = null;
        if (typeof(T) == typeof(int)) info = Default.Int32;
        else if (typeof(T) == typeof(float)) info = Default.Single;
        else if (typeof(T) == typeof(double)) info = Default.Double;
        else if (typeof(T) == typeof(bool)) info = Default.Boolean;
        else if (typeof(T) == typeof(string)) info = Default.String;
        else if (typeof(T) == typeof(Guid)) info = Default.Guid;
        return (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)info!;
    }
}

// JsonSerializer.SerializeToElement(value, GameJson.Default.X) for the shapes the engine writes, with the writer options the
// serializer derives from GameJson (indented, depth 64, unvalidated). Writing directly gives the same element and keeps
// JsonSerializer's converter layer out of Native AOT players that never read script fields (engine/tests/results/aot-size-2).
// GameJsonWriterChecks compares both for each shape.
internal static class JsonElements
{
    private static readonly JsonWriterOptions Options = new() { Indented = true, MaxDepth = 64, SkipValidation = true };

    internal static JsonElement Of(bool value) => Write(value, static (w, v) => w.WriteBooleanValue(v));
    internal static JsonElement Of(double value) => Write(value, static (w, v) => w.WriteNumberValue(v));
    internal static JsonElement Of(string? value) => Write(value, static (w, v) => w.WriteStringValue(v));
    internal static JsonElement Of(Guid value) => Write(value, static (w, v) => w.WriteStringValue(v));
    internal static JsonElement Of(Guid[]? ids) => Write(ids, static (w, v) =>
    {
        if (v == null) { w.WriteNullValue(); return; }
        w.WriteStartArray();
        foreach (var id in v) w.WriteStringValue(id);
        w.WriteEndArray();
    });
    internal static JsonElement Of(JsonNode? node) => Write(node, static (w, v) =>
    {
        if (v == null) w.WriteNullValue();
        else v.WriteTo(w, GameJson.Default.Options);
    });

    // A `record struct X(Guid Id)` as JsonSerializer reads it with GameJson's web defaults: an object whose properties are
    // matched case-insensitively, the last Id wins, other properties are skipped and a missing Id is Guid.Empty.
    // Anything else (null, another kind, an Id that is not a GUID string) is the JsonException ReadOverride turns into "keep".
    internal static bool ReadReference(JsonElement value, out Guid id)
    {
        id = default;
        if (value.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in value.EnumerateObject())
            if (string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase) &&
                (property.Value.ValueKind != JsonValueKind.String || !property.Value.TryGetGuid(out id))) return false;
        return true;
    }

    // ReadOverride's primitive fields as JsonSerializer reads them with GameJson's web defaults (numbers also from strings).
    // False is the JsonException ReadOverride turns into "keep"; a JSON null string gives true and null.
    internal static bool ReadField<T>(JsonElement value, out T result)
    {
        result = default!;
        if (typeof(T) == typeof(int)) { if (!Int32(value, out int v)) return false; result = (T)(object)v; }
        else if (typeof(T) == typeof(float)) { if (!Single(value, out float v)) return false; result = (T)(object)v; }
        else if (typeof(T) == typeof(double)) { if (!Double(value, out double v)) return false; result = (T)(object)v; }
        else if (typeof(T) == typeof(bool)) { if (!Boolean(value, out bool v)) return false; result = (T)(object)v; }
        else if (typeof(T) == typeof(Guid)) { if (!Guid(value, out var v)) return false; result = (T)(object)v; }
        else if (typeof(T) == typeof(string))
        {
            if (value.ValueKind == JsonValueKind.Null) return true;
            if (!Text(value, out var text)) return false;
            result = (T)(object)text;
        }
        else throw new ArgumentNullException("jsonTypeInfo"); // GameJson.Field<T> had no type info, which Deserialize rejected
        return true;
    }

    // The readers of JsonSerializer's primitive converters with JsonNumberHandling.AllowReadingFromString: a quoted number is
    // NaN, Infinity or -Infinity, or else parsed from its unescaped UTF-8 with Utf8Parser within the reader's length limit and finite.
    internal static bool Int32(JsonElement value, out int result)
    {
        result = 0;
        if (value.ValueKind == JsonValueKind.Number) return value.TryGetInt32(out result);
        return Quoted(value, out var span) && span.Length <= 11 &&
            System.Buffers.Text.Utf8Parser.TryParse(span, out result, out int used) && used == span.Length;
    }

    internal static bool Single(JsonElement value, out float result)
    {
        result = 0;
        if (value.ValueKind == JsonValueKind.Number) return value.TryGetSingle(out result);
        if (!Quoted(value, out var span)) return false;
        if (Constant(span) is double named) { result = (float)named; return true; }
        return span.Length <= 128 && System.Buffers.Text.Utf8Parser.TryParse(span, out result, out int used) && used == span.Length && float.IsFinite(result);
    }

    internal static bool Double(JsonElement value, out double result)
    {
        result = 0;
        if (value.ValueKind == JsonValueKind.Number) return value.TryGetDouble(out result);
        if (!Quoted(value, out var span)) return false;
        if (Constant(span) is double named) { result = named; return true; }
        return span.Length <= 128 && System.Buffers.Text.Utf8Parser.TryParse(span, out result, out int used) && used == span.Length && double.IsFinite(result);
    }

    internal static bool Boolean(JsonElement value, out bool result)
    {
        result = value.ValueKind == JsonValueKind.True;
        return value.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }

    internal static bool Guid(JsonElement value, out Guid result)
    {
        result = default;
        if (value.ValueKind != JsonValueKind.String) return false;
        try { return value.TryGetGuid(out result); }
        catch (InvalidOperationException) { return false; } // escapes that are not valid UTF-16
    }

    internal static bool Text(JsonElement value, out string result)
    {
        result = null!;
        if (value.ValueKind != JsonValueKind.String) return false;
        try { result = value.GetString()!; return true; }
        catch (InvalidOperationException) { return false; } // escapes that are not valid UTF-16
    }

    // A dictionary key as JsonSerializer's Guid property-name reader takes it: exactly the 36-character "D" form.
    internal static bool GuidName(string name, out Guid result)
    {
        result = default;
        Span<byte> utf8 = stackalloc byte[37];
        return System.Text.Encoding.UTF8.TryGetBytes(name, utf8, out int length) && length == 36 &&
            System.Buffers.Text.Utf8Parser.TryParse(utf8[..36], out result, out int used, 'D') && used == 36;
    }

    // The quoted names the reader accepts before parsing (exact spelling, as JsonReaderHelper.TryGetFloatingPointConstant).
    private static double? Constant(ReadOnlySpan<byte> span) =>
        span.SequenceEqual("NaN"u8) ? double.NaN : span.SequenceEqual("Infinity"u8) ? double.PositiveInfinity :
        span.SequenceEqual("-Infinity"u8) ? double.NegativeInfinity : null;

    private static bool Quoted(JsonElement value, out ReadOnlySpan<byte> span)
    {
        span = default;
        if (!Text(value, out var text)) return false;
        span = System.Text.Encoding.UTF8.GetBytes(text);
        return true;
    }

    private static JsonElement Write<T>(T value, Action<Utf8JsonWriter, T> write) => Write(value, Options, write);

    internal static JsonElement Write<T>(T value, JsonWriterOptions options, Action<Utf8JsonWriter, T> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, options)) write(writer, value);
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
