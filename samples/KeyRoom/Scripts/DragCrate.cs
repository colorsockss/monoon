using System;
using System.Numerics;
using Engine.Core;

public sealed class DragCrate : GameBehaviour, IWorldPointerHandler
{
    public string Label = "Crate";
    public Guid Readout;
    private WorldPointerPhase? previous;

    public void OnWorldPointer(WorldPointerEvent pointer)
    {
        if (pointer.Phase == WorldPointerPhase.Drag &&
            pointer.Objects.TryGetWorldMatrix(pointer.ObjectId, out var pose))
        {
            var grab = Vector2.Transform(pointer.GrabLocalPosition, pose);
            pointer.Objects.TryMoveStep(pointer.ObjectId, pointer.WorldPosition - grab);
        }
        if (previous != pointer.Phase)
            pointer.Ui.SetText(Readout, $"{Label} crate  /  {pointer.Phase}");
        previous = pointer.Phase;
    }
}
