using System;
using Engine.Core;

// The probe course's sensor: raises "crossed" when the probe enters and lights its plate (Active) from then on.
public sealed class SensorEntry : GameBehaviour
{
    public SpriteReference Active;
    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (!contact.Objects.HasScript(contact.OtherId,"Probe")) return;
        contact.Session.Set("crossed");
        if (Active.Id!=Guid.Empty) contact.Objects.SetSprite(contact.ObjectId,Active);
    }
}
