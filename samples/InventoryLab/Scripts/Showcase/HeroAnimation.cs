using Engine.Core;

// Showcase: feeds the hero's animator (Hero.animator) the walking direction, so it picks the facing clip and Idle or Walk.
public sealed class HeroAnimation : GameBehaviour
{
    public override void FixedUpdate(ScriptTick tick)
    {
        var animator = tick.Objects.Animator(tick.ObjectId);
        if (animator == null) return;
        animator.SetVector2("Move", tick.Direction);
        animator.SetFloat("Speed", tick.Direction == System.Numerics.Vector2.Zero ? 0 : 1);
    }
}
