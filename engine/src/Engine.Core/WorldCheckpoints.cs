using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine.Core;

[Flags]
public enum WorldSaveFields { Existence = 0, Transform = 1, Active = 2, Collider = 4, All = Transform | Active | Collider }

internal sealed record WorldCheckpoint(int Version, Dictionary<Guid, SavedSceneWorld> Scenes);
internal sealed record SavedSceneWorld(SavedObject[] Objects, SavedPrefab[] Prefabs);
internal sealed record SavedPrefab(Guid Asset, Guid? Parent, float SpawnX, float SpawnY, Dictionary<Guid, Guid> Members, SavedObject[] Objects);
internal sealed record SavedObject(Guid Id, WorldSaveFields Fields, bool Removed, SavedPose? Pose, bool? Active, bool? Collider,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SavedParent? Reparented = null);
internal sealed record SavedParent(Guid? Id);
internal sealed record SavedPose(float X, float Y, float Angle,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SavedBasis? Basis = null);
internal sealed record SavedBasis(float M11, float M12, float M21, float M22, bool NegativeX)
{
    internal Matrix3x2 At(Vector2 origin) => new(M11, M12, M21, M22, origin.X, origin.Y);
}

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
        bool extended = scenes.Values.Any(scene => scene.Objects.Any(NeedsHierarchyPose) ||
            scene.Prefabs.Any(prefab => prefab.Objects.Any(NeedsHierarchyPose)));
        data.Values[key] = JsonSerializer.SerializeToElement(new WorldCheckpoint(extended ? 2 : 1, scenes), WorldCheckpointJson.Default.WorldCheckpoint);

        static bool NeedsHierarchyPose(SavedObject obj) => obj.Reparented != null || obj.Pose?.Basis != null;
    }

    public void LoadSavedScene(Guid room, GameData data, Action<SessionState>? onEntered = null, string key = "world")
    {
        Dictionary<Guid, SavedSceneWorld> scenes = [];
        if (data.Values.TryGetValue(key, out var value))
        {
            WorldCheckpoint? checkpoint;
            try { checkpoint = value.Deserialize(WorldCheckpointJson.Default.WorldCheckpoint); }
            catch (JsonException ex) { throw new InvalidDataException("Invalid world checkpoint.", ex); }
            if (checkpoint is not { Version: 1 or 2, Scenes: not null } || checkpoint.Scenes.ContainsKey(Guid.Empty))
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
        var expanded = new HashSet<Guid>();
        Track(body, fields);
        void Track(Body member, WorldSaveFields selected)
        {
            const WorldSaveFields ancestor = WorldSaveFields.Transform | WorldSaveFields.Active;
            if (member.Origin is {} origin)
            {
                origin.Fields = origin.Fields.GetValueOrDefault() | selected;
                if (!expanded.Add(origin.Root)) return;
                foreach (var instance in origin.Members.Values)
                    if (byId.TryGetValue(instance, out var child) && !removed.Contains(instance) &&
                        child.Source.ParentId is Guid parent && byId[parent].Origin != origin) Track(byId[parent], ancestor);
            }
            else
            {
                trackedObjects[member.Id] = trackedObjects.GetValueOrDefault(member.Id) | selected;
                if (expanded.Add(member.Id) && member.Source.ParentId is Guid parent) Track(byId[parent], ancestor);
            }
        }
    }

    internal SavedSceneWorld? CaptureWorld()
    {
        if (!worldTracked) return null;
        var objects = trackedObjects.Select(entry => Capture(entry.Key, entry.Value)).ToArray();
        var prefabs = new List<SavedPrefab>();
        var captured = new HashSet<Guid>();
        foreach (var obj in world.ParentFirst())
        {
            if (removed.Contains(obj.Id) || byId[obj.Id].Origin is not { Fields: {} fields } origin || !captured.Add(origin.Root)) continue;
            var root = byId.GetValueOrDefault(origin.Root)?.Source;
            var spawn = root?.LocalMatrix.Translation ?? Vector2.Zero;
            prefabs.Add(new(origin.Asset, root?.ParentId, spawn.X, spawn.Y, origin.Members,
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
            SavedBasis? basis = null;
            if (body.EditedBasis)
            {
                var local = CurrentLocal(body);
                basis = new(local.M11, local.M12, local.M21, local.M22, body.Source.ScaleX < 0);
            }
            pose = new(position.X, position.Y, body.Angle, basis);
        }
        return new(id, fields, false, pose, fields.HasFlag(WorldSaveFields.Active) ? body.ActiveSelf : null,
            fields.HasFlag(WorldSaveFields.Collider) && body.ColliderConfig != null ? body.ColliderEnabled : null,
            pose != null && body.Origin?.Root != id && body.Source.ParentId != body.InitialParent ? new(body.Source.ParentId) : null);
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
                obj.Reparented != null && (obj.Pose == null || obj.Reparented.Id == Guid.Empty || obj.Reparented.Id == obj.Id) ||
                obj.Pose is {} pose && (!float.IsFinite(pose.X) || !float.IsFinite(pose.Y) || !float.IsFinite(pose.Angle) ||
                    pose.Basis != null && !Invertible(pose.Basis.At(new(pose.X, pose.Y)))))
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
        // Instantiate all members before resolving links: an authored object can now be
        // parented to a spawned object, and members of different instances can interleave.
        var parents = new Dictionary<Guid, Guid?>();
        foreach (var prefab in saved.Prefabs)
        {
            var root = Instantiate(new(prefab.Asset), new(prefab.SpawnX, prefab.SpawnY), null, prefab.Members);
            parents.Add(root, prefab.Parent);
            byId[root].Origin!.Fields = prefab.Objects[0].Fields;
            foreach (var obj in prefab.Objects) states.Add(obj.Id, obj);
        }
        foreach (var obj in states.Values)
            if (obj.Reparented is {} link) parents[obj.Id] = link.Id;
        foreach (var (id, parent) in parents)
        {
            if (parent.HasValue && !byId.ContainsKey(parent.Value)) throw new InvalidDataException($"Missing saved parent {parent}.");
            byId[id].Source.SetParent(parent is Guid p ? byId[p].Source : null);
        }
        if (parents.Count > 0)
        {
            if (world.ParentFirst().Count() != world.Objects.Count) throw new InvalidDataException("Cycle in saved object parents.");
            dirty = true;
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
            float angle = obj?.Pose?.Angle ?? parent?.Angle ?? 0;
            if (obj?.Pose is {} pose) local = new(pose.X, pose.Y);
            var matrix = body.Source.LocalMatrix;
            if (obj?.Pose?.Basis is {} basis)
            {
                matrix = basis.At(local);
                body.Source.ScaleX = basis.NegativeX ? -1 : 1;
                body.Source.SetLocalMatrix(matrix);
                body.EditedBasis = true;
            }
            matrix.Translation = local;
            matrix *= ParentMatrix(body);
            if (!body.EditedBasis && angle != (parent?.Angle ?? 0))
            {
                var position = matrix.Translation;
                matrix *= Matrix3x2.CreateRotation(angle - (parent?.Angle ?? 0));
                matrix.Translation = position;
            }
            body.SetWorldPose(matrix, angle);
            body.ActiveSelf = obj?.Active ?? body.ActiveSelf;
            body.Active = body.ActiveSelf && (parent?.Active ?? true);
            body.ColliderEnabled = obj?.Collider ?? body.ColliderEnabled;
        }
        // Queue creation only for survivors and let OnCreate see the final hierarchy and values.
        notifications.Clear();
        RegisterLife(parentFirst, initializeAnimation: false);
        UpdateTileWorlds();
        HasRestoredWorld = worldTracked = true;
    }
}
