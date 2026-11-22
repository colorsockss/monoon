using System;
using System.Numerics;
using Engine.Core;

public sealed class PushCrate : GameBehaviour
{
    public ComponentReference<PlayerMovement> Player;
    private bool touchingPlayer;

    public override void OnCollisionEnter(ScriptContact contact)
    {
        if (contact.OtherId == Player.ObjectId) touchingPlayer = true;
    }

    public override void OnCollisionExit(ScriptContact contact)
    {
        if (contact.OtherId == Player.ObjectId) touchingPlayer = false;
    }

    public override void OnDisable(ScriptLife life) => touchingPlayer = false;

    public override void FixedUpdate(ScriptTick tick)
    {
        float horizontal = tick.Direction.X;
        if (!touchingPlayer || horizontal == 0 || !tick.Objects.TryGetPosition(Player.ObjectId,out var player)) return;
        var offset = tick.Position - player;
        if (Math.Abs(offset.X) <= Math.Abs(offset.Y) || offset.X * horizontal <= 0) return;
        if (Player.Resolve(tick.Objects) is {} movement)
            tick.Objects.TryMoveStep(tick.ObjectId, new Vector2(horizontal * movement.MoveSpeed * tick.DeltaTime, 0));
    }
}
