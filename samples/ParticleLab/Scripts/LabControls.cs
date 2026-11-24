using System;
using Engine.Core;

// N opens the next lab scene, 2 the 10,240-particle Stress scene. Space or 1 replays the manual emitter's burst (Particle
// Emitter 2D, Trigger Manual) and those of every object in the Burst group.
public sealed class LabControls : GameBehaviour
{
    private static readonly Guid StressScene = new("f94d7d8d-773d-5014-b991-2f1ef4d32f9d");
    public SceneReference Next;
    public Guid Manual;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("Next")) { tick.Session.LoadScene(Next.Id); return; }
        if (tick.Input.Pressed("Stress")) { tick.Session.LoadScene(StressScene); return; }
        if (!tick.Input.Pressed("Burst")) return;
        if (Manual != Guid.Empty) tick.Objects.EmitParticles(Manual);
        foreach (var id in tick.Objects.FindByGroup("Burst")) tick.Objects.EmitParticles(id);
    }
}
