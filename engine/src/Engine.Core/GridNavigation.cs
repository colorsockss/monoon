using System.Drawing;

namespace Engine.Core;

public enum GridDirections { Four = 4, Eight = 8 }

public sealed record GridPath(IReadOnlyList<Point> Cells, long Cost)
{
    internal static readonly GridPath Unreachable = new(Array.Empty<Point>(), 0);
}

/// <summary>Rectangular, row-major navigation grid. Zero blocks entry. Positive values are terrain weights.</summary>
public sealed class GridNavigation
{
    private readonly int[] costs;
    public int Width { get; }
    public int Height { get; }

    public GridNavigation(int width, int height, ReadOnlySpan<int> entryCosts)
    {
        if (width <= 0 || height <= 0 || (long)width * height != entryCosts.Length)
            throw new ArgumentException("Grid dimensions must match the number of entry costs.");
        if (entryCosts.ContainsAnyExceptInRange(0, int.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(entryCosts), "Terrain weights must be nonnegative.");
        Width = width;
        Height = height;
        costs = entryCosts.ToArray();
    }

    private bool Contains(Point cell) => (uint)cell.X < Width && (uint)cell.Y < Height;
    private int Index(Point cell) => cell.Y * Width + cell.X;

    public void SetEntryCost(Point cell, int cost)
    {
        if (!Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        costs[Index(cell)] = cost;
    }

    /// <summary>Includes both endpoints. Each step costs 10 (straight) or 14 (diagonal) times the destination weight.</summary>
    public GridPath FindPath(Point start, Point goal, GridDirections directions = GridDirections.Four)
    {
        if (directions is not (GridDirections.Four or GridDirections.Eight))
            throw new ArgumentOutOfRangeException(nameof(directions));
        if (!Contains(start) || !Contains(goal) || costs[Index(start)] == 0 || costs[Index(goal)] == 0)
            return GridPath.Unreachable;
        if (start == goal) return new(new[] { start }, 0);

        // Manhattan / octile lower bound for terrain weights >= 1.
        long Estimate(Point cell)
        {
            long dx = Math.Abs(cell.X - goal.X), dy = Math.Abs(cell.Y - goal.Y);
            return 10 * (dx + dy) - (directions == GridDirections.Eight ? 6 * Math.Min(dx, dy) : 0);
        }

        int first = Index(start), last = Index(goal);
        var distance = new long[costs.Length];
        Array.Fill(distance, long.MaxValue);
        distance[first] = 0;
        var parent = new int[costs.Length];
        // The cell index breaks equal-score ties identically on each platform.
        var open = new PriorityQueue<int, (long Score, int Index)>();
        open.Enqueue(first, (Estimate(start), first));
        while (open.TryDequeue(out int current, out var priority))
        {
            Point cell = new(current % Width, current / Width);
            if (priority.Score != distance[current] + Estimate(cell)) continue;
            if (current == last)
            {
                var route = new List<Point> { goal };
                while (current != first)
                {
                    current = parent[current];
                    route.Add(new(current % Width, current / Width));
                }
                route.Reverse();
                return new(route.AsReadOnly(), distance[last]);
            }
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                bool diagonal = dx != 0 && dy != 0;
                if (diagonal && directions == GridDirections.Four) continue;
                Point next = new(cell.X + dx, cell.Y + dy);
                if (!Contains(next)) continue;
                int index = Index(next);
                if (costs[index] == 0 || diagonal && (costs[Index(new(next.X, cell.Y))] == 0 || costs[Index(new(cell.X, next.Y))] == 0))
                    continue;
                long candidate = distance[current] + (diagonal ? 14L : 10L) * costs[index];
                if (candidate >= distance[index]) continue;
                distance[index] = candidate;
                parent[index] = current;
                open.Enqueue(index, (candidate + Estimate(next), index));
            }
        }
        return GridPath.Unreachable;
    }
}
