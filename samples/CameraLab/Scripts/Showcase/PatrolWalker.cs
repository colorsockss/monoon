using System;
using System.Numerics;
using Engine.Core;

// Showcase: walks the partner along Waypoints (marker objects) in a loop, pausing briefly at each, and feeds the walking
// direction to its picture's animator (Hero.animator style: Move vector, Speed).
public sealed class PatrolWalker : GameBehaviour
{
    public Guid[] Waypoints = [];
    public Guid Picture;
    public float Speed = 110, Pause = .6f;
    private int next;
    private float wait;

    public override void FixedUpdate(ScriptTick tick)
    {
        var animator = tick.Objects.Animator(Picture);
        if (Waypoints.Length == 0 || !tick.Objects.TryGetPosition(Waypoints[next], out var goal)) return;
        var to = goal - tick.Position;
        float step = Speed * tick.DeltaTime;
        if (wait > 0 || to.Length() <= step)
        {
            if (wait <= 0) { tick.Move(to); wait = Pause; }
            else if ((wait -= tick.DeltaTime) <= 0) next = (next + 1) % Waypoints.Length;
            animator?.SetFloat("Speed", 0);
            return;
        }
        var direction = Vector2.Normalize(to);
        tick.Move(direction * step);
        // The facing set picks N/E/S/W from the larger axis, like the arrow keys.
        animator?.SetVector2("Move", MathF.Abs(direction.X) >= MathF.Abs(direction.Y) ? new(MathF.Sign(direction.X), 0) : new(0, MathF.Sign(direction.Y)));
        animator?.SetFloat("Speed", 1);
    }
}
