using System.Numerics;

namespace Engine.Core;

// Position and rotation follow the Inspector's local pivot and clockwise degrees.
public readonly record struct LocalTransform2D(Vector2 Position, float Rotation, Vector2 Scale, float Shear = 0);

public sealed partial class SceneGame
{
    private Matrix3x2 ParentMatrix(Body body) => body.Source.ParentId is Guid id
        ? byId[id].MatrixAt(byId[id].Position) : Matrix3x2.Identity;

    private Matrix3x2 CurrentLocal(Body body) => body.MatrixAt(body.Position) * Affine2D.Inverse(ParentMatrix(body));

    public bool TryGetLocalTransform(Guid id, out LocalTransform2D transform)
    {
        transform = default;
        if (!byId.TryGetValue(id, out var body) || removed.Contains(id)) return false;
        var local = body.Source.CopyTransform(id);
        local.SetLocalMatrix(CurrentLocal(body));
        transform = new(new(local.X, local.Y), local.Rotation, new(local.ScaleX, local.ScaleY), local.Shear);
        return true;
    }

    public bool TryGetParent(Guid id, out Guid? parent)
    {
        parent = null;
        if (!byId.TryGetValue(id, out var body) || removed.Contains(id)) return false;
        parent = body.Source.ParentId;
        return true;
    }

    public bool SetLocalTransform(Guid id, LocalTransform2D transform)
    {
        if (closing || removed.Contains(id) || !byId.TryGetValue(id, out var body) || body.Interface) return false;
        var local = body.Source.CopyTransform(id);
        local.X = transform.Position.X;
        local.Y = transform.Position.Y;
        local.Rotation = transform.Rotation;
        local.ScaleX = transform.Scale.X;
        local.ScaleY = transform.Scale.Y;
        local.Shear = transform.Shear;
        var worldPose = local.LocalMatrix * ParentMatrix(body);
        if (!ApplySubtreePose(body, worldPose)) return false;
        body.Source.X = local.X;
        body.Source.Y = local.Y;
        body.Source.Rotation = local.Rotation;
        body.Source.ScaleX = local.ScaleX;
        body.Source.ScaleY = local.ScaleY;
        body.Source.Shear = local.Shear;
        body.EditedBasis = true;
        return true;
    }

    public bool SetParent(Guid id, Guid? parent, bool keepWorld = true)
    {
        if (closing || removed.Contains(id) || !byId.TryGetValue(id, out var body) || body.Interface) return false;
        Body? next = null;
        if (parent is Guid p && (removed.Contains(p) || !byId.TryGetValue(p, out next) || next.Interface ||
            next.SceneOwner != body.SceneOwner || next.Source.IsWithin(body.Source))) return false;
        if (body.Source.ParentId == parent) return true;
        var parentWorld = next?.MatrixAt(next.Position) ?? Matrix3x2.Identity;
        var local = keepWorld ? body.MatrixAt(body.Position) * Affine2D.Inverse(parentWorld) : CurrentLocal(body);
        if (!Invertible(local) || (!keepWorld && !ApplySubtreePose(body, local * parentWorld))) return false;
        body.Source.SetParent(next?.Source);
        body.Source.SetLocalMatrix(local);
        body.EditedBasis = true;
        // Motion, sorting and queries in the rest of this tick must already see the new subtree.
        Reindex();
        Ui.Rebuild(world, assets);
        if (body.Origin?.Fields is {} selected) TrackForSave(id, selected);
        else if (trackedObjects.TryGetValue(id, out var fields)) TrackForSave(id, fields);
        RefreshActive(body);
        return true;
    }

    private static bool Invertible(Matrix3x2 matrix) => Affine2D.Finite(matrix) &&
        Matrix3x2.Invert(matrix, out var inverse) && Affine2D.Finite(inverse);

    private bool ApplySubtreePose(Body root, Matrix3x2 target)
    {
        if (!Invertible(target)) return false;
        var delta = Affine2D.Inverse(root.MatrixAt(root.Position)) * target;
        var poses = new List<(Body Body, Matrix3x2 World)>();
        foreach (var obj in world.ParentFirst(root.Source))
        {
            var member = byId[obj.Id];
            var matrix = member == root ? target : member.MatrixAt(member.Position) * delta;
            if (!Invertible(matrix)) return false;
            poses.Add((member, matrix));
        }
        foreach (var (member, matrix) in poses) member.SetWorldPose(matrix, member.Angle);
        return true;
    }
}
