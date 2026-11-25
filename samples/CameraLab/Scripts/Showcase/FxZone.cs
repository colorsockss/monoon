using System;
using System.Drawing;
using Engine.Core;

// Showcase: a place on the map that sets off a screen effect when the player walks in, on that player's camera only
// (Session.Screen.For). "Impact": the powder barrel blows
// (shake, zoom punch, white flash, its Burst particles) and disappears. "Grade": the evening square warms the colours while
// the player is inside. Raises the "fx" / "grade" HUD tag flags.
public sealed class FxZone : GameBehaviour
{
    public Guid Player, Camera, Hide, Burst;
    public string Kind = "Impact";
    private bool used;

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId != Player) return;
        var screen = Camera != Guid.Empty ? contact.Session.Screen.For(Camera) : contact.Session.Screen;
        if (Kind == "Grade")
        {
            screen.Adjust(Color.FromArgb(255, 200, 150), -.1f, .6);
            contact.Session.Set("grade");
            return;
        }
        if (used) return;
        used = true;
        screen.Shake(10, .5f);
        screen.ZoomPunch(.15f, .35f);
        screen.Flash(Color.White, .35);
        if (Burst != Guid.Empty) contact.Objects.EmitParticles(Burst);
        if (Hide != Guid.Empty) contact.Objects.SetActive(Hide, false);
        contact.Session.Set("fx");
    }

    public override void OnTriggerExit(ScriptContact contact)
    {
        if (contact.OtherId == Player && Kind == "Grade")
            (Camera != Guid.Empty ? contact.Session.Screen.For(Camera) : contact.Session.Screen).Adjust(Color.White, 0, .6);
    }
}
