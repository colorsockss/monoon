using System;
using System.Numerics;
using Engine.Core;

public sealed class DragCrate : GameBehaviour, IWorldPointerHandler
{
    public string Label = "Crate";
    public Guid Readout;
    private WorldPointerPhase? previous;
    private bool dragged;

    public void OnWorldPointer(WorldPointerEvent pointer)
    {
        if (pointer.Phase == WorldPointerPhase.Drag &&
            pointer.Objects.TryGetWorldMatrix(pointer.ObjectId, out var pose))
        {
            var grab = Affine2D.Transform(pointer.GrabLocalPosition, pose);
            pointer.Objects.TryMoveStep(pointer.ObjectId, pointer.WorldPosition - grab);
            dragged = true;
        }
        if (previous != pointer.Phase)
            pointer.Ui.SetText(Readout, $"<b>Mouse drag</b>   <color=#BFD0DC>{Label}  /  {pointer.Phase}</color>");
        previous = pointer.Phase;
    }

    // Pointer events carry no session; the next tick raises "dragged" for the HUD tag.
    public override void FixedUpdate(ScriptTick tick)
    {
        if (dragged) tick.Session.Set("dragged");
    }
}
