using System.Numerics;
using Engine.Core;
using Color = System.Drawing.Color;

namespace Engine.Editor;

// Points and tangent handles of a Line 2D or Path Follower 2D, or the corner points of an Edge Collider 2D (no tangents),
// edited in the Scene view.
internal sealed record PathEdit(PathPoint[] Points, Matrix3x2 ToWorld, Action<PathPoint[]> Set, bool PinFirst, bool Closed, int MinPoints = 2)
{
    public static PathEdit? Of(SceneObject obj)
    {
        // The collider wins over a Line 2D drawn on the same object.
        if (obj.GetComponent<EdgeCollider2DComponent>() is { } edge)
        {
            // Edge points are in size units (0–1 spans the object); the gizmo works in local pixels.
            float w = obj.Width > 0 ? obj.Width : 1, h = obj.Height > 0 ? obj.Height : 1;
            return new(edge.Points.Select(v => new PathPoint(v.X * w, v.Y * h)).ToArray(), obj.WorldMatrix,
                p => edge.Points = p.Select(q => new ColliderVertex(q.X / w, q.Y / h)).ToArray(), false, edge.Loop, edge.Loop ? 3 : 2);
        }
        if (obj.GetComponent<PathFollower2DComponent>() is { } follower)
        {
            // Same origin as the runtime follower: the first point sits at the object's start position in its parent's space.
            var points = follower.Points;
            var parent = obj.Parent?.WorldMatrix ?? Matrix3x2.Identity;
            var start = Vector2.Transform(obj.WorldPosition, Affine2D.Inverse(parent)) - new Vector2(points[0].X, points[0].Y);
            return new(points, Matrix3x2.CreateTranslation(start) * parent, p => follower.Points = p, true, follower.Repeat == PathRepeat.Loop);
        }
        if (obj.GetComponent<Line2DComponent>() is { } line) return new(line.Points, obj.WorldMatrix, p => line.Points = p, false, false);
        return null;
    }

    public Vector2 World(int index, int part = 0)
    {
        var p = Points[index];
        var local = new Vector2(p.X, p.Y) + part switch { 1 => new(p.InX, p.InY), 2 => new(p.OutX, p.OutY), _ => Vector2.Zero };
        return Vector2.Transform(local, ToWorld);
    }

    public static bool Smooth(PathPoint p) => p.InX != 0 || p.InY != 0 || p.OutX != 0 || p.OutY != 0;

    // A new point halfway along the segment after the selection, or one segment length past the last point.
    public PathPoint[] Insert(int after)
    {
        var list = Points.ToList();
        var a = Points[after];
        PathPoint added = after + 1 < Points.Length
            ? new((a.X + Points[after + 1].X) / 2, (a.Y + Points[after + 1].Y) / 2)
            : after > 0 ? new(2 * a.X - Points[after - 1].X, 2 * a.Y - Points[after - 1].Y) : new(a.X + 32, a.Y);
        list.Insert(after + 1, added);
        return list.ToArray();
    }

    public bool CanRemove(int index) => Points.Length > Math.Max(2, MinPoints) && !(PinFirst && index == 0);
    public PathPoint[] Remove(int index) => Points.Where((_, i) => i != index).ToArray();

    // Corner -> smooth handles along the neighbours' direction (a third of the way); smooth -> corner.
    public PathPoint[] ToggleSmooth(int index)
    {
        var copy = (PathPoint[])Points.Clone();
        var p = copy[index];
        if (Smooth(p)) copy[index] = p with { InX = 0, InY = 0, OutX = 0, OutY = 0 };
        else
        {
            var prev = index > 0 ? copy[index - 1] : p;
            var next = index + 1 < copy.Length ? copy[index + 1] : p;
            var handle = new Vector2(next.X - prev.X, next.Y - prev.Y) / (index > 0 && index + 1 < copy.Length ? 6 : 3);
            if (handle == Vector2.Zero) handle = new(32, 0);
            copy[index] = p with { InX = -handle.X, InY = -handle.Y, OutX = handle.X, OutY = handle.Y };
        }
        return copy;
    }
}

public sealed partial class SceneViewport
{
    internal int PathIndex { get; set; }
    internal event Action? PathEdited;
    private int pathGrip = -1, pathPart;
    private Vector2 pathGrab;
    private PathPoint pathStart;
    private const float PathHit = 6;

    private PathEdit? SelectedPath => Selection.Count == 1 && EditMap == null && !IsPlaying ? PathEdit.Of(Selection[0]) : null;

    private bool BeginPathDrag(Vector2 point)
    {
        if (SelectedPath is not { } path) return false;
        bool Near(Vector2 at) => (at - point).Length() * Scale <= PathHit;
        PathIndex = Math.Clamp(PathIndex, 0, path.Points.Length - 1);
        int grip = -1, part = 0;
        if (PathEdit.Smooth(path.Points[PathIndex]))
            foreach (int handle in new[] { 2, 1 })
                if (grip < 0 && Near(path.World(PathIndex, handle))) (grip, part) = (PathIndex, handle);
        for (int i = path.Points.Length - 1; grip < 0 && i >= 0; i--)
            if (!(path.PinFirst && i == 0) && Near(path.World(i))) grip = i;
        if (grip < 0) return false;
        (pathGrip, pathPart, pathGrab, pathStart) = (grip, part, point, path.Points[grip]);
        if (PathIndex != grip) { PathIndex = grip; PathEdited?.Invoke(); }
        InvalidateUi();
        return true;
    }

    private void DragPath(Vector2 point)
    {
        if (SelectedPath is not { } path || pathGrip >= path.Points.Length) return;
        var d = point - pathGrab;
        if (!movedDuringDrag && d.Length() * Scale < 2) return;
        if (SnapEnabled) d = new(MathF.Round(d.X / 16, MidpointRounding.AwayFromZero) * 16, MathF.Round(d.Y / 16, MidpointRounding.AwayFromZero) * 16);
        var local = Vector2.TransformNormal(d, Affine2D.Inverse(path.ToWorld));
        var s = pathStart;
        var points = path.Points;
        points[pathGrip] = pathPart switch
        {
            // Dragging one handle mirrors the other, keeping the curve smooth through the point.
            1 => s with { InX = s.InX + local.X, InY = s.InY + local.Y, OutX = -(s.InX + local.X), OutY = -(s.InY + local.Y) },
            2 => s with { OutX = s.OutX + local.X, OutY = s.OutY + local.Y, InX = -(s.OutX + local.X), InY = -(s.OutY + local.Y) },
            _ => s with { X = s.X + local.X, Y = s.Y + local.Y },
        };
        path.Set(points);
        movedDuringDrag = true;
        Moved?.Invoke();
        InvalidateUi();
    }

    private void DrawPath(ISceneCanvas canvas, float unit)
    {
        if (SelectedPath is not { } path) return;
        var flat = new CurvePath().Build(path.Points, path.Closed).Points;
        for (int i = 1; i < flat.Length; i++)
            canvas.DrawLine(Vector2.Transform(flat[i - 1], path.ToWorld), Vector2.Transform(flat[i], path.ToWorld), 1.5f * unit, Color.Gold);
        if (path.Closed && flat.Length > 2)
            canvas.DrawLine(Vector2.Transform(flat[^1], path.ToWorld), Vector2.Transform(flat[0], path.ToWorld), 1.5f * unit, Color.Gold);
        int selected = Math.Clamp(PathIndex, 0, path.Points.Length - 1);
        void Square(Vector2 at, float size, Color color) => canvas.FillRectangle(new(at.X - size * unit / 2, at.Y - size * unit / 2, size * unit, size * unit), color);
        if (PathEdit.Smooth(path.Points[selected]))
            foreach (int handle in new[] { 1, 2 })
            {
                var at = path.World(selected, handle);
                canvas.DrawLine(path.World(selected), at, unit, SelectionColor);
                Square(at, 6, SelectionColor);
            }
        for (int i = 0; i < path.Points.Length; i++)
        {
            Square(path.World(i), 9, SurroundColor);
            Square(path.World(i), 7, i == selected ? Color.Gold : path.PinFirst && i == 0 ? FrameColor : SelectionColor);
        }
    }

    // Inspector buttons: change the selected path, then record one undo step.
    internal void EditPath(Func<PathEdit, int, (PathPoint[] Points, int Index)?> change)
    {
        if (SelectedPath is not { } path) return;
        int index = Math.Clamp(PathIndex, 0, path.Points.Length - 1);
        if (change(path, index) is not { } result) return;
        path.Set(result.Points);
        PathIndex = result.Index;
        MoveFinished?.Invoke();
        PathEdited?.Invoke();
        InvalidateUi();
    }
}
