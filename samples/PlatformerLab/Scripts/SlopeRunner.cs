using System;
using System.Globalization;
using Engine.Core;

// Player of the Slopes scene: a capsule character walks over a row of 8 px tiles and up an edge-collider slope, then back.
// A snag is a tick where the grounded runner was asked to walk but moved less than half a step.
public sealed class SlopeRunner : GameBehaviour
{
    public Guid Readout, Result, Marker;
    public float Speed = 150, Jump = 320, TopX = 480;
    private int ticks, snags, markers;
    private float lastX, drive, startX, farthest;
    private bool reported;
    private string shown = "";

    public override void FixedUpdate(ScriptTick tick)
    {
        var motion = tick.Objects.Motion;
        if (!motion.TryGetMotion(tick.ObjectId, out var body)) return;
        float x = tick.Position.X;
        if (ticks++ == 0) startX = lastX = farthest = x;
        if (body.Grounded && drive != 0 && MathF.Abs(x - lastX) < MathF.Abs(drive) * tick.DeltaTime * .5f) snags++;
        farthest = MathF.Max(farthest, x);
        drive = tick.Direction.X * Speed;
        lastX = x;
        motion.DriveCharacter(tick.ObjectId, drive, tick.Input.Pressed("Jump") ? Jump : 0);
        string text = string.Create(CultureInfo.InvariantCulture,
            $"X  {x:F1}      Y  {tick.Position.Y:F1}      {(body.Grounded ? "Ground" : "Air")}      snags {snags}");
        if (text != shown) tick.Ui.SetText(Readout, shown = text);
        if (reported || farthest < TopX || MathF.Abs(x - startX) > 2 || drive != 0) return;
        reported = true;
        tick.Ui.SetText(Result, snags == 0 ? "Round trip without a snag" : $"Round trip with {snags} snags");
        var report = new GameData();
        report.Set("snags", snags);
        report.Set("markers", markers);
        report.Set("ticks", ticks);
        if (tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(1, report);
        tick.Session.Log($"Slopes round trip: {snags} snags, {markers} marker entries, {ticks} ticks.");
    }

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId == Marker) markers++;
    }
}
