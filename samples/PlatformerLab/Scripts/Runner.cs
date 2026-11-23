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

    private bool sawOutbound, sawReturn, animationReported;
    private int rearPlants, frontPlants, rampSwings;
    private float maxSoleError, maxAngleError, maxFootSlip, maxRideSlip, maxTravel;
    private Vector2? lastRear, lastFront;

    // Record the actual post-physics pose from the preceding step. The incline endpoints
    // are transformed separately so the check does not simply reread the IK raycast.
    private void MeasureAnimation(ScriptTick tick, MotionState2D motion)
    {
        if (animationReported) return;
        if (motion.Support==Ramp && tick.Objects.TryGetWorldMatrix(Ramp,out var matrix))
        {
            // Platform.scene defines the ramp edge from (0,90) to (190,0) in local pixels.
            var a=Affine2D.Transform(new Vector2(0,90),matrix);
            var b=Affine2D.Transform(new Vector2(190,0),matrix);
            var tangent=Vector2.Normalize(b-a);
            void Foot(string name,ref int planted,ref Vector2? previous)
            {
                if (!tick.Objects.TryGetBonePose(tick.ObjectId,name,out var foot) || foot.Ground!=Ramp ||
                    foot.Start.X<a.X+12 || foot.Start.X>b.X-12) {previous=null;return;}
                var axis=Vector2.Normalize(foot.End-foot.Start);
                float alignment=Math.Abs(axis.X*tangent.Y-axis.Y*tangent.X);
                maxAngleError=Math.Max(maxAngleError,float.RadiansToDegrees(MathF.Asin(Math.Min(1,alignment))));
                float height=(foot.Start.X-a.X)*tangent.Y/tangent.X+a.Y-foot.Start.Y;
                if (!foot.Planted) {if(height>2)rampSwings++;previous=null;return;}
                if (Math.Abs(motion.Velocity.X)>1) planted++;
                maxSoleError=Math.Max(maxSoleError,Math.Abs(height));
                if(previous is Vector2 last)maxFootSlip=Math.Max(maxFootSlip,Vector2.Distance(foot.Start,last));
                previous=foot.Start;
            }
            Foot("RearFoot",ref rearPlants,ref lastRear);
            Foot("FrontFoot",ref frontPlants,ref lastFront);
        }
        else {lastRear=null;lastFront=null;}
        if (rideTicks==0 || motion.Support!=Ferry || !tick.Objects.TryGetPosition(Ferry,out var deck)) return;
        float distance=deck.X-ferryAt.X;
        maxTravel=Math.Max(maxTravel,distance);
        maxRideSlip=Math.Max(maxRideSlip,((tick.Position-boardedAt)-(deck-ferryAt)).Length());
        if (distance>133.99f) sawOutbound=true;
        if (sawOutbound && distance<100) sawReturn=true;
        if (!sawReturn || distance>.01f) return;
        animationReported=true;
        bool passed=rearPlants>0 && frontPlants>0 && rampSwings>0 && maxSoleError<.1f && maxAngleError<.1f &&
            maxFootSlip<.1f && maxRideSlip<.1f;
        var data=new GameData();
        data.Set("passed",passed);data.Set("rearPlants",rearPlants);data.Set("frontPlants",frontPlants);
        data.Set("swingSamples",rampSwings);data.Set("maxSoleError",maxSoleError);data.Set("maxAngleError",maxAngleError);
        data.Set("maxPlantedSlip",maxFootSlip);data.Set("maxRideSlip",maxRideSlip);
        data.Set("outbound",maxTravel);data.Set("returned",distance);
        if(tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(5,data);
        tick.Session.Log($"Shuttle round trip: {maxTravel:F1} px; foot IK error: {maxSoleError:F3} px.");
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var motion = tick.Objects.Motion;
        if (!motion.TryGetMotion(tick.ObjectId, out var body)) return;
        MeasureAnimation(tick,body);
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
