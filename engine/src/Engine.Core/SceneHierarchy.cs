using System.Numerics;

namespace Engine.Core;

public sealed partial class SceneDocument
{
    private void ResolveHierarchy()
    {
        var byId = new Dictionary<Guid, SceneObject>();
        foreach (var obj in Objects)
            if (obj == null || obj.Id == Guid.Empty || !byId.TryAdd(obj.Id, obj))
                throw new InvalidDataException("Each object needs a unique ID.");

        var resolved = new HashSet<Guid>();
        foreach (var obj in Objects)
        {
            var chain = new List<SceneObject>();
            var visiting = new HashSet<Guid>();
            for (var node = obj; !resolved.Contains(node.Id);)
            {
                if (!visiting.Add(node.Id)) throw new InvalidDataException($"Parent cycle at '{node.Name}'.");
                chain.Add(node);
                if (node.ParentId is not Guid id) break;
                if (!byId.TryGetValue(id, out node)) throw new InvalidDataException($"Missing parent of '{chain[^1].Name}'.");
            }
            foreach (var node in chain)
            {
                node.SetParent(node.ParentId is Guid id ? byId[id] : null);
                resolved.Add(node.Id);
            }
        }
    }

    public bool Reparent(SceneObject obj, SceneObject? parent, bool keepWorld = true)
    {
        if (!Objects.Contains(obj) || obj.Parent == parent ||
            (parent != null && (!Objects.Contains(parent) || parent.IsWithin(obj)))) return false;
        if (!keepWorld) { obj.SetParent(parent); return true; }
        var previousRect = ScreenUi.IsWidget(obj) ? new ScreenUi(this, null).Bounds(obj.Id) : null;
        var local = obj.WorldMatrix * Affine2D.Inverse(parent?.WorldMatrix ?? Matrix3x2.Identity);
        obj.SetParent(parent);
        obj.SetLocalMatrix(local);
        if (previousRect is { } before && new ScreenUi(this, null).Bounds(obj.Id) is { } after)
        {
            var offset = Affine2D.TransformNormal(new(before.X - after.X,before.Y - after.Y), Affine2D.Inverse(parent?.WorldMatrix ?? Matrix3x2.Identity));
            obj.X += offset.X; obj.Y += offset.Y;
        }
        return true;
    }

    public void RemoveObject(SceneObject obj)
    {
        foreach (var child in Objects.Where(child => child.Parent == obj)) Reparent(child, obj.Parent);
        Objects.Remove(obj);
        foreach (var script in Objects.SelectMany(o => o.Components).OfType<ScriptComponent>())
            if (script.EventLinks?.Any(l => l.Target == obj.Id) == true)
            {
                var remaining = script.EventLinks.Where(l => l.Target != obj.Id).ToArray();
                script.EventLinks = remaining.Length == 0 ? null : remaining;
            }
    }

    public bool MoveTo(SceneObject obj, Vector2 world)
    {
        var local = obj.Parent?.WorldToLocal(world) ?? world;
        if (!float.IsFinite(local.X) || !float.IsFinite(local.Y) || obj.LocalMatrix.Translation == local) return false;
        obj.SetOrigin(local);
        return true;
    }

    public void Resize(SceneObject obj, float width, float height)
    {
        if (!float.IsFinite(width) || !float.IsFinite(height)) return;
        obj.Width = Math.Max(1, width);
        obj.Height = Math.Max(1, height);
    }

    internal IEnumerable<SceneObject> ParentFirst(SceneObject? root = null)
    {
        var children = Objects.ToLookup(obj => obj.Parent);
        var pending = root == null ? new Stack<SceneObject>(children[null].Reverse()) : new Stack<SceneObject>([root]);
        while (pending.TryPop(out var node))
        {
            yield return node;
            foreach (var child in children[node].Reverse()) pending.Push(child);
        }
    }
}
