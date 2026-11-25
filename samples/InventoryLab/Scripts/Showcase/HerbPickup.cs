using System;
using Engine.Core;

// Showcase: a herb growing in the yard. When the player walks over it, HerbYard puts it in the bag (if it fits) and the
// plant pops and disappears.
public sealed class HerbPickup : GameBehaviour
{
    public Guid Yard, Player, Leaves;
    public string Item = "herb";
    private bool taken;

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (taken || contact.OtherId != Player || contact.Objects.GetComponent<HerbYard>(Yard) is not { } yard) return;
        if (!yard.Collect(Item, contact.Ui, contact.Objects)) return;
        taken = true;
        contact.Objects.SetColliderEnabled(contact.ObjectId, false);
        if (Leaves != Guid.Empty) contact.Objects.EmitParticles(Leaves);
        contact.Objects.Tween(contact.ObjectId).Scale(new(1.4f, 1.4f), .08f, TweenEase.EaseOut).Scale(new(0.1f, 0.1f), .14f, TweenEase.EaseIn);
        contact.Objects.Tween(contact.ObjectId).Delay(.1f).Alpha(0, .12f);
    }
}
