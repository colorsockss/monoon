using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Engine.Core;
using KeyRoom.Rules;

// KeyRoom chest rules: one coin per chest, only within reach, collected by walking into it.
public sealed class ChestTests
{
    private const string Room = "Scenes/KeyRoom.scene";

    private static Guid[] Coins(GameTestRun run) => run.World.Objects.Where(o => o.Name == "Coin").Select(o => o.Id).ToArray();

    [GameTest(Room)]
    public IEnumerable<ScriptWait> InteractSpawnsOneCoin(GameTestRun run)
    {
        run.AreEqual(0, Coins(run).Length, "coins before opening");
        run.Tap("E");
        yield return ScriptWait.Frames(1);
        var coin = Coins(run).Single();
        run.Assert(!run.Objects.IsActive(run.Find("Chest Lid")) && run.Session.Has("chest"), "the lid opens and the chest is remembered");
        run.AreEqual(new Vector2(384, 126), run.PositionOf("Coin"), "coin spawn position");
        run.AreEqual(true, run.Objects.GetComponent<BoxCollider2DComponent>(coin)?.IsTrigger, "coin is a trigger");
        yield return ScriptWait.Frames(5);
        run.Tap("E");
        yield return ScriptWait.Frames(2);
        run.Assert(Coins(run).SequenceEqual([coin]) && !run.Session.Has(LootRules.CoinFlag), "a second press neither spawns nor collects");
    }

    [GameTest(Room)]
    public IEnumerable<ScriptWait> WalkingIntoTheCoinCollectsIt(GameTestRun run)
    {
        run.Tap("E");
        yield return ScriptWait.Frames(1);
        var coin = Coins(run).Single();
        run.Press("Right");
        yield return ScriptWait.Until(() => run.Session.Has(LootRules.CoinFlag));
        run.Assert(Coins(run).Length == 0 && !run.Objects.TryGetPosition(coin, out _), "the coin is removed once");
        run.AreEqual(0, run.Objects.FindByGroup(LootRules.Collectibles).Count, "collectibles left");
    }

    [GameTest(Room)]
    public IEnumerable<ScriptWait> ChestNeedsToBeWithinReach(GameTestRun run)
    {
        run.Press("Left");
        yield return ScriptWait.Frames(10);
        run.Release("Left");
        run.Assert(Vector2.Distance(run.PositionOf("Player"), run.PositionOf("Chest")) > LootRules.ChestReach, "the player walked out of reach");
        run.Tap("E");
        yield return ScriptWait.Frames(2);
        run.Assert(Coins(run).Length == 0 && !run.Session.Has("chest"), "the chest stays closed out of reach");
    }
}
