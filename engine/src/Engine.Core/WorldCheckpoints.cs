using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine.Core;

[Flags]
public enum WorldSaveFields { Existence = 0, Transform = 1, Active = 2, Collider = 4, All = Transform | Active | Collider }

internal sealed record WorldCheckpoint(int Version, Dictionary<Guid, SavedSceneWorld> Scenes);
internal sealed record SavedSceneWorld(SavedObject[] Objects, SavedPrefab[] Prefabs);
internal sealed record SavedPrefab(Guid Asset, Guid? Parent, float SpawnX, float SpawnY, Dictionary<Guid, Guid> Members, SavedObject[] Objects);
internal sealed record SavedObject(Guid Id, WorldSaveFields Fields, bool Removed, SavedPose? Pose, bool? Active, bool? Collider);
internal sealed record SavedPose(float X, float Y, float Angle);

// Every constructor argument is part of the on-disk schema, including nullable overrides.
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, RespectRequiredConstructorParameters = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(WorldCheckpoint))]
internal partial class WorldCheckpointJson : JsonSerializerContext;

public sealed partial class SessionState
{
    internal SceneGame? LiveWorld;
    private Dictionary<Guid, SavedSceneWorld> rememberedWorlds = [];

    // Include visited primary scenes so saving in another room keeps the first room's loot.
    public void SaveWorld(GameData data, string key = "world")
    {
        var scenes = new Dictionary<Guid, SavedSceneWorld>(rememberedWorlds);
        if (SceneId is Guid id && LiveWorld?.CaptureWorld() is {} current) scenes[id] = current;
        if (scenes.Count == 0) { data.Values.Remove(key); return; }
        data.Values[key] = JsonSerializer.SerializeToElement(new WorldCheckpoint(1, scenes), WorldCheckpointJson.Default.WorldCheckpoint);
    }

    public void LoadSavedScene(Guid room, GameData data, Action<SessionState>? onEntered = null, string key = "world")
    {
        Dictionary<Guid, SavedSceneWorld> scenes = [];
        if (data.Values.TryGetValue(key, out var value))
        {
            WorldCheckpoint? checkpoint;
            try { checkpoint = value.Deserialize(WorldCheckpointJson.Default.WorldCheckpoint); }
            catch (JsonException ex) { throw new InvalidDataException("Invalid world checkpoint.", ex); }
            if (checkpoint is not { Version: 1, Scenes: not null } || checkpoint.Scenes.ContainsKey(Guid.Empty))
                throw new InvalidDataException("Unsupported world checkpoint.");
            foreach (var scene in checkpoint.Scenes.Values) SceneGame.ValidateWorld(scene);
            scenes = checkpoint.Scenes;
        }
        PendingScene?.Cancel();
        Requested = new(room, SceneChange.Replace, null, onEntered, scenes);
    }

    internal SavedSceneWorld? WorldForEntry(SceneRequest request)
    {
        if (request.Worlds != null) return request.Worlds.GetValueOrDefault(request.Id);
        return SceneId == request.Id ? LiveWorld?.CaptureWorld() : rememberedWorlds.GetValueOrDefault(request.Id);
    }

    internal void SwitchWorld(SceneRequest request, SceneGame next, SavedSceneWorld? outgoing)
    {
        if (request.Worlds != null) rememberedWorlds = request.Worlds;
        else if (SceneId is Guid previous && outgoing != null) rememberedWorlds[previous] = outgoing;
        LiveWorld = next;
    }
}

public sealed partial class SceneGame
{
    internal sealed record SpawnOrigin(Guid Asset, Guid Root, Dictionary<Guid, Guid> Members)
    {
        internal WorldSaveFields? Fields;
    }

    private readonly Dictionary<Guid, WorldSaveFields> trackedObjects = [];
    private bool worldTracked;
    public bool HasRestoredWorld { get; private set; }

    // Selecting any prefab member tracks the instance, including its source-to-instance references.
    public void TrackForSave(Guid id, WorldSaveFields fields = WorldSaveFields.All)
    {
        if ((fields & ~WorldSaveFields.All) != 0) throw new ArgumentOutOfRangeException(nameof(fields));
        if (!byId.TryGetValue(id, out var body) || removed.Contains(id) || body.SceneOwner.HasValue)
            throw new ArgumentException("Choose a live object in the primary scene.", nameof(id));
        worldTracked = true;
        while (true)
        {
            if (body.Origin is {} origin)
            {
                origin.Fields = origin.Fields.GetValueOrDefault() | fields;
                body = byId[origin.Root];
            }
            else trackedObjects[body.Id] = trackedObjects.GetValueOrDefault(body.Id) | fields;
            if (body.Source.ParentId is not Guid parent) break;
            body = byId[parent];
            fields = WorldSaveFields.Transform | WorldSaveFields.Active;
        }
    }

    internal SavedSceneWorld? CaptureWorld()
    {
        if (!worldTracked) return null;
        var objects = trackedObjects.Select(entry => Capture(entry.Key, entry.Value)).ToArray();
        var prefabs = new List<SavedPrefab>();
        foreach (var obj in world.ParentFirst())
        {
            if (removed.Contains(obj.Id) || byId[obj.Id].Origin is not { Fields: {} fields } origin || origin.Root != obj.Id) continue;
            var spawn = obj.LocalMatrix.Translation;
            prefabs.Add(new(origin.Asset, obj.ParentId, spawn.X, spawn.Y, origin.Members,
                origin.Members.Values.Select(id => Capture(id, fields)).ToArray()));
        }
        return new(objects, prefabs.ToArray());
    }

    private SavedObject Capture(Guid id, WorldSaveFields fields)
    {
        if (removed.Contains(id) || !byId.TryGetValue(id, out var body)) return new(id, fields, true, null, null, null);
        SavedPose? pose = null;
        if (fields.HasFlag(WorldSaveFields.Transform))
        {
            var position = body.Position;
            if (body.Source.ParentId is Guid parent)
            {
                var owner = byId[parent];
                position = Vector2.Transform(position, Affine2D.Inverse(owner.MatrixAt(owner.Position)));
            }
            pose = new(position.X, position.Y, body.Angle);
        }
        return new(id, fields, false, pose, fields.HasFlag(WorldSaveFields.Active) ? body.ActiveSelf : null,
            fields.HasFlag(WorldSaveFields.Collider) && body.ColliderConfig != null ? body.ColliderEnabled : null);
    }

    internal static void ValidateWorld(SavedSceneWorld saved)
    {
        if (saved is not { Objects: not null, Prefabs: not null }) throw new InvalidDataException("Incomplete scene checkpoint.");
        var ids = new HashSet<Guid>();
        foreach (var obj in saved.Objects) Check(obj);
        foreach (var prefab in saved.Prefabs)
        {
            if (prefab is not { Members: { Count: > 0 }, Objects: { Length: > 0 } } || prefab.Asset == Guid.Empty ||
                prefab.Members.ContainsKey(Guid.Empty) || !float.IsFinite(prefab.SpawnX) || !float.IsFinite(prefab.SpawnY))
                throw new InvalidDataException("Incomplete saved prefab.");
            foreach (var obj in prefab.Objects) Check(obj);
            if (prefab.Members.Count != prefab.Objects.Length ||
                !prefab.Members.Values.ToHashSet().SetEquals(prefab.Objects.Select(o => o.Id)) ||
                prefab.Objects.Any(o => o.Fields != prefab.Objects[0].Fields))
                throw new InvalidDataException("Invalid saved prefab member map.");
        }
        void Check(SavedObject obj)
        {
            if (obj == null || obj.Id == Guid.Empty || !ids.Add(obj.Id) || (obj.Fields & ~WorldSaveFields.All) != 0 ||
                (obj.Pose != null) != (!obj.Removed && obj.Fields.HasFlag(WorldSaveFields.Transform)) ||
                (obj.Active != null) != (!obj.Removed && obj.Fields.HasFlag(WorldSaveFields.Active)) ||
                obj.Collider != null && (obj.Removed || !obj.Fields.HasFlag(WorldSaveFields.Collider)) ||
                obj.Pose is {} pose && (!float.IsFinite(pose.X) || !float.IsFinite(pose.Y) || !float.IsFinite(pose.Angle)))
                throw new InvalidDataException("Invalid saved object state.");
        }
    }

    // Only called on a fresh stage. Validation/preparation failures leave the playing scene intact.
    internal void RestoreWorld(SavedSceneWorld saved)
    {
        var states = new Dictionary<Guid, SavedObject>();
        foreach (var obj in saved.Objects)
        {
            if (!byId.ContainsKey(obj.Id)) throw new InvalidDataException($"Missing authored object {obj.Id}.");
            trackedObjects.Add(obj.Id, obj.Fields);
            states.Add(obj.Id, obj);
        }
        // Capture emits parent instances before children, including independently spawned nested prefabs.
        foreach (var prefab in saved.Prefabs)
        {
            var position = new Vector2(prefab.SpawnX, prefab.SpawnY);
            if (prefab.Parent is Guid parent)
            {
                if (!byId.TryGetValue(parent, out var owner)) throw new InvalidDataException($"Missing saved prefab parent {parent}.");
                position = Vector2.Transform(position, owner.MatrixAt(owner.Position));
            }
            var root = Instantiate(new(prefab.Asset), position, prefab.Parent, prefab.Members);
            byId[root].Origin!.Fields = prefab.Objects[0].Fields;
            foreach (var obj in prefab.Objects) states.Add(obj.Id, obj);
        }
        foreach (var obj in states.Values) if (obj.Removed) Destroy(obj.Id);
        CommitChanges();
        foreach (var obj in states.Values)
            if (!obj.Removed && !byId.ContainsKey(obj.Id)) throw new InvalidDataException($"Saved object {obj.Id} has a removed parent.");

        foreach (var body in parentFirst)
        {
            var obj = states.GetValueOrDefault(body.Id);
            var parent = body.Source.ParentId is Guid p ? byId[p] : null;
            var local = body.Source.LocalMatrix.Translation;
            body.Angle = parent?.Angle ?? 0;
            if (obj?.Pose is {} pose) { local = new(pose.X, pose.Y); body.Angle = pose.Angle; }
            body.Position = parent == null ? local : Vector2.Transform(local, parent.MatrixAt(parent.Position));
            body.ActiveSelf = obj?.Active ?? body.ActiveSelf;
            body.Active = body.ActiveSelf && (parent?.Active ?? true);
            body.ColliderEnabled = obj?.Collider ?? body.ColliderEnabled;
            body.BeginStep();
        }
        // Queue creation only for survivors and let OnCreate see the final hierarchy and values.
        notifications.Clear();
        RegisterLife(parentFirst);
        UpdateTileWorlds();
        HasRestoredWorld = worldTracked = true;
    }
}
