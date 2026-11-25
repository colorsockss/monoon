using System;
using System.Drawing;
using Engine.Core;

// Showcase: a doorway the player walks into. It hides the player's picture, so the hero goes through the door rather than
// standing on top of the wall, and can fade the screen to black (the last doorway, which ends the demo).
// Leave FadeOut off where the doorway also changes scene: a fade lasts for the whole session.
public sealed class DoorwayFade : GameBehaviour
{
    public Guid Player, Hide;
    public bool FadeOut;

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId != Player) return;
        if (Hide != Guid.Empty) contact.Objects.SetActive(Hide, false);
        if (FadeOut) contact.Session.Screen.Fade(Color.Black, 0.6f);
    }
}
