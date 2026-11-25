using System;
using Engine.Core;

// Showcase: Space makes the guard attack the training dummy (Guard animator: Attack while Combo < 3, then Recover → Idle on
// exit time). Each "Swing" clip event knocks the dummy back (its Tilt frames, wood chips). After the third swing the dummy
// springs back and hits the guard: the Hit trigger takes the Any State transition to Hurt. The combo resets once the guard
// has stood idle for a moment. Raises the HUD tag flags "combo", "exit", "event", "hurt".
public sealed class GuardTrainer : GameBehaviour
{
    public Guid Dummy, Chips;
    public SpriteReference DummyUp, DummyBack, DummyForward;
    public float ResetAfter = 2.5f;   // longer than the pause between presses, so three spaced attacks still count as one combo
    private int combo, swings;
    private float idle, dummyTimer, payback = -1;

    public override void FixedUpdate(ScriptTick tick)
    {
        var animator = tick.Objects.Animator(tick.ObjectId);
        if (animator == null) return;
        if (tick.Input.Pressed("Interact"))
        {
            animator.SetInt("Combo", combo++);   // attacks made so far: 0, 1, 2 may attack, the 4th press is refused
            animator.SetTrigger("Attack");
            tick.Session.Set("combo");
        }
        if (animator.State == "Recover") tick.Session.Set("exit");
        // Back at rest: the combo starts over.
        if (animator.State == "Idle" && (idle += tick.DeltaTime) > ResetAfter && combo != 0) animator.SetInt("Combo", combo = 0);
        else if (animator.State != "Idle") idle = 0;
        // The dummy swings back to upright; after the third blow it springs forward and hits back.
        if (dummyTimer > 0 && (dummyTimer -= tick.DeltaTime) <= 0) tick.Objects.SetSprite(Dummy, DummyUp);
        if (payback > 0 && (payback -= tick.DeltaTime) <= 0)
        {
            tick.Objects.SetSprite(Dummy, DummyForward);
            dummyTimer = .25f;
            animator.SetTrigger("Hit");
            tick.Session.Set("hurt");
            payback = -1;
        }
    }

    public override void OnAnimationEvent(ScriptAnimationEvent frame)
    {
        if (frame.Marker.Name != "Swing") return;
        frame.Objects.SetSprite(Dummy, DummyBack);
        frame.Objects.EmitParticles(Chips);
        dummyTimer = .2f;
        frame.Session.Set("event");
        if (++swings % 3 == 0) payback = .3f;
    }
}
