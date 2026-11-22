using System;
using System.Numerics;
using Engine.Core;

// One prefab works with Rent/Return and with Instantiate/Destroy.
public sealed class PooledBullet : GameBehaviour
{
    private Vector2 velocity;

    public void Launch(Vector2 worldVelocity) => velocity = worldVelocity;

    // A reused bullet must not keep the speed of its previous life.
    public override void OnRent(ScriptLife life) => velocity = Vector2.Zero;

    public override void FixedUpdate(ScriptTick tick)
    {
        var p = tick.Position;
        if (p.X < -20 || p.X > 820 || p.Y < -20 || p.Y > 470) Retire(tick.Objects, tick.ObjectId);
        else tick.Move(velocity * tick.DeltaTime);
    }

    public override void OnTriggerEnter(ScriptContact contact) => Retire(contact.Objects, contact.ObjectId);

    private static void Retire(IGameObjects objects, Guid id)
    {
        if (!objects.Return(id)) objects.Destroy(id);
    }
}
