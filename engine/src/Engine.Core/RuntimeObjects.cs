using System.Collections.Immutable;
using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public readonly record struct PrefabReference(Guid Id);

public interface IGameObjects
{
    // Position is in world coordinates, including when a parent is supplied.
    Guid Instantiate(PrefabReference prefab, Vector2 position, Guid? parent = null);
    bool HasRestoredWorld { get; }
    void TrackForSave(Guid id, WorldSaveFields fields = WorldSaveFields.All);
    bool Destroy(Guid id);
    // Pooled prefab instances: Return deactivates and resets; Rent reuses one or instantiates a new one.
    Guid Rent(PrefabReference prefab, Vector2 position, Guid? parent = null);
    bool Return(Guid id);
    PrefabPoolStats PoolStats(PrefabReference prefab);
    bool TryMoveStep(Guid id, Vector2 displacement);
    SpritePool2D CreateSpritePool(Guid owner, int capacity);
    bool EmitParticles(Guid id);
    // Starts a Position Tween 2D or Path Follower 2D whose Play on start is off.
    bool PlayTween(Guid id);
    // Line 2D: world points replace the line's points; reading returns the drawn (flattened) points in world space.
    bool SetLinePoints(Guid id, ReadOnlySpan<Vector2> worldPoints);
    bool TryGetLinePoints(Guid id, List<Vector2> worldPoints);
    // Trail 2D: the points recorded on game ticks, oldest first, in world space.
    bool TryGetTrailPoints(Guid id, List<Vector2> worldPoints);
    bool PlayCutscene(Guid director);
    CutsceneState? Cutscene { get; }
    CutsceneExit? LastCutsceneExit { get; }
    bool TryGetBonePose(Guid id,string bone,out BonePose2D pose);
    bool SetSprite(Guid id, SpriteReference sprite);
    bool TryGetSpriteColor(Guid id, out Color color) { color = default; return false; }
    bool SetSpriteTint(Guid id, string? tint);
    bool SetColliderEnabled(Guid id, bool enabled);
    bool IsColliderEnabled(Guid id);
    // Runs steps until their first wait. Stops when the owner is disabled or removed, or the scene ends.
    ScriptRoutine StartRoutine(Guid owner, IEnumerable<ScriptWait> steps);
    bool SetActive(Guid id, bool active);
    bool IsActive(Guid id);
    IReadOnlyList<Guid> FindByTag(string tag);
    IReadOnlyList<Guid> FindByGroup(string group);
    bool TryGetPosition(Guid id, out Vector2 position);
    bool TryGetWorldMatrix(Guid id, out Matrix3x2 transform);
    bool TryGetLocalTransform(Guid id, out LocalTransform2D transform);
    bool SetLocalTransform(Guid id, LocalTransform2D transform);
    bool TryGetParent(Guid id, out Guid? parent);
    bool SetParent(Guid id, Guid? parent, bool keepWorld = true);
    GameTable ReadTable(TableReference asset);
    GameData ReadData(DataReference asset);
    bool HasScript(Guid id, string script);
    T? GetComponent<T>(Guid id) where T : class;
    IGameAnimator? Animator(Guid id);
    bool TryGetTileStream(Guid layer, out TileStreamState state);
    bool ReplaceTiles(Guid layer, int width, int height, ReadOnlySpan<int> tiles);
    bool TryGetTile(Guid layer, Point cell, out TileInfo info);
    bool SetTile(Guid layer, Point cell, int tile);
    GridNavigation? TileNavigation(Guid layer, Rectangle area);
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
    private readonly ScriptLibrary? library;
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
        body.SpritePool = new(capacity, body.Size, body.Appearance);
        body.RenderGrid?.Mark(body.RenderOrdinal);
        return body.SpritePool;
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
            if (config is T && config is not ScriptComponent)
                return ((config is SpriteRendererComponent ? body.Renderer! : config) with { }) as T;
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

    public Guid Instantiate(PrefabReference prefab, Vector2 position, Guid? parent = null) =>
        Instantiate(prefab, position, parent, null);

    private Guid Instantiate(PrefabReference prefab, Vector2 position, Guid? parent, Dictionary<Guid, Guid>? restoredIds)
    {
        ObjectDisposedException.ThrowIf(closing,this);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) throw new ArgumentException("Position must be finite.");
        var owner = parent is Guid pid && byId.TryGetValue(pid, out var b) && !removed.Contains(pid) ? b : null;
        if (parent.HasValue && owner == null) throw new ArgumentException("Prefab parent is not in the current scene.");
        var template = ReadPrefab(prefab, out var path);
        var ids = restoredIds ?? template.Objects.ToDictionary(o => o.Id, _ => Guid.NewGuid());
        if (restoredIds != null && (ids.Count != template.Objects.Count || template.Objects.Any(o => !ids.ContainsKey(o.Id)) ||
            ids.Values.Any(id => id == Guid.Empty || byId.ContainsKey(id)) || ids.Values.Distinct().Count() != ids.Count))
            throw new InvalidDataException($"{path}: saved instance IDs do not match this prefab.");
        var origin = new SpawnOrigin(prefab.Id, ids[template.Objects.Single(o => o.Parent == null).Id], ids);
        var staged = new Dictionary<Guid, Body>();
        foreach (var src in template.ParentFirst())
        {
            var obj = src.CopyTransform(ids[src.Id]);
            obj.Components = src.Components.Select(Own).Select(c=>c is Camera2DComponent { Follow: Guid link } cam && ids.TryGetValue(link,out var mapped)
                ? cam with { Follow=mapped } : c is Tilemap2DComponent { StreamTarget: Guid anchor } map && ids.TryGetValue(anchor,out var focus) ? map with { StreamTarget=focus } : c is Joint2DComponent { Connected: Guid connected } joint && ids.TryGetValue(connected,out var jointTarget)
                    ? joint with { Connected=jointTarget } : c).ToImmutableArray();
            var p = src.Parent == null ? owner : staged[src.Parent.Id];
            obj.SetParent(p?.Source);
            if (src.Parent == null)
            {
                obj.SetOrigin(owner == null ? position : Affine2D.Transform(position, Affine2D.Inverse(owner.MatrixAt(owner.Position))));
            }
            foreach (var script in obj.Components.OfType<ScriptComponent>()) script.InstanceIds = ids;
            var body = CreateBody(obj);
            body.SpawnedFrom=path;
            body.Origin = origin;
            body.SceneOwner = owner == null ? executingScene : owner.SceneOwner;
            body.Active = p?.Active ?? true;
            body.SetWorldPose(obj.LocalMatrix * (p?.MatrixAt(p.Position) ?? Matrix3x2.Identity), p?.Angle ?? 0);
            staged.Add(src.Id, body);
        }
        // Runtime prefab resources can be absent from the scene's initial asset list.
        // Prepare this batch before publishing IDs or running creation callbacks.
        PreparingBodies?.Invoke(staged.Values);
        foreach (var body in staged.Values)
        {
            RegisterBody(body);
            world.Objects.Add(body.Source);
            added.Add(body);
        }
        dirty = true;
        RegisterLife(staged.Values);
        FlushLife();
        return origin.Root;
    }

    // Templates are parsed once per prefab and shared, so every instance gets its own component records and collections.
    private static SceneComponent Own(SceneComponent c) => c switch
    {
        ScriptComponent s => s with { Values = new(s.Values, s.Values.Comparer), EventLinks = (GameEventLink[]?)s.EventLinks?.Clone() },
        UiTextComponent t => t with { Icons = t.Icons is {} icons ? new(icons, icons.Comparer) : null,
            Translations = t.Translations is {} text ? new(text, text.Comparer) : null },
        UiDropdownComponent d => d with { Options = (string[])d.Options.Clone() },
        Tilemap2DComponent m => m with { ChunkFiles = (TileChunkRef[]?)m.ChunkFiles?.Clone() }, // Tile chunks are immutable and copied on write.
        _ => c with { },
    };

    private sealed record CachedPrefab(string Path, SceneDocument Template, (string File, DateTime Written)[] Files);
    private readonly Dictionary<Guid, CachedPrefab> prefabTemplates = [];

    // shortcut: a rewrite with the same timestamp is missed; compare content hashes if editors ever write within one tick.
    private static bool Unchanged((string File, DateTime Written)[] files)
    {
        foreach (var (file, written) in files) if (File.GetLastWriteTimeUtc(file) != written) return false;
        return true;
    }

    private SceneDocument ReadPrefab(PrefabReference prefab, out string path)
    {
        path = assets?.PathFor(prefab.Id)!;
        if (path == null || !path.EndsWith(ProjectAssets.Prefab, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Missing prefab {prefab.Id}.");
        if (prefabTemplates.TryGetValue(prefab.Id, out var cached) && cached.Path == path && Unchanged(cached.Files))
            return cached.Template;
        var project=world.ProjectDirectory!;
        var exported = File.Exists(Path.Combine(project,".monoon-player"));
        var file = Path.Combine(project,path);
        var stamps = new List<(string File, DateTime Written)> { (file, File.GetLastWriteTimeUtc(file)) }; // Before the read: a write during it is seen next time.
        var template = exported ? SceneDocument.ReadAsset(file) : SceneDocument.ReadResolvedPrefab(project,assets!,prefab.Id);
        template.ResolveAssets(assets!);
        if (template.Objects.Count(o => o.Parent == null) != 1) throw new InvalidDataException($"{path}: expected one root.");
        // Editor Play resolves nested prefabs from their own files; exported prefabs are already flattened.
        if (!exported)
            foreach (var obj in template.Objects)
                if (obj.Prefab?.AssetId is Guid id && assets!.PathFor(id) is {} relative &&
                    Path.Combine(project,relative) is var nested && !stamps.Exists(s => s.File == nested))
                    stamps.Add((nested, File.GetLastWriteTimeUtc(nested)));
        prefabTemplates[prefab.Id] = new(path, template, exported ? [] : stamps.ToArray()); // Exported content never changes while the game runs.
        return template;
    }

    public bool Destroy(Guid id)
    {
        if (closing || !byId.TryGetValue(id, out var root) || removed.Contains(id)) return false;
        foreach (var obj in world.ParentFirst(root.Source).Reverse())
        {
            if (!MarkRemoved(obj)) continue;
            var body = byId[obj.Id];
            if (body.Active) QueueLife(body,LifeEvent.Disable);
            body.Active = false;
            Silence(body);
            QueueLife(body,LifeEvent.Destroy);
        }
        dirty = true;
        Ui.ValidateInputModes();
        FlushLife();
        return true;
    }

    public bool IsColliderEnabled(Guid id) =>
        !removed.Contains(id) && byId.TryGetValue(id, out var body) && body.Collider != null;

    public bool SetColliderEnabled(Guid id, bool enabled)
    {
        if (closing || removed.Contains(id) || !byId.TryGetValue(id, out var body) || body.ColliderConfig == null) return false;
        if (body.ColliderEnabled == enabled) return true;
        body.ColliderEnabled = enabled;
        if (!enabled)
        {
            body.ContactRevision++;
            foreach (var rigid in rigidBodies)
                if (rigid.Support == body || rigid == body.MotionOwner) rigid.Support = null;
        }
        return true;
    }

    public bool SetSprite(Guid id, SpriteReference sprite)
    {
        if (closing || removed.Contains(id) || !byId.TryGetValue(id, out var body) || body.Renderer is not {} renderer) return false;
        string? path = null;
        if (sprite.Id != Guid.Empty)
        {
            if (world.ProjectDirectory is not {} root || assets == null) return false;
            path = assets.PathFor(sprite.Id);
            var packed = assets.Sprites(root).Find(sprite.Id);
            if (packed == null && (path == null || !SpriteAssets.IsReference(path) || !File.Exists(Path.Combine(root, path)))) return false;
        }
        Guid? assetId = sprite.Id == Guid.Empty ? null : sprite.Id;
        if (renderer.AssetId == assetId && renderer.Asset == path) return true;
        var next = renderer with { AssetId = assetId, Asset = path };
        var appearance = SceneAppearance.From(next, world.ProjectDirectory, assets);
        var previous = body.Appearance;
        body.Appearance = appearance;
        try { PreparingBodies?.Invoke([body]); }
        catch { body.Appearance = previous; throw; }
        body.Renderer = next;
        if (body.SpritePool != null) body.SpritePool.Appearance = appearance;
        AssetsChanged?.Invoke();
        return true;
    }

    public bool TryGetSpriteColor(Guid id, out Color color)
    {
        color = default;
        if (removed.Contains(id) || !byId.TryGetValue(id, out var body) || body.Renderer == null) return false;
        color = body.DisplayAppearance.Color;
        return true;
    }

    public bool SetSpriteTint(Guid id, string? tint)
    {
        if (closing || removed.Contains(id) || !byId.TryGetValue(id, out var body) || body.Renderer is not {} renderer) return false;
        if (tint != null && !SceneObject.TryColor(tint, out _)) return false;
        if (renderer.Tint == tint) return true;
        var next = renderer with { Tint = tint };
        body.Appearance = SceneAppearance.From(next, world.ProjectDirectory, assets);
        body.Renderer = next;
        if (body.SpritePool != null) body.SpritePool.Appearance = body.Appearance;
        return true;
    }

    public bool SetActive(Guid id, bool active)
    {
        if (closing || !byId.TryGetValue(id, out var root) || removed.Contains(id)) return false;
        if (root.ActiveSelf == active) return true;
        root.ActiveSelf = active;
        RefreshActive(root);
        return true;
    }

    private void RefreshActive(Body root)
    {
        var changed = new List<Body>();
        foreach (var obj in world.ParentFirst(root.Source))
        {
            var body = byId[obj.Id];
            var next = !removed.Contains(obj.Id) && body.ActiveSelf && (obj.ParentId == null || byId[obj.ParentId.Value].Active);
            if (next != body.Active) { body.Active = next; if (!next) { body.ContactRevision++; Silence(body); body.Particles?.Reset(); body.Trail?.Clear(); } changed.Add(body); }
        }
        if (!root.Active) { changed.Reverse(); Ui.ValidateInputModes(); }
        foreach (var body in changed) QueueLife(body, body.Active ? LifeEvent.Enable : LifeEvent.Disable);
        HierarchyRevision++;
        FlushLife();
    }

    private void CommitChanges()
    {
        CommitPools();
        if (!dirty) return;
        world.Objects.RemoveAll(o => removed.Contains(o.Id));
        foreach (var id in removed)
            if (byId.Remove(id,out var b) && b.Stream is {} stream && ownedWorlds.Remove(stream)) stream.Dispose();
        // A parent may have moved later in the spawning tick. Newly registered bodies
        // start at their final parent transform and first participate in the next simulation step.
        foreach (var body in added)
        {
            if (removed.Contains(body.Id) || body.Source.ParentId is not Guid parent) continue;
            body.Position = Affine2D.Transform(body.Source.LocalMatrix.Translation, byId[parent].MatrixAt(byId[parent].Position));
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
        colliders = bodies.Where(b => b.ColliderConfig != null).ToArray();
        queryGrid = colliders.Length >= 32 ? new ColliderGrid(colliders) : null;
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].ColliderOrdinal = i;
            colliders[i].QueryGrid = queryGrid;
        }
        solids = colliders.Where(b => !b.ColliderConfig!.IsTrigger).ToArray();
        drawOrder = new WorldDrawOrder(world.Objects, DrawMatrix);
        renderGrid = bodies.Length >= 128 ? new RenderGrid(bodies) : null;
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].RenderGrid = renderGrid;
            bodies[i].RenderOrdinal = i;
        }
        foreach (var b in parentFirst)
            b.MotionOwner = b.Rigid != null ? b : b.Source.ParentId is Guid parent ? byId[parent].MotionOwner : null;
        rigidBodies = parentFirst.Where(b => b.Rigid != null).ToArray();
        joints = parentFirst.Where(b => b.Joints.Length > 0).ToArray();
        emitters = bodies.Where(b => b.Particles != null && !b.Interface).ToArray();
        lights = bodies.Where(b => b.Light != null && !b.Interface).ToArray();
        trails = bodies.Where(b => b.Trail != null && !b.Interface).ToArray();
        skeletons=bodies.Where(b=>b.Skeleton!=null && !b.Interface).ToArray();
        foreach (var b in skeletons) if (b.FootIK is {} ik) b.FootFilter=new(ik.Mask,Ignore:b.MotionOwner?.Id??b.Id);
        IndexCameras();
        IndexAudio();
        tileLayers = bodies.Where(b => b.Map != null).ToArray();
    }
}
