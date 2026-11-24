using System;
using System.Numerics;
using System.Linq;
using Engine.Core;
using KeyRoom.Rules;

public sealed class PlayerMovement : GameBehaviour
{
    /// <summary>Walking speed in pixels per second.</summary>
    [Header("Movement"), Range(60, 480)]
    [FormerlySerializedAs("Speed")]
    public float MoveSpeed = 240f;

    [Tooltip("How far a coin can be picked up or dropped, in pixels.")]
    [SerializeField, Min(16)] private float coinReach = 128f;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("CarryCoin")) ToggleCoin(tick, coinReach);
        tick.Move(tick.Direction * (MoveSpeed * tick.DeltaTime));
    }

    private static void ToggleCoin(ScriptTick tick, float reach)
    {
        var objects = tick.Objects;
        var area = Shape2D.Circle(tick.Position, reach);
        Guid? nearest = null;
        float distance = float.PositiveInfinity;
        var collectibles = objects.FindByGroup(LootRules.Collectibles);
        foreach (var hit in objects.Physics.Overlap(area, new PhysicsFilter(IncludeTriggers: true)))
        {
            var coin = hit.ObjectId;
            if (!collectibles.Contains(coin) || !objects.TryGetParent(coin, out var parent)) continue;
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
