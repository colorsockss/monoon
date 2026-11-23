using System;
using Engine.Core;

// The Landing's far edge leads to the Joints scene.
public sealed class SceneExit : GameBehaviour
{
    public Guid Player;
    public SceneReference Destination;
    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId == Player) contact.Session.LoadScene(Destination.Id);
    }
}
