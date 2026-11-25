using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

// SeedLab terrain showcase. The field's tiles carry terrain names and entry costs in Field.tileset (road 1, grass 3, mud 8,
// bridge 1; water and trees block). TileNavigation over the board finds the cheapest route; the courier steps along it. At
// RainAt the northern road turns to mud, at BridgeAt the southern road is joined by a bridge across the river. Both are SetTile
// calls; the grid's Revision changes and the courier re-plans from its cell. A Line 2D draws the plan.
// Raises "cost", "rain", "bridge".
public sealed class RouteCourier : GameBehaviour
{
    public Guid Courier, Chest, Plan, Status;
    private const int Cell = 32, Grass = 1, Road = 2, Mud = 3, Water = 4, Bridge = 5, Tree = 6;
    // Timed so rain lands while the courier climbs the west road (cell 2,2) and the bridge while it crosses the grass (6,2).
    private const float Step = .5f, RainAt = 2.2f, BridgeAt = 4.2f;
    private static readonly string[] Map =
    {
        "TTTTTTTTT~~TTTTTTTTT",
        "T.======.BB.======.T",
        "T.=.....~~~......=.T",
        "T.=....~~~.......=.T",
        "T.=....~~~.......=.T",
        "T======~~~========.T",
        "T......~~~.........T",
        "T..TT..~~~...TT....T",
        "T......~~~.........T",
        "TTTTTTT~~~TTTTTTTTTT",
    };
    private static readonly Point Start = new(1, 5), Goal = new(18, 5);
    private GridNavigation grid = null!;
    private GridPath route = null!;
    private Point at = Start;
    private int next = 1;
    private float time, stepClock;
    private long planned = -1;
    private bool rained, bridged;
    private string note = "Cheapest route around the river.";
    private Vector2 origin;
    private Vector2[] line = new Vector2[64];

    private int Width => Map[0].Length;

    public override void OnCreate(ScriptLife life)
    {
        var tiles = new int[Width * Map.Length];
        for (int y = 0; y < Map.Length; y++)
        for (int x = 0; x < Width; x++)
            tiles[y * Width + x] = Map[y][x] switch { 'T' => Tree, '~' => Water, '=' => Road, 'B' => Bridge, _ => Grass };
        tiles[Start.Y * Width + Start.X] = Road;
        tiles[Goal.Y * Width + Goal.X] = Road;
        life.Objects.ReplaceTiles(life.ObjectId, Width, Map.Length, tiles);
        life.Objects.TryGetPosition(life.ObjectId, out origin);
        grid = life.Objects.TileNavigation(life.ObjectId, new Rectangle(0, 0, Width, Map.Length))
               ?? throw new InvalidOperationException("RouteCourier needs a non-streamed tilemap.");
        life.Objects.SetLocalTransform(Chest, new LocalTransform2D(Centre(Goal) - new Vector2(20, 24), 0, Vector2.One));
        Place(life.Objects, Start);
    }

    private Vector2 Centre(Point c) => origin + new Vector2(c.X * Cell + Cell / 2f, c.Y * Cell + Cell / 2f);

    private void Place(IGameObjects objects, Point c)
    {
        at = c;
        objects.SetLocalTransform(Courier, new LocalTransform2D(Centre(c) - new Vector2(10, 22), 0, Vector2.One));
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        time += tick.DeltaTime;
        var objects = tick.Objects;
        tick.Session.Set("cost");
        if (!rained && time >= RainAt)
        {
            rained = true;
            for (int x = 3; x <= 7; x++) objects.SetTile(tick.ObjectId, new Point(x, 1), Mud);
            note = "Rain: the north road is mud now (8 a cell).";
            tick.Session.Set("rain");
        }
        if (!bridged && time >= BridgeAt)
        {
            bridged = true;
            for (int x = 7; x <= 9; x++) objects.SetTile(tick.ObjectId, new Point(x, 5), Bridge);
            note = "A bridge joins the south road.";
            tick.Session.Set("bridge");
        }
        if (grid.Revision != planned) Replan(objects, tick.Ui);
        if (at == Goal || (stepClock += tick.DeltaTime) < Step) return;
        stepClock = 0;
        if (next < route.Cells.Count) { Place(objects, route.Cells[next++]); DrawPlan(objects); }
        if (at == Goal) tick.Ui.SetText(Status, $"Arrived.\n{note}");
    }

    private void Replan(IGameObjects objects, IGameUi ui)
    {
        planned = grid.Revision;
        route = grid.FindPath(at, Goal, GridDirections.Four);
        next = 1;
        DrawPlan(objects);
        // FindPath charges 10 x the entry cost per straight step; four-way routes divide back to the legend's units.
        ui.SetText(Status, $"Route cost <color=#FFD84A>{route.Cost / 10}</color>\n{route.Cells.Count - 1} steps\n<color=#BFD0DC>{note}</color>");
    }

    private void DrawPlan(IGameObjects objects)
    {
        int count = Math.Max(2, route.Cells.Count - next + 1);
        if (line.Length < count) line = new Vector2[count * 2];
        line[0] = Centre(at);
        for (int i = next, j = 1; i < route.Cells.Count; i++, j++) line[j] = Centre(route.Cells[i]);
        if (route.Cells.Count - next + 1 < 2) line[1] = line[0];
        objects.SetLinePoints(Plan, line.AsSpan(0, count));
    }
}
