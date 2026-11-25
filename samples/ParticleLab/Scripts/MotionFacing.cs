using System.Numerics;
using Engine.Core;

// Showcase: on a picture child of a moving body, feeds the body's horizontal velocity to the picture's animator
// (a 4-direction clip set and a Speed for Idle/Walk), so the hero and the guard face and step the way they move.
// Standing still keeps the last facing.
public sealed class MotionFacing : GameBehaviour
{
    public override void FixedUpdate(ScriptTick tick)
    {
        var animator = tick.Objects.Animator(tick.ObjectId);
        if (animator == null || !tick.Objects.TryGetParent(tick.ObjectId, out var parent) || parent is not { } body ||
            !tick.Objects.Motion.TryGetMotion(body, out var motion)) return;
        bool walking = System.Math.Abs(motion.Velocity.X) > 1;
        animator.SetVector2("Move", walking ? new Vector2(motion.Velocity.X, 0) : Vector2.Zero);
        animator.SetFloat("Speed", walking ? 1 : 0);
    }
}
