using System.Drawing;
using Engine.Core;

internal static class GridNavigationChecks
{
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
