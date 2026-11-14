using System.Collections.Immutable;
using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public readonly record struct PrefabReference(Guid Id);

public interface IGameObjects
{
    // Position is in world coordinates, including when a parent is supplied.
    Guid Instantiate(PrefabReference prefab, Vector2 position, Guid? parent = null);
    bool Destroy(Guid id);
    bool TryMoveStep(Guid id, Vector2 displacement);
    SpritePool2D CreateSpritePool(Guid owner, int capacity);
    bool EmitParticles(Guid id);
    bool PlayTween(Guid id);
    bool PlayCutscene(Guid director);
    CutsceneState? Cutscene { get; }
    CutsceneExit? LastCutsceneExit { get; }
    bool TryGetBonePose(Guid id,string bone,out BonePose2D pose);
    bool SetActive(Guid id, bool active);
    bool IsActive(Guid id);
    bool TryGetPosition(Guid id, out Vector2 position);
    bool TryGetWorldMatrix(Guid id, out Matrix3x2 transform);
    GameTable ReadTable(TableReference asset);
    GameData ReadData(DataReference asset);
    bool HasScript(Guid id, string script);
    T? GetComponent<T>(Guid id) where T : class;
    IGameAnimator? Animator(Guid id);
    bool TryGetTileStream(Guid layer, out TileStreamState state);
    bool ReplaceTiles(Guid layer, int width, int height, ReadOnlySpan<int> tiles);
    bool IsTileChunkLoaded(Guid layer,int x,int y);
    IBodyMotion2D Motion { get; }
    IPhysics2D Physics { get; }
    INavigation2D Navigation { get; }
    IGameAudio Audio { get; }
}

public sealed partial class SceneGame
{
    private readonly SceneDocument world;
    private readonly ProjectAssets? assets;
    private readonly ProjectScripts? library;
    private readonly Func<ScriptComponent, GameBehaviour>? compiledScripts;
    private readonly Dictionary<Guid, Body> byId = [];
    private readonly Dictionary<Guid, TilePalette> palettes = [];
    private readonly Dictionary<string, Size> imageSizes = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> removed = [];
    private readonly List<Body> added = [];
    private bool dirty;

    public IReadOnlyList<SceneObject> Objects => world.Objects;
    public long HierarchyRevision { get; private set; }
    public bool IsActive(Guid id) => !removed.Contains(id) && byId.TryGetValue(id, out var body) && body.Active;
    public SpritePool2D CreateSpritePool(Guid owner, int capacity)
    {
        if (closing || removed.Contains(owner) || !byId.TryGetValue(owner, out var body) ||
            body.Interface || body.Appearance.Image == null)
            throw new InvalidOperationException("A sprite pool requires a live world object with a Sprite Renderer.");
        if (body.SpritePool != null) throw new InvalidOperationException("This object already owns a sprite pool.");
        return body.SpritePool = new(capacity, body.Size, body.Appearance);
    }

    public SpritePoolStats PoolStatistics(out int unbatchedPools)
    {
        unbatchedPools = 0;
        SpritePoolStats total = default;
        foreach (var body in bodies)
        {
            if (!body.Active || body.SpritePool is not {} pool) continue;
            var s = pool.Stats;
            if (!pool.Batched) unbatchedPools++;
            total = new(total.Active + s.Active, total.Capacity + s.Capacity, total.Peak + s.Peak,
                total.Spawned + s.Spawned, total.Returned + s.Returned, total.Reused + s.Reused, total.Dropped + s.Dropped);
        }
        return total;
    }

    private sealed record CachedData(GameData Data, HashSet<Guid?> Owners);
    private readonly Dictionary<Guid, CachedData> dataAssets = [];
    private void ReleaseSceneData(Guid sceneId)
    {
        foreach (var (id, cached) in dataAssets.ToArray())
            if (cached.Owners.Remove(sceneId) && cached.Owners.Count == 0) dataAssets.Remove(id);
    }
    public GameTable ReadTable(TableReference asset)
    {
        if (assets == null || world.ProjectDirectory == null) throw new InvalidDataException("Tables require a project.");
        return GameTable.Load(Path.Combine(world.ProjectDirectory,GameTable.AssetPath(assets,asset)));
    }
    public GameData ReadData(DataReference asset)
    {
        if (assets == null || world.ProjectDirectory == null) throw new InvalidDataException("Data assets require a project.");
        if (!dataAssets.TryGetValue(asset.Id, out var cached))
            dataAssets[asset.Id] = cached = new(GameData.Load(GameData.AssetPath(world.ProjectDirectory,assets,asset)), []);
        cached.Owners.Add(executingScene);
        return new GameData { Version = cached.Data.Version, Values = new(cached.Data.Values,StringComparer.Ordinal) };
    }
    public IGameAnimator? Animator(Guid id) => !removed.Contains(id) && byId.TryGetValue(id,out var body) ? body.Animation : null;
    public bool HasScript(Guid id, string script) => byId.TryGetValue(id, out var body) && !removed.Contains(id) &&
        body.Source.Components.OfType<ScriptComponent>().Any(c => c.Script == script);
    public T? GetComponent<T>(Guid id) where T : class
    {
        if (!byId.TryGetValue(id, out var body) || removed.Contains(id)) return null;
        foreach (var behaviour in body.Behaviours) if (behaviour is T instance) return instance;
        // Built-in components are configuration snapshots, never references into the authored scene.
        foreach (var config in body.Source.Components)
            if (config is T && config is not ScriptComponent) return (config with { }) as T;
        return null;
    }

    public bool TryGetWorldMatrix(Guid id, out Matrix3x2 transform)
    {
        if (byId.TryGetValue(id,out var b) && !removed.Contains(id)) { transform = b.MatrixAt(b.Position); return true; }
        transform = default; return false;
    }

    public bool TryGetPosition(Guid id, out Vector2 position)
    {
        if (byId.TryGetValue(id, out var body) && !removed.Contains(id)) { position = body.Position; return true; }
        position = default;
        return false;
    }

    public Guid Instantiate(PrefabReference prefab, Vector2 position, Guid? parent = null)
    {
        ObjectDisposedException.ThrowIf(closing,this);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) throw new ArgumentException("Position must be finite.");
        var owner = parent is Guid pid && byId.TryGetValue(pid, out var b) && !removed.Contains(pid) ? b : null;
        if (parent.HasValue && owner == null) throw new ArgumentException("Prefab parent is not in the current scene.");
        var path = assets?.PathFor(prefab.Id);
        if (path == null || !path.EndsWith(ProjectAssets.Prefab, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Missing prefab {prefab.Id}.");
        // Each read owns its mutable components (tile layers, script overrides, etc.).
        var project=world.ProjectDirectory!;
        var template = File.Exists(Path.Combine(project,".monoon-player"))
            ? SceneDocument.ReadAsset(Path.Combine(project,path))
            : SceneDocument.ReadResolvedPrefab(project,assets!,prefab.Id);
        template.ResolveAssets(assets!);
        if (template.Objects.Count(o => o.Parent == null) != 1) throw new InvalidDataException($"{path}: expected one root.");
        var ids = template.Objects.ToDictionary(o => o.Id, _ => Guid.NewGuid());
        var staged = new Dictionary<Guid, Body>();
        foreach (var src in template.ParentFirst())
        {
            var obj = src.CopyTransform(ids[src.Id]);
            obj.Components = src.Components.Select(c=>c is Camera2DComponent { Follow: Guid link } cam && ids.TryGetValue(link,out var mapped)
                ? cam with { Follow=mapped } : c is Tilemap2DComponent { StreamTarget: Guid anchor } map && ids.TryGetValue(anchor,out var focus) ? map with { StreamTarget=focus } : c is DistanceJoint2DComponent { Connected: Guid connected } joint && ids.TryGetValue(connected,out var jointTarget)
                    ? joint with { Connected=jointTarget } : c).ToImmutableArray();
            var p = src.Parent == null ? owner : staged[src.Parent.Id];
            obj.SetParent(p?.Source);
            if (src.Parent == null)
            {
                obj.SetOrigin(owner == null ? position : Vector2.Transform(position, Affine2D.Inverse(owner.MatrixAt(owner.Position))));
            }
            foreach (var script in obj.Components.OfType<ScriptComponent>()) script.InstanceIds = ids;
            var body = CreateBody(obj);
            body.SpawnedFrom=path;
            body.SceneOwner = owner == null ? executingScene : owner.SceneOwner;
            body.Active = p?.Active ?? true;
            body.Position = p == null ? obj.WorldPosition : Vector2.Transform(obj.LocalMatrix.Translation, p.MatrixAt(p.Position));
            body.BeginStep();
            staged.Add(src.Id, body);
        }
        // Runtime prefab resources can be absent from the scene's initial asset list.
        // Prepare this batch before publishing IDs or running creation callbacks.
        PreparingBodies?.Invoke(staged.Values);
        foreach (var body in staged.Values) { byId.Add(body.Id, body); world.Objects.Add(body.Source); added.Add(body); }
        dirty = true;
        RegisterLife(staged.Values);
        FlushLife();
        return ids[template.Objects.Single(o => o.Parent == null).Id];
    }

    public bool Destroy(Guid id)
    {
        if (closing || !byId.TryGetValue(id, out var root) || removed.Contains(id)) return false;
        foreach (var obj in world.ParentFirst(root.Source).Reverse())
        {
            if (!removed.Add(obj.Id)) continue;
            var body = byId[obj.Id];
            if (body.Active) QueueLife(body,LifeEvent.Disable);
            body.Active = false;
            Silence(body);
            QueueLife(body,LifeEvent.Destroy);
        }
        dirty = true;
        FlushLife();
        return true;
    }

    public bool SetActive(Guid id, bool active)
    {
        if (closing || !byId.TryGetValue(id, out var root) || removed.Contains(id)) return false;
        if (root.ActiveSelf == active) return true;
        root.ActiveSelf = active;
        var changed = new List<Body>();
        foreach (var obj in world.ParentFirst(root.Source))
        {
            var body = byId[obj.Id];
            var next = !removed.Contains(obj.Id) && body.ActiveSelf && (obj.ParentId == null || byId[obj.ParentId.Value].Active);
            if (next != body.Active) { body.Active = next; if (!next) { Silence(body); body.Particles?.Reset(); } changed.Add(body); }
        }
        if (!active) changed.Reverse();
        foreach (var body in changed) QueueLife(body, active ? LifeEvent.Enable : LifeEvent.Disable);
        overlapping.RemoveWhere(pair => !pair.Trigger.Active || !pair.Other.Active);
        HierarchyRevision++;
        FlushLife();
        return true;
    }

    private void CommitChanges()
    {
        if (!dirty) return;
        world.Objects.RemoveAll(o => removed.Contains(o.Id));
        foreach (var id in removed)
            if (byId.Remove(id,out var b) && b.Stream is {} stream && ownedWorlds.Remove(stream)) stream.Dispose();
        overlapping.RemoveWhere(p => removed.Contains(p.Trigger.Id) || removed.Contains(p.Other.Id));
        // A parent may have moved later in the spawning tick. Newly registered bodies
        // start at their final parent transform and first participate in the next simulation step.
        foreach (var body in added)
        {
            if (removed.Contains(body.Id) || body.Source.ParentId is not Guid parent) continue;
            body.Position = Vector2.Transform(body.Source.LocalMatrix.Translation, byId[parent].MatrixAt(byId[parent].Position));
            body.BeginStep();
        }
        removed.Clear();
        added.Clear();
        Reindex();
        Ui.Rebuild(world, assets);
        dirty = false;
        HierarchyRevision++;
        AssetsChanged?.Invoke();
    }

    private void Reindex()
    {
        bodies = world.Objects.Select(o => byId[o.Id]).ToArray();
        parentFirst = world.ParentFirst().Select(o => byId[o.Id]).ToArray();
        for (int i = 0; i < parentFirst.Length; i++) { parentFirst[i].Index = i; parentFirst[i].SubtreeEnd = i + 1; }
        for (int i = parentFirst.Length - 1; i >= 0; i--)
            if (parentFirst[i].Source.ParentId is Guid id)
                byId[id].SubtreeEnd = Math.Max(byId[id].SubtreeEnd, parentFirst[i].SubtreeEnd);
        colliders = bodies.Where(b => b.Collider != null).ToArray();
        solids = colliders.Where(b => !b.Collider!.IsTrigger).ToArray();
        triggers = colliders.Where(b => b.Collider!.IsTrigger).ToArray();
        drawOrder = world.WorldItems().Select(i => (byId[i.Owner.Id], i.Map != null)).ToArray();
        foreach (var b in parentFirst)
            b.MotionOwner = b.Rigid != null ? b : b.Source.ParentId is Guid parent ? byId[parent].MotionOwner : null;
        rigidBodies = parentFirst.Where(b => b.Rigid != null).ToArray();
        joints = parentFirst.Where(b => b.Joint != null).ToArray();
        emitters = bodies.Where(b => b.Particles != null && !b.Interface).ToArray();
        lights = bodies.Where(b => b.Light != null && !b.Interface).ToArray();
        skeletons=bodies.Where(b=>b.Skeleton!=null && !b.Interface).ToArray();
        foreach (var b in skeletons) if (b.FootIK is {} ik) b.FootFilter=new(ik.Mask,Ignore:b.MotionOwner?.Id??b.Id);
        IndexCameras();
        IndexAudio();
        tileLayers = bodies.Where(b => b.Map != null).ToArray();
    }
}
