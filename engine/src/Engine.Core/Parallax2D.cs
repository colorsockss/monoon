using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

// Sprite Renderer draw modes. Tiled repeats the sprite at one world unit per pixel over the object's size; Sliced stretches
// a 9-slice (Slice borders) so its corners keep their pixel size. Tiled with Slice borders tiles the edges and centre.
public enum SpriteDrawMode { Simple, Tiled, Sliced }

[DisplayName("Parallax 2D"), YamlObject]
public sealed partial record Parallax2DComponent : SceneComponent
{
    public Parallax2DComponent() { }
    [YamlConstructor] public Parallax2DComponent(float scrollX = .5f, float scrollY = 1) => (ScrollX, ScrollY) = (scrollX, scrollY);
    // The share of the camera's travel the layer follows: 1 moves with the world, 0 stays on screen.
    public float ScrollX { get; set; } = .5f;
    public float ScrollY { get; set; } = 1;
    // Repeat the sprite along the axis to fill the camera view: at the tile size for Tiled sprites, else the object's size.
    public bool RepeatX { get; set; }
    public bool RepeatY { get; set; }

    internal void Validate(SceneObject owner)
    {
        if (!float.IsFinite(ScrollX) || !float.IsFinite(ScrollY) || Math.Abs(ScrollX) > 16 || Math.Abs(ScrollY) > 16 ||
            owner.GetComponent<SpriteRendererComponent>() == null || owner.GetComponent<Collider2DComponent>() != null ||
            owner.GetComponent<Rigidbody2DComponent>() != null || owner.GetComponent<Tilemap2DComponent>() != null)
            throw new InvalidDataException($"{owner.Name}: Parallax 2D needs a Sprite Renderer, no collider, body or tilemap, and scroll factors from -16 to 16.");
    }
}

internal static class SpriteGrid
{
    // Source cells of the fallback path, reused so tiled sprites allocate nothing per frame. Render code runs on one thread.
    private static readonly Dictionary<(int, int, int, int), SpriteRegion> regions = [];
    private static SpriteRegion Region(int x, int y, int w, int h)
    {
        if (regions.TryGetValue((x, y, w, h), out var region)) return region;
        if (regions.Count >= 1024) regions.Clear();
        return regions[(x, y, w, h)] = new(x, y, w, h);
    }

    // Splits the source into up to nine pieces (none without borders) and draws each stretched, or repeated at one local unit
    // per pixel along the axes where it can grow, clipping the last tile. Canvases without their own tiling use this.
    internal static bool Draw(ICanvas2D canvas, string path, RectangleF local, Matrix3x2 matrix, Color tint, SpriteRegion? source,
        Size size, UiBorders? borders, bool tiled)
    {
        int ox = source?.X ?? 0, oy = source?.Y ?? 0;
        Span<int> sx = [0, borders?.Left ?? 0, size.Width - (borders?.Right ?? 0), size.Width];
        Span<int> sy = [0, borders?.Top ?? 0, size.Height - (borders?.Bottom ?? 0), size.Height];
        Span<float> dx = stackalloc float[4], dy = stackalloc float[4];
        Edges(dx, local.X, local.Width, sx[1], size.Width - sx[2]);
        Edges(dy, local.Y, local.Height, sy[1], size.Height - sy[2]);
        for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
            {
                int sw = sx[i + 1] - sx[i], sh = sy[j + 1] - sy[j];
                if (sw <= 0 || sh <= 0 || dx[i + 1] <= dx[i] || dy[j + 1] <= dy[j]) continue;
                // Only the middle column and row grow; corners and the borders' short sides keep their size.
                float stepX = tiled && i == 1 ? sw : dx[i + 1] - dx[i], stepY = tiled && j == 1 ? sh : dy[j + 1] - dy[j];
                for (float y = dy[j]; y < dy[j + 1] - 1e-4f; y += stepY)
                {
                    float h = Math.Min(stepY, dy[j + 1] - y);
                    int ch = tiled && j == 1 ? Math.Max(1, (int)MathF.Round(h)) : sh;
                    for (float x = dx[i]; x < dx[i + 1] - 1e-4f; x += stepX)
                    {
                        float w = Math.Min(stepX, dx[i + 1] - x);
                        int cw = tiled && i == 1 ? Math.Max(1, (int)MathF.Round(w)) : sw;
                        if (!canvas.DrawAffine(path, new RectangleF(x, y, w, h), matrix, tint, Region(ox + sx[i], oy + sy[j], cw, ch))) return false;
                    }
                }
            }
        return true;
    }

    // Borders keep their pixel size until the destination is narrower than both together, then shrink in proportion.
    private static void Edges(Span<float> cuts, float start, float length, float before, float after)
    {
        cuts[0] = start;
        cuts[3] = start + length;
        if (before + after > length) cuts[1] = cuts[2] = start + length * (before + after == 0 ? 0 : before / (before + after));
        else { cuts[1] = start + before; cuts[2] = cuts[3] - after; }
    }
}

public sealed partial class SceneGame
{
    /// <summary>The world offset the active camera's parallax adds to a Parallax 2D layer at the last game tick.</summary>
    public bool TryGetParallaxOffset(Guid layer, out Vector2 offset)
    {
        offset = default;
        if (!byId.TryGetValue(layer, out var body) || body.Parallax == null || ViewCamera is not { Active: true } lens) return false;
        offset = ParallaxOffset(body.Parallax, lens, Sample(lens, 1));
        return true;
    }

    // A layer follows Scroll × the camera's travel from its first frame, so the scene starts as authored in the editor.
    private Vector2 ParallaxOffset(Parallax2DComponent layer, Body lens, CameraView shot)
    {
        var travel = shot.Center - lens.CameraHome;
        var offset = new Vector2(travel.X * (1 - layer.ScrollX), travel.Y * (1 - layer.ScrollY));
        if (!IntegerScale) return offset;
        return new(MathF.Round(offset.X * shot.Zoom) / shot.Zoom, MathF.Round(offset.Y * shot.Zoom) / shot.Zoom);
    }

    private void DrawParallax(ICanvas2D canvas, Body body, Body lens, CameraView shot, Matrix3x2 matrix, Matrix3x2 view)
    {
        var layer = body.Parallax!;
        var appearance = body.DisplayAppearance;
        if (appearance.Image == null) return;
        matrix.Translation += ParallaxOffset(layer, lens, shot);
        var local = new RectangleF(0, 0, body.Size.X, body.Size.Y);
        if (!layer.RepeatX && !layer.RepeatY)
        {
            if (shot.World.IntersectsWith(Affine2D.Bounds(local, matrix))) appearance.Draw(canvas, local, matrix * view);
            return;
        }
        // The camera frame in the layer's local space; repeats start a whole period from the object's origin.
        var seen = Affine2D.Bounds(shot.World, Affine2D.Inverse(matrix));
        var tile = appearance.Mode == SpriteDrawMode.Tiled ? appearance.TileSize(canvas) : Size.Empty;
        var period = tile.IsEmpty ? new Vector2(local.Width, local.Height) : new Vector2(tile.Width, tile.Height);
        if (period.X <= 0 || period.Y <= 0) return;
        if (layer.RepeatX) local = RectangleF.FromLTRB(MathF.Floor(seen.Left / period.X) * period.X, local.Top, seen.Right, local.Bottom);
        if (layer.RepeatY) local = RectangleF.FromLTRB(local.Left, MathF.Floor(seen.Top / period.Y) * period.Y, local.Right, seen.Bottom);
        if (local.Width <= 0 || local.Height <= 0) return;
        if (!tile.IsEmpty) { appearance.Draw(canvas, local, matrix * view); return; } // one tiled draw covers the view
        for (float y = local.Top; y < local.Bottom; y += layer.RepeatY ? period.Y : float.PositiveInfinity)
            for (float x = local.Left; x < local.Right; x += layer.RepeatX ? period.X : float.PositiveInfinity)
                appearance.Draw(canvas, new RectangleF(x, y, body.Size.X, body.Size.Y), matrix * view);
    }
}
