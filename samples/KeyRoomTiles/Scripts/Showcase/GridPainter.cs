using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

// KeyRoomTiles showcase: four Tilemap 2D layers, one per layout (Orthogonal, Isometric, HexPointy, HexFlat), each an island in
// animated water. The painter lays a road across every island with SetTile, one cell per beat (the road tile is a terrain brush,
// so each new cell reconnects its neighbours), lifts the middle road cell back to grass and restores it, then walks a villager
// from cell to cell along each road. Paths and branches come from showcase-src/tiles/scene.py as (x, y) pairs.
// Raises the HUD flags "layouts", "water", "paint".
public sealed class GridPainter : GameBehaviour
{
    public Guid[] Boards = [], Cursors = [], Walkers = [], Calls = [];
    public int[] Kinds = [], Sizes = [];
    public int[] Path0 = [], Path1 = [], Path2 = [], Path3 = [], Branch0 = [], Branch1 = [], Branch2 = [], Branch3 = [];
    private const int Grass = 1, Road = 7;
    private const float Start = 1.2f, Beat = .45f, WalkStep = .4f;
    private float time;
    private int painted = -1, phase;   // phase 0 paint, 1 lifted, 2 restored, 3 walking
    private float phaseAt;
    private int[] walked = [];

    private int[] Path(int b) => b switch { 0 => Path0, 1 => Path1, 2 => Path2, _ => Path3 };
    private int[] Branch(int b) => b switch { 0 => Branch0, 1 => Branch1, 2 => Branch2, _ => Branch3 };
    private Point Step(int b, int i)
    {
        var path = Path(b);
        int n = path.Length / 2;
        var list = i < n ? path : Branch(b);
        int j = i < n ? i : i - n;
        return new(list[2 * j], list[2 * j + 1]);
    }
    private int Steps(int b) => (Path(b).Length + Branch(b).Length) / 2;

    // Top-left of a cell's bounding box in world units (the same formulas as TileGrid).
    private Vector2 CellOrigin(IGameObjects objects, int b, Point c)
    {
        float w = Sizes[2 * b], h = Sizes[2 * b + 1];
        var local = Kinds[b] switch
        {
            1 => new Vector2((c.X - c.Y) * .5f * w, (c.X + c.Y) * .5f * h),
            2 => new Vector2((c.X + c.Y * .5f) * w, c.Y * .75f * h),
            3 => new Vector2(c.X * .75f * w, (c.Y + c.X * .5f) * h),
            _ => new Vector2(c.X * w, c.Y * h),
        };
        objects.TryGetPosition(Boards[b], out var origin);
        return origin + local;
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        time += tick.DeltaTime;
        if (time > .3f) tick.Session.Set("layouts");
        if (time > 1f) tick.Session.Set("water");
        var objects = tick.Objects;
        int longest = 0;
        for (int b = 0; b < Boards.Length; b++) longest = Math.Max(longest, Steps(b));

        if (phase == 0)
        {
            int due = (int)MathF.Floor((time - Start) / Beat);
            if (due <= painted || time < Start) return;
            painted = due;
            if (painted >= longest) { phase = 1; phaseAt = time + .6f; return; }
            tick.Session.Set("paint");
            for (int b = 0; b < Boards.Length; b++)
                if (painted < Steps(b)) Paint(tick, b, Step(b, painted), Road, "road");
            return;
        }
        if (time < phaseAt) return;
        if (phase == 1)
        {
            for (int b = 0; b < Boards.Length; b++) Paint(tick, b, Middle(b), Grass, "grass");
            phase = 2; phaseAt = time + 1.2f;
        }
        else if (phase == 2)
        {
            for (int b = 0; b < Boards.Length; b++) Paint(tick, b, Middle(b), Road, "road");
            phase = 3; phaseAt = time + 1f;
            walked = new int[Boards.Length];
        }
        else if (phase == 3)
        {
            for (int b = 0; b < Boards.Length; b++)
            {
                objects.SetActive(Cursors[b], false);
                int n = Path(b).Length / 2;
                if (walked[b] >= n - 1) { tick.Ui.SetText(Calls[b], $"walked {n} cells"); continue; }
                walked[b]++;
                var target = CellOrigin(objects, b, Step(b, walked[b])) + new Vector2(Sizes[2 * b] / 2f - 10, Sizes[2 * b + 1] / 2f - 24);   // the 20 × 28 walker stands on the cell centre
                objects.Tween(Walkers[b], TweenClock.Game).Move(new Vector2(MathF.Round(target.X), MathF.Round(target.Y)), WalkStep, TweenEase.Linear);
                tick.Ui.SetText(Calls[b], $"walking  cell ({Step(b, walked[b]).X}, {Step(b, walked[b]).Y})");
            }
            phaseAt = time + WalkStep;
        }
    }

    private Point Middle(int b) => Step(b, Path(b).Length / 4);

    private void Paint(ScriptTick tick, int b, Point cell, int tile, string name)
    {
        tick.Objects.SetTile(Boards[b], cell, tile);
        var at = CellOrigin(tick.Objects, b, cell);
        tick.Objects.SetLocalTransform(Cursors[b], new LocalTransform2D(at, 0, Vector2.One));
        tick.Ui.SetText(Calls[b], $"SetTile({cell.X}, {cell.Y}, {name})");
    }
}
