using System;
using Engine.Core;

// The probe course's sensor: raises "crossed" when the probe enters, lights its plate (Active) and says so in the world text Note.
public sealed class SensorEntry : GameBehaviour
{
    public SpriteReference Active;
    public Guid Note;
    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (!contact.Objects.HasScript(contact.OtherId,"Probe")) return;
        contact.Session.Set("crossed");
        if (Active.Id!=Guid.Empty) contact.Objects.SetSprite(contact.ObjectId,Active);
        contact.Objects.SetWorldText(Note,"<color=#9CF09C><b>trigger: probe entered</b></color>");
    }
}
