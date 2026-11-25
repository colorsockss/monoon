using Engine.Core;

// Showcase: raises the crowd scene's HUD tag flags, "crowd" from the start and "gate" once the gate is toggled
// (CrowdRun itself reads the same ToggleGate action).
public sealed class CrowdTags : GameBehaviour
{
    public override void OnCreate(ScriptLife life) => life.Session.Set("crowd");

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("ToggleGate")) tick.Session.Set("gate");
    }
}
