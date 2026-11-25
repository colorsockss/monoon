using System;
using System.Numerics;
using Engine.Core;

// Showcase: lays chain-link sprites evenly along the distance joint between Anchor and Bob each tick, so the pendulum
// visibly hangs from its beam. The links are unrotated; at this swing they read as a chain.
public sealed class ChainLinks : GameBehaviour
{
    public Guid Anchor, Bob;
    public Guid[] Links = [];
    public Vector2 AnchorCentre = new(4, 4), BobCentre = new(11, 11), LinkSize = new(9, 10);

    public override void FixedUpdate(ScriptTick tick)
    {
        if (!tick.Objects.TryGetPosition(Anchor, out var a) || !tick.Objects.TryGetPosition(Bob, out var b)) return;
        a += AnchorCentre; b += BobCentre;
        for (int i = 0; i < Links.Length; i++)
        {
            float t = (i + .5f) / Links.Length;
            var centre = new Vector2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);   // plain arithmetic, not Vector2.Lerp (transform-2d.md)
            tick.Objects.SetLocalTransform(Links[i], new LocalTransform2D(centre - LinkSize / 2, 0, Vector2.One));
        }
    }
}
