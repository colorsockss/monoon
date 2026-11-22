using Engine.Core;

public sealed class MoveMarker : GameBehaviour
{
    public override void FixedUpdate(ScriptTick tick) =>
        tick.Move(tick.Direction * (120 * tick.DeltaTime));
}
