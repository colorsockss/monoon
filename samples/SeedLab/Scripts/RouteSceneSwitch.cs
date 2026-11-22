using Engine.Core;

public sealed class RouteSceneSwitch : GameBehaviour
{
    public SceneReference Destination;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("SwitchLab")) tick.Session.LoadScene(Destination.Id);
    }
}
