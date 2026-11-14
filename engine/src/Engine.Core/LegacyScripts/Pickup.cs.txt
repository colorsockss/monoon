using System;
using Engine.Core;

public sealed class Pickup : GameBehaviour
{
    public Guid Collector;
    public Guid Opens;

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId != Collector) return;
        contact.Deactivate(contact.ObjectId);
        if (Opens != Guid.Empty) contact.Deactivate(Opens);
    }
}
