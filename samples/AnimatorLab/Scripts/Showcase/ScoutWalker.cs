using System;
using System.Numerics;
using Engine.Core;

// Showcase: walks the scout with the arrow keys (diagonals too) inside an area and feeds Scout.animator's Move vector and
// Speed, so the Walk state picks one of its 8 direction clips (Idle one of 4). Facing shows the picked direction.
// Raises the "walk8" HUD tag flag on the first diagonal step.
public sealed class ScoutWalker : GameBehaviour
{
    public Guid Picture, Facing;
    public float Speed = 110;
    public Vector2 Min = new(20, 120), Max = new(230, 270);
    private string shown = "";

    public override void FixedUpdate(ScriptTick tick)
    {
        var input = tick.Input;
        var move = new Vector2((input.Held("MoveRight") ? 1 : 0) - (input.Held("MoveLeft") ? 1 : 0),
                               (input.Held("MoveDown") ? 1 : 0) - (input.Held("MoveUp") ? 1 : 0));
        var step = move == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(move) * (Speed * tick.DeltaTime);
        tick.Move(Vector2.Clamp(tick.Position + step, Min, Max) - tick.Position);
        if (tick.Objects.Animator(Picture) is not { } animator) return;
        animator.SetVector2("Move", move);
        animator.SetFloat("Speed", move == Vector2.Zero ? 0 : 1);
        if (move.X != 0 && move.Y != 0) tick.Session.Set("walk8");
        string text = $"<b>Scout</b>   {animator.State}  ·  {AnimationState.Compass[animator.Facing]}";
        if (text != shown) tick.Ui.SetText(Facing, shown = text);
    }
}
