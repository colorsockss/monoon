using System.Numerics;

namespace Engine.Core;

/// <summary>Agents that follow <see cref="NavArea2D"/> routes with simple separation (roadmap 16.15). Each step moves an
/// agent toward its next waypoint at <see cref="Speed"/>, pushes overlapping agents apart (half the overlap each, found
/// through a uniform bucket grid) and slides along closed cells. Positions are updated together after every agent is
/// computed, in index order, so a run is identical on every platform. Re-plans are staggered: an agent re-plans when the
/// area changes, when its goal changes and once every <see cref="ReplanTicks"/> steps, at most <see cref="MaxPlansPerStep"/> per
/// step. Nothing allocates after construction.</summary>
public sealed class NavCrowd2D
{
    private readonly NavArea2D area;
    private readonly Vector2[] position, next, goal, path;
    private readonly int[] pathCount, waypoint, head, link;
    private readonly long[] planned;
    private readonly int stride, bucketColumns, bucketRows;
    private readonly float bucket;
    private long ticks;

    public NavCrowd2D(NavArea2D area, int capacity, int pathPoints = 32)
    {
        if (capacity is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (pathPoints is < 2 or > 1024) throw new ArgumentOutOfRangeException(nameof(pathPoints));
        this.area = area;
        stride = pathPoints;
        position = new Vector2[capacity]; next = new Vector2[capacity]; goal = new Vector2[capacity];
        path = new Vector2[capacity * pathPoints];
        pathCount = new int[capacity]; waypoint = new int[capacity]; link = new int[capacity];
        planned = new long[capacity];
        bucket = 2 * area.AgentRadius;
        bucketColumns = Math.Max(1, (int)MathF.Ceiling(area.Bounds.Width / bucket));
        bucketRows = Math.Max(1, (int)MathF.Ceiling(area.Bounds.Height / bucket));
        if ((long)bucketColumns * bucketRows > NavArea2D.MaxCells) throw new InvalidDataException("Agent radius is too small for this area.");
        head = new int[bucketColumns * bucketRows];
    }

    public NavArea2D Area => area;
    public int Count { get; private set; }
    public int Capacity => position.Length;
    /// <summary>World pixels per second.</summary>
    public float Speed { get; set; } = 60;
    public int ReplanTicks { get; set; } = 60;
    /// <summary>Route queries per step. When the area changes, stale agents re-plan in index order across several steps
    /// (keeping their old route meanwhile) instead of all in one tick.</summary>
    public int MaxPlansPerStep { get; set; } = 16;
    public int PlansLastStep { get; private set; }
    public ReadOnlySpan<Vector2> Positions => position.AsSpan(0, Count);
    public Vector2 Goal(int agent) => goal[Check(agent)];
    public ReadOnlySpan<Vector2> Route(int agent) => path.AsSpan(Check(agent) * stride, pathCount[agent]);
    public bool Arrived(int agent) => position[Check(agent)] == goal[agent];

    public int ArrivedCount
    {
        get { int n = 0; for (int i = 0; i < Count; i++) if (position[i] == goal[i]) n++; return n; }
    }

    private int Check(int agent) => (uint)agent < (uint)Count ? agent : throw new ArgumentOutOfRangeException(nameof(agent));

    public int Add(Vector2 start, Vector2 target)
    {
        if (Count == position.Length) throw new InvalidOperationException("The crowd is full.");
        if (!Shape2D.Finite(start) || !Shape2D.Finite(target)) throw new ArgumentException("Expected finite positions.");
        int i = Count++;
        (position[i], goal[i], pathCount[i], planned[i]) = (start, target, 0, long.MinValue);
        return i;
    }

    public void SetGoal(int agent, Vector2 target)
    {
        if (!Shape2D.Finite(target)) throw new ArgumentException("Expected a finite goal.");
        goal[Check(agent)] = target;
        planned[agent] = long.MinValue;
    }

    /// <summary>Removes every agent and restarts the re-plan schedule; buffers are kept.</summary>
    public void Clear() { Count = 0; ticks = 0; }

    private void Plan(int i)
    {
        pathCount[i] = area.FindPath(position[i], goal[i], path.AsSpan(i * stride, stride));
        waypoint[i] = 1;
        planned[i] = area.Revision;
        PlansLastStep++;
    }

    private int Bucket(Vector2 p)
    {
        int x = Math.Clamp((int)MathF.Floor((p.X - area.Bounds.Left) / bucket), 0, bucketColumns - 1);
        int y = Math.Clamp((int)MathF.Floor((p.Y - area.Bounds.Top) / bucket), 0, bucketRows - 1);
        return y * bucketColumns + x;
    }

    public void Step(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        PlansLastStep = 0;
        Array.Fill(head, -1);
        for (int i = 0; i < Count; i++) { int b = Bucket(position[i]); link[i] = head[b]; head[b] = i; }
        float reach = area.CellSize * .5f, step = Speed * seconds, diameter = 2 * area.AgentRadius, limit = diameter * diameter;
        for (int i = 0; i < Count; i++)
        {
            var p = position[i];
            // A route cut short by the point buffer ends at a corner: plan the rest once the agent stands on it.
            if (PlansLastStep < MaxPlansPerStep && p != goal[i] && (planned[i] != area.Revision || ReplanTicks > 0 && (ticks + i) % ReplanTicks == 0 ||
                pathCount[i] == stride && p == path[i * stride + stride - 1])) Plan(i);
            // Seek: skip waypoints already reached, then move up to one step; the final waypoint snaps exactly onto the goal.
            var to = p;
            float ux = 0, uy = 0;
            int n = pathCount[i];
            if (p != goal[i] && n > 0)
            {
                int w = waypoint[i];
                var at = path[i * stride + Math.Min(w, n - 1)];
                while (w < n - 1 && Distance(p, at) <= reach) at = path[i * stride + ++w];
                waypoint[i] = w;
                float d = Distance(p, at);
                to = d <= step ? at : p + (at - p) * (step / d);
                if (d > 0) { ux = (at.X - p.X) / d; uy = (at.Y - p.Y) / d; }
            }
            // Separation: half the overlap with each neighbour, away from it; coincident agents split by index.
            // A neighbour ahead also adds a sideways push away from its side, so a walker passes a standing agent
            // instead of pressing against it.
            float px = 0, py = 0;
            int bx = Math.Clamp((int)MathF.Floor((p.X - area.Bounds.Left) / bucket), 0, bucketColumns - 1);
            int by = Math.Clamp((int)MathF.Floor((p.Y - area.Bounds.Top) / bucket), 0, bucketRows - 1);
            for (int y = Math.Max(0, by - 1); y <= Math.Min(bucketRows - 1, by + 1); y++)
            for (int x = Math.Max(0, bx - 1); x <= Math.Min(bucketColumns - 1, bx + 1); x++)
            for (int j = head[y * bucketColumns + x]; j >= 0; j = link[j])
            {
                if (j == i) continue;
                float dx = p.X - position[j].X, dy = p.Y - position[j].Y, dd = dx * dx + dy * dy;
                if (dd >= limit) continue;
                if (dd == 0) { px += (j < i ? .5f : -.5f) * diameter; continue; }
                float dist = MathF.Sqrt(dd), share = (diameter - dist) * .5f / dist;
                px += dx * share; py += dy * share;
                if (ux * dx + uy * dy < 0)
                {
                    float side = uy * dx - ux * dy, overlap = (diameter - dist) * .5f;
                    if (side >= 0) { px += uy * overlap; py -= ux * overlap; } else { px -= uy * overlap; py += ux * overlap; }
                }
            }
            // Bound the push to one radius per step so a dense jam relaxes over several steps instead of jumping.
            float push = px * px + py * py, most = area.AgentRadius * area.AgentRadius;
            if (push > most) { float scale = area.AgentRadius / MathF.Sqrt(push); px *= scale; py *= scale; }
            var target = new Vector2(to.X + px, to.Y + py);
            // Slide along closed cells: the whole move, else one axis, else stay. An agent already off the area moves freely.
            if (area.IsOpen(p) && !area.IsOpen(target))
                target = area.IsOpen(new Vector2(target.X, p.Y)) ? new(target.X, p.Y) : area.IsOpen(new Vector2(p.X, target.Y)) ? new(p.X, target.Y) : p;
            next[i] = target;
        }
        Array.Copy(next, position, Count);
        ticks++;
    }

    private static float Distance(Vector2 a, Vector2 b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
