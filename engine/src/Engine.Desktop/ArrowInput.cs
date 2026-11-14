using System.Numerics;
using Brutal.SdlApi;

namespace Engine.Desktop;

internal sealed class ArrowInput
{
    private readonly HashSet<SdlScancode> held = [];
    private readonly HashSet<SdlScancode> pressedSinceTick = [];

    public void Press(SdlScancode key)
    {
        if (held.Add(key))
            pressedSinceTick.Add(key);
    }

    public void Release(SdlScancode key) => held.Remove(key);

    public void Reset()
    {
        held.Clear();
        pressedSinceTick.Clear();
    }

    public Vector2 TakeMovement()
    {
        int Axis(SdlScancode key) => held.Contains(key) || pressedSinceTick.Contains(key) ? 1 : 0;

        var movement = new Vector2(
            Axis(SdlScancode.Right) - Axis(SdlScancode.Left),
            Axis(SdlScancode.Down) - Axis(SdlScancode.Up));
        // A press and release between simulation ticks still moves for one tick.
        pressedSinceTick.Clear();
        return movement.LengthSquared() > 1 ? Vector2.Normalize(movement) : movement;
    }
}
