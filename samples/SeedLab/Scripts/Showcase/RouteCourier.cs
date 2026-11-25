using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

// SeedLab terrain showcase. The field's tiles carry terrain names and entry costs in Field.tileset (road 1, grass 3, mud 8,
// bridge 1; water and hedges block); the map itself is in the scene (showcase-src/seed/route.py). TileNavigation over the board
// finds the cheapest route and the hero walks it a cell at a time. Rain turns the north road to mud cell by cell, then a bridge
// is laid over the south ford plank by plank: each SetTile changes the grid's Revision and the hero re-plans from where he
// stands. A Line 2D draws the plan and the panel shows its cost. Raises "cost", "rain", "bridge", "arrived".
public sealed class RouteCourier : GameBehaviour
{
    public Guid Courier, Chest, Spark, Plan, Number, Note, Rain, Build;
    public SpriteReference IdleE, IdleN, IdleS, IdleW, WalkE1, WalkE2, WalkE3, WalkE4, WalkN1, WalkN2, WalkN3, WalkN4,
        WalkS1, WalkS2, WalkS3, WalkS4, WalkW1, WalkW2, WalkW3, WalkW4;
    private const int Cell = 32, Mud = 3, Bridge = 5, Width = 18, Height = 9;
    private const float Step = .5f, RainAt = 1.6f, BridgeAt = 3.6f;
    // Cells from route.py's map: the north road between the west lane and the north bridge, and the south ford.
    private static readonly Point[] MudCells = [new(3, 1), new(4, 1), new(5, 1), new(6, 1), new(7, 1)];
    private static readonly Point[] BridgeCells = [new(8, 5), new(9, 5)];
    private static readonly Point Start = new(1, 5), Goal = new(15, 5);   // the cell beside the chest at (16, 5)
    private GridNavigation grid = null!;
    private GridPath route = null!;
    private Point at = Start, heading = new(1, 0);
    private int next = 1, mudDone, bridgeDone;
    private float time, stepClock;
    private long planned = -1;
    private bool arrived;
    private string note = "Road all the way";
    private Vector2 origin;
    private Vector2[] line = new Vector2[64];

    public override void OnCreate(ScriptLife life)
    {
        life.Objects.TryGetPosition(life.ObjectId, out origin);
        grid = life.Objects.TileNavigation(life.ObjectId, new Rectangle(0, 0, Width, Height))
               ?? throw new InvalidOperationException("RouteCourier needs a non-streamed tilemap.");
        life.Objects.SetLocalTransform(Courier, new LocalTransform2D(Feet(Start), 0, Vector2.One));
    }

    private Vector2 Centre(Point c) => origin + new Vector2(c.X * Cell + Cell / 2f, c.Y * Cell + Cell / 2f);
    private Vector2 Feet(Point c) => Centre(c) - new Vector2(21, 48);   // the 42 x 60 hero stands with his feet low in the cell

    public override void FixedUpdate(ScriptTick tick)
    {
        time += tick.DeltaTime;
        var objects = tick.Objects;
        tick.Session.Set("cost");
        // Rain sweeps over the north road; the road turns to mud one cell after another.
        if (time >= RainAt - .4f && time < RainAt + 2.4f) objects.SetActive(Rain, true);
        else if (time >= RainAt + 2.4f) objects.SetActive(Rain, false);
        if (mudDone < MudCells.Length && time >= RainAt + mudDone * .12f)
        {
            objects.SetTile(tick.ObjectId, MudCells[mudDone++], Mud);
            note = "Rain turned the road to mud";
            tick.Session.Set("rain");
        }
        // Then the bridge goes down over the south ford, a plank at a time.
        if (bridgeDone < BridgeCells.Length && time >= BridgeAt + bridgeDone * .25f)
        {
            var cell = BridgeCells[bridgeDone++];
            objects.SetTile(tick.ObjectId, cell, Bridge);
            objects.SetLocalTransform(Build, new LocalTransform2D(Centre(cell), 0, Vector2.One));
            objects.EmitParticles(Build);
            note = "A bridge over the ford";
            tick.Session.Set("bridge");
        }
        if (grid.Revision != planned) Replan(objects, tick.Ui);
        if (arrived) return;
        Animate(objects);
        if ((stepClock += tick.DeltaTime) < Step) return;
        stepClock = 0;
        if (next < route.Cells.Count)
        {
            var to = route.Cells[next++];
            heading = new Point(to.X - at.X, to.Y - at.Y);
            at = to;
            objects.Tween(Courier).Move(Feet(to), Step, TweenEase.Linear);
            DrawPlan(objects);
        }
        else if (at == Goal)
        {
            arrived = true;
            tick.Session.Set("arrived");
            objects.SetSprite(Courier, IdleE);
            objects.EmitParticles(Spark);
            objects.TryGetPosition(Chest, out var chest);
            objects.Tween(Chest).Move(chest - new Vector2(0, 8), .12f, TweenEase.EaseOut).Move(chest, .2f, TweenEase.EaseIn);
            tick.Ui.SetText(Note, "Arrived at the chest");
        }
    }

    private void Animate(IGameObjects objects)
    {
        int f = (int)(time / .12f) % 4;
        SpriteReference pick(SpriteReference a, SpriteReference b, SpriteReference c, SpriteReference d) => f switch { 0 => a, 1 => b, 2 => c, _ => d };
        objects.SetSprite(Courier, heading.X > 0 ? pick(WalkE1, WalkE2, WalkE3, WalkE4) : heading.X < 0 ? pick(WalkW1, WalkW2, WalkW3, WalkW4)
                                  : heading.Y < 0 ? pick(WalkN1, WalkN2, WalkN3, WalkN4) : pick(WalkS1, WalkS2, WalkS3, WalkS4));
    }

    private void Replan(IGameObjects objects, IGameUi ui)
    {
        planned = grid.Revision;
        route = grid.FindPath(at, Goal, GridDirections.Four);
        next = 1;
        DrawPlan(objects);
        // FindPath charges 10 x the entry cost per straight step; four-way routes divide back to the caption's units.
        ui.SetText(Number, (route.Cost / 10).ToString());
        ui.SetText(Note, note);
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
