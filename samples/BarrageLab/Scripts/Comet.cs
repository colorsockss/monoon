using System;
using System.Numerics;
using Engine.Core;

// A pooled projectile with a Trail 2D: it bounces inside the arena and returns to the pool after its life.
public sealed class Comet : GameBehaviour
{
    private Vector2 velocity;
    private int life;

    public void Launch(Vector2 worldVelocity, int ticks) => (velocity, life) = (worldVelocity, ticks);

    public override void OnRent(ScriptLife rented) => (velocity, life) = (Vector2.Zero, 0);

    public override void FixedUpdate(ScriptTick tick)
    {
        if (--life < 0)
        {
            if (!tick.Objects.Return(tick.ObjectId)) tick.Objects.Destroy(tick.ObjectId);
            return;
        }
        var next = tick.Position + velocity * tick.DeltaTime;
        if (next.X < 20 || next.X > 772) velocity.X = -velocity.X;
        if (next.Y < 60 || next.Y > 364) velocity.Y = -velocity.Y;
        tick.Move(velocity * tick.DeltaTime);
    }
}
