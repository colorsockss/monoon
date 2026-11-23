using System;
using Engine.Core;

// Box2D v3 has no pulley joint. Each deck slides on a vertical Slider Joint 2D with ±16 px limits and no gravity
// (the counterweights balance); this script ties their vertical speeds like a rope over a wheel: what one deck
// gains downward the other gains upward, so a rider's weight on one deck lifts the other.
public sealed class PulleyPair : GameBehaviour
{
    public Guid Partner;
    public float Stiffness = 4; // Recovers rope length lost to rounding, in 1/s.
    private float rope;
    private bool started;

    public override void FixedUpdate(ScriptTick tick)
    {
        var motion = tick.Objects.Motion;
        if (!motion.TryGetMotion(tick.ObjectId, out var self) || !motion.TryGetMotion(Partner, out var other) ||
            !tick.Objects.TryGetPosition(Partner, out var partner)) return;
        float length = tick.Position.Y + partner.Y;
        if (!started) { rope = length; started = true; }
        float shared = (self.Velocity.Y - other.Velocity.Y) * .5f;
        float drift = (length - rope) * Stiffness * .5f;
        motion.SetVelocity(tick.ObjectId, new(0, shared - drift));
        motion.SetVelocity(Partner, new(0, -shared - drift));
    }
}
