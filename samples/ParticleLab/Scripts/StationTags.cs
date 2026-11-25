using System;
using Engine.Core;

// Showcase: raises a session flag as the hero walks past each effect ("fire", "rain", "water", "rockets", "storm") so the
// HUD's feature tags (uiRect showIf) appear there, and starts the 10,240-particle storm when the hero reaches the portal.
public sealed class StationTags : GameBehaviour
{
    public float Fire = 130, Rain = 290, Water = 470, Rockets = 600, Storm = 800;
    public Guid StormEmitter;
    private bool stormed;

    public override void FixedUpdate(ScriptTick tick)
    {
        float x = tick.Position.X;
        var session = tick.Session;
        if (x > Fire) session.Set("fire");
        if (x > Rain) session.Set("rain");
        if (x > Water) session.Set("water");
        if (x > Rockets) session.Set("rockets");
        if (x <= Storm || stormed) return;
        stormed = true;
        session.Set("storm");
        if (StormEmitter != Guid.Empty) tick.Objects.EmitParticles(StormEmitter);
    }
}
