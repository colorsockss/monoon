using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Engine.Core;

// Roadmap 16.15: cost of top-down navigation on SeedLab's Crowd yard (94×43 cells of 8 px, agent radius 4, gate closed).
// Five alternating rounds of: the existing GridNavigation.FindPath (8-way, allocating) on the same open cells, NavArea2D.FindPath
// (8-way + string pulling, preallocated), and a 200-courier NavCrowd2D run until every courier arrives (every tick timed, the first 13 plan 16 couriers each).
// Usage: Navigation [rounds] [queries]
static class P
{
    static void Main(string[] args)
    {
        int rounds = args.Length > 0 ? int.Parse(args[0]) : 5, queries = args.Length > 1 ? int.Parse(args[1]) : 2000;
        var bounds = new RectangleF(24, 72, 752, 344);
        RectangleF[] walls = [new(232, 120, 24, 130), new(232, 300, 24, 116), new(432, 72, 24, 258), new(540, 110, 40, 40), new(540, 230, 40, 40)];
        var gate = new RectangleF(232, 250, 24, 50);
        var shapes = walls.Select(Shape2D.Box).ToArray();
        long t0 = Stopwatch.GetTimestamp();
        var area = NavArea2D.Bake(bounds, 8, 4, shapes);
        double bakeMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        area.AddObstacle(gate);
        var costs = new int[area.Columns * area.Rows];
        for (int y = 0; y < area.Rows; y++) for (int x = 0; x < area.Columns; x++) costs[y * area.Columns + x] = area.IsOpen(x, y) ? 1 : 0;
        var grid = new GridNavigation(area.Columns, area.Rows, costs);
        // 200 start/goal pairs: the couriers' rows.
        var starts = new Vector2[200]; var goals = new Vector2[200];
        for (int i = 0; i < 200; i++) { starts[i] = new(45 + i % 10 * 10, 155 + i / 10 * 10); goals[i] = new(665 + i % 10 * 10, 155 + i / 10 * 10); }
        Point Cell(Vector2 p) => new((int)((p.X - bounds.Left) / 8), (int)((p.Y - bounds.Top) / 8));
        var points = new Vector2[32];
        int smoothed = 0, raw = 0;
        // Warm-up (JIT, tiering) outside the timed rounds.
        for (int i = 0; i < 400; i++) { grid.FindPath(Cell(starts[i % 200]), Cell(goals[i % 200]), GridDirections.Eight); area.FindPath(starts[i % 200], goals[i % 200], points); }
        var gridUs = new List<double>(); var navUs = new List<double>(); var tickUs = new List<double>(); var worstUs = new List<double>();
        long gridBytes = 0, navBytes = 0, crowdBytes = 0; int arrivedTick = 0, plans = 0;
        for (int round = 0; round < rounds; round++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            for (int i = 0; i < queries; i++) raw = grid.FindPath(Cell(starts[i % 200]), Cell(goals[i % 200]), GridDirections.Eight).Cells.Count;
            gridUs.Add(Stopwatch.GetElapsedTime(start).TotalMicroseconds / queries);
            gridBytes = (GC.GetAllocatedBytesForCurrentThread() - before) / queries;

            before = GC.GetAllocatedBytesForCurrentThread(); start = Stopwatch.GetTimestamp();
            for (int i = 0; i < queries; i++) smoothed = area.FindPath(starts[i % 200], goals[i % 200], points);
            double navTime = Stopwatch.GetElapsedTime(start).TotalMicroseconds / queries;
            navBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            navUs.Add(navTime);

            var crowd = new NavCrowd2D(area, 200) { Speed = 72 };
            for (int i = 0; i < 200; i++) crowd.Add(starts[i], goals[i]);
            int ticks = 0; double worst = 0; plans = 0;
            before = GC.GetAllocatedBytesForCurrentThread(); start = Stopwatch.GetTimestamp();
            while (crowd.ArrivedCount < 200 && ticks < 3600)
            {
                long s = Stopwatch.GetTimestamp();
                crowd.Step(1f / 60);
                worst = Math.Max(worst, Stopwatch.GetElapsedTime(s).TotalMicroseconds);
                plans += crowd.PlansLastStep; ticks++;
            }
            double tickTime = Stopwatch.GetElapsedTime(start).TotalMicroseconds / ticks;
            crowdBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            tickUs.Add(tickTime);
            worstUs.Add(worst);
            arrivedTick = ticks;
        }
        string Stat(List<double> v) { var s = v.Order().ToList(); return string.Create(CultureInfo.InvariantCulture, $"median {s[s.Count / 2]:0.00} (range {s[0]:0.00}–{s[^1]:0.00})"); }
        Console.WriteLine($"bake 94x43 cells, 5 walls: {bakeMs:0.00} ms (cold, includes JIT); open cells {area.OpenCells()} with gate closed");
        Console.WriteLine($"GridNavigation.FindPath µs/query: {Stat(gridUs)}; {raw} cells; {gridBytes} B/query");
        Console.WriteLine($"NavArea2D.FindPath  µs/query: {Stat(navUs)}; {smoothed} points; {navBytes} B in {queries} queries");
        Console.WriteLine($"NavCrowd2D.Step 200 agents µs/tick: {Stat(tickUs)}; worst tick {Stat(worstUs)}; {crowdBytes} B; arrived tick {arrivedTick}; {plans} route queries ({(double)plans / arrivedTick:0.00}/tick, at most 16)");
    }
}
