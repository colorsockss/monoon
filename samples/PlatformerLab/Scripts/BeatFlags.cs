using System;
using System.Numerics;
using Engine.Core;

// Showcase: turns what the runner is doing into session flags ("ledge", "slope", "seen", "talk", "ride", "rig"), so the HUD's
// feature tags (uiRect showIf) appear at the moment each engine feature is on screen. Flags stay set once raised.
public sealed class BeatFlags : GameBehaviour
{
    public Guid Ledge, Ramp, Ferry, Lookout, Rig;
    public float Sight = 112, RigNear = 200;

    public override void FixedUpdate(ScriptTick tick)
    {
        var session = tick.Session;
        if (tick.Objects.Motion.TryGetMotion(tick.ObjectId, out var body))
        {
            if (body.Support == Ledge) session.Set("ledge");
            if (body.Support == Ramp) session.Set("slope");
            if (body.Support == Ferry) session.Set("ride");
        }
        if (tick.Objects.Cutscene.HasValue) session.Set("talk");
        if (Lookout != Guid.Empty && tick.Objects.Navigation.CanSee(Lookout, tick.ObjectId, Sight)) session.Set("seen");
        if (Rig != Guid.Empty && tick.Objects.TryGetPosition(Rig, out var rig) && Vector2.Distance(rig, tick.Position) < RigNear) session.Set("rig");
    }
}
