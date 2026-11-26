using System;
using System.Collections.Generic;
using System.IO;
using Engine.Core;

public sealed class TrailRunnerTests
{
    private const string Meadow = "Scenes/Meadow.scene";

    private static MotionState2D Motion(GameTestRun run)
    {
        run.Assert(run.Objects.Motion.TryGetMotion(run.Find("Courier"), out var motion), "courier has a body");
        return motion;
    }

    [GameTest(Meadow)]
    public IEnumerable<ScriptWait> AirDashCannotBeRepeatedInTheSameJump(GameTestRun run)
    {
        yield return ScriptWait.Until(() => Motion(run).Grounded);
        run.Press("Left");
        run.Press("Space");
        yield return ScriptWait.Frames(3);
        run.Tap("X");
        yield return ScriptWait.Frames(1);
        run.Assert(Motion(run).Velocity.X < -245 && !Motion(run).Grounded, "air dash accelerates in the facing direction");
        yield return ScriptWait.Frames(10);
        run.Tap("X");
        yield return ScriptWait.Frames(1);
        run.Assert(Math.Abs(Motion(run).Velocity.X) < 120 && !Motion(run).Grounded, "second air dash is locked out");
    }

    [GameTest(Meadow)]
    public IEnumerable<ScriptWait> GroundDashAcrossLedgeRemainsSpentUntilLanding(GameTestRun run)
    {
        yield return ScriptWait.Until(() => Motion(run).Grounded);
        run.Press("Right");
        yield return ScriptWait.Until(() => run.PositionOf("Courier").X >= 200);
        run.Assert(Motion(run).Grounded, "the dash starts on the first ledge");
        run.Tap("X");
        yield return ScriptWait.Frames(8);
        run.Assert(!Motion(run).Grounded && Motion(run).Velocity.X > 245, "the first dash carried the courier off the ledge");
        run.Tap("X");
        yield return ScriptWait.Frames(1);
        run.Assert(!Motion(run).Grounded && Motion(run).Velocity.X < 120, "the second dash stayed locked until landing");
    }

    [GameTest("Scenes/Summit.scene")]
    public IEnumerable<ScriptWait> SummitObjectiveReturnsAfterPause(GameTestRun run)
    {
        var status = run.Find("Status");
        string? objective = run.World.Ui.TextOf(status);
        run.AreEqual("Land three stomps to raise the summit flag.", objective, "original objective");
        run.AreEqual("HEARTS  3", run.World.Ui.TextOf(run.Find("Totals")), "Summit HUD shows attainable stats");
        run.Tap("Pad.Back");
        yield return ScriptWait.Until(() => run.Time.IsPaused);
        run.Assert(run.World.Ui.TextOf(status)!.Contains("controller Back"), "pause help names the controller button");
        run.Tap("P");
        yield return ScriptWait.Until(() => !run.Time.IsPaused);
        run.AreEqual(objective, run.World.Ui.TextOf(status), "resuming restored the objective");
    }

    [GameTest("Scenes/Trailhead.scene")]
    public IEnumerable<ScriptWait> TrailheadKeepsValidSlotsAvailableWhenOneIsCorrupt(GameTestRun run)
    {
        var valid = new GameData();
        valid.Set("unlocked", 4);
        valid.Set("stage4.clear", true);
        valid.Set("stage4.bestTicks", 120);
        run.Session.Storage.SaveSlot(2, valid);
        string profile = Path.Combine(Environment.GetEnvironmentVariable("MONOON_USER_DATA")!, "Play", "ce247eed9c6150a8bdaf486165fbaccc");
        string corrupt = Path.Combine(profile, "slot-001.save");
        File.WriteAllText(corrupt, "[[[broken");
        var trailhead = run.SceneId;
        run.Session.LoadScene(trailhead!.Value);
        yield return ScriptWait.Frames(5);
        run.AreEqual("Slot unreadable. Choose another slot.", run.World.Ui.TextOf(run.Find("Record")), "corrupt slot notice");
        run.Tap("Return");
        yield return ScriptWait.Frames(5);
        run.AreEqual(trailhead, run.SceneId, "unreadable slot is not opened");
        run.Tap("Tab");
        yield return ScriptWait.Frames(2);
        run.Assert(run.World.Ui.TextOf(run.Find("Selection"))!.Contains("Slot 2"), "slot 2 remains selectable");
        run.AreEqual("Best   2.0 s", run.World.Ui.TextOf(run.Find("Record")), "Summit record has no gem score");
        run.Tap("Return");
        yield return ScriptWait.Frames(5);
        run.Assert(run.SceneId != trailhead && run.Script<TrailRunner>("Courier").Stage == 4, "valid slot enters Summit");
        run.AreEqual("[[[broken", File.ReadAllText(corrupt), "corrupt file remains intact");
    }

    [GameTest(Meadow)]
    public IEnumerable<ScriptWait> HoldingJumpRisesHigher(GameTestRun run)
    {
        yield return ScriptWait.Until(() => Motion(run).Grounded);
        float start = run.PositionOf("Courier").Y;
        run.Tap("Space");
        float shortApex = start;
        for (int i = 0; i < 35; i++)
        {
            yield return ScriptWait.Frames(1);
            shortApex = Math.Min(shortApex, run.PositionOf("Courier").Y);
        }

        yield return ScriptWait.Until(() => Motion(run).Grounded);
        run.Press("Space");
        float longApex = start;
        for (int i = 0; i < 30; i++)
        {
            yield return ScriptWait.Frames(1);
            longApex = Math.Min(longApex, run.PositionOf("Courier").Y);
        }
        run.Release("Space");
        run.Assert(shortApex < start - 5 && longApex < shortApex - 15, "holding Space increases jump height");
    }
}
