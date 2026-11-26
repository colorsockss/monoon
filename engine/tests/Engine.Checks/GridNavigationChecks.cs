using System.Drawing;
using Engine.Core;

internal static class GridNavigationChecks
{
    [Check(1000)] static void Registered(CheckRun c) => Run();
    [Check(1001, "hex-navigation")] static void Hex(CheckRun _)
    {
        const int width = 9, height = 7;
        int[] terrain = Enumerable.Range(0, width * height).Select(i => i % 11 == 0 ? 0 : 1 + i % 4).ToArray();
        var grid = new GridNavigation(width, height, terrain, GridDirections.Hex);
        Point start = new(3, 2);
        int startIndex = start.Y * width + start.X;
        var shortest = Enumerable.Repeat(long.MaxValue, terrain.Length).ToArray();
        shortest[startIndex] = 0;
        // Independent relaxation of all ordered cell pairs on the small fixture.
        for (int pass = 0; pass < terrain.Length; pass++)
        for (int a = 0; a < terrain.Length; a++)
        for (int b = 0; b < terrain.Length; b++)
        {
            if (shortest[a] == long.MaxValue || terrain[b] == 0) continue;
            int dx = a % width - b % width, dy = a / width - b / width;
            if (Math.Max(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Abs(dx + dy)) != 1) continue;
            shortest[b] = Math.Min(shortest[b], shortest[a] + 10 * terrain[b]);
        }

        var path = new Point[terrain.Length];
        var reachable = new GridReach[terrain.Length];
        void Compare()
        {
            for (int i = 0; i < terrain.Length; i++)
            {
                Point goal = new(i % width, i / width);
                int count = grid.FindPath(start, goal, path, out long cost);
                if (shortest[i] == long.MaxValue)
                {
                    if (count != 0 || cost != 0) throw new InvalidDataException("Hex route crossed blocked terrain.");
                    continue;
                }
                if (count == 0 || cost != shortest[i] || path[0] != start || path[count - 1] != goal)
                    throw new InvalidDataException($"Wrong hex route from {start} to {goal}.");
                for (int step = 1; step < count; step++)
                {
                    int dx = path[step].X - path[step - 1].X, dy = path[step].Y - path[step - 1].Y;
                    if (Math.Max(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Abs(dx + dy)) != 1)
                        throw new InvalidDataException("Hex route returned nonadjacent cells.");
                }
            }
            int reached = grid.Reachable(start, 65, reachable);
            int expected = 0;
            foreach (long distance in shortest) if (distance <= 65) expected++;
            if (reached != expected) throw new InvalidDataException("Wrong hex reach count.");
            for (int i = 0; i < reached; i++)
            {
                var item = reachable[i];
                if (item.Cost != shortest[item.Cell.Y * width + item.Cell.X] || item.Cost > 65 ||
                    i > 0 && item.Cost < reachable[i - 1].Cost)
                    throw new InvalidDataException("Wrong hex reach cost or ordering.");
            }
        }
        Compare();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 40; i++) Compare();
        if (GC.GetAllocatedBytesForCurrentThread() != before) throw new InvalidDataException("Buffered hex routes allocated.");
        Console.WriteLine("PASS weighted axial hex routes and movement range agree with an independent model, zero after warmup");
    }

    internal static void Run()
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
        Point start = new(0, 0), goal = new(1, 1);
        var grid = new GridNavigation(2, 2, [1, 0, 0, 1]);
        Require(grid.FindPath(start, goal, GridDirections.Eight).Cells.Count == 0, "Cut across two blocked corners");
        grid.SetEntryCost(new(1, 0), 2);
        var detour = grid.FindPath(start, goal, GridDirections.Eight);
        Require(detour.Cost == 30 && detour.Cells.SequenceEqual([start, new Point(1,0), goal]), "Cut across a single blocked corner or ignored entry cost");
        grid.SetEntryCost(new(0, 1), 1);
        Require(grid.FindPath(start, goal, GridDirections.Eight).Cost == 14 && grid.FindPath(start, goal).Cost == 20, "Invalid diagonal or cardinal cost");
        Require(detour.Cost == 30 && detour.Cells.Count == 3, "Editing the grid invalidated an earlier route");
        Require(grid.FindPath(start, start).Cells.SequenceEqual([start]), "Missing zero-step route");
        grid.SetEntryCost(goal, 0);
        Require(grid.FindPath(goal, goal).Cells.Count == 0 && grid.FindPath(new(-1, 0), start).Cells.Count == 0 &&
            grid.FindPath(start, goal).Cells.Count == 0, "Blocked or out-of-bounds endpoints accepted");
        var expensive = new GridNavigation(3, 1, [1, int.MaxValue, int.MaxValue]);
        Require(expensive.FindPath(start, new(2, 0)).Cost == 20L * int.MaxValue, "Terrain cost overflow");
        try { _ = new GridNavigation(1, 1, [-1]); throw new InvalidDataException("Accepted negative weight"); }
        catch (ArgumentOutOfRangeException) { }
        Console.WriteLine("PASS grid navigation: costs, corner clearance, blocked endpoints, stable snapshots and long scores");
    }
}
