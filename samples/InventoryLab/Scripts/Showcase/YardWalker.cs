using Engine.Core;

// Showcase: top-down walking that stops while a conversation is open (HerbYard raises the "talking" flag).
public sealed class YardWalker : GameBehaviour
{
    public float Speed = 150;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (!tick.Session.Has("talking")) tick.Move(tick.Direction * (Speed * tick.DeltaTime));
    }
}
