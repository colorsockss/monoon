using System;
using System.Numerics;
using Engine.Core;

public sealed class PlayerMovement : GameBehaviour
{
    public float Speed = 240f;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("CarryCoin")) ToggleCoin(tick);
        tick.Move(tick.Direction * (Speed * tick.DeltaTime));
    }

    private static void ToggleCoin(ScriptTick tick)
    {
        var objects = tick.Objects;
        var reach = Shape2D.Circle(tick.Position, 128);
        Guid? nearest = null;
        float distance = float.PositiveInfinity;
        foreach (var hit in objects.Physics.Overlap(reach, new PhysicsFilter(IncludeTriggers: true)))
        {
            var coin = hit.ObjectId;
            if (objects.GetComponent<CollectCoin>(coin) == null || !objects.TryGetParent(coin, out var parent)) continue;
            if (parent == tick.ObjectId)
            {
                objects.SetParent(coin, null, keepWorld: true);
                return;
            }
            if (!objects.TryGetPosition(coin, out var position)) continue;
            float candidate = Vector2.DistanceSquared(position, tick.Position);
            if (candidate < distance) { nearest = coin; distance = candidate; }
        }
        if (nearest is Guid id && objects.SetParent(id, tick.ObjectId, keepWorld: false))
            objects.SetLocalTransform(id, new LocalTransform2D(new Vector2(56, -30), 30, new Vector2(1.5f, .75f)));
    }
}
