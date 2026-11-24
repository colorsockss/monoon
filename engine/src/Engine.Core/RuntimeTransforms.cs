using System.Numerics;

namespace Engine.Core;

// Position and rotation follow the Inspector's local pivot and clockwise degrees.
public readonly record struct LocalTransform2D(Vector2 Position, float Rotation, Vector2 Scale, float Shear = 0);

public sealed partial class SceneGame
{
    // Transform arithmetic of the local transform API (tweens and clip poses call it every tick) without a copy per call.
    private readonly SceneObject transformScratch = new();
    private SceneObject ScratchTransform(SceneObject source)
    {
        var s = transformScratch;
        (s.X, s.Y, s.Width, s.Height, s.Rotation) = (source.X, source.Y, source.Width, source.Height, source.Rotation);
        (s.ScaleX, s.ScaleY, s.PivotX, s.PivotY, s.Shear) = (source.ScaleX, source.ScaleY, source.PivotX, source.PivotY, source.Shear);
        return s;
    }

    private Matrix3x2 ParentMatrix(Body body) => body.Source.ParentId is Guid id
        ? byId[id].MatrixAt(byId[id].Position) : Matrix3x2.Identity;

    private Matrix3x2 CurrentLocal(Body body) => body.MatrixAt(body.Position) * Affine2D.Inverse(ParentMatrix(body));

    public bool TryGetLocalTransform(Guid id, out LocalTransform2D transform)
    {
        transform = default;
        if (!byId.TryGetValue(id, out var body) || removed.Contains(id)) return false;
        var local = ScratchTransform(body.Source);
        local.SetLocalMatrix(CurrentLocal(body));
        transform = new(new(local.X, local.Y), local.Rotation, new(local.ScaleX, local.ScaleY), local.Shear);
        return true;
    }

    private void ApplyAnimationPose(Body body)
    {
        var pose = body.Animation!.Pose;
        var previous = body.AppliedPose;
        body.AppliedPose = pose;
        if (pose.Rotation == null && pose.ScaleX == null && pose.ScaleY == null &&
            previous.Rotation == null && previous.ScaleX == null && previous.ScaleY == null) return;
        if (!TryGetLocalTransform(body.Id, out var local)) return;
        var basis = body.BeforeAnimation;
        if (previous.Rotation == null) basis = basis with { Rotation = local.Rotation };
        if (previous.ScaleX == null) basis = basis with { ScaleX = local.Scale.X };
        if (previous.ScaleY == null) basis = basis with { ScaleY = local.Scale.Y };
        body.BeforeAnimation = basis;
        float rotation = pose.Rotation ?? (previous.Rotation.HasValue ? basis.Rotation!.Value : local.Rotation);
        var scale = new Vector2(pose.ScaleX ?? (previous.ScaleX.HasValue ? basis.ScaleX!.Value : local.Scale.X),
            pose.ScaleY ?? (previous.ScaleY.HasValue ? basis.ScaleY!.Value : local.Scale.Y));
        if (rotation != local.Rotation || scale != local.Scale)
            SetLocalTransform(body.Id, local with { Rotation = rotation, Scale = scale });
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
        var local = ScratchTransform(body.Source);
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
        if (!dirty && root.Index < parentFirst.Length && parentFirst[root.Index] == root)
        {
            // Animated hierarchies usually keep the same membership across ticks.
            var members = Subtree(root);
            foreach (var member in members[1..])
                if (!Invertible(member.MatrixAt(member.Position) * delta)) return false;
            root.SetWorldPose(target, root.Angle);
            foreach (var member in members[1..])
                member.SetWorldPose(member.MatrixAt(member.Position) * delta, member.Angle);
            return true;
        }
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
