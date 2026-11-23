using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public readonly record struct SpriteInstance2D(RectangleF Bounds, Color Tint);

public readonly record struct SpritePoolStats(int Active, int Capacity, int Peak, long Spawned, long Returned,
    long Reused, long Dropped);

/// <summary>A fixed-capacity layer of moving sprites. Coordinates and velocities are world pixels
/// and pixels per second. Instances use the owner's sprite and draw order.</summary>
public sealed class SpritePool2D
{
    private struct Slot
    {
        public Vector2 Previous, Position, Velocity;
        public Color Tint;
    }

    private readonly Slot[] slots;
    private readonly int[] active, free;
    private readonly SpriteInstance2D[] visible;
    private readonly Vector2 size;
    internal SceneAppearance Appearance { get; set; }
    private int count, available, used, peak;
    private long spawned, returned, reused, dropped;

    internal SpritePool2D(int capacity, Vector2 size, SceneAppearance sprite)
    {
        if (capacity is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(capacity));
        slots = new Slot[capacity];
        active = new int[capacity];
        free = new int[capacity];
        visible = new SpriteInstance2D[capacity];
        this.size = size;
        Appearance = sprite;
    }

    public bool Batched { get; set; } = true;
    public SpritePoolStats Stats => new(count, slots.Length, peak, spawned, returned, reused, dropped);

    public bool TrySpawn(Vector2 center, Vector2 velocity, Color tint)
    {
        if (!Shape2D.Finite(center) || !Shape2D.Finite(velocity)) throw new ArgumentException("Expected finite sprite motion.");
        if (count == slots.Length) { dropped++; return false; }
        int index;
        if (available > 0) { index = free[--available]; reused++; }
        else index = used++;
        slots[index] = new Slot { Position = center, Previous = center, Velocity = velocity, Tint = tint };
        active[count++] = index;
        peak = Math.Max(peak, count);
        spawned++;
        return true;
    }

    private RectangleF Bounds(Vector2 center) => new(center.X - size.X / 2, center.Y - size.Y / 2, size.X, size.Y);

    public void Advance(float seconds, RectangleF keepAlive)
    {
        if (!float.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (keepAlive.Width <= 0 || keepAlive.Height <= 0 || !float.IsFinite(keepAlive.Left) ||
            !float.IsFinite(keepAlive.Top) || !float.IsFinite(keepAlive.Right) || !float.IsFinite(keepAlive.Bottom))
            throw new ArgumentOutOfRangeException(nameof(keepAlive));
        int kept = 0;
        for (int i = 0; i < count; i++)
        {
            int index = active[i];
            ref var slot = ref slots[index];
            slot.Previous = slot.Position;
            slot.Position += slot.Velocity * seconds;
            if (keepAlive.IntersectsWith(Bounds(slot.Position))) active[kept++] = index;
            else { free[available++] = index; returned++; }
        }
        count = kept;
    }

    // Begin a new run without allocating or changing the selected draw mode.
    public void Reset()
    {
        count = available = used = peak = 0;
        spawned = returned = reused = dropped = 0;
    }

    internal void Draw(ICanvas2D canvas, RectangleF frame, Matrix3x2 view, float interpolation)
    {
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            ref readonly var slot = ref slots[active[i]];
            var bounds = Bounds(Affine2D.Lerp(slot.Previous, slot.Position, interpolation));
            if (!frame.IntersectsWith(bounds)) continue;
            var color = Appearance.Color;
            var tint = slot.Tint;
            visible[n++] = new(bounds, Color.FromArgb(color.A * tint.A / 255, color.R * tint.R / 255,
                color.G * tint.G / 255, color.B * tint.B / 255));
        }
        if (n != 0) canvas.DrawSpriteInstances(Appearance, visible.AsSpan(0, n), view, Batched);
    }
}
