using Engine.Core;

public sealed class SensorEntry : GameBehaviour
{
    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.Objects.HasScript(contact.OtherId,"Probe")) contact.Session.Set("crossed");
    }
}
