using Engine.Core;

// Showcase: walks the hero left and right on the ordinary character motor (no jumping).
public sealed class Walker : GameBehaviour
{
    public float Speed = 90;

    public override void FixedUpdate(ScriptTick tick) =>
        tick.Objects.Motion.DriveCharacter(tick.ObjectId, tick.Direction.X * Speed);
}
