using System.Numerics;
using Engine.Core;

public sealed class PointerWorkshop : GameBehaviour
{
    public SceneReference Destination;
    public bool PanCamera;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("PointerWorkshop"))
        {
            tick.Session.LoadScene(Destination.Id);
            return;
        }
        if (!PanCamera || tick.Direction == Vector2.Zero) return;
        var center = Vector2.Clamp(tick.Position + tick.Direction * (160 * tick.DeltaTime),
            new Vector2(300, 155), new Vector2(500, 295));
        tick.Move(center - tick.Position);
    }
}
