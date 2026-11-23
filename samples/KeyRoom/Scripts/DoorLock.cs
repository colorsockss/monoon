using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Engine.Core;

public sealed class DoorLock : GameBehaviour
{
    [GameEventHandler]
    public void Unlock(GameEventContext message)
    {
        if (message.Sender is not KeySequence key) return;
        message.Session.Log("Door received KeyAcquired.");
        Open(message.Objects, message.ObjectId, key);
        message.Objects.Animator(message.ObjectId)?.PlayThenShowSprite();
        message.Objects.Audio.Play(message.ObjectId);
        message.Objects.StartRoutine(message.ObjectId, ShowHint(message.Session));
    }

    // Game time: pausing or stepping delays the hint by the same ticks. Removing the door (F6) cancels it.
    private static IEnumerable<ScriptWait> ShowHint(SessionState session)
    {
        yield return ScriptWait.Seconds(0.5);
        session.Set("doorHint");
    }

    public override void OnCreate(ScriptLife life)
    {
        if (life.Session.Has("key") && EventSources<KeySequence>(nameof(Unlock)).FirstOrDefault() is {} key)
        {
            Open(life.Objects, life.ObjectId, key);
            life.Session.Log("Door restored open.");
        }
    }

    private static void Open(IGameObjects objects, Guid door, KeySequence key)
    {
        var sprite = key.OpenSprite;
        if (sprite.Id == Guid.Empty || !objects.SetSprite(door, sprite) || !objects.SetColliderEnabled(door, false))
            throw new InvalidOperationException("DoorLock needs an open sprite, Sprite Renderer and collider.");
        if (key.Offset != Vector2.Zero) objects.TryMoveStep(door, key.Offset);
    }
}
