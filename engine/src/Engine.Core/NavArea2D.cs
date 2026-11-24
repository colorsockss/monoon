using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine.Core;

/// <summary>Top-down free-movement area (roadmap 16.15), baked once from static colliders and solid tiles.
/// A cell is open when a circle of the agent radius at its centre touches no obstacle and stays inside the area,
/// so a route through open cells keeps the agent clear. Paths are 8-way A* (integer costs 10/14, no corner cutting,
/// cell-index ties) smoothed by line of sight (string pulling). Every buffer is allocated here: queries,
/// dynamic obstacles and <see cref="NavCrowd2D"/> steps allocate nothing.</summary>
public sealed class NavArea2D
{
    public const int MaxCells = 1 << 18;
    private readonly byte[] solid;
    private readonly ushort[] dynamic;
    private readonly int[] cost, parent, stamp, heap, slot, route;
    private readonly long[] key;
    private int generation, heapCount;

    public RectangleF Bounds { get; }
    public float CellSize { get; }
    public float AgentRadius { get; }
    public int Columns { get; }
    public int Rows { get; }
    /// <summary>Increases whenever a dynamic obstacle opens or closes cells. Agents compare it to re-plan.</summary>
    public long Revision { get; private set; }

    private NavArea2D(RectangleF bounds, float cellSize, float radius)
    {
        if (!float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) || !(bounds.Width > 0) || !(bounds.Height > 0) ||
            !float.IsFinite(bounds.Right) || !float.IsFinite(bounds.Bottom))
            throw new ArgumentOutOfRangeException(nameof(bounds));
        if (cellSize is not (>= 1 and <= 1024)) throw new ArgumentOutOfRangeException(nameof(cellSize));
        if (!(radius > 0) || !float.IsFinite(radius)) throw new ArgumentOutOfRangeException(nameof(radius));
        int columns = (int)MathF.Ceiling(bounds.Width / cellSize), rows = (int)MathF.Ceiling(bounds.Height / cellSize);
        if ((long)columns * rows > MaxCells)
            throw new InvalidDataException($"Navigation area needs {columns}×{rows} cells (limit {MaxCells}); increase spacing or split it.");
        (Bounds, CellSize, AgentRadius, Columns, Rows) = (bounds, cellSize, radius, columns, rows);
        int n = columns * rows;
        solid = new byte[n]; dynamic = new ushort[n];
        cost = new int[n]; parent = new int[n]; stamp = new int[n]; heap = new int[n]; slot = new int[n]; route = new int[n];
        key = new long[n];
    }

    /// <summary>Bakes the open cells for agents of <paramref name="agentRadius"/> around world-space obstacles.</summary>
    public static NavArea2D Bake(RectangleF bounds, float cellSize, float agentRadius, ReadOnlySpan<Shape2D> obstacles)
    {
        var area = new NavArea2D(bounds, cellSize, agentRadius);
        float r = agentRadius;
        for (int y = 0; y < area.Rows; y++)
        for (int x = 0; x < area.Columns; x++)
        {
            var c = area.Center(x, y);
            if (c.X - r < bounds.Left || c.X + r > bounds.Right || c.Y - r < bounds.Top || c.Y + r > bounds.Bottom) area.solid[y * area.Columns + x] = 1;
        }
        foreach (var shape in obstacles)
        {
            shape.Check();
            var box = shape.Bounds;
            area.Range(RectangleF.FromLTRB(box.Left - r, box.Top - r, box.Right + r, box.Bottom + r), out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * area.Columns + x;
                if (area.solid[i] == 0 && CollisionGeometry.Overlap(Shape2D.Circle(area.Center(x, y), r), shape)) area.solid[i] = 1;
            }
        }
        return area;
    }

    // Editor gizmo and checks: the same static obstacles the running scene hands to Bake, read from the document.
    internal static NavArea2D? BakeDocument(SceneObject region, IEnumerable<SceneObject> objects, Func<Guid?, TilePalette?> palettes)
    {
        if (region.GetComponent<NavigationRegion2DComponent>() is not { Enabled: true } config) return null;
        var bounds = Affine2D.Bounds(new(0, 0, region.Width, region.Height), region.WorldMatrix);
        float radius = config.AgentWidth * .5f;
        var query = bounds; query.Inflate(radius, radius);
        var shapes = new List<Shape2D>();
        foreach (var o in objects)
        {
            if (!o.ActiveInHierarchy) continue;
            if (o.GetComponent<Collider2DComponent>() is { IsTrigger: false, Enabled: true } c && (config.TerrainMask & (1u << c.Layer)) != 0 &&
                o.GetComponent<Rigidbody2DComponent>() is not { BodyType: BodyType2D.Dynamic })
            {
                var shape = CollisionGeometry.ForObject(o).Translated(o.WorldMatrix.Translation);
                if (query.IntersectsWith(shape.Bounds)) shapes.Add(shape);
            }
            if (o.GetComponent<Tilemap2DComponent>() is not { Enabled: true } map || (config.TerrainMask & (1u << map.CollisionLayer)) == 0 ||
                palettes(map.Tileset) is not { } palette) continue;
            foreach (var (cell, id) in map.InView(Vector2.Zero, Affine2D.Bounds(query, Affine2D.Inverse(o.WorldMatrix))))
                if (palette.Tiles.TryGetValue(id, out var tile) && TileCollision.WorldShape(tile, map.Bounds(Vector2.Zero, cell), map.Layout, o.WorldMatrix) is { } s)
                    shapes.Add(s);
        }
        return Bake(bounds, config.Spacing, radius, CollectionsMarshal.AsSpan(shapes));
    }

    public Vector2 Center(int column, int row) => new(Bounds.Left + (column + .5f) * CellSize, Bounds.Top + (row + .5f) * CellSize);
    public bool IsOpen(int column, int row) => (uint)column < (uint)Columns && (uint)row < (uint)Rows && Open(row * Columns + column);
    public bool IsOpen(Vector2 point) => Cell(point, out int x, out int y) && Open(y * Columns + x);
    private bool Open(int i) => solid[i] == 0 && dynamic[i] == 0;

    public int OpenCells()
    {
        int n = 0;
        for (int i = 0; i < solid.Length; i++) if (Open(i)) n++;
        return n;
    }

    private bool Cell(Vector2 p, out int x, out int y)
    {
        float fx = MathF.Floor((p.X - Bounds.Left) / CellSize), fy = MathF.Floor((p.Y - Bounds.Top) / CellSize);
        bool inside = fx >= 0 && fy >= 0 && fx < Columns && fy < Rows;
        x = inside ? (int)fx : -1; y = inside ? (int)fy : -1;
        return inside;
    }

    // Cells whose centres lie in the rectangle, clamped to the area (empty when it misses).
    private void Range(RectangleF r, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = Math.Max(0, (int)MathF.Ceiling((r.Left - Bounds.Left) / CellSize - .5f));
        y0 = Math.Max(0, (int)MathF.Ceiling((r.Top - Bounds.Top) / CellSize - .5f));
        x1 = Math.Min(Columns - 1, (int)MathF.Floor((r.Right - Bounds.Left) / CellSize - .5f));
        y1 = Math.Min(Rows - 1, (int)MathF.Floor((r.Bottom - Bounds.Top) / CellSize - .5f));
    }

    /// <summary>Closes the cells an agent could not occupy beside <paramref name="box"/> (a door, a crate) until removed.</summary>
    public void AddObstacle(RectangleF box) => Mark(box, 1);
    public void RemoveObstacle(RectangleF box) => Mark(box, -1);

    private void Mark(RectangleF box, int delta)
    {
        if (!(box.Width > 0) || !(box.Height > 0) || !float.IsFinite(box.Left) || !float.IsFinite(box.Top)) throw new ArgumentOutOfRangeException(nameof(box));
        float r = AgentRadius, r2 = r * r;
        Range(RectangleF.FromLTRB(box.Left - r, box.Top - r, box.Right + r, box.Bottom + r), out int x0, out int y0, out int x1, out int y1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            var c = Center(x, y);
            float dx = c.X - Math.Clamp(c.X, box.Left, box.Right), dy = c.Y - Math.Clamp(c.Y, box.Top, box.Bottom);
            if (dx * dx + dy * dy >= r2) continue;
            int i = y * Columns + x;
            if (delta < 0 && dynamic[i] == 0) throw new InvalidOperationException("Removed an obstacle that was never added.");
            dynamic[i] = (ushort)(dynamic[i] + delta);
        }
        Revision++;
    }

    // The open cell nearest to p within two rings (fixed scan order breaks ties); -1 when there is none.
    private int Nearest(Vector2 p)
    {
        float fx = Math.Clamp(MathF.Floor((p.X - Bounds.Left) / CellSize), 0, Columns - 1);
        float fy = Math.Clamp(MathF.Floor((p.Y - Bounds.Top) / CellSize), 0, Rows - 1);
        int cx = (int)fx, cy = (int)fy;
        if (Open(cy * Columns + cx) && Cell(p, out _, out _)) return cy * Columns + cx;
        int best = -1; float bestDistance = float.PositiveInfinity;
        for (int y = cy - 2; y <= cy + 2; y++)
        for (int x = cx - 2; x <= cx + 2; x++)
        {
            if (!IsOpen(x, y)) continue;
            var c = Center(x, y);
            float dx = c.X - p.X, dy = c.Y - p.Y, d = dx * dx + dy * dy;
            if (d < bestDistance) (best, bestDistance) = (y * Columns + x, d);
        }
        return best;
    }

    /// <summary>True when the segment crosses only open cells; a step through a cell corner needs both side cells open.</summary>
    public bool Clear(Vector2 a, Vector2 b)
    {
        if (!Cell(a, out int cx, out int cy) || !Cell(b, out int ex, out int ey) || !Open(cy * Columns + cx)) return false;
        float x0 = (a.X - Bounds.Left) / CellSize, y0 = (a.Y - Bounds.Top) / CellSize;
        float dx = (b.X - Bounds.Left) / CellSize - x0, dy = (b.Y - Bounds.Top) / CellSize - y0;
        int sx = Math.Sign(ex - cx), sy = Math.Sign(ey - cy);
        float deltaX = sx != 0 ? 1 / MathF.Abs(dx) : float.PositiveInfinity, deltaY = sy != 0 ? 1 / MathF.Abs(dy) : float.PositiveInfinity;
        float maxX = sx > 0 ? (cx + 1 - x0) * deltaX : sx < 0 ? (x0 - cx) * deltaX : float.PositiveInfinity;
        float maxY = sy > 0 ? (cy + 1 - y0) * deltaY : sy < 0 ? (y0 - cy) * deltaY : float.PositiveInfinity;
        // Each axis only moves toward the end cell, so the walk ends there even with rounding.
        while (cx != ex || cy != ey)
        {
            bool stepX = cy == ey || cx != ex && maxX < maxY, stepY = cx == ex || cy != ey && maxY < maxX;
            if (!stepX && !stepY)
            {
                if (!IsOpen(cx + sx, cy) || !IsOpen(cx, cy + sy)) return false;
                stepX = stepY = true;
            }
            if (stepX) { cx += sx; maxX += deltaX; }
            if (stepY) { cy += sy; maxY += deltaY; }
            if (!Open(cy * Columns + cx)) return false;
        }
        return true;
    }

    private long Estimate(int cell, int gx, int gy)
    {
        int dx = Math.Abs(cell % Columns - gx), dy = Math.Abs(cell / Columns - gy);
        return 10 * (dx + dy) - 6 * Math.Min(dx, dy);
    }

    /// <summary>Writes the smoothed route from <paramref name="start"/> to <paramref name="goal"/> into
    /// <paramref name="points"/> and returns the point count (0 when unreachable). The first point is the start; the last is
    /// the goal, or the centre of the nearest open cell when the goal itself is blocked. A route longer than the buffer ends
    /// at an intermediate corner (count == points.Length): query again from there.</summary>
    public int FindPath(Vector2 start, Vector2 goal, Span<Vector2> points)
    {
        if (points.Length < 2) throw new ArgumentException("Expected room for at least two points.", nameof(points));
        if (!Shape2D.Finite(start) || !Shape2D.Finite(goal)) throw new ArgumentException("Expected finite endpoints.");
        int s = Nearest(start), g = Nearest(goal);
        if (s < 0 || g < 0) return 0;
        var end = IsOpen(goal) ? goal : Center(g % Columns, g / Columns);
        int length = Search(s, g);
        if (length == 0) return 0;
        int count = 0;
        points[count++] = start;
        var anchor = start;
        // A start off the route's first centre (blocked, or beside a closed corner) steps to that centre first.
        if (!Clear(start, end) && !Clear(start, CenterOf(route[Math.Min(1, length - 1)])))
        {
            points[count++] = anchor = CenterOf(route[0]);
            if (count == points.Length) return count;
        }
        int at = 0;
        while (at < length - 1 && !Clear(anchor, end))
        {
            int k = at + 1;
            while (k + 1 < length && Clear(anchor, CenterOf(route[k + 1]))) k++;
            points[count++] = anchor = CenterOf(route[k]);
            at = k;
            if (count == points.Length) return count;
        }
        points[count++] = end;
        return count;
    }

    private Vector2 CenterOf(int cell) => Center(cell % Columns, cell / Columns);

    // A* over open cells into route[0..length) from s to g; 0 when unreachable.
    private int Search(int s, int g)
    {
        if (++generation == int.MaxValue) { Array.Clear(stamp); generation = 1; }
        int gx = g % Columns, gy = g / Columns;
        heapCount = 0;
        stamp[s] = generation; cost[s] = 0; parent[s] = -1;
        Push(s, Estimate(s, gx, gy));
        while (heapCount > 0)
        {
            int c = Pop();
            if (c == g)
            {
                int length = 0;
                for (int i = g; i >= 0; i = parent[i]) route[length++] = i;
                Array.Reverse(route, 0, length);
                return length;
            }
            int cx = c % Columns, cy = c / Columns;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0 || !IsOpen(cx + dx, cy + dy)) continue;
                bool diagonal = dx != 0 && dy != 0;
                if (diagonal && (!IsOpen(cx + dx, cy) || !IsOpen(cx, cy + dy))) continue;
                int n = (cy + dy) * Columns + cx + dx, next = cost[c] + (diagonal ? 14 : 10);
                if (stamp[n] != generation)
                {
                    stamp[n] = generation; cost[n] = next; parent[n] = c;
                    Push(n, next + Estimate(n, gx, gy));
                }
                else if (slot[n] >= 0 && next < cost[n])
                {
                    cost[n] = next; parent[n] = c;
                    key[n] = (long)(next + Estimate(n, gx, gy)) << 32 | (uint)n;
                    Up(slot[n]);
                }
            }
        }
        return 0;
    }

    // Binary heap of cells keyed by (f, cell index); slot[cell] is its heap position, -1 once closed.
    private void Push(int cell, long f)
    {
        key[cell] = f << 32 | (uint)cell;
        heap[heapCount] = cell; slot[cell] = heapCount;
        Up(heapCount++);
    }

    private int Pop()
    {
        int top = heap[0];
        slot[top] = -1;
        if (--heapCount > 0)
        {
            heap[0] = heap[heapCount]; slot[heap[0]] = 0;
            Down(0);
        }
        return top;
    }

    private void Up(int i)
    {
        int cell = heap[i];
        while (i > 0)
        {
            int p = (i - 1) >> 1;
            if (key[heap[p]] <= key[cell]) break;
            heap[i] = heap[p]; slot[heap[i]] = i; i = p;
        }
        heap[i] = cell; slot[cell] = i;
    }

    private void Down(int i)
    {
        int cell = heap[i];
        while (true)
        {
            int child = 2 * i + 1;
            if (child >= heapCount) break;
            if (child + 1 < heapCount && key[heap[child + 1]] < key[heap[child]]) child++;
            if (key[heap[child]] >= key[cell]) break;
            heap[i] = heap[child]; slot[heap[i]] = i; i = child;
        }
        heap[i] = cell; slot[cell] = i;
    }
}
