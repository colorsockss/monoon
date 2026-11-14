using System;
using System.Numerics;
using Engine.Core;

public sealed class MirrorPlayer : GameBehaviour
{
    public Guid Player;
    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Objects.TryGetPosition(Player,out var player))
            tick.Move(new Vector2(1568,864)-player-tick.Position);
    }
}
