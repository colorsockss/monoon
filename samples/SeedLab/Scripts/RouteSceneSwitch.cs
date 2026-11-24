using Engine.Core;

public sealed class RouteSceneSwitch : GameBehaviour
{
    public SceneReference Destination;
    public string Action = "SwitchLab";

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed(Action)) tick.Session.LoadScene(Destination.Id);
    }
}
