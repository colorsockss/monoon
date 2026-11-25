using System.Collections.Generic;
using System.Linq;
using Engine.Core;

// The course showcase: the runner crosses bridge, pulley, lift and hill, picks every coin on the way and ends at the pennant.
public sealed class CourseTests
{
    [GameTest("Scenes/ShowcaseCourse.scene", MaxFrames = 2400)]
    public IEnumerable<ScriptWait> RunnerPicksEveryCoinAndReachesThePennant(GameTestRun run)
    {
        yield return ScriptWait.Until(() => run.PositionOf("Runner").X >= 2239);
        yield return ScriptWait.Frames(30);
        var left = run.World.Objects.Where(o => o.Name.StartsWith("Coin ") && char.IsDigit(o.Name[5]) && run.Objects.IsActive(o.Id)).Select(o => o.Name).ToArray();
        run.Assert(left.Length == 0, $"every coin is picked (left: {string.Join(", ", left)})");
        run.Assert(new[] { "motor", "bridge", "pulley", "path", "chain" }.All(run.Session.Has), "all five features light up");
    }
}
