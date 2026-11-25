using System.Numerics;
using Engine.Core;

// Showcase: walks the player inside the map (Min..Max, top-left of the feet box). Raises the HUD tag flags: "split" and
// "map" from the start, "edge" once the player is close enough to the map edge that its camera stops at the bounds.
public sealed class FieldWalker : GameBehaviour
{
    public float Speed = 150, EdgeX = 140;
    public Vector2 Min = new(16, 40), Max = new(1560, 860);

    public override void OnCreate(ScriptLife life) { life.Session.Set("split"); life.Session.Set("map"); }

    public override void FixedUpdate(ScriptTick tick)
    {
        var next = Vector2.Clamp(tick.Position + tick.Direction * (Speed * tick.DeltaTime), Min, Max);
        tick.Move(next - tick.Position);
        if (next.X < EdgeX) tick.Session.Set("edge");
    }
}
