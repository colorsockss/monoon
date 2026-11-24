using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

// 200 couriers cross the yard to their own slot on the right. The area is baked once from the wall colliders;
// the gate is a dynamic obstacle, so opening or closing it re-plans every courier from where it stands.
public sealed partial class CrowdRun : GameBehaviour
{
    public Guid Units, Gate, Status;
    private const int Columns = 10, Rows = 20, Spacing = 10;
    // The gate sprite covers this box in the middle opening of the first wall.
    private static readonly RectangleF GateBox = new RectangleF(232, 250, 24, 50);
    private static readonly Color[] Bands = { Color.FromArgb(255, 242, 207), Color.FromArgb(132, 220, 198), Color.FromArgb(255, 166, 158), Color.FromArgb(165, 192, 255) };
    private NavArea2D area;
    private NavCrowd2D crowd;
    private SpritePool2D pool;
    private readonly Vector2[] previous = new Vector2[Columns * Rows];
    private bool walking = true, gateOpen;
    private int ticks, shownArrived = -1;

    // The test copy adds a body that records positions; the shipped game compiles the call away.
    partial void Traced(ScriptTick tick, NavCrowd2D crowd, int ticks);

    public override void OnCreate(ScriptLife life)
    {
        area = life.Objects.Navigation.BakeArea(life.ObjectId) ?? throw new InvalidOperationException("The yard needs a Navigation Region 2D.");
        crowd = new NavCrowd2D(area, Columns * Rows) { Speed = 72 };
        pool = life.Objects.CreateSpritePool(Units, Columns * Rows);
        area.AddObstacle(GateBox);
        Restart();
    }

    private void Restart()
    {
        crowd.Clear();
        for (int row = 0; row < Rows; row++)
        for (int column = 0; column < Columns; column++)
            crowd.Add(new Vector2(45 + column * Spacing, 155 + row * Spacing), new Vector2(665 + column * Spacing, 155 + row * Spacing));
        crowd.Positions.CopyTo(previous);
        ticks = 0;
        shownArrived = -1;
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("WaitTurn")) walking = !walking;
        if (tick.Input.Pressed("Rebuild")) { Restart(); walking = true; }
        if (tick.Input.Pressed("ToggleGate"))
        {
            gateOpen = !gateOpen;
            if (gateOpen) area.RemoveObstacle(GateBox); else area.AddObstacle(GateBox);
            tick.Objects.SetActive(Gate, !gateOpen);
            shownArrived = -1;
        }
        crowd.Positions.CopyTo(previous);
        if (walking) { crowd.Step(tick.DeltaTime); ticks++; }
        // Redraw from the previous to the new positions so the pool interpolates between fixed steps.
        pool.Reset();
        var positions = crowd.Positions;
        float rate = tick.DeltaTime > 0 ? 1 / tick.DeltaTime : 0;
        for (int i = 0; i < positions.Length; i++)
            pool.TrySpawn(previous[i], (positions[i] - previous[i]) * rate, Bands[i / Columns / 5]);
        pool.Advance(tick.DeltaTime, area.Bounds);
        int arrived = crowd.ArrivedCount;
        if (arrived != shownArrived && (ticks % 15 == 0 || arrived == positions.Length))
        {
            shownArrived = arrived;
            tick.Ui.SetText(Status, $"Arrived {arrived} / {positions.Length}    Gate {(gateOpen ? "open" : "closed")}    {ticks / 60f:0.0} s");
        }
        Traced(tick, crowd, ticks);
    }
}
