using System;
using Engine.Core;

// Showcase: the moment a session flag ("key", "chest") is first set, bursts a particle emitter, nudges the screen and
// switches an object on (the open chest). Delay holds all of that back, so the door can open after the camera reaches it.
// A restored save switches the object on without the burst.
public sealed class FlagFx : GameBehaviour
{
    public string Flag = "";
    public Guid Emitter, Show;
    public float Shake, Punch, Delay;
    private bool had;
    private GameTimer wait;

    public override void OnCreate(ScriptLife life)
    {
        had = life.Session.Has(Flag);   // a restored save does not replay it
        if (had && Show != Guid.Empty) life.Objects.SetActive(Show, true);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (!had && tick.Session.Has(Flag))
        {
            had = true;
            if (Delay > 0) wait = tick.Session.Time.StartTimer(Delay); else Fire(tick);
        }
        else if (wait?.Consume() == true) Fire(tick);
    }

    private void Fire(ScriptTick tick)
    {
        if (Show != Guid.Empty) tick.Objects.SetActive(Show, true);
        if (Emitter != Guid.Empty) tick.Objects.EmitParticles(Emitter);
        if (Shake > 0) tick.Session.Screen.Shake(Shake, 0.35f);
        if (Punch > 0) tick.Session.Screen.ZoomPunch(Punch, 0.25f);
    }
}
