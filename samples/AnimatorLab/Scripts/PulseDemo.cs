using System;
using Engine.Core;

public sealed class PulseDemo : GameBehaviour
{
    public Guid EventLabel;
    private int count;

    public override void FixedUpdate(ScriptTick tick)
    {
        var animator = tick.Objects.Animator(tick.ObjectId)!;
        if (tick.Input.Pressed("MoveRight")) animator.SetBool("Running",true);
        if (tick.Input.Pressed("MoveLeft")) animator.SetBool("Running",false);
    }

    public override void OnAnimationEvent(ScriptAnimationEvent frame)
    {
        var marker = frame.Marker;
        frame.Ui.SetText(EventLabel,$"{marker.State}  /  frame {marker.Frame + 1}  /  {++count}  {marker.Name}");
    }
}
