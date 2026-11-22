using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

// Entry costs come from the Route tileset (terrain and move cost per tile). The script only swaps cells.
public sealed class CourierRoute : GameBehaviour
{
    public Guid Courier, Status, CostLabel;
    private const int CellSize = 40;
    private const int Wall = 1, Floor = 2, Mud = 3, DoorClosed = 4, DoorOpen = 5, Start = 6, Goal = 7,
        FloorDot = 8, MudDot = 9, DryMud = 10, DryMudDot = 11;
    private static readonly string[] Terrain = {
        "#############",
        "#...........#",
        "#..#######..#",
        "#...#####...#",
        "#S....D....G#",
        "#..mmmmmmm..#",
        "#############"
    };
    // B paints this strip of the dry northern corridor.
    private static readonly Point[] Patch = { new Point(4, 1), new Point(5, 1), new Point(6, 1), new Point(7, 1), new Point(8, 1) };

    private int[] ground;
    private GridNavigation grid;
    private GridPath route;
    private GridDirections mode = GridDirections.Four;
    private Point start, gate, goal, position, mudSample;
    private bool open, cheapMud, painted, walking;
    private int next, ticks;
    private long plannedRevision;
    private int Width => Terrain[0].Length;

    public override void OnCreate(ScriptLife life)
    {
        ground = new int[Width * Terrain.Length];
        for (int y = 0; y < Terrain.Length; y++)
        for (int x = 0; x < Width; x++)
        {
            char tile = Terrain[y][x];
            ground[y * Width + x] = tile switch { '#' => Wall, 'm' => Mud, 'D' => DoorClosed, 'S' => Start, 'G' => Goal, _ => Floor };
            if (tile == 'S') start = new Point(x, y);
            if (tile == 'G') goal = new Point(x, y);
            if (tile == 'D') gate = new Point(x, y);
            if (tile == 'm' && mudSample.IsEmpty) mudSample = new Point(x, y);
        }
        life.Objects.ReplaceTiles(life.ObjectId, Width, Terrain.Length, ground);
        grid = life.Objects.TileNavigation(life.ObjectId, new Rectangle(0, 0, Width, Terrain.Length));
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

    private void Paint(Guid board, IGameObjects objects, Point cell, int tile)
    {
        ground[cell.Y * Width + cell.X] = tile;
        objects.SetTile(board, cell, tile);
    }

    private void Plan(Guid board, IGameObjects objects, IGameUi ui)
    {
        route = grid.FindPath(position, goal, mode);
        plannedRevision = grid.Revision;
        next = 1;
        ticks = 0;
        var dots = new bool[ground.Length];
        foreach (Point cell in route.Cells) dots[cell.Y * Width + cell.X] = true;
        // Dotted tiles share their terrain cost, so redrawing the route never changes the grid.
        for (int i = 0; i < ground.Length; i++)
        {
            int tile = !dots[i] ? ground[i] : ground[i] switch { Floor => FloorDot, Mud => MudDot, DryMud => DryMudDot, var t => t };
            Point cell = new Point(i % Width, i / Width);
            if (!objects.TryGetTile(board, cell, out var shown) || shown.Tile != tile) objects.SetTile(board, cell, tile);
        }
        objects.TryGetTile(board, mudSample, out var mud);
        ui.SetText(Status, $"{(int)mode}-way    Door {(open ? "open" : "closed")}    Mud x{mud.MoveCost}" + (painted ? "    North mud" : ""));
        ui.SetText(CostLabel, $"Plan: {Math.Max(0, route.Cells.Count - 1)} steps   Cost {route.Cost}");
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        bool replan = false;
        if (tick.Input.Pressed("ToggleGate") && position != gate)
        {
            open = !open;
            Paint(tick.ObjectId, tick.Objects, gate, open ? DoorOpen : DoorClosed);
        }
        if (tick.Input.Pressed("RouteDirections"))
        {
            mode = mode == GridDirections.Four ? GridDirections.Eight : GridDirections.Four;
            replan = true;
        }
        if (tick.Input.Pressed("MudCost"))
        {
            cheapMud = !cheapMud;
            for (int i = 0; i < ground.Length; i++)
                if (ground[i] is Mud or DryMud) Paint(tick.ObjectId, tick.Objects, new Point(i % Width, i / Width), cheapMud ? DryMud : Mud);
        }
        if (tick.Input.Pressed("PaintMud"))
        {
            painted = !painted;
            foreach (Point cell in Patch) Paint(tick.ObjectId, tick.Objects, cell, !painted ? Floor : cheapMud ? DryMud : Mud);
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
        // Any cell change that alters an entry cost re-plans from the courier's current cell, even mid-walk.
        if (replan || grid.Revision != plannedRevision) Plan(tick.ObjectId, tick.Objects, tick.Ui);
        if (!walking || ++ticks < 12) return;
        ticks = 0;
        if (next < route.Cells.Count) Place(tick.Objects, route.Cells[next++]);
        if (next >= route.Cells.Count) walking = false;
    }
}
