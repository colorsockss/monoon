using System;
using Engine.Core;
using System.Numerics;

public enum DoorFrame { First, Last }

public sealed class KeySequence : GameBehaviour
{
    public SpriteReference[] Frames = [];
    public DoorFrame RestFrame = DoorFrame.Last;
    public Vector2 Offset;
    public Guid Collector, KeyImage, DoorCamera, PlayerCamera;
    /// <summary>How long the door camera stays on after the key is picked up.</summary>
    public float HoldSeconds = 1;
    public event Action KeyAcquired;
    private GameTimer returnToPlayer;

    public SpriteReference OpenSprite => Frames.Length == 0 ? default : Frames[RestFrame == DoorFrame.First ? 0 : ^1];

    public override void OnCreate(ScriptLife life)
    {
        if (life.Session.Has("key")) life.Objects.SetActive(KeyImage, false);
    }

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId != Collector || !contact.Session.Set("key")) return;
        contact.Objects.SetActive(KeyImage, false);
        contact.Session.Log("Key acquired.");
        KeyAcquired?.Invoke();
        contact.Cameras.TrySetActive(DoorCamera);
        returnToPlayer = contact.Session.Time.StartTimer(HoldSeconds);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (returnToPlayer?.Consume() == true) tick.Cameras.TrySetActive(PlayerCamera);
    }
}
