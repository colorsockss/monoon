using System.Drawing;
using System.Numerics;
using Engine.Core;

// Roadmap 16.15: top-down free movement. Bake around colliders, string-pulled routes, dynamic obstacles,
// allocation-free queries and crowd steps, and the SeedLab Crowd yard (editor bake = runtime bake, 200 arrivals).
internal static class NavigationFreeChecks
{
    [Check(1100, "nav-free")] static void Registered(CheckRun c) => Run(c.Repository);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException("navigation-free: " + message);
    }

    internal static void Run(string repository)
    {
        Routes();
        Yard(repository);
    }

    private static void Routes()
    {
        // 160×80 area, a wall from the top to y=60 at x 76–84: the only way across is the gap below it.
        var wall = Shape2D.Box(new RectangleF(76, 0, 8, 60));
        var area = NavArea2D.Bake(new RectangleF(0, 0, 160, 80), 4, 4, [wall]);
        Require(area.Columns == 40 && area.Rows == 20, "cell grid size");
        Require(!area.IsOpen(new Vector2(72, 30)) && !area.IsOpen(new Vector2(87, 30)) && area.IsOpen(new Vector2(69, 30)),
            "agent radius inflates the wall by 4 px");
        Require(!area.IsOpen(new Vector2(2, 40)) && !area.IsOpen(new Vector2(40, 78)), "area edges keep the agent inside");
        Vector2 start = new(10, 10), goal = new(150, 10);
        Span<Vector2> points = stackalloc Vector2[16];
        int n = area.FindPath(start, goal, points);
        Require(n >= 4 && points[0] == start && points[n - 1] == goal, $"route ends ({n} points)");
        Require(!area.Clear(start, goal), "the straight line crosses the wall");
        for (int i = 1; i < n; i++) Require(area.Clear(points[i - 1], points[i]), $"segment {i} crosses a closed cell");
        bool under = false;
        for (int i = 0; i < n; i++) under |= points[i].Y >= 62 && points[i].X is > 64 and < 96;
        Require(under, "route turns under the wall");
        // String pulling: only the corners at the gap remain (a raw 8-way route would hold dozens of cells).
        Require(n <= 5, $"smoothed route keeps {n} points");
        var first = points[..n].ToArray();
        Require(area.FindPath(start, goal, points) == n && points[..n].SequenceEqual(first), "repeat query differs");
        // A start inside the inflated wall steps to the nearest open centre first.
        int k = area.FindPath(new(74, 30), goal, points);
        Require(k >= 3 && points[0] == new Vector2(74, 30) && area.IsOpen(points[1]), "blocked start leaves through an open centre");
        // Dynamic obstacle: closing the gap makes the goal unreachable, removing it restores the same route.
        var gate = new RectangleF(76, 60, 8, 20);
        area.AddObstacle(gate);
        Require(area.Revision == 1 && area.FindPath(start, goal, points) == 0, "closed gap still routed");
        area.RemoveObstacle(gate);
        Require(area.Revision == 2 && area.FindPath(start, goal, points) == n && points[..n].SequenceEqual(first), "reopened gap changed the route");
        try { area.RemoveObstacle(gate); throw new InvalidDataException("navigation-free: removed an obstacle twice"); }
        catch (InvalidOperationException) { }
        // A short buffer ends at an intermediate corner; querying from there finishes the route.
        Span<Vector2> two = stackalloc Vector2[2];
        Require(area.FindPath(start, goal, two) == 2 && two[1] != goal && area.FindPath(two[1], goal, points) >= 2 && points[area.FindPath(two[1], goal, points) - 1] == goal,
            "truncated route resumes from its last corner");
        var boxed = NavArea2D.Bake(new RectangleF(0, 0, 80, 80), 4, 4, [Shape2D.Box(new(30, 30, 20, 2)), Shape2D.Box(new(30, 48, 20, 2)), Shape2D.Box(new(30, 30, 2, 20)), Shape2D.Box(new(48, 30, 2, 20))]);
        Require(boxed.FindPath(new(10, 10), new(40, 40), points) == 0, "goal sealed in a box is unreachable");

        // Zero allocation after warm-up: path queries, obstacles and crowd steps.
        var crowd = new NavCrowd2D(area, 16);
        for (int i = 0; i < 16; i++) crowd.Add(new(10 + i % 4 * 9, 10 + i / 4 * 9), new(124 + i % 4 * 9, 10 + i / 4 * 9));
        for (int i = 0; i < 5; i++) { area.FindPath(start, goal, points); crowd.Step(1f / 60); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) area.FindPath(start, goal, points);
        area.AddObstacle(gate); area.RemoveObstacle(gate);
        for (int i = 0; i < 1200; i++) crowd.Step(1f / 60);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(allocated == 0, $"queries and crowd steps allocated {allocated} B");
        Require(crowd.ArrivedCount == 16, $"16 agents through the gap ({crowd.ArrivedCount} arrived): " + string.Join(" ", Enumerable.Range(0, 16).Where(i => !crowd.Arrived(i)).Select(i => $"{i}:{crowd.Positions[i]}->{crowd.Goal(i)} r{crowd.Route(i).Length}")));
        Console.WriteLine($"PASS navigation-free routes: wall detour {n} points, blocked start, dynamic gap, truncated resume, sealed goal, 0 B after warm-up");
    }

    private static void Yard(string repository)
    {
        string project = Path.Combine(repository, "samples", "SeedLab");
        var scene = SceneDocument.Load(Path.Combine(project, "Scenes", "ShowcaseCrowd.scene"));
        var region = scene.Objects.Single(o => o.Name == "Yard");
        var assets = ProjectAssets.ReadManifest(project);
        var edited = NavArea2D.BakeDocument(region, scene.Objects, id => id is { } t ? TilePalette.Load(project, assets, t) : null)!;
        // The runtime bake from the loaded scene (scripts removed: this check drives its own crowd).
        foreach (var o in scene.Objects) o.Components = o.Components.RemoveAll(c => c is ScriptComponent);
        using var session = new GameSession(scene);
        var area = session.Current.Navigation.BakeArea(region.Id)!;
        int same = 0;
        for (int y = 0; y < area.Rows; y++) for (int x = 0; x < area.Columns; x++) if (area.IsOpen(x, y) == edited.IsOpen(x, y)) same++;
        Require(area.Columns == 94 && area.Rows == 43 && same == area.Columns * area.Rows, $"editor gizmo bake differs from the game ({same} of {area.Columns * area.Rows} cells, open {area.OpenCells()} vs editor {edited.OpenCells()})");
        int open = area.OpenCells();
        // The CrowdRun layout: 10×20 couriers, 10 px apart, gate closed.
        area.AddObstacle(new RectangleF(232, 250, 24, 50));
        var crowd = new NavCrowd2D(area, 200) { Speed = 72 };
        for (int r = 0; r < 20; r++) for (int c = 0; c < 10; c++) crowd.Add(new(45 + c * 10, 155 + r * 10), new(665 + c * 10, 155 + r * 10));
        ulong hash = 14695981039346656037;
        int tick = 0, done = -1;
        var walls = scene.Objects.Where(o => o.GetComponent<Collider2DComponent>() != null)
            .Select(o => new RectangleF(o.X, o.Y, o.Width, o.Height)).ToArray();
        while (tick < 3600 && done < 0)
        {
            crowd.Step(1f / 60);
            tick++;
            foreach (var p in crowd.Positions)
            {
                hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(p.X)) * 1099511628211;
                hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(p.Y)) * 1099511628211;
                foreach (var w in walls) Require(!w.Contains(p.X, p.Y), $"courier inside {w} at tick {tick}");
            }
            if (crowd.ArrivedCount == 200) done = tick;
        }
        Require(done > 0, $"{crowd.ArrivedCount} of 200 couriers arrived in 60 s");
        float closest = float.MaxValue;
        var final = crowd.Positions;
        for (int i = 0; i < final.Length; i++) for (int j = i + 1; j < final.Length; j++) closest = Math.Min(closest, Vector2.Distance(final[i], final[j]));
        Console.WriteLine($"PASS navigation-free yard: editor bake = runtime bake ({open} of {area.Columns * area.Rows} cells open), " +
            $"200/200 arrived at tick {done}, closest pair {closest:0.0} px, trace FNV {hash:X16}");
    }
}
