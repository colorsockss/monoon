using System.Buffers;
using System.Text;
using System.Text.Json;
using VYaml.Emitter;

namespace Engine.Core;

// Documents a player writes (saves, settings, replays, crash reports, mod and update overrides) bind to these overloads
// instead of the generic Encode<T>. Native AOT keeps every generated VYaml Serialize (about 2.4 MB) as soon as one
// IYamlFormatter<T>.Serialize call with a reference-type T exists: the calls share one canonical interface slot.
// Each writer makes the generated formatter's emitter calls in the same order; Engine.Checks compares bytes with Encode<T>.
internal static partial class ProjectYaml
{
    private delegate void Body<T>(ref Utf8YamlEmitter e, T value);
    private static readonly ScriptValueFormatter Script = new();

    internal static byte[] Encode(GameData value) => Emit(value, Data);
    internal static byte[] Encode(GameTable value) => Emit(value, Table);
    internal static byte[] Encode(string[] value) => Emit(value, Texts);
    internal static byte[] Encode(AssetEntry[] value) => Emit(value, Entries);
    internal static byte[] Encode(InputReplay value) => Emit(value, Replay);
    internal static byte[] Encode(GridReplay value) => Emit(value, Grid);
    internal static byte[] Encode(PlayerCrashReport value) => Emit(value, Crash);
    internal static void Write(string path, GameData value) => WriteBytes(path, Encode(value));
    internal static void Write(string path, string[] value) => WriteBytes(path, Encode(value));
    internal static void Write(string path, AssetEntry[] value) => WriteBytes(path, Encode(value));
    internal static void Write(string path, GridReplay value) => WriteBytes(path, Encode(value));

    private static byte[] Emit<T>(T value, Body<T> body)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var e = new Utf8YamlEmitter(buffer, null);
        body(ref e, value);
        return buffer.WrittenSpan.ToArray();
    }

    // Generated object formatter: null, or a block mapping of its members.
    private static void Map<T>(ref Utf8YamlEmitter e, T? value, Body<T> members) where T : class
    {
        if (value is null) { e.WriteNull(); return; }
        e.BeginMapping();
        members(ref e, value);
        e.EndMapping();
    }

    // ArrayFormatter<T>.
    private static void Seq<T>(ref Utf8YamlEmitter e, T[]? items, Body<T> item)
    {
        if (items is null) { e.WriteNull(); return; }
        e.BeginSequence();
        foreach (var x in items) item(ref e, x);
        e.EndSequence();
    }

    private static void Text(ref Utf8YamlEmitter e, string? s) { if (s is null) e.WriteNull(); else e.WriteString(s); }
    private static void Value(ref Utf8YamlEmitter e, JsonElement v) => Script.Serialize(ref e, v, null!);
    private static void Id(ref Utf8YamlEmitter e, Guid id)
    {
        var d = new byte[36]; // GuidFormatter: "D" format, written as a raw scalar.
        id.TryFormat(d, out int n);
        e.WriteScalar(d.AsSpan(0, n));
    }
    // EnumAsStringFormatter with the default lowerCamelCase names.
    private static void Name<T>(ref Utf8YamlEmitter e, T value) where T : struct, Enum
    {
        string name = value.ToString();
        e.WriteScalar(Encoding.UTF8.GetBytes(char.ToLowerInvariant(name[0]) + name[1..]));
    }
    // Optional members are left out when null (YamlIgnoreCondition.WhenWritingNull).
    private static void Opt(ref Utf8YamlEmitter e, ReadOnlySpan<byte> key, string? s) { if (s != null) { e.WriteScalar(key); e.WriteString(s); } }
    private static void Opt(ref Utf8YamlEmitter e, ReadOnlySpan<byte> key, int? v) { if (v is int x) { e.WriteScalar(key); e.WriteInt32(x); } }
    private static void Opt(ref Utf8YamlEmitter e, ReadOnlySpan<byte> key, float? v) { if (v is float x) { e.WriteScalar(key); e.WriteFloat(x); } }
    private static void Opt(ref Utf8YamlEmitter e, ReadOnlySpan<byte> key, bool? v) { if (v is bool x) { e.WriteScalar(key); e.WriteBool(x); } }

    private static void Data(ref Utf8YamlEmitter e, GameData? data) => Map(ref e, data, static (ref Utf8YamlEmitter e, GameData d) =>
    {
        e.WriteScalar("version"u8); e.WriteInt32(d.Version);
        e.WriteScalar("values"u8);
        if (d.Values is null) { e.WriteNull(); return; }
        e.BeginMapping();
        foreach (var (key, value) in d.Values) { Text(ref e, key); Value(ref e, value); }
        e.EndMapping();
    });

    private static void Texts(ref Utf8YamlEmitter e, string[]? items) => Seq(ref e, items, Text);

    private static void Entries(ref Utf8YamlEmitter e, AssetEntry[]? items) => Seq(ref e, items, static (ref Utf8YamlEmitter e, AssetEntry a) =>
        Map(ref e, a, static (ref Utf8YamlEmitter e, AssetEntry a) =>
        {
            e.WriteScalar("id"u8); Id(ref e, a.Id);
            Opt(ref e, "path"u8, a.Path);
            Opt(ref e, "hash"u8, a.Hash);
        }));

    private static void Table(ref Utf8YamlEmitter e, GameTable? table) => Map(ref e, table, static (ref Utf8YamlEmitter e, GameTable t) =>
    {
        e.WriteScalar("version"u8); e.WriteInt32(t.Version);
        if (t.Columns != null)
        {
            e.WriteScalar("columns"u8);
            Seq(ref e, t.Columns, static (ref Utf8YamlEmitter e, TableColumn c) => Map(ref e, c, static (ref Utf8YamlEmitter e, TableColumn c) =>
            {
                Opt(ref e, "name"u8, c.Name);
                Opt(ref e, "type"u8, c.Type);
            }));
        }
        if (t.Rows != null)
        {
            e.WriteScalar("rows"u8);
            Seq(ref e, t.Rows, static (ref Utf8YamlEmitter e, TableRow r) => Map(ref e, r, static (ref Utf8YamlEmitter e, TableRow r) =>
            {
                Opt(ref e, "key"u8, r.Key);
                if (r.Cells != null) { e.WriteScalar("cells"u8); Seq(ref e, r.Cells, Value); }
            }));
        }
    });

    private static void Replay(ref Utf8YamlEmitter e, InputReplay? replay) => Map(ref e, replay, static (ref Utf8YamlEmitter e, InputReplay r) =>
    {
        e.WriteScalar("version"u8); e.WriteInt32(r.Version);
        e.WriteScalar("project"u8); Id(ref e, r.Project);
        e.WriteScalar("startScene"u8); Id(ref e, r.StartScene);
        e.WriteScalar("frameTicks"u8); Seq(ref e, r.FrameTicks, static (ref Utf8YamlEmitter e, long t) => e.WriteInt64(t));
        e.WriteScalar("events"u8);
        Seq(ref e, r.Events, static (ref Utf8YamlEmitter e, RecordedInput x) => Map(ref e, x, static (ref Utf8YamlEmitter e, RecordedInput x) =>
        {
            e.WriteScalar("frame"u8); e.WriteInt32(x.Frame);
            e.WriteScalar("timeTicks"u8); e.WriteInt64(x.TimeTicks);
            e.WriteScalar("input"u8);
            var i = x.Input;
            e.BeginMapping();
            e.WriteScalar("kind"u8); Name(ref e, i.Kind);
            Opt(ref e, "control"u8, i.Control);
            Opt(ref e, "down"u8, i.Down);
            Opt(ref e, "reverse"u8, i.Reverse);
            Opt(ref e, "x"u8, i.X);
            Opt(ref e, "y"u8, i.Y);
            Opt(ref e, "value"u8, i.Value);
            Opt(ref e, "text"u8, i.Text);
            e.EndMapping();
        }));
    });

    private static void Grid(ref Utf8YamlEmitter e, GridReplay? replay) => Map(ref e, replay, static (ref Utf8YamlEmitter e, GridReplay r) =>
    {
        e.WriteScalar("version"u8); e.WriteInt32(r.Version);
        e.WriteScalar("setup"u8);
        Map(ref e, r.Setup, static (ref Utf8YamlEmitter e, GridRunRules s) =>
        {
            e.WriteScalar("rules"u8); Text(ref e, s.Rules);
            e.WriteScalar("generatorVersion"u8); e.WriteInt32(s.GeneratorVersion);
            e.WriteScalar("seed"u8); e.WriteUInt64(s.Seed);
            e.WriteScalar("columns"u8); e.WriteInt32(s.Columns);
            e.WriteScalar("rows"u8); e.WriteInt32(s.Rows);
            e.WriteScalar("startX"u8); e.WriteInt32(s.StartX);
            e.WriteScalar("startY"u8); e.WriteInt32(s.StartY);
        });
        e.WriteScalar("turns"u8);
        Seq(ref e, r.Turns, static (ref Utf8YamlEmitter e, RecordedTurn t) => Map(ref e, t, static (ref Utf8YamlEmitter e, RecordedTurn t) =>
        {
            e.WriteScalar("command"u8); Name(ref e, t.Command);
            e.WriteScalar("x"u8); e.WriteInt32(t.X);
            e.WriteScalar("y"u8); e.WriteInt32(t.Y);
        }));
    });

    private static void Crash(ref Utf8YamlEmitter e, PlayerCrashReport? report) => Map(ref e, report, static (ref Utf8YamlEmitter e, PlayerCrashReport r) =>
    {
        e.WriteScalar("version"u8); e.WriteInt32(r.Version);
        Opt(ref e, "utc"u8, r.Utc);
        Opt(ref e, "oS"u8, r.OS);
        Opt(ref e, "architecture"u8, r.Architecture);
        Opt(ref e, "run"u8, r.Run);
        if (r.Scene is Guid scene) { e.WriteScalar("scene"u8); Id(ref e, scene); }
        if (r.Build != null) { e.WriteScalar("build"u8); Data(ref e, r.Build); }
        if (r.Causes == null) return;
        e.WriteScalar("causes"u8);
        Seq(ref e, r.Causes, static (ref Utf8YamlEmitter e, CrashCause c) => Map(ref e, c, static (ref Utf8YamlEmitter e, CrashCause c) =>
        {
            Opt(ref e, "type"u8, c.Type);
            Opt(ref e, "message"u8, c.Message);
            if (c.Frames == null) return;
            e.WriteScalar("frames"u8);
            Seq(ref e, c.Frames, static (ref Utf8YamlEmitter e, CrashFrame f) => Map(ref e, f, static (ref Utf8YamlEmitter e, CrashFrame f) =>
            {
                Opt(ref e, "method"u8, f.Method);
                Opt(ref e, "assembly"u8, f.Assembly);
                Opt(ref e, "moduleId"u8, f.ModuleId);
                Opt(ref e, "methodToken"u8, f.MethodToken);
                Opt(ref e, "ilOffset"u8, f.IlOffset);
                Opt(ref e, "imageOffset"u8, f.ImageOffset);
            }));
        }));
    });
}
