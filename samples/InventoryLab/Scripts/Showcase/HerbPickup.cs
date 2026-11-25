using System;
using System.Numerics;
using Engine.Core;

// Showcase: a herb growing in the yard. When the player walks over it, HerbYard puts it in the bag (if it fits) and the
// plant pops and disappears while a "+1" rents from the Text 2D number pool (Labels/RisingNumber) and rises over it.
public sealed class HerbPickup : GameBehaviour
{
    public Guid Yard, Player, Leaves;
    public string Item = "herb";
    public PrefabReference Pop;
    public float PopX;                   // the pooled label's left edge, so it centres over the plant
    private bool taken;

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (taken || contact.OtherId != Player || contact.Objects.GetComponent<HerbYard>(Yard) is not { } yard) return;
        if (!yard.Collect(Item, contact.Ui, contact.Objects)) return;
        taken = true;
        var objects = contact.Objects;
        objects.SetColliderEnabled(contact.ObjectId, false);
        if (Leaves != Guid.Empty) objects.EmitParticles(Leaves);
        if (Pop.Id != Guid.Empty && objects.TryGetPosition(contact.ObjectId, out var at))
            objects.SetWorldText(objects.Rent(Pop, at + new Vector2(PopX, -8)), "<b>+1</b>");
        objects.Tween(contact.ObjectId).Scale(new(1.4f, 1.4f), .08f, TweenEase.EaseOut).Scale(new(0.1f, 0.1f), .14f, TweenEase.EaseIn);
        objects.Tween(contact.ObjectId).Delay(.1f).Alpha(0, .12f);
    }
}
