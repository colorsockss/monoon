using System.Numerics;
using Engine.Core;

// A pooled damage number: rises 30 px over 60 ticks, then goes back to the pool.
public sealed class RisingNumber : GameBehaviour
{
    private int age;

    public override void OnRent(ScriptLife life) => age = 0;

    public override void FixedUpdate(ScriptTick tick)
    {
        tick.Move(new Vector2(0, -.5f));
        if (++age >= 60 && !tick.Objects.Return(tick.ObjectId)) tick.Objects.Destroy(tick.ObjectId);
    }
}
