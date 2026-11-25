using System;
using Engine.Core;

// Space or 1 replays the manual emitter's burst (Particle Emitter 2D, Trigger Manual) and those of every object in the Burst
// group.
public sealed class LabControls : GameBehaviour
{
    public Guid Manual;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (!tick.Input.Pressed("Burst")) return;
        if (Manual != Guid.Empty) tick.Objects.EmitParticles(Manual);
        foreach (var id in tick.Objects.FindByGroup("Burst")) tick.Objects.EmitParticles(id);
    }
}
