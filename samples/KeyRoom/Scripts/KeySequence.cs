using System;
using Engine.Core;

public sealed class KeySequence : GameBehaviour
{
    public Guid Collector, KeyImage, Door, DoorCamera, PlayerCamera;
    public SpriteReference OpenSprite;
    private GameTimer returnToPlayer;

    private void OpenDoor(IGameObjects objects)
    {
        if (OpenSprite.Id == Guid.Empty || !objects.SetSprite(Door, OpenSprite) || !objects.SetColliderEnabled(Door, false))
            throw new InvalidOperationException("KeySequence needs a door with a Sprite Renderer, an open sprite and a collider.");
        objects.SetActive(KeyImage, false);
    }

    public override void OnCreate(ScriptLife life)
    {
        if (life.Session.Has("key")) OpenDoor(life.Objects);
    }

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId != Collector || !contact.Session.Set("key")) return;
        OpenDoor(contact.Objects);
        contact.Objects.Animator(Door)?.PlayThenShowSprite();
        contact.Objects.Audio.Play(Door);
        contact.Cameras.TrySetActive(DoorCamera);
        returnToPlayer = contact.Session.Time.StartTimer(1);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (returnToPlayer?.Consume() == true) tick.Cameras.TrySetActive(PlayerCamera);
    }
}
