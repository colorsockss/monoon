using System;
using Engine.Core;

public sealed class KeySequence : GameBehaviour
{
    public Guid Collector, KeyImage, DoorBlocker, DoorVisual, DoorCamera, PlayerCamera;
    private float remaining;

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId != Collector || !contact.Session.Set("key")) return;
        contact.Deactivate(KeyImage);
        contact.Deactivate(DoorBlocker);
        contact.PlayEffect(DoorVisual);
        contact.Cameras.TrySetActive(DoorCamera);
        remaining = 1;
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (remaining <= 0) return;
        remaining -= tick.DeltaTime;
        if (remaining <= 0.00001f) { remaining = 0; tick.Cameras.TrySetActive(PlayerCamera); }
    }
}
