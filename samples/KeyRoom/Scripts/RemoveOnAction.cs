using Engine.Core;

// KeyRoom's lifetime demo. Loading a checkpoint creates a fresh scene.
public sealed class RemoveOnAction : GameBehaviour
{
    public string Action = "";

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed(Action) && tick.Objects.Destroy(tick.ObjectId))
            tick.Session.Log(Action);
    }
}
