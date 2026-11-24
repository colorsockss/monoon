using System;
using Engine.Core;

// Drives Guard.animator: every transition is decided by its parameters, conditions, exit times and list order.
public sealed class GuardDemo : GameBehaviour
{
    public Guid StateLabel;
    public Guid EventLabel;
    private int combo, events;
    private bool grounded = true;
    private string shownState;
    private int shownFrame = -2, shownCombo = -1;
    private bool shownGrounded;

    public override void FixedUpdate(ScriptTick tick)
    {
        var animator = tick.Objects.Animator(tick.ObjectId)!;
        var input = tick.Input;
        if (input.Pressed("MoveRight")) animator.SetFloat("Speed", 1);
        if (input.Pressed("MoveLeft")) { animator.SetFloat("Speed", 0); animator.SetInt("Combo", combo = 0); }
        if (input.Pressed("Interact")) { animator.SetInt("Combo", ++combo); animator.SetTrigger("Attack"); }
        if (input.Pressed("MoveUp")) animator.SetTrigger("Hit");
        if (input.Pressed("MoveDown")) animator.SetBool("Grounded", grounded = !grounded);
        // Rewrite the label only when something shown changed.
        if (animator.State == shownState && animator.Frame == shownFrame && combo == shownCombo && grounded == shownGrounded) return;
        (shownState, shownFrame, shownCombo, shownGrounded) = (animator.State, animator.Frame, combo, grounded);
        tick.Ui.SetText(StateLabel, $"{shownState}   frame {shownFrame + 1}   combo {combo}   {(grounded ? "grounded" : "in the air")}");
    }

    public override void OnAnimationEvent(ScriptAnimationEvent frame) =>
        frame.Ui.SetText(EventLabel, $"{frame.Marker.State}  /  {frame.Marker.Name}  /  {++events}");
}
