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
// PivotX/PivotY: a PNG's import pivot (13.16), the Transform pivot the editor gives objects drawn with it (editor only).
internal sealed partial record AssetMeta([property: YamlRequired] Guid Id, SheetSlice[]? Slices = null, SheetInput[]? Inputs = null, string? ImportHash = null,
    float? PivotX = null, float? PivotY = null);

[YamlObject]
internal sealed partial record SheetInput(int Cell, Guid Sprite, string Hash);

// The editor and both kinds of player read with these generated VYaml formatters. Player-written documents use the
// hand-written overloads in ProjectYamlWriters.cs; see there before adding a runtime Encode<T> with a reference type.
internal static partial class ProjectYaml
{
    internal static readonly YamlSerializerOptions Options = CreateOptions();

    // Editor and build tools set this to VYaml's BuiltinResolver (Engine.Authoring's module initializer) for formatters
    // the player never needs, such as Dictionary<string, ContentBuildEntry>. The player leaves it null.
    internal static IYamlFormatterResolver? ToolFormatters;

    // YamlSerializerOptions' constructor stores StandardResolver.Instance, which keeps BuiltinResolver and its ~70 formatters
    // in Native AOT; the player's VYaml.Substitutions.xml removes it. Set every field the constructor sets instead
    // (ProjectYamlResolverChecks compares the two).
    private static YamlSerializerOptions CreateOptions()
    {
        var options = (YamlSerializerOptions)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(YamlSerializerOptions));
        options.Resolver = CompositeResolver.Create([new ScriptValueFormatter(), new ArrayFormatter<RecordedTurn>(), new ArrayFormatter<RecordedInput>(), new ArrayFormatter<long>(), new ArrayFormatter<AssetEntry>(), new ArrayFormatter<TableColumn>(), new ArrayFormatter<TableRow>(), new ArrayFormatter<JsonElement>()], [ScalarYamlResolver.Instance, GeneratedResolver.Instance]);
        options.EmitOptions = new();
        options.MaxDepth = 64;
        options.DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull;
        return options;
    }

    internal static byte[] Encode<T>(T value) => YamlSerializer.Serialize(value, Options).ToArray();
    internal static void EncodeTo<T>(IBufferWriter<byte> writer, T value) => YamlSerializer.Serialize(writer, value, Options);

    // The steps of YamlSerializer.DeserializeMultipleDocuments(ReadOnlyMemory<byte>, options). VYaml's own version rents its
    // context from a pool whose factory reads YamlSerializerOptions.Standard, which the Native AOT player removes. Every
    // document is still read, as before.
    internal static T Decode<T>(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        var parser = YamlParser.FromSequence(sequence);
        parser.MaxDepth = Options.MaxDepth;
        var context = spareContext ?? new YamlDeserializationContext(Options);
        spareContext = null;
        try
        {
            var formatter = Options.Resolver.GetFormatterWithVerify<T>();
            T? first = default;
            int documents = 0;
            while (true)
            {
                parser.SkipAfter(ParseEventType.DocumentStart);
                if (parser.End) break;
                context.Reset();
                var document = context.DeserializeWithAlias(formatter, ref parser);
                if (documents++ == 0) first = document;
            }
            if (documents != 1 || first is null) throw new InvalidDataException($"Expected exactly one {typeof(T).Name} document.");
            return first;
        }
        finally
        {
            ReturnPool(ref parser);
            spareContext = context;
        }
    }
    // One spare context per thread replaces VYaml's thread-local pool; a nested Decode makes its own.
    [ThreadStatic] private static YamlDeserializationContext? spareContext;
    [System.Runtime.CompilerServices.UnsafeAccessor(System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "ReturnPool")]
    private static extern void ReturnPool(ref YamlParser parser);
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

// StandardResolver puts BuiltinResolver first, whose static constructor builds about 70 formatters (Uri, DateTime, BigInteger,
// Type, multi-dimensional arrays …). Native AOT kept all of them and the BCL code behind them (~0.3 MB per game). Engine
// documents only use the scalars below; this returns the same instances, and GeneratedResolver has every array, list,
// dictionary, nullable and enum formatter the generated members need. ProjectYamlResolverChecks compares both resolvers.
internal sealed class ScalarYamlResolver : IYamlFormatterResolver
{
    internal static readonly ScalarYamlResolver Instance = new();
    public IYamlFormatter<T>? GetFormatter<T>() => Cache<T>.Formatter;
    internal static object? Scalar(Type type) =>
        type == typeof(string) ? NullableStringFormatter.Instance :
        type == typeof(bool) ? BooleanFormatter.Instance :
        type == typeof(int) ? Int32Formatter.Instance :
        type == typeof(uint) ? UInt32Formatter.Instance :
        type == typeof(long) ? Int64Formatter.Instance :
        type == typeof(ulong) ? UInt64Formatter.Instance :
        type == typeof(float) ? Float32Formatter.Instance :
        type == typeof(double) ? Float64Formatter.Instance :
        type == typeof(Guid) ? GuidFormatter.Instance :
        type == typeof(bool?) ? NullableBooleanFormatter.Instance :
        type == typeof(int?) ? NullableInt32Formatter.Instance :
        type == typeof(uint?) ? NullableUInt32Formatter.Instance :
        type == typeof(long?) ? NullableInt64Formatter.Instance :
        type == typeof(float?) ? NullableFloat32Formatter.Instance :
        type == typeof(double?) ? NullableFloat64Formatter.Instance :
        type == typeof(Guid?) ? new StaticNullableFormatter<Guid>(GuidFormatter.Instance) : null;
    private static class Cache<T>
    {
        internal static readonly IYamlFormatter<T>? Formatter = (IYamlFormatter<T>?)Scalar(typeof(T)) ?? ProjectYaml.ToolFormatters?.GetFormatter<T>();
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
