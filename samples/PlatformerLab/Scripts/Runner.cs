using System;
using System.Numerics;
using Engine.Core;

public sealed class Runner : GameBehaviour
{
    public Guid Ramp, Ferry, Anchor, Bob, Readout, Result, Crossing;
    public float Speed = 150, Jump = 320;
    private bool guided, launched, finished, touchedRamp, crossingPlayed;
    private int rideTicks;
    private Vector2 start, boardedAt, ferryAt;
    private float rise, drift, jointError, swing;

    private bool sawOutbound, sawReturn, rideReported;
    private float maxRideSlip, maxTravel;

    // Record the rider on the shuttle from the preceding step: it must travel out and back without slipping on the deck.
    private void MeasureRide(ScriptTick tick, MotionState2D motion)
    {
        if (rideReported) return;
        if (rideTicks==0 || motion.Support!=Ferry || !tick.Objects.TryGetPosition(Ferry,out var deck)) return;
        float distance=deck.X-ferryAt.X;
        maxTravel=Math.Max(maxTravel,distance);
        maxRideSlip=Math.Max(maxRideSlip,((tick.Position-boardedAt)-(deck-ferryAt)).Length());
        if (distance>133.99f) sawOutbound=true;
        if (sawOutbound && distance<100) sawReturn=true;
        if (!sawReturn || distance>.01f) return;
        rideReported=true;
        var data=new GameData();
        data.Set("passed",maxRideSlip<.1f);data.Set("maxRideSlip",maxRideSlip);
        data.Set("outbound",maxTravel);data.Set("returned",distance);
        if(tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(5,data);
        tick.Session.Log($"Shuttle round trip: {maxTravel:F1} px; ride slip: {maxRideSlip:F3} px.");
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var motion = tick.Objects.Motion;
        if (!motion.TryGetMotion(tick.ObjectId, out var body)) return;
        MeasureRide(tick,body);
        if (!launched && (tick.Input.Pressed("Route") || tick.Direction.X != 0))
        {
            launched = true;
            guided = tick.Input.Pressed("Route");
            start = tick.Position;
            motion.ApplyImpulse(Bob, new Vector2(120, 0));
        }
        if (!crossingPlayed && body.Support == Ferry && tick.Objects.PlayCutscene(Crossing))
        {
            crossingPlayed = true;
            guided = false; // Even an early skip hands control straight back to the player.
        }
        bool inDialogue = tick.Objects.Cutscene.HasValue;
        float axis = inDialogue ? 0 : guided ? tick.Position.X < 503 && rideTicks == 0 ? 1 : 0 : tick.Direction.X;
        motion.DriveCharacter(tick.ObjectId, axis * Speed, !inDialogue && tick.Input.Pressed("Jump") ? Jump : 0);
        if (!launched) return;
        tick.Objects.TryGetPosition(Anchor, out var anchor);
        tick.Objects.TryGetPosition(Bob, out var bob);
        var separation = bob + new Vector2(11) - (anchor + new Vector2(4));
        jointError = Math.Max(jointError, Math.Abs(separation.Length() - 64));
        swing = Math.Max(swing, Math.Abs(separation.X));
        if (body.Support == Ramp) touchedRamp = true;
        if (touchedRamp && rideTicks == 0) rise = Math.Max(rise, start.Y - tick.Position.Y);
        if (body.Support == Ferry && axis == 0 && !finished)
        {
            tick.Objects.TryGetPosition(Ferry, out var ferry);
            if (rideTicks == 0)
            {
                boardedAt = tick.Position;
                ferryAt = ferry;
                tick.Objects.PlayTween(Ferry);
            }
            float travel = ferry.X - ferryAt.X;
            drift = Math.Max(drift, ((tick.Position - boardedAt) - (ferry - ferryAt)).Length());
            if (++rideTicks >= 100)
            {
                finished = true;
                bool passed = touchedRamp && rise > 85 && travel > 110 && drift < .05f && jointError < .1f && swing > 10;
                tick.Ui.SetText(Result, passed ? "Route complete" : "Check route");
                tick.Ui.SetText(Readout, $"Climb  {rise:F1} px      Ride  {travel:F1} px      Slip  {drift:F3} px");
                var report = new GameData();
                report.Set("passed", passed); report.Set("rampContact", touchedRamp);
                report.Set("climb", rise); report.Set("ride", travel); report.Set("slip", drift);
                report.Set("jointError", jointError); report.Set("swing", swing); report.Set("rideTicks", rideTicks);
                if (tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(1, report);
                tick.Session.Log($"Platformer route: {passed}, rise {rise:F2}, ride {travel:F2}, slip {drift:F4}.");
                guided = false;
            }
        }
        else if (!finished && rideTicks > 0) rideTicks = 0;
        if (!finished)
        {
            string stage = body.Support == Ferry ? "Shuttle" : body.Support == Ramp ? "Incline" : body.Grounded ? "Ground" : "Air";
            tick.Ui.SetText(Result, stage);
            tick.Ui.SetText(Readout, $"Climb  {rise:F1} px      X  {tick.Position.X:F1}      Y  {tick.Position.Y:F1}");
        }
    }
}
