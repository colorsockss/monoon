using VYaml.Annotations;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine.Core;

// Only the script identifier and Inspector overrides belong to the authored scene.
[YamlObject]
public sealed partial record ScriptComponent : SceneComponent
{
    [JsonRequired] public required string Script { get; init; }
    public Dictionary<string, JsonElement> Values { get => field ??= []; set; }
    internal override string Kind => "script:" + Script;

    internal IReadOnlyDictionary<Guid, Guid>? InstanceIds { get; set; }
    internal Guid InstanceId(Guid id) => InstanceIds?.GetValueOrDefault(id, id) ?? id;

    // Also used by the statically generated factory when a player loads older content.
    public string SerializedFieldName(string current, params ReadOnlySpan<string> formerNames)
    {
        if (!Values.ContainsKey(current))
            foreach (var name in formerNames)
                if (Values.ContainsKey(name)) return name;
        return current;
    }

    internal Guid[] ReadReferences(string name, bool sceneObjects)
    {
        if (!Values.TryGetValue(name, out var stored)) return [];
        if (stored.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"{Script}.{name} must be a list of references.");
        var ids = new Guid[stored.GetArrayLength()];
        int i = 0;
        foreach (var item in stored.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !item.TryGetGuid(out var id))
                throw new InvalidDataException($"{Script}.{name}[{i}] is not a reference.");
            ids[i++] = sceneObjects ? InstanceId(id) : id;
        }
        return ids;
    }

    internal void WriteReferences(string field, Guid[] ids) =>
        Values[field] = JsonSerializer.SerializeToElement(ids, GameJson.Default.GuidArray);

    public T[] ReadAssetList<T>(string name, T[] initial, Converter<Guid,T> wrap) =>
        Values.ContainsKey(name) ? Array.ConvertAll(ReadReferences(name, false), wrap) : initial;

    public T ReadEnum<T>(string name, T initial) where T : struct, Enum
    {
        if (!Values.TryGetValue(name, out var stored)) return initial;
        if (stored.ValueKind == JsonValueKind.String && Enum.GetNames<T>().Contains(stored.GetString()) &&
            Enum.TryParse<T>(stored.GetString(), out var value)) return value;
        throw new InvalidDataException($"{Script}.{name} is not a declared {typeof(T).Name} value.");
    }

    public Vector2 ReadVector(string name, Vector2 initial)
    {
        if (!Values.TryGetValue(name, out var stored)) return initial;
        if (stored.ValueKind == JsonValueKind.Object && Axis("x", out var x) && Axis("y", out var y)) return new(x,y);
        throw new InvalidDataException($"{Script}.{name} requires finite x and y coordinates.");
        bool Axis(string axis, out float n)
        {
            n = 0;
            return stored.TryGetProperty(axis,out var v) && v.ValueKind == JsonValueKind.Number &&
                v.TryGetSingle(out n) && float.IsFinite(n);
        }
    }

    public T ReadOverride<T>(string name, T initialValue)
    {
        if (!Values.TryGetValue(name, out var stored)) return initialValue;
        if (typeof(T) == typeof(Guid[])) return (T)(object)ReadReferences(name, true);
        try
        {
            var info = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)GameJson.Default.GetTypeInfo(typeof(T))!;
            var value = stored.Deserialize(info) ?? initialValue;
            return value is Guid id ? (T)(object)InstanceId(id) : value;
        }
        catch (JsonException) { return initialValue; }
    }

    internal ScriptComponent Duplicate(IReadOnlyDictionary<Guid, SceneObject> copies, ScriptLibrary? library)
    {
        var copy = this with
        {
            EventLinks = EventLinks?.Select(link => copies.TryGetValue(InstanceId(link.Target), out var target)
                ? link with { Target = target.Id } : link).ToArray(),
            Values = Values.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), Values.Comparer)
        };
        bool PointsIntoCopy(JsonElement value) =>
            value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id) && copies.ContainsKey(id);
        if (library == null || !Values.Values.Any(v => PointsIntoCopy(v) ||
                v.ValueKind == JsonValueKind.Array && v.EnumerateArray().Any(PointsIntoCopy))) return copy;

        // A GUID-shaped string is also a valid text field. Only rewrite declared object references.
        ScriptDefinition definition;
        try { definition = library.Get(Script); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return copy; // Preserve serialized overrides when the script is missing or cannot compile.
        }
        foreach (var field in definition.Fields)
            if (field.Type == typeof(Guid) && field.Value(this) is Guid id && copies.TryGetValue(id, out var target))
                copy.Values[field.Name] = JsonSerializer.SerializeToElement(target.Id, GameJson.Default.Guid);
            else if (field.IsObjectList && Values.ContainsKey(field.Name))
                copy.WriteReferences(field.Name, ReadReferences(field.Name, true)
                    .Select(entry => copies.TryGetValue(entry, out var obj) ? obj.Id : entry).ToArray());
        return copy;
    }
}

public abstract partial class GameBehaviour
{
    public virtual void OnCreate(ScriptLife life) { }
    public virtual void OnEnable(ScriptLife life) { }
    public virtual void OnDisable(ScriptLife life) { }
    public virtual void OnDestroy(ScriptLife life) { }
    // A pooled prefab instance: after OnEnable on every Rent, before OnDisable on Return.
    public virtual void OnRent(ScriptLife life) { }
    public virtual void OnReturn(ScriptLife life) { }
    public virtual void UpdateUi(UiTick tick) { }
    public virtual void FixedUpdate(ScriptTick tick) { }
    public virtual void OnAnimationEvent(ScriptAnimationEvent frame) { }
    public virtual void OnUiValueChanged(UiValueChanged change) { }
    public virtual void OnUiClick(UiClick click) { }
    public virtual void OnTriggerEnter(ScriptContact contact) { }
    public virtual void OnTriggerStay(ScriptContact contact) { }
    public virtual void OnTriggerExit(ScriptContact contact) { }
    public virtual void OnCollisionEnter(ScriptContact contact) { }
    public virtual void OnCollisionStay(ScriptContact contact) { }
    public virtual void OnCollisionExit(ScriptContact contact) { }
}

public readonly record struct ScriptAnimationEvent(Guid ObjectId, AnimationMarker Marker, SessionState Session, IGameObjects Objects, IGameUi Ui);

public readonly record struct UiTick(Guid ObjectId, SessionState Session, IGameUi Ui, GameInput Input, double DeltaTime)
{
    public GameTime Time => Session.Time;
}

public readonly record struct ScriptLife(Guid ObjectId, SessionState Session, IGameObjects Objects, IGameUi Ui);

// A scene-local handle; resolves the current instance, or null after its owner was destroyed.
public readonly record struct ComponentReference<T>(Guid ObjectId) where T : class
{
    public T? Resolve(IGameObjects objects) => objects.GetComponent<T>(ObjectId);
}

public readonly struct ScriptTick
{
    private readonly SceneGame.Body body;
    public SessionState Session { get; }
    public IGameCameras Cameras { get; }
    public IGameObjects Objects { get; }
    public IGameUi Ui { get; }
    internal ScriptTick(SceneGame game, SceneGame.Body body, GameInput input, float seconds) =>
        (Session, Cameras, Ui, Objects, this.body, Input, DeltaTime) = (game.State, game, game.Ui, game, body, input, seconds);

    public Guid ObjectId => body.Id;
    public Vector2 Position => body.Position;
    public Matrix3x2 WorldMatrix => body.MatrixAt(body.Position);
    public Vector2 LocalToWorld(Vector2 point) => Vector2.Transform(point, WorldMatrix);
    public Vector2 WorldToLocal(Vector2 point) => Vector2.Transform(point, Affine2D.Inverse(WorldMatrix));
    public GameInput Input { get; }
    public Vector2 Direction => Input.Move;
    public float DeltaTime { get; }
    public void Move(Vector2 displacement)
    {
        var total = body.Displacement + displacement;
        if (!float.IsFinite(total.X) || !float.IsFinite(total.Y)) throw new ArgumentException("Movement must be finite.");
        body.Displacement = total;
    }
}

public readonly struct ScriptContact
{
    private readonly SceneGame game;
    internal ScriptContact(SceneGame game, Guid self, Guid other) => (this.game, ObjectId, OtherId) = (game, self, other);
    public Guid ObjectId { get; }
    public IGameCameras Cameras => game;
    public IGameObjects Objects => game;
    public IGameUi Ui => game.Ui;
    public SessionState Session => game.State;
    public Guid OtherId { get; }
    public void PlayEffect(Guid id) => game.PlayEffect(id);
    public void Deactivate(Guid id) => game.Deactivate(id);
}
