using System.Numerics;
using Engine.Core;
using Color = System.Drawing.Color;

namespace Engine.Editor;

// Capsule Collider 2D outline and radius handle in the Scene view. Edge Collider 2D points use the path editor (PathEdit).
public sealed partial class SceneViewport
{
    private bool radiusGrip;

    private (SceneObject Owner, CapsuleCollider2DComponent Capsule, Shape2D Shape)? SelectedCapsule =>
        Selection.Count == 1 && EditMap == null && !IsPlaying && Selection[0].GetComponent<CapsuleCollider2DComponent>() is { } capsule
            ? (Selection[0], capsule, CollisionGeometry.ForObject(Selection[0]).Translated(Selection[0].WorldMatrix.Translation)) : null;

    // The handle sits on the capsule's side, a radius away from the middle of its axis.
    private static Vector2 RadiusHandle(Shape2D s)
    {
        if (s.Vertices is not [var a, var b]) return s.Offset + new Vector2(s.Radius, 0);
        var axis = Vector2.Normalize(b - a);
        return s.Offset + (a + b) * .5f + new Vector2(-axis.Y, axis.X) * s.Radius;
    }

    private bool BeginRadiusDrag(Vector2 point)
    {
        if (SelectedCapsule is not { } c || (RadiusHandle(c.Shape) - point).Length() * Scale > PathHit) return false;
        radiusGrip = true;
        return true;
    }

    private void DragRadius(Vector2 point)
    {
        if (SelectedCapsule is not { } c) return;
        // Distance from the axis in local pixels, clamped like the runtime (half the shorter side).
        var local = Vector2.Transform(point, Affine2D.Inverse(c.Owner.WorldMatrix));
        var size = new Vector2(c.Owner.Width, c.Owner.Height);
        float r = size.Y >= size.X ? MathF.Abs(local.X - size.X * .5f) : MathF.Abs(local.Y - size.Y * .5f);
        r = MathF.Round(Math.Clamp(r, .5f, Math.Min(size.X, size.Y) * .5f), 1);
        if (r == c.Capsule.Radius) return;
        c.Capsule.Radius = r;
        movedDuringDrag = true;
        Moved?.Invoke();
        InvalidateUi();
    }

    private void DrawCapsule(ISceneCanvas canvas, float unit)
    {
        if (SelectedCapsule is not { } c) return;
        var s = c.Shape;
        void Ring(Vector2 center)
        {
            var last = center + new Vector2(s.Radius, 0);
            for (int i = 1; i <= 32; i++)
            {
                float angle = i * MathF.Tau / 32;
                var next = center + s.Radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                canvas.DrawLine(last, next, 1.5f * unit, Color.Gold);
                last = next;
            }
        }
        if (s.Vertices is [var a0, var b0])
        {
            Vector2 a = a0 + s.Offset, b = b0 + s.Offset;
            var side = Vector2.Normalize(new Vector2(b.Y - a.Y, a.X - b.X)) * s.Radius;
            canvas.DrawLine(a + side, b + side, 1.5f * unit, Color.Gold);
            canvas.DrawLine(a - side, b - side, 1.5f * unit, Color.Gold);
            Ring(a); Ring(b);
        }
        else Ring(s.Offset);
        var at = RadiusHandle(s);
        canvas.FillRectangle(new(at.X - 4.5f * unit, at.Y - 4.5f * unit, 9 * unit, 9 * unit), SurroundColor);
        canvas.FillRectangle(new(at.X - 3.5f * unit, at.Y - 3.5f * unit, 7 * unit, 7 * unit), radiusGrip ? Color.Gold : SelectionColor);
    }
}
