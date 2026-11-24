using System.Numerics;

namespace Engine.Core;

/// <summary>Counts for one prefab's runtime pool in the current world.</summary>
public readonly record struct PrefabPoolStats(int Active, int Available, int Created, int Reused, int Returned);

public sealed partial class SceneGame
{
    private sealed class PrefabPool(SceneObject[] template)
    {
        // Prefab objects in parent-first order; their local values are the reset pose.
        public readonly SceneObject[] Template = template;
        public readonly Stack<PooledInstance> Free = new();
        public int Created, Reused, Returned;
    }

    private sealed class PooledInstance(PrefabPool pool, Body[] members)
    {
        public readonly PrefabPool Pool = pool;
        public readonly Body[] Members = members; // Same order as Pool.Template.
        public bool Rented = true;
    }

    private readonly Dictionary<Guid, PrefabPool> prefabPools = [];
    private readonly Dictionary<Guid, PooledInstance> pooledRoots = [];
    private readonly List<Body> poolSpawns = [];

    public Guid Rent(PrefabReference prefab, Vector2 position, Guid? parent = null)
    {
        ObjectDisposedException.ThrowIf(closing, this);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) throw new ArgumentException("Position must be finite.");
        var owner = parent is Guid pid && byId.TryGetValue(pid, out var b) && !removed.Contains(pid) ? b : null;
        if (parent.HasValue && owner == null) throw new ArgumentException("Prefab parent is not in the current scene.");
        if (!prefabPools.TryGetValue(prefab.Id, out var pool))
            prefabPools.Add(prefab.Id, pool = new(ReadPrefab(prefab, out _).ParentFirst().ToArray()));
        while (pool.Free.TryPop(out var instance))
        {
            var root = instance.Members[0];
            if (!byId.ContainsKey(root.Id) || removed.Contains(root.Id)) continue; // Destroyed while pooled.
            Reuse(instance, position, owner);
            pool.Reused++;
            return root.Id;
        }
        var id = Instantiate(prefab, position, parent);
        var origin = byId[id].Origin!;
        var members = Array.ConvertAll(pool.Template, t => byId[origin.Members[t.Id]]);
        pooledRoots[id] = new(pool, members);
        pool.Created++;
        foreach (var member in members) QueueLife(member, LifeEvent.Rent);
        FlushLife();
        return id;
    }

    public bool Return(Guid id)
    {
        if (closing || removed.Contains(id) || !pooledRoots.TryGetValue(id, out var instance) || !instance.Rented) return false;
        instance.Rented = false;
        instance.Pool.Returned++;
        var root = instance.Members[0];
        foreach (var member in instance.Members) QueueLife(member, LifeEvent.Return);
        FlushLife();
        if (removed.Contains(id)) return true; // OnReturn destroyed the instance.
        if (root.Origin is {} origin) origin.Fields = null; // A pooled instance is not part of a world checkpoint.
        if (root.Source.ParentId != null) SetParent(id, null);
        SetPooledActive(root, false);
        foreach (var member in instance.Members) PoolRoles(member.Source, false);
        instance.Pool.Free.Push(instance);
        return true;
    }

    public PrefabPoolStats PoolStats(PrefabReference prefab)
    {
        if (!prefabPools.TryGetValue(prefab.Id, out var pool)) return default;
        int active = 0, available = 0;
        foreach (var (id, instance) in pooledRoots)
            if (instance.Pool == pool && byId.ContainsKey(id) && !removed.Contains(id))
            {
                if (instance.Rented) active++;
                else available++;
            }
        return new(active, available, pool.Created, pool.Reused, pool.Returned);
    }

    private void Reuse(PooledInstance instance, Vector2 position, Body? owner)
    {
        var members = instance.Members;
        var root = members[0];
        var scene = owner == null ? executingScene : owner.SceneOwner;
        foreach (var member in members) member.SceneOwner = scene;
        if (root.Source.ParentId != owner?.Id && !SetParent(root.Id, owner?.Id))
            throw new ArgumentException("Pooled prefab cannot be placed under this parent.");
        for (int i = 0; i < members.Length; i++)
        {
            var member = members[i];
            var authored = instance.Pool.Template[i];
            var local = member.Source;
            (local.X, local.Y, local.Rotation) = (authored.X, authored.Y, authored.Rotation);
            (local.ScaleX, local.ScaleY, local.Shear) = (authored.ScaleX, authored.ScaleY, authored.Shear);
            if (i == 0) local.SetOrigin(owner == null ? position : Affine2D.Transform(position, Affine2D.Inverse(owner.MatrixAt(owner.Position))));
            var parent = local.ParentId is Guid p ? byId[p] : null;
            member.SetWorldPose(local.LocalMatrix * (parent?.MatrixAt(parent.Position) ?? Matrix3x2.Identity), parent?.Angle ?? 0);
            member.EditedBasis = false;
            member.ActiveSelf = i == 0 || local.Active; // the authored state; the rented root is shown
            member.ColliderEnabled = member.ColliderConfig?.Enabled != false;
            member.Displacement = default;
            member.Trail?.Clear();
            member.Support = null;
            if (member.Rigid is {} rigid)
            {
                member.Velocity = rigid.BodyType == BodyType2D.Static ? default : new(rigid.VelocityX, rigid.VelocityY);
                member.Spin = rigid.BodyType == BodyType2D.Static || rigid.FreezeRotation || member.Motor != null ? 0 : float.DegreesToRadians(rigid.AngularVelocity);
            }
            PoolRoles(local, true);
            // Like a fresh instance, it first updates and touches other colliders on the next tick.
            member.Spawning = true;
            poolSpawns.Add(member);
        }
        instance.Rented = true;
        SetPooledActive(root, true, rent: members);
    }

    // RefreshActive without walking the scene document when the indexed hierarchy is current.
    private void SetPooledActive(Body root, bool active, Body[]? rent = null)
    {
        root.ActiveSelf = active;
        if (dirty || root.Index >= parentFirst.Length || parentFirst[root.Index] != root) RefreshActive(root);
        else
        {
            var subtree = Subtree(root);
            if (active)
                foreach (var body in subtree)
                {
                    bool next = !removed.Contains(body.Id) && body.ActiveSelf && (body.Source.ParentId is not Guid p || byId[p].Active);
                    if (next == body.Active) continue;
                    body.Active = next;
                    QueueLife(body, LifeEvent.Enable);
                }
            else
            {
                for (int i = subtree.Length - 1; i >= 0; i--)
                {
                    var body = subtree[i];
                    if (!body.Active) continue;
                    body.Active = false;
                    body.ContactRevision++;
                    Silence(body);
                    body.Particles?.Reset();
                    body.Trail?.Clear();
                    QueueLife(body, LifeEvent.Disable);
                }
                Ui.ValidateInputModes();
            }
            HierarchyRevision++;
        }
        if (rent != null) foreach (var member in rent) QueueLife(member, LifeEvent.Rent);
        FlushLife();
    }

    private void PoolRoles(SceneObject obj, bool add)
    {
        if (obj.Tag is {} tag) Role(MembersFor(RoleKind.Tag, tag), obj.Id, add);
        if (obj.Groups is {} groups)
            foreach (var group in groups) Role(MembersFor(RoleKind.Group, group), obj.Id, add);
        static void Role(Members members, Guid id, bool add) { if (add) members.Add(id); else members.Remove(id); }
    }

    // Called when the tick's structural changes are committed.
    private void CommitPools()
    {
        foreach (var body in poolSpawns) body.Spawning = false;
        poolSpawns.Clear();
        if (dirty) foreach (var id in removed) pooledRoots.Remove(id);
    }
}
