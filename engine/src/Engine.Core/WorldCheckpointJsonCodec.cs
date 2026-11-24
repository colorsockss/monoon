using System.Text.Json;

namespace Engine.Core;

// WorldCheckpoint (WorldCheckpoints.cs) as JsonSerializer writes and reads it with WorldCheckpointJson: web defaults (camelCase,
// case-insensitive names, last duplicate wins, numbers also from strings), every constructor argument without a default
// required, enums as numbers, Guid keys in "D" form, Reparented and Basis left out when null. By hand it keeps JsonSerializer's
// converter layer out of Native AOT players (engine/tests/results/aot-feature-switches). GameJsonWriterChecks compares both.
internal static class WorldCheckpointJsonCodec
{
    private static readonly JsonWriterOptions Options = new() { MaxDepth = 64, SkipValidation = true };

    internal static JsonElement Write(WorldCheckpoint checkpoint) => JsonElements.Write(checkpoint, Options, static (w, c) =>
    {
        w.WriteStartObject();
        w.WriteNumber("version", c.Version);
        w.WriteStartObject("scenes");
        foreach (var (id, scene) in c.Scenes)
        {
            Key(w, id);
            w.WriteStartObject();
            Objects(w, scene.Objects);
            w.WriteStartArray("prefabs");
            foreach (var prefab in scene.Prefabs)
            {
                w.WriteStartObject();
                w.WriteString("asset", prefab.Asset);
                Id(w, "parent", prefab.Parent);
                w.WriteNumber("spawnX", prefab.SpawnX);
                w.WriteNumber("spawnY", prefab.SpawnY);
                w.WriteStartObject("members");
                foreach (var (source, instance) in prefab.Members) { Key(w, source); w.WriteStringValue(instance); }
                w.WriteEndObject();
                Objects(w, prefab.Objects);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndObject();
        w.WriteEndObject();
    });

    private static void Objects(Utf8JsonWriter w, SavedObject[] objects)
    {
        w.WriteStartArray("objects");
        foreach (var o in objects)
        {
            w.WriteStartObject();
            w.WriteString("id", o.Id);
            w.WriteNumber("fields", (int)o.Fields);
            w.WriteBoolean("removed", o.Removed);
            if (o.Pose is { } pose)
            {
                w.WriteStartObject("pose");
                w.WriteNumber("x", pose.X);
                w.WriteNumber("y", pose.Y);
                w.WriteNumber("angle", pose.Angle);
                if (pose.Basis is { } b)
                {
                    w.WriteStartObject("basis");
                    w.WriteNumber("m11", b.M11);
                    w.WriteNumber("m12", b.M12);
                    w.WriteNumber("m21", b.M21);
                    w.WriteNumber("m22", b.M22);
                    w.WriteBoolean("negativeX", b.NegativeX);
                    w.WriteEndObject();
                }
                w.WriteEndObject();
            }
            else w.WriteNull("pose");
            Flag(w, "active", o.Active);
            Flag(w, "collider", o.Collider);
            if (o.Reparented is { } parent) { w.WriteStartObject("reparented"); Id(w, "id", parent.Id); w.WriteEndObject(); }
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    // A Guid dictionary key: the serializer formats it "D" (lower-case hex) into a stack buffer as well.
    private static void Key(Utf8JsonWriter w, Guid id)
    {
        Span<char> name = stackalloc char[36];
        id.TryFormat(name, out _, "D");
        w.WritePropertyName(name);
    }

    private static void Id(Utf8JsonWriter w, string name, Guid? id) { if (id is Guid g) w.WriteString(name, g); else w.WriteNull(name); }
    private static void Flag(Utf8JsonWriter w, string name, bool? on) { if (on is bool b) w.WriteBoolean(name, b); else w.WriteNull(name); }

    // Throws JsonException where JsonSerializer does; the caller reports it as an invalid checkpoint.
    internal static WorldCheckpoint? Read(JsonElement e) => Nullable(e, static e =>
    {
        int version = 0; Dictionary<Guid, SavedSceneWorld>? scenes = null;
        Properties(e, ["version", "scenes"], 0b11, (i, v) =>
        {
            if (i == 0) version = Int(v);
            else scenes = Nullable(v, static v => Dictionary(v, static v => Nullable(v, Scene)!));
        });
        return new WorldCheckpoint(version, scenes!);
    });

    private static SavedSceneWorld Scene(JsonElement e)
    {
        SavedObject[]? objects = null; SavedPrefab[]? prefabs = null;
        Properties(e, ["objects", "prefabs"], 0b11, (i, v) =>
        {
            if (i == 0) objects = Objects(v);
            else prefabs = Array(v, static v => Nullable(v, Prefab)!);
        });
        return new(objects!, prefabs!);
    }

    private static SavedPrefab Prefab(JsonElement e)
    {
        Guid asset = default; Guid? parent = null; float x = 0, y = 0; Dictionary<Guid, Guid>? members = null; SavedObject[]? objects = null;
        Properties(e, ["asset", "parent", "spawnX", "spawnY", "members", "objects"], 0b111111, (i, v) =>
        {
            switch (i)
            {
                case 0: asset = Id(v); break;
                case 1: parent = OptionalId(v); break;
                case 2: x = Float(v); break;
                case 3: y = Float(v); break;
                case 4: members = Nullable(v, static v => Dictionary(v, Id)); break;
                default: objects = Objects(v); break;
            }
        });
        return new(asset, parent, x, y, members!, objects!);
    }

    private static SavedObject[] Objects(JsonElement e) => Array(e, static v => Nullable(v, Object)!);

    private static SavedObject Object(JsonElement e)
    {
        Guid id = default; int fields = 0; bool removed = false; bool? active = null, collider = null; SavedPose? pose = null; SavedParent? parent = null;
        Properties(e, ["id", "fields", "removed", "pose", "active", "collider", "reparented"], 0b111111, (i, v) =>
        {
            switch (i)
            {
                case 0: id = Id(v); break;
                case 1: fields = Enum(v); break;
                case 2: removed = Bool(v); break;
                case 3: pose = Nullable(v, Pose); break;
                case 4: active = OptionalBool(v); break;
                case 5: collider = OptionalBool(v); break;
                default: parent = Nullable(v, static v =>
                {
                    Guid? link = null;
                    Properties(v, ["id"], 0b1, (_, x) => link = OptionalId(x));
                    return new SavedParent(link);
                }); break;
            }
        });
        return new(id, (WorldSaveFields)fields, removed, pose, active, collider, parent);
    }

    private static SavedPose Pose(JsonElement e)
    {
        float x = 0, y = 0, angle = 0; SavedBasis? basis = null;
        Properties(e, ["x", "y", "angle", "basis"], 0b111, (i, v) =>
        {
            switch (i)
            {
                case 0: x = Float(v); break;
                case 1: y = Float(v); break;
                case 2: angle = Float(v); break;
                default: basis = Nullable(v, static v =>
                {
                    float m11 = 0, m12 = 0, m21 = 0, m22 = 0; bool negative = false;
                    Properties(v, ["m11", "m12", "m21", "m22", "negativeX"], 0b11111, (j, x) =>
                    {
                        switch (j)
                        {
                            case 0: m11 = Float(x); break;
                            case 1: m12 = Float(x); break;
                            case 2: m21 = Float(x); break;
                            case 3: m22 = Float(x); break;
                            default: negative = Bool(x); break;
                        }
                    });
                    return new SavedBasis(m11, m12, m21, m22, negative);
                }); break;
            }
        });
        return new(x, y, angle, basis);
    }

    // An object's properties matched case-insensitively (the last one wins, unknown ones are skipped); 'required' bits must be seen.
    private static void Properties(JsonElement e, string[] names, int required, Action<int, JsonElement> read)
    {
        if (e.ValueKind != JsonValueKind.Object) throw new JsonException();
        int seen = 0;
        foreach (var property in e.EnumerateObject())
        {
            int i = System.Array.FindIndex(names, n => string.Equals(n, property.Name, StringComparison.OrdinalIgnoreCase));
            if (i < 0) continue;
            read(i, property.Value);
            seen |= 1 << i;
        }
        if ((seen & required) != required) throw new JsonException();
    }

    private static T? Nullable<T>(JsonElement e, Func<JsonElement, T> read) where T : class =>
        e.ValueKind == JsonValueKind.Null ? null : read(e);

    private static T[] Array<T>(JsonElement e, Func<JsonElement, T> read)
    {
        if (e.ValueKind == JsonValueKind.Null) return null!;
        if (e.ValueKind != JsonValueKind.Array) throw new JsonException();
        var items = new T[e.GetArrayLength()];
        int i = 0;
        foreach (var item in e.EnumerateArray()) items[i++] = read(item);
        return items;
    }

    private static Dictionary<Guid, T> Dictionary<T>(JsonElement e, Func<JsonElement, T> read)
    {
        if (e.ValueKind != JsonValueKind.Object) throw new JsonException();
        var items = new Dictionary<Guid, T>();
        foreach (var property in e.EnumerateObject())
            items[JsonElements.GuidName(property.Name, out var key) ? key : throw new JsonException()] = read(property.Value);
        return items;
    }

    private static Guid Id(JsonElement e) => JsonElements.Guid(e, out var id) ? id : throw new JsonException();
    private static Guid? OptionalId(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : Id(e);
    private static int Int(JsonElement e) => JsonElements.Int32(e, out int v) ? v : throw new JsonException();
    private static float Float(JsonElement e) => JsonElements.Single(e, out float v) ? v : throw new JsonException();
    private static bool Bool(JsonElement e) => JsonElements.Boolean(e, out bool v) ? v : throw new JsonException();
    private static bool? OptionalBool(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : Bool(e);
    private static int Enum(JsonElement e) => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int v) ? v : throw new JsonException();
}
