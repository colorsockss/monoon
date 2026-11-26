using System.Drawing;

namespace Engine.Core;

public enum GridDirections { Four = 4, Hex = 6, Eight = 8 }

public sealed record GridPath(IReadOnlyList<Point> Cells, long Cost)
{
    internal static readonly GridPath Unreachable = new(Array.Empty<Point>(), 0);
}

public readonly record struct GridReach(Point Cell, long Cost);

/// <summary>Row-major navigation grid (axial q,r for Hex). Zero blocks entry. Positive values are terrain weights.</summary>
public sealed class GridNavigation
{
    private readonly int[] costs;
    private readonly object access = new();
    private SearchBuffer? buffer;

    public int Width { get; }
    public int Height { get; }
    public GridDirections Directions { get; }
    /// <summary>Increases whenever an entry cost changes. Walkers compare it to re-plan from their current cell.</summary>
    public long Revision { get; private set; }

    public GridNavigation(int width, int height, ReadOnlySpan<int> entryCosts) : this(width, height, entryCosts, GridDirections.Four) { }

    public GridNavigation(int width, int height, ReadOnlySpan<int> entryCosts, GridDirections directions)
    {
        if (width <= 0 || height <= 0 || (long)width * height != entryCosts.Length)
            throw new ArgumentException("Grid dimensions must match the number of entry costs.");
        if (entryCosts.ContainsAnyExceptInRange(0, int.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(entryCosts), "Terrain weights must be nonnegative.");
        CheckDirections(directions);
        Width = width;
        Height = height;
        Directions = directions;
        costs = entryCosts.ToArray();
    }

    private static void CheckDirections(GridDirections directions)
    {
        if (directions is not (GridDirections.Four or GridDirections.Eight or GridDirections.Hex))
            throw new ArgumentOutOfRangeException(nameof(directions));
    }

    private bool Contains(Point cell) => (uint)cell.X < Width && (uint)cell.Y < Height;
    private int Index(Point cell) => cell.Y * Width + cell.X;
    private Point Cell(int index) => new(index % Width, index / Width);
    private bool Open(Point cell) => Contains(cell) && costs[Index(cell)] != 0;

    public void SetEntryCost(Point cell, int cost)
    {
        if (!Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        lock (access)
        {
            if (costs[Index(cell)] == cost) return;
            costs[Index(cell)] = cost;
            Revision++;
        }
    }

    public GridPath FindPath(Point start, Point goal) => FindPath(start, goal, Directions);

    /// <summary>Includes both endpoints. Steps cost 10, or 14 for square diagonals, times the destination weight.</summary>
    public GridPath FindPath(Point start, Point goal, GridDirections directions)
    {
        CheckDirections(directions);
        lock (access)
        {
            if (!Open(start) || !Open(goal)) return GridPath.Unreachable;
            var search = buffer ??= new(costs.Length);
            int end = Index(goal);
            if (!Search(search, Index(start), end, directions, long.MaxValue, default, out _)) return GridPath.Unreachable;
            int count = PathLength(search, end);
            var cells = new Point[count];
            WritePath(search, end, cells);
            return new(Array.AsReadOnly(cells), search.Distance[end]);
        }
    }

    /// <summary>Returns the length of the shortest path including both endpoints. If the buffer is too short,
    /// returns the required length and leaves it untouched. An unreachable result is length and cost zero.</summary>
    public int FindPath(Point start, Point goal, Span<Point> path, out long cost, GridDirections? directions = null)
    {
        var mode = directions ?? Directions;
        CheckDirections(mode);
        lock (access)
        {
            cost = 0;
            if (!Open(start) || !Open(goal)) return 0;
            var search = buffer ??= new(costs.Length);
            int end = Index(goal);
            if (!Search(search, Index(start), end, mode, long.MaxValue, default, out _)) return 0;
            cost = search.Distance[end];
            int count = PathLength(search, end);
            if (path.Length >= count) WritePath(search, end, path[..count]);
            return count;
        }
    }

    /// <summary>Writes reached cells in cost order (row order breaks ties). The return value is the total count
    /// including the start at zero cost, even when the output buffer is shorter.</summary>
    public int Reachable(Point start, long budget, Span<GridReach> output, GridDirections? directions = null)
    {
        var mode = directions ?? Directions;
        CheckDirections(mode);
        if (budget < 0) throw new ArgumentOutOfRangeException(nameof(budget));
        lock (access)
        {
            if (!Open(start)) return 0;
            Search(buffer ??= new(costs.Length), Index(start), -1, mode, budget, output, out int count);
            return count;
        }
    }

    private static int PathLength(SearchBuffer search, int end)
    {
        int count = 0;
        for (int at = end; at >= 0; at = search.Parent[at]) count++;
        return count;
    }

    private void WritePath(SearchBuffer search, int end, Span<Point> path)
    {
        for (int at = end, i = path.Length - 1; at >= 0; at = search.Parent[at])
            path[i--] = Cell(at);
    }

    private static long Estimate(Point cell, Point goal, GridDirections directions)
    {
        long dx = (long)cell.X - goal.X, dy = (long)cell.Y - goal.Y;
        long x = Math.Abs(dx), y = Math.Abs(dy);
        return directions switch
        {
            GridDirections.Hex => 10 * Math.Max(Math.Max(x, y), Math.Abs(dx + dy)),
            GridDirections.Eight => 10 * (x + y) - 6 * Math.Min(x, y),
            _ => 10 * (x + y)
        };
    }

    private bool Search(SearchBuffer search, int first, int last, GridDirections mode, long budget, Span<GridReach> reached, out int count)
    {
        bool flooding = last < 0;
        Point goal = flooding ? default : Cell(last);
        var edges = mode switch
        {
            GridDirections.Hex => Tilemap2DComponent.HexEdges,
            GridDirections.Eight => Tilemap2DComponent.SquareNeighbours,
            _ => Tilemap2DComponent.SquareEdges
        };
        search.Clear();
        search.Set(first, -1, 0, flooding ? 0 : Estimate(Cell(first), goal, mode));
        count = 0;
        while (search.Count != 0)
        {
            int current = search.Pop();
            if (current == last) return true;
            Point cell = Cell(current);
            if (flooding)
            {
                if (count < reached.Length) reached[count] = new(cell, search.Distance[current]);
                count++;
            }
            foreach (var edge in edges)
            {
                Point next = new(cell.X + edge.X, cell.Y + edge.Y);
                bool diagonal = mode == GridDirections.Eight && edge.X != 0 && edge.Y != 0;
                if (!Open(next) || diagonal && (!Open(new(next.X, cell.Y)) || !Open(new(cell.X, next.Y)))) continue;
                int index = Index(next);
                long step = (diagonal ? 14L : 10L) * costs[index];
                if (step > budget - search.Distance[current]) continue;
                long distance = search.Distance[current] + step;
                if (!search.Improves(index, distance)) continue;
                search.Set(index, current, distance, distance + (flooding ? 0 : Estimate(next, goal, mode)));
            }
        }
        return false;
    }

    // One indexed heap entry per cell, regardless of how often its score improves during search.
    private sealed class SearchBuffer(int size)
    {
        public readonly long[] Distance = new long[size];
        public readonly int[] Parent = new int[size];
        private readonly int[] heap = new int[size], slot = new int[size], stamp = new int[size];
        private readonly long[] score = new long[size];
        private int generation;
        public int Count { get; private set; }

        public void Clear()
        {
            Count = 0;
            if (++generation != int.MaxValue) return;
            Array.Clear(stamp);
            generation = 1;
        }

        public bool Improves(int cell, long distance) =>
            stamp[cell] != generation || slot[cell] >= 0 && distance < Distance[cell];

        public void Set(int cell, int previous, long distance, long key)
        {
            if (stamp[cell] != generation)
            {
                stamp[cell] = generation;
                slot[cell] = Count++;
            }
            Distance[cell] = distance;
            Parent[cell] = previous;
            score[cell] = key;
            int position = slot[cell];
            while (position > 0)
            {
                int above = (position - 1) / 2;
                if (!Less(cell, heap[above])) break;
                Put(position, heap[above]);
                position = above;
            }
            Put(position, cell);
        }

        public int Pop()
        {
            int top = heap[0];
            slot[top] = -1;
            int last = heap[--Count];
            if (Count == 0) return top;
            int position = 0;
            while (position < Count / 2)
            {
                int child = 2 * position + 1;
                if (child + 1 < Count && Less(heap[child + 1], heap[child])) child++;
                if (!Less(heap[child], last)) break;
                Put(position, heap[child]);
                position = child;
            }
            Put(position, last);
            return top;
        }

        private bool Less(int a, int b) => score[a] < score[b] || score[a] == score[b] && a < b;

        private void Put(int position, int cell)
        {
            heap[position] = cell;
            slot[cell] = position;
        }
    }
}
