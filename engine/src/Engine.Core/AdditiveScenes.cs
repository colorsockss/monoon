using System.Collections.Immutable;
namespace Engine.Core;

public sealed partial class SceneGame
{
    internal event Action? AssetsChanged;
    private Guid? executingScene;
    private Guid? unloadingScene;
    private readonly struct SceneScope(SceneGame game, Guid? previous) : IDisposable
    {
        public void Dispose() => game.executingScene = previous;
    }

    private SceneScope EnterScene(Body body)
    {
        var scope = new SceneScope(this, executingScene);
        executingScene = body.SceneOwner == unloadingScene ? null : body.SceneOwner;
        return scope;
    }

    // Authored duplicates (e.g. a copied scene) keep their local links, even when
    // a primary scene or runtime prefab already owns one of their object IDs.
    internal void ResolveAdditiveIds(SceneDocument scene)
    {
        if (!scene.Objects.Any(o => byId.ContainsKey(o.Id))) return;
        var cameraId = scene.DefaultCameraId;
        var copies = scene.Objects.ToDictionary(o => o.Id, o => o.CopyTransform(byId.ContainsKey(o.Id) ? Guid.NewGuid() : o.Id));
        var ids = copies.ToDictionary(p => p.Key, p => p.Value.Id);
        foreach (var source in scene.Objects)
        {
            var copy = copies[source.Id];
            copy.SetParent(source.ParentId is Guid parent ? copies[parent] : null);
            copy.Components = source.Components.Select(c=>c is ScriptComponent sc ? sc with { InstanceIds = ids } : c is Camera2DComponent { Follow: Guid link } cam && ids.TryGetValue(link,out var mapped)
                ? cam with { Follow=mapped } : c is Tilemap2DComponent { StreamTarget: Guid anchor } map && ids.TryGetValue(anchor,out var focus) ? map with { StreamTarget=focus } : c is DistanceJoint2DComponent { Connected: Guid endpoint } joint && ids.TryGetValue(endpoint, out var other)
                    ? joint with { Connected = other } : c).ToImmutableArray();
            if (source.Prefab is {} prefab)
                copy.Prefab = prefab with { Instances = prefab.Instances.ToDictionary(p => p.Key,p => ids.GetValueOrDefault(p.Value,p.Value)) };
        }
        scene.Objects = scene.Objects.Select(o => copies[o.Id]).ToList();
        scene.DefaultCameraId = cameraId.HasValue ? ids[cameraId.Value] : null;
    }

    internal void Attach(Guid sceneId, SceneGame incoming)
    {
        if (incoming.bodies.Any(b => byId.ContainsKey(b.Id)))
            throw new InvalidDataException("An incoming scene object ID is already in use.");
        foreach (var body in incoming.bodies)
        {
            if (body.Stream is {} stream && incoming.ownedWorlds.Remove(stream)) ownedWorlds.Add(stream);
            body.SceneOwner = sceneId;
            BindFrameEvents(body);
            RegisterBody(body);
            world.Objects.Add(body.Source);
        }
        Reindex();
        camera ??= incoming.camera;
        Ui.Rebuild(world, assets);
        RegisterLife(incoming.parentFirst);
        HierarchyRevision++;
    }

    internal void Detach(Guid sceneId)
    {
        unloadingScene = sceneId;
        try
        {
            // Mark all owners before running destruction callbacks. Standalone objects
            // deliberately created by teardown callbacks belong to the remaining world.
            foreach (var body in parentFirst.Reverse().Where(b => b.SceneOwner == sceneId))
            {
                if (!MarkRemoved(body.Source)) continue;
                if (body.Active) QueueLife(body, LifeEvent.Disable);
                body.Active = false;
                Silence(body);
                QueueLife(body, LifeEvent.Destroy);
            }
            dirty = true;
            FlushLife();
        }
        finally
        {
            unloadingScene = null;
            ReleaseSceneData(sceneId);
            CommitChanges();
            if (camera?.Active != true)
                camera = defaultCamera?.Active == true ? defaultCamera : bodies.FirstOrDefault(b => b.Active && b.Camera?.Output == Camera2DComponent.Primary);
        }
    }
}
