using System;
using Engine.Core;

public sealed class KeySequence : GameBehaviour
{
    public Guid Collector, KeyImage, DoorBlocker, DoorVisual, DoorCamera, PlayerCamera;
    private GameTimer returnToPlayer;

    public override void OnCreate(ScriptLife life)
    {
        if (!life.Session.Has("key")) return;
        life.Objects.SetActive(KeyImage, false);
        life.Objects.SetActive(DoorBlocker, false);
        life.Objects.SetActive(DoorVisual, false);
    }

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId != Collector || !contact.Session.Set("key")) return;
        contact.Deactivate(KeyImage);
        contact.Deactivate(DoorBlocker);
        contact.PlayEffect(DoorVisual);
        contact.Cameras.TrySetActive(DoorCamera);
        returnToPlayer = contact.Session.Time.StartTimer(1);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (returnToPlayer?.Consume() == true) tick.Cameras.TrySetActive(PlayerCamera);
    }
}
