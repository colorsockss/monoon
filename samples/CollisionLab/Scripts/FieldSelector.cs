using Engine.Core;

public sealed class FieldSelector : GameBehaviour
{
    public SceneReference Small, Large, Lab;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("SmallField")) tick.Session.LoadScene(Small.Id);
        else if (tick.Input.Pressed("LargeField")) tick.Session.LoadScene(Large.Id);
        else if (tick.Input.Pressed("ReturnToLab")) tick.Session.LoadScene(Lab.Id);
    }
}
