using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using Engine.Core;

public sealed class ExpeditionTests
{
    private const string Marsh = "Scenes/Marsh.scene";
    private const string Haven = "Scenes/Haven.scene";

    [GameTest(Marsh)]
    public void IsoWaterShapesAreBakedIntoNavigation(GameTestRun run)
    {
        var layer = run.Find("Diamond terrain");
        var area = run.Objects.Navigation.BakeArea(run.Find("Free navigation"))!;
        foreach (var cell in new[] { new Point(5, 6), new Point(5, 7), new Point(7, 8) })
        {
            var center = run.Objects.TileCellCenter(layer, cell)!.Value;
            run.AreEqual(cell, run.Objects.TileCellAt(layer, center), "world-to-cell round trip");
            run.Assert(!area.IsOpen(center), "solid water is closed at its world center");
        }
        var land = run.Objects.TileCellCenter(layer, new(4, 5))!.Value;
        run.Assert(area.IsOpen(land), "land along the diamond shore is walkable");
    }

    [GameTest(Marsh, MaxFrames = 400)]
    public IEnumerable<ScriptWait> ClickMovesTheBandIntoAutoCombat(GameTestRun run)
    {
        yield return ScriptWait.Frames(2);
        var origin = run.PositionOf("Captain");
        var follower = run.PositionOf("Companion 1");
        var ground = run.Find("Diamond terrain");
        var target = run.Objects.TileCellCenter(ground, new(4, 4))!.Value;
        run.Assert(run.World.TryGetView(run.Find("Expedition camera"), out var camera), "primary camera is ready");
        var pixel = camera.WorldToScreen(target);
        run.Click(pixel.X, pixel.Y);
        yield return ScriptWait.Until(() => run.Script<Expedition>("Captain").Defeated > 0);
        run.Assert(Vector2.Distance(run.PositionOf("Captain"), origin) > 20, "captain moved toward the selected cell");
        run.Assert(Vector2.Distance(run.PositionOf("Companion 1"), follower) > 8, "companion followed");
        run.Assert(!run.Objects.IsActive(run.Find("Foe 1")) || !run.Objects.IsActive(run.Find("Foe 2")), "an enemy was removed by auto combat");
    }

    [GameTest(Haven)]
    public IEnumerable<ScriptWait> HiringAndGearPersistThroughSceneReload(GameTestRun run)
    {
        var saved = new GameData();
        saved.Set("coins", 40);
        saved.Set("xp", 30);
        run.Session.Storage.SaveSlot(1, saved);
        run.Session.LoadScene(run.SceneId!.Value);
        yield return ScriptWait.Frames(5);

        run.Tap("H");
        yield return ScriptWait.Frames(5);
        run.Assert(run.Objects.IsActive(run.Find("Companion 3")), "the hired healer enters the formation");
        run.Tap("U");
        yield return ScriptWait.Frames(3);

        var progress = run.Session.Storage.LoadSlot(1, 1)!;
        run.AreEqual(3d, progress.GetNumber("recruits"), "recruit count is saved");
        run.AreEqual(2d, progress.GetNumber("gear"), "gear upgrade is saved");
        run.AreEqual(26d, progress.GetNumber("coins"), "crown costs were applied once");
    }

    [GameTest(Haven)]
    public IEnumerable<ScriptWait> FinishedExpeditionRequiresExplicitReplay(GameTestRun run)
    {
        var haven = run.SceneId;
        var saved = new GameData();
        saved.Set("route", 3);
        saved.Set("xp", 30);
        saved.Set("coins", 18);
        run.Session.Storage.SaveSlot(1, saved);
        run.Session.LoadScene(haven!.Value);
        yield return ScriptWait.Frames(5);
        run.Assert(run.World.Ui.TextOf(run.Find("Instructions"))!.StartsWith("Expedition complete."), "completed save shows Haven's ending");

        // Reuse the original route's three waypoints to reach the gate.
        run.Click(275.81732f, 189.08514f);
        yield return ScriptWait.Frames(68);
        run.Click(299.52597f, 246.76773f);
        yield return ScriptWait.Frames(68);
        run.Click(290, 211);
        yield return ScriptWait.Frames(68);
        run.Tap("Return");
        yield return ScriptWait.Frames(6);
        run.AreEqual(haven, run.SceneId, "normal travel did not reopen the Crypt");
        run.Tap("N");
        yield return ScriptWait.Frames(6);
        run.AreEqual<Guid?>(Guid.Parse("a1b92a57-febd-5582-87cc-8700e056ac09"), run.SceneId, "explicit replay opens Marsh");
        var restarted = run.Session.Storage.LoadSlot(1, 1)!;
        run.AreEqual(0d, restarted.GetNumber("route"), "route restarted");
        run.AreEqual(30d, restarted.GetNumber("xp"), "experience preserved");
        run.AreEqual(18d, restarted.GetNumber("coins"), "crowns preserved");
    }

    [GameTest(Haven)]
    public IEnumerable<ScriptWait> BothArchersAttackAtRange(GameTestRun run)
    {
        var saved = new GameData();
        saved.Set("coins", 18);
        saved.Set("xp", 30);
        run.Session.Storage.SaveSlot(1, saved);
        run.Session.LoadScene(run.SceneId!.Value);
        yield return ScriptWait.Frames(5);
        for (int i = 0; i < 3; i++)
        {
            run.Tap("H");
            yield return ScriptWait.Frames(5);
        }
        run.Assert(run.Objects.IsActive(run.Find("Companion 5")), "all three recruits were hired");

        run.Session.LoadScene(Guid.Parse("fd96a88e-4577-5ef2-ac84-c67c9df6467d"));
        yield return ScriptWait.Frames(3);
        var script = run.Script<Expedition>("Captain");
        // Inspect the real auto-attack timers without adding a debug API to the game.
        var allies = Actors(script, "allies");
        var foes = Actors(script, "foes");
        // Match the QA fixture with Foe 1 farther south than its normal spawn.
        SetActorField(foes, 0, "Feet", new Vector2(405, 173));
        SetActorField(foes, 0, "Health", 7);
        SetActorField(allies, 1, "Cooldown", 0);
        SetActorField(allies, 4, "Cooldown", 0);
        yield return ScriptWait.Frames(1);
        foreach (int archer in new[] { 1, 4 })
        {
            var feet = ActorField<Vector2>(allies, archer, "Feet");
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < foes.Length; i++)
                if (ActorField<int>(foes, i, "Health") > 0)
                    nearest = Math.Min(nearest, Vector2.Distance(feet, ActorField<Vector2>(foes, i, "Feet")));
            run.Assert(nearest is > 30 and < 65, $"archer {archer + 1} has a target beyond melee distance ({nearest:F1}px)");
            run.AreEqual(28, ActorField<int>(allies, archer, "Cooldown"), $"archer {archer + 1} attacked the target");
        }
    }

    private static Array Actors(Expedition script, string name) =>
        (Array)typeof(Expedition).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;

    private static T ActorField<T>(Array actors, int index, string name) =>
        (T)actors.GetType().GetElementType()!.GetField(name)!.GetValue(actors.GetValue(index))!;

    private static void SetActorField<T>(Array actors, int index, string name, T value)
    {
        var actor = actors.GetValue(index)!;
        actor.GetType().GetField(name)!.SetValue(actor, value);
        actors.SetValue(actor, index);
    }
}
