using System.Collections.Generic;
using System.Numerics;
using Engine.Core;

// The terrain showcase: the courier re-plans when rain turns the north road to mud and again when the south bridge opens.
public sealed class RouteTests
{
    // Cell centres in world units: the Field tilemap sits at (16, 56) with 32-unit cells; the courier stands 10 left, 22 up.
    private static Vector2 Courier(int x, int y) => new(16 + x * 32 + 16 - 10, 56 + y * 32 + 16 - 22);

    [GameTest("Scenes/ShowcaseRoute.scene")]
    public IEnumerable<ScriptWait> CourierTakesTheNewBridge(GameTestRun run)
    {
        bool grassDetour = false, southBridge = false, northBridge = false;
        while (run.PositionOf("Courier") != Courier(18, 5))
        {
            var at = run.PositionOf("Courier");
            grassDetour |= at == Courier(4, 2);
            southBridge |= at == Courier(8, 5);
            northBridge |= at == Courier(9, 1);
            yield return ScriptWait.Frames(1);
        }
        run.Assert(run.Session.Has("rain") && run.Session.Has("bridge"), "rain and bridge both happened");
        run.Assert(grassDetour, "after the rain the courier leaves the muddy road for the grass");
        run.Assert(southBridge && !northBridge, "the courier crosses on the new south bridge, not the north one");
    }
}
