using System.Drawing;

namespace Engine.Core;

// Stores caller-owned ordinals. Unbounded entries participate in every query.
internal sealed class SpatialGrid(int count)
{
    private readonly record struct Cells(int Left, int Top, int Right, int Bottom);
    private readonly Dictionary<(int X, int Y), List<int>> buckets = [];
    private readonly List<int> large = [];
    private readonly Cells?[] membership = new Cells?[count];

    internal void Add(int index, RectangleF? bounds)
    {
        var cells = bounds is {} rect ? Cover(rect, 256) : null;
        membership[index] = cells;
        Insert(index, cells);
    }

    private static Cells? Cover(RectangleF bounds, int limit)
    {
        const double cellSize = 64;
        double x = Math.Floor(bounds.Left / cellSize), y = Math.Floor(bounds.Top / cellSize);
        double r = Math.Floor(bounds.Right / cellSize), b = Math.Floor(bounds.Bottom / cellSize);
        // Use the closed right/bottom edges for tangent and zero-width rays.
        // Unbounded coordinates or excessive ranges take the bounded-memory fallback.
        if (!(x >= int.MinValue && y >= int.MinValue && r < int.MaxValue && b < int.MaxValue &&
              r >= x && b >= y && (r - x + 1) * (b - y + 1) <= limit)) return null;
        return new((int)x, (int)y, (int)r, (int)b);
    }

    private void Insert(int index, Cells? region)
    {
        if (region is not {} cells) { large.Add(index); return; }
        for (int y = cells.Top; y <= cells.Bottom; y++)
            for (int x = cells.Left; x <= cells.Right; x++)
            {
                if (!buckets.TryGetValue((x, y), out var bucket)) buckets[(x, y)] = bucket = [];
                bucket.Add(index);
            }
    }

    internal void Update(int index, RectangleF? bounds)
    {
        var next = bounds is {} rect ? Cover(rect, 256) : null;
        var old = membership[index];
        if (old == next) return;
        if (old is {} cells)
        {
            for (int y = cells.Top; y <= cells.Bottom; y++)
                for (int x = cells.Left; x <= cells.Right; x++)
                {
                    var bucket = buckets[(x, y)];
                    bucket.Remove(index);
                    if (bucket.Count == 0) buckets.Remove((x, y));
                }
        }
        else large.Remove(index);
        membership[index] = next;
        Insert(index, next);
    }

    internal bool Collect(RectangleF bounds, List<int> result)
    {
        if (Cover(bounds, Math.Min(4096, count)) is not {} cells) return false;
        result.Clear();
        result.AddRange(large);
        for (int y = cells.Top; y <= cells.Bottom; y++)
            for (int x = cells.Left; x <= cells.Right; x++)
                if (buckets.TryGetValue((x, y), out var bucket))
                    foreach (int i in bucket)
                    {
                        var occupied = membership[i]!.Value;
                        // Exactly one cell owns a candidate even when it spans multiple buckets.
                        if (x == Math.Max(cells.Left, occupied.Left) && y == Math.Max(cells.Top, occupied.Top))
                            result.Add(i);
                    }
        return true;
    }
}
