using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

public sealed class CourierRoute : GameBehaviour
{
    public Guid Courier, Status, CostLabel;
    private const int CellSize = 40;
    private static readonly string[] Terrain = {
        "#############",
        "#...........#",
        "#..#######..#",
        "#...#####...#",
        "#S....D....G#",
        "#..mmmmmmm..#",
        "#############"
    };

    private GridNavigation grid;
    private GridPath route;
    private GridDirections mode = GridDirections.Four;
    private Point start, gate, goal, position;
    private bool open, cheapMud, walking;
    private int next, ticks;
    private int Width => Terrain[0].Length;
    private int Cost(char tile) => tile switch { '#' => 0, 'D' => open ? 1 : 0, 'm' => cheapMud ? 1 : 12, _ => 1 };

    public override void OnCreate(ScriptLife life)
    {
        var costs = new int[Width * Terrain.Length];
        for (int y = 0; y < Terrain.Length; y++)
        for (int x = 0; x < Width; x++)
        {
            char tile = Terrain[y][x];
            costs[y * Width + x] = Cost(tile);
            if (tile == 'S') start = new Point(x, y);
            if (tile == 'G') goal = new Point(x, y);
            if (tile == 'D') gate = new Point(x, y);
        }
        grid = new GridNavigation(Width, Terrain.Length, costs);
        Place(life.Objects, start);
        Plan(life.ObjectId, life.Objects, life.Ui);
    }

    private void Place(IGameObjects objects, Point cell)
    {
        position = cell;
        // The courier is a child of the tilemap with a centered 16px sprite.
        if (!objects.TryGetLocalTransform(Courier, out var local) ||
            !objects.SetLocalTransform(Courier, local with { Position = new Vector2(cell.X * CellSize + 12, cell.Y * CellSize + 12) }))
            throw new InvalidOperationException("The courier must be present on the route board.");
    }

    private void Plan(Guid board, IGameObjects objects, IGameUi ui)
    {
        route = grid.FindPath(position, goal, mode);
        next = 1;
        ticks = 0;
        int[] tiles = new int[Width * Terrain.Length];
        for (int y = 0; y < Terrain.Length; y++)
        for (int x = 0; x < Width; x++)
            tiles[y * Width + x] = Terrain[y][x] switch
            { '#' => 1, 'm' => 3, 'D' => open ? 5 : 4, 'S' => 6, 'G' => 7, _ => 2 };
        foreach (Point cell in route.Cells)
        {
            int i = cell.Y * Width + cell.X;
            if (tiles[i] == 2) tiles[i] = 8;
            else if (tiles[i] == 3) tiles[i] = 9;
        }
        objects.ReplaceTiles(board, Width, Terrain.Length, tiles);
        ui.SetText(Status, $"{(int)mode}-way    Door {(open ? "open" : "closed")}    Mud x{Cost('m')}");
        ui.SetText(CostLabel, $"Plan: {Math.Max(0, route.Cells.Count - 1)} steps   Cost {route.Cost}");
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        bool replan = false;
        if (tick.Input.Pressed("ToggleGate") && position != gate)
        {
            open = !open;
            grid.SetEntryCost(gate, Cost('D'));
            replan = true;
        }
        if (tick.Input.Pressed("RouteDirections"))
        {
            mode = mode == GridDirections.Four ? GridDirections.Eight : GridDirections.Four;
            replan = true;
        }
        if (tick.Input.Pressed("MudCost"))
        {
            cheapMud = !cheapMud;
            for (int y = 0; y < Terrain.Length; y++)
            for (int x = 0; x < Width; x++)
                if (Terrain[y][x] == 'm') grid.SetEntryCost(new Point(x, y), Cost('m'));
            replan = true;
        }
        if (tick.Input.Pressed("Rebuild"))
        {
            Place(tick.Objects, start);
            walking = false;
            replan = true;
        }
        if (tick.Input.Pressed("WaitTurn"))
        {
            if (position == goal)
            {
                Place(tick.Objects, start);
                replan = true;
            }
            walking = !walking;
        }
        if (replan) Plan(tick.ObjectId, tick.Objects, tick.Ui);
        if (!walking || ++ticks < 12) return;
        ticks = 0;
        if (next < route.Cells.Count) Place(tick.Objects, route.Cells[next++]);
        if (next >= route.Cells.Count) walking = false;
    }
}
