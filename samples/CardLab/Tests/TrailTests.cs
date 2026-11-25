using System.Collections.Generic;
using Engine.Core;
using MoNoon.Decks;

// The deck walkthrough in docs/manual/decks.md: seed 42 deals Bearing, Shortcut, Climb; playing the second card walks the hero
// three stones (stone pictures sit on whole units, the path on 27.5-unit steps); a save and a load after more play bring the same hand and draw pile back.
public sealed class TrailTests
{
    [GameTest("Scenes/ShowcaseTrail.scene")]
    public IEnumerable<ScriptWait> SeedDealsTheManualHand(GameTestRun run)
    {
        yield return ScriptWait.Frames(2);
        var deck = run.Script<DeckSource>("Deck").Deck;
        for (int i = 0; i < 3; i++) { run.Tap("D"); yield return ScriptWait.Frames(30); }
        run.AreEqual("compass map rope", string.Join(" ", deck.Hand), "seed 42 hand (Bearing, Shortcut, Climb)");

        float start = run.PositionOf("Hero").X;
        run.Tap("2");
        yield return ScriptWait.Frames(90);
        float walked = run.PositionOf("Hero").X - start, stones = run.PositionOf("Stone 3").X - run.PositionOf("Stone 0").X;
        run.Assert(System.MathF.Abs(walked - stones) < 2, $"Shortcut walks three stones ({walked:0.#} of {stones:0.#})");
        run.AreEqual("compass rope", string.Join(" ", deck.Hand), "hand after playing the second card");

        run.Tap("S"); yield return ScriptWait.Frames(5);
        string draw = deck.Capture().Values["draw"].ToString();
        run.Tap("D"); yield return ScriptWait.Frames(30);
        run.Tap("1"); yield return ScriptWait.Frames(60);
        run.Tap("L"); yield return ScriptWait.Frames(5);
        run.AreEqual("compass rope", string.Join(" ", deck.Hand), "hand after load");
        run.AreEqual(draw, deck.Capture().Values["draw"].ToString(), "draw pile after load");
    }
}
