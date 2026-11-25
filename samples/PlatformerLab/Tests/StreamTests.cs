using System.Collections.Generic;
using System.Linq;
using Engine.Core;

// The streaming showcase: the runner crosses all seven chunks, never more than three are in memory, and the one under him
// always is.
public sealed class StreamTests
{
    [GameTest("Scenes/ShowcaseStream.scene", MaxFrames = 2400)]
    public IEnumerable<ScriptWait> RunnerCrossesTheRidgeWithThreeChunksAtMost(GameTestRun run)
    {
        var ground = run.Find("Ground");
        int most = 0;
        var seen = new HashSet<int>();
        while (run.PositionOf("Runner").X < 3390)
        {
            int loaded = Enumerable.Range(0, 7).Count(i => run.Objects.IsTileChunkLoaded(ground, i, 0));
            most = System.Math.Max(most, loaded);
            int under = (int)(run.PositionOf("Runner").X / (16 * 32));
            run.Assert(run.Objects.IsTileChunkLoaded(ground, under, 0), $"the chunk under the runner ({under}) is loaded");
            seen.Add(under);
            run.Assert(run.PositionOf("Runner").Y < 520, "the runner never falls into a pit");
            yield return ScriptWait.Frames(1);
        }
        run.Assert(most <= 3, $"at most three chunks in memory (saw {most})");
        run.AreEqual(7, seen.Count, "chunks crossed");
    }
}
