using Engine.Core;

public sealed class MovementWorkshop : GameBehaviour
{
    public SceneReference Destination;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("MovementWorkshop")) tick.Session.LoadScene(Destination.Id);
    }
}
