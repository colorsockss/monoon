using System;
using Engine.Core;

public sealed class RoomExit : GameBehaviour
{
    public Guid Player;
    public SceneReference Destination;
    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId == Player && contact.Session.Has("key")) contact.Session.LoadScene(Destination.Id);
    }
}
