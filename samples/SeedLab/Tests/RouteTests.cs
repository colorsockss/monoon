using System.Collections.Generic;
using System.Numerics;
using Engine.Core;

// The terrain showcase: the hero starts up the west lane for the north road, turns back when rain turns it to mud (the south
// road and the grass beside the river are now cheaper), and crosses on the bridge laid over the south ford.
public sealed class RouteTests
{
    // The cell the hero stands on when his position sits on one (he walks between cells with a tween): RouteCourier places
    // his 42 x 60 picture 21 left and 48 above the centre of a 32-unit cell of the Field at (0, 0).
    private static (int X, int Y)? Cell(Vector2 at)
    {
        var c = (at + new Vector2(21, 48) - new Vector2(16, 16)) / 32;
        var r = new Vector2(System.MathF.Round(c.X), System.MathF.Round(c.Y));
        return Vector2.Distance(c, r) < .1f ? ((int)r.X, (int)r.Y) : null;
    }

    [GameTest("Scenes/ShowcaseRoute.scene")]
    public IEnumerable<ScriptWait> HeroTakesTheNewBridge(GameTestRun run)
    {
        var seen = new HashSet<(int, int)>();
        while (!run.Session.Has("arrived"))
        {
            if (Cell(run.PositionOf("Courier")) is { } c) seen.Add(c);
            yield return ScriptWait.Frames(1);
        }
        run.Assert(run.Session.Has("rain") && run.Session.Has("bridge"), "rain and bridge both happened");
        run.Assert(seen.Contains((2, 3)) && !seen.Contains((3, 1)), $"he heads for the north road, then turns back before the mud (walked {string.Join(" ", seen)})");
        run.Assert(seen.Contains((8, 5)) && !seen.Contains((9, 1)), "he crosses on the new south bridge, not the north one");
        run.Assert(seen.Contains((15, 5)), "he stops beside the chest");
    }
}
