using Engine.Core;

public sealed class PlayerMovement : GameBehaviour
{
    public float Speed = 240f;

    public override void FixedUpdate(ScriptTick tick)
    {
        tick.Move(tick.Direction * (Speed * tick.DeltaTime));
    }
}
