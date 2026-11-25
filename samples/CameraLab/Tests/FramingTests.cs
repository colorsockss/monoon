using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Engine.Core;

// The framing showcase: the hero picks all seven herbs, the Frame camera takes over on the last one, and hero and partner
// end at the herbalist.
public sealed class FramingTests
{
    [GameTest("Scenes/ShowcaseFraming.scene")]
    public IEnumerable<ScriptWait> HerbsThenFramedWalkToTheHerbalist(GameTestRun run)
    {
        var picks = Enumerable.Range(1, 7).Select(i => run.Find($"Pick {i}")).ToArray();
        yield return ScriptWait.Until(() => run.Session.Has("frame"));
        run.Assert(run.Session.Has("deadzone") && run.Session.Has("lead"), "deadzone and look-ahead came first");
        run.Assert(picks.All(p => !run.Objects.IsActive(p)), "all seven herbs are picked by the time the Frame camera takes over");
        yield return ScriptWait.Frames(600);
        run.Assert(Vector2.Distance(run.PositionOf("Player"), new Vector2(1290, 488)) < 1 &&
                   Vector2.Distance(run.PositionOf("Partner"), new Vector2(1322, 522)) < 1, "both stand at the herbalist");
    }
}
