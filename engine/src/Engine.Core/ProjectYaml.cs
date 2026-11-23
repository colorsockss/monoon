using System.Buffers;
using System.Text;
using System.Text.Json;
using VYaml.Annotations;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Engine.Core;

[YamlObject]
// ImportHash: SHA-256 of the Aseprite file this sheet was generated from (editor and build tools only).
internal sealed partial record AssetMeta([property: YamlRequired] Guid Id, SheetSlice[]? Slices = null, SheetInput[]? Inputs = null, string? ImportHash = null);

[YamlObject]
internal sealed partial record SheetInput(int Cell, Guid Sprite, string Hash);

// The editor and both kinds of player use these same generated VYaml formatters.
internal static class ProjectYaml
{
    private static readonly YamlSerializerOptions Options = new()
    {
        Resolver = CompositeResolver.Create([new ScriptValueFormatter(), new ArrayFormatter<RecordedTurn>(), new ArrayFormatter<RecordedInput>(), new ArrayFormatter<long>(), new ArrayFormatter<AssetEntry>(), new ArrayFormatter<TableColumn>(), new ArrayFormatter<TableRow>(), new ArrayFormatter<JsonElement>()], [StandardResolver.Instance]),
        DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull
    };

    internal static byte[] Encode<T>(T value) => YamlSerializer.Serialize(value, Options).ToArray();
    internal static T Decode<T>(byte[] bytes)
    {
        using var docs = YamlSerializer.DeserializeMultipleDocuments<T>(bytes, Options).GetEnumerator();
        if (!docs.MoveNext() || docs.Current is not { } value || docs.MoveNext())
            throw new InvalidDataException($"Expected exactly one {typeof(T).Name} document.");
        return value;
    }
    internal static T Read<T>(string path)
    {
        try { return Decode<T>(File.ReadAllBytes(path)); }
        catch (Exception ex) when (ex is VYaml.YamlException or FormatException or InvalidDataException)
        { throw new InvalidDataException($"{path}: {ex.Message}", ex); }
    }

    internal static void Write<T>(string path, T value) => WriteBytes(path, Encode(value));
    internal static void WriteBytes(string path, byte[] bytes, bool overwrite = true)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, bytes); File.Move(temp, path, overwrite); }
        finally { File.Delete(temp); }
    }
}

// Inspector overrides have dynamic shapes (including object/scene references).
// JsonElement is only their in-memory representation; emit and parse ordinary YAML nodes.
internal sealed class ScriptValueFormatter : IYamlFormatter<JsonElement>
{
    public void Serialize(ref Utf8YamlEmitter emitter, JsonElement value, YamlSerializationContext context)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null: emitter.WriteNull(); break;
            case JsonValueKind.True: case JsonValueKind.False: emitter.WriteBool(value.GetBoolean()); break;
            case JsonValueKind.Number: emitter.WriteScalar(Encoding.UTF8.GetBytes(value.GetRawText())); break;
            case JsonValueKind.String: emitter.WriteString(value.GetString()!, ScalarStyle.DoubleQuoted); break;
            case JsonValueKind.Array:
                emitter.BeginSequence();
                foreach (var item in value.EnumerateArray()) Serialize(ref emitter, item, context);
                emitter.EndSequence(); break;
            case JsonValueKind.Object:
                emitter.BeginMapping();
                foreach (var item in value.EnumerateObject())
                {
                    emitter.WriteString(item.Name);
                    Serialize(ref emitter, item.Value, context);
                }
                emitter.EndMapping(); break;
            default: throw new InvalidDataException("Undefined script field value.");
        }
    }

    public JsonElement Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) ReadNode(ref parser, writer);
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void ReadNode(ref YamlParser parser, Utf8JsonWriter writer)
    {
        switch (parser.CurrentEventType)
        {
            case ParseEventType.SequenceStart:
                parser.Read(); writer.WriteStartArray();
                while (parser.CurrentEventType != ParseEventType.SequenceEnd) ReadNode(ref parser, writer);
                writer.WriteEndArray(); break;
            case ParseEventType.MappingStart:
                parser.Read(); writer.WriteStartObject();
                while (parser.CurrentEventType != ParseEventType.MappingEnd)
                {
                    writer.WritePropertyName(parser.ReadScalarAsString() ?? throw new InvalidDataException("Expected a script field name."));
                    ReadNode(ref parser, writer);
                }
                writer.WriteEndObject(); break;
            case ParseEventType.Scalar:
                if (parser.IsNullScalar()) writer.WriteNullValue();
                else if (parser.TryGetScalarAsBool(out bool flag)) writer.WriteBooleanValue(flag);
                else if (parser.TryGetScalarAsInt64(out long signed)) writer.WriteNumberValue(signed);
                else if (parser.TryGetScalarAsUInt64(out ulong unsigned)) writer.WriteNumberValue(unsigned);
                else if (parser.TryGetScalarAsDouble(out double d) && double.IsFinite(d)) writer.WriteNumberValue(d);
                else writer.WriteStringValue(parser.GetScalarAsString());
                break;
            default: throw new InvalidDataException($"Unexpected script field node: {parser.CurrentEventType}.");
        }
        parser.Read();
    }
}
