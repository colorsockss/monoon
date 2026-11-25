using System;
using System.Collections.Generic;
using Engine.Core;
using MoNoon.Inventory;
using MoNoon.Quests;

// The herb yard walkthrough of docs/manual/quests.md, on the route of engine/tests/fixtures/InventoryLab/Herbalist.inputreplay:
// accept the request, walk over three herbs (each pops a pooled "+1"), claim the crystal, pick a slot and use a herb, then
// open the stock scene with C and come back with B.
public sealed class YardTests
{
    private static readonly Guid Yard = Guid.Parse("0b0b8126-2ff1-5bbb-a7f0-5ef0af802bf8"), Stock = Guid.Parse("09ca087e-0f44-5886-9546-1553180b2fba");
    private static readonly PrefabReference Number = new(Guid.Parse("bc54ecea-34dc-460f-b0a4-3a94f644ac48"));   // Assets/Labels/DamageNumber.prefab

    private static IEnumerable<ScriptWait> Hold(GameTestRun run, string key, int frames)
    {
        run.Press(key);
        yield return ScriptWait.Frames(frames);
        run.Release(key);
        yield return ScriptWait.Frames(1);
    }

    private static IEnumerable<ScriptWait> Keys(GameTestRun run, string keys)
    {
        foreach (var key in keys.Split(' ')) { run.Tap(key); yield return ScriptWait.Frames(10); }
    }

    [GameTest("Scenes/Showcase.scene")]
    public IEnumerable<ScriptWait> HerbalistQuestInTheYard(GameTestRun run)
    {
        yield return ScriptWait.Frames(2);
        var bag = run.Script<InventoryContainer>("Yard").Bag;
        var quest = run.Script<QuestLog>("Yard").Journal["herbalist"];
        foreach (var wait in Hold(run, "Up", 10)) yield return wait;
        foreach (var wait in Keys(run, "T 1 1 1")) yield return wait;
        run.AreEqual(QuestStage.Active, quest.Stage, "the request is accepted in the dialogue");

        foreach (var (key, frames) in new[] { ("Right", 80), ("Up", 10), ("Left", 10), ("Up", 10), ("Left", 100) })
            foreach (var wait in Hold(run, key, frames)) yield return wait;
        run.AreEqual(3, quest.Collected, "three herbs walked over");
        run.AreEqual(("herb", 3), (bag[0].Item, bag[0].Count), "the herbs stack in the first slot");
        var pool = run.Objects.PoolStats(Number);
        run.AreEqual(3, pool.Created + pool.Reused, "each herb rents a +1 label from the pool");

        foreach (var wait in Hold(run, "Right", 30)) yield return wait;
        foreach (var wait in Keys(run, "T 2 1 1")) yield return wait;
        run.AreEqual(QuestStage.Claimed, quest.Stage, "the reward is claimed");
        run.AreEqual(("gem", 1), (bag[1].Item, bag[1].Count), "the crystal goes to the second slot");

        foreach (var wait in Keys(run, "E U")) yield return wait;
        run.AreEqual(0, bag[1].Count, "E selects the crystal's slot and U uses it");
        foreach (var wait in Keys(run, "Q U")) yield return wait;
        run.AreEqual(2, bag[0].Count, "Q goes back to the herbs and U uses one");

        foreach (var wait in Keys(run, "C")) yield return wait;
        run.AreEqual(Stock, run.SceneId, "C opens the stock scene");
        foreach (var wait in Keys(run, "B")) yield return wait;
        run.AreEqual(Yard, run.SceneId, "B returns to the yard");
    }
}
