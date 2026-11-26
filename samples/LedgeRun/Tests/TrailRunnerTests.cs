using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
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

    [GameTest(Meadow)]
    public IEnumerable<ScriptWait> CoyoteJumpWorksJustPastTheLedge(GameTestRun run)
    {
        yield return ScriptWait.Until(() => Motion(run).Grounded);
        run.Press("Right");
        yield return ScriptWait.Until(() => !Motion(run).Grounded);
        run.Press("Space");
        yield return ScriptWait.Frames(2);
        run.Assert(Motion(run).Velocity.Y < -200, "jump still launches just beyond the ledge");
    }

    [GameTest("Scenes/Tower.scene")]
    public IEnumerable<ScriptWait> WallKickPushesAwayFromMasonry(GameTestRun run)
    {
        run.Objects.SetLocalTransform(run.Find("Courier"), new(new Vector2(32, 1504), 0, Vector2.One));
        run.Press("Left");
        yield return ScriptWait.Frames(10);
        run.Assert(!Motion(run).Grounded && Motion(run).Velocity.Y < 75, "the wall caps the descent speed");
        run.Press("Space");
        yield return ScriptWait.Frames(2);
        run.Assert(Motion(run).Velocity.X > 100 && Motion(run).Velocity.Y < -200, "the kick travels up and away");
    }

    [GameTest("Scenes/Tower.scene")]
    public IEnumerable<ScriptWait> LiftStartsAndCarriesCourierWhenBoarded(GameTestRun run)
    {
        var deck = run.PositionOf("Bell lift");
        var rider = deck + new Vector2(12, -20);
        run.Objects.SetLocalTransform(run.Find("Courier"), new(rider, 0, Vector2.One));
        yield return ScriptWait.Until(() => run.PositionOf("Bell lift").Y <= deck.Y - 32);
        run.Assert(Motion(run).Grounded, "the motor stays supported by the moving deck");
        float gap = run.PositionOf("Bell lift").Y - run.PositionOf("Courier").Y;
        run.Assert(Math.Abs(gap - 20) < .1f, "the lift moves the rider with it");
    }

    [GameTest("Scenes/Summit.scene")]
    public IEnumerable<ScriptWait> WardenStompOnlyHurtsDuringWallStun(GameTestRun run)
    {
        var player = run.Find("Courier");
        var warden = run.Script<SummitWarden>("Guardian");
        run.Press("Right");
        yield return ScriptWait.Until(() => warden.State == SummitWarden.Phase.Brace);
        run.Release("Right");

        void DropOnCrown()
        {
            Vector2 crown = run.PositionOf("Guardian") + new Vector2(24, -31);
            run.Objects.SetLocalTransform(player, new(crown, 0, Vector2.One));
            run.Objects.Motion.SetVelocity(player, new(0, 157));
        }

        DropOnCrown();
        yield return ScriptWait.Until(() => Motion(run).Velocity.Y < -50);
        run.AreEqual(3, warden.Health, "closed shell deflects the stomp");
        yield return ScriptWait.Until(() => warden.State == SummitWarden.Phase.Stunned);

        DropOnCrown();
        yield return ScriptWait.Until(() => warden.Health == 2);
        run.AreEqual(SummitWarden.Phase.Recoil, warden.State, "a hit immediately closes the opening");
        run.Assert(Motion(run).Velocity.Y < -100, "stomp knocks the courier back above the crown");
        yield return ScriptWait.Frames(32);
        run.AreEqual(2, warden.Health, "the same opening cannot take a second hit");
    }

    [GameTest("Scenes/Summit.scene")]
    public IEnumerable<ScriptWait> SummitResultUsesThisExpeditionsLegs(GameTestRun run)
    {
        TrailExpedition.Record(run.Session, 1, 600, 8, 1);
        TrailExpedition.Record(run.Session, 3, 900, 7, 2);
        yield return ScriptWait.Frames(120);
        run.Script<TrailRunner>("Courier").Finish(run.Session, run.Objects, run.World.Ui);
        yield return ScriptWait.Frames(72);
        run.AreEqual("27.0s", run.World.Ui.TextOf(run.Find("Results time")), "total of cleared legs");
        run.AreEqual("15", run.World.Ui.TextOf(run.Find("Results haul")), "total tokens");
        run.AreEqual("3", run.World.Ui.TextOf(run.Find("Results gems")), "total gems");
        run.Assert(run.World.Ui.TextOf(run.Find("Result leg 2 time"))!.Contains("-"), "skipped legs are marked");
        run.Assert(run.World.Ui.TextOf(run.Find("Result leg 4 time"))!.Contains("2.0s"), "Summit split");
        run.AreEqual(0d, TrailProfile.Load(run.Session, 1).GetNumber("bestExpeditionTicks"), "a partial run is not an expedition best");
        TrailExpedition.Begin(run.Session);
        var fresh = TrailExpedition.Record(run.Session, 4, 120, 0, 0);
        run.AreEqual(120L, fresh.Total.Ticks, "a new expedition starts clean");
    }

    [GameTest("Scenes/Trailhead.scene")]
    public IEnumerable<ScriptWait> SlotCardsShowBestFullExpeditionAndGems(GameTestRun run)
    {
        TrailProfile.Select(run.Session, 2);
        TrailProfile.ClearStage(run.Session, 1, 8, 2, 600, null);
        TrailProfile.ClearStage(run.Session, 2, 6, 1, 420, null);
        TrailProfile.ClearStage(run.Session, 4, 0, 0, 120, 1590);
        TrailProfile.ClearStage(run.Session, 4, 0, 0, 110, 1680);
        var scene = run.SceneId!.Value;
        run.Session.LoadScene(scene);
        yield return ScriptWait.Frames(5);
        run.AreEqual("<b>NEW</b>", run.World.Ui.TextOf(run.Find("Slot status 1")), "untouched slot");
        run.AreEqual("26.5s", run.World.Ui.TextOf(run.Find("Slot time 2")), "fastest full expedition survives a slower run");
        run.AreEqual("3", run.World.Ui.TextOf(run.Find("Slot gems 2")), "best gems across stages");
        run.AreEqual("<b>NEW</b>", run.World.Ui.TextOf(run.Find("Slot status 3")), "untouched slot");
    }

    [GameTest(Meadow)]
    public IEnumerable<ScriptWait> FlagClearRecordsOnlyTheSelectedSlot(GameTestRun run)
    {
        run.Session.Set("TrailSlot3");
        run.Objects.SetLocalTransform(run.Find("Courier"), new(run.PositionOf("Checkpoint 1") + new Vector2(8, 8), 0, Vector2.One));
        yield return ScriptWait.Until(() => run.Script<TrailRunner>("Courier").HasCheckpoint);
        run.Objects.SetLocalTransform(run.Find("Courier"), new(new Vector2(826, 300), 0, Vector2.One));
        yield return ScriptWait.Until(() => run.Script<TrailRunner>("Courier").Health == 2);
        run.Assert(run.PositionOf("Courier").X is > 800 and < 850, "fall returns to the lit checkpoint");
        run.Objects.SetLocalTransform(run.Find("Courier"), new(run.PositionOf("Goal 1") + new Vector2(8, 8), 0, Vector2.One));
        yield return ScriptWait.Until(() => run.Script<TrailRunner>("Courier").Completed);
        var saved = run.Session.Storage.LoadSlot(3, 1)!;
        run.Assert(saved.GetBool("stage1.clear"), "clear is saved");
        run.AreEqual(2d, saved.GetNumber("unlocked"), "next stage is unlocked");
        run.Assert(run.Session.Storage.LoadSlot(1, 1) == null, "slot 1 is untouched");
    }

    [GameTest("Scenes/Summit.scene")]
    public IEnumerable<ScriptWait> SummitTitleAndCompactHudSurvivePause(GameTestRun run)
    {
        var card = run.Find("Stage title");
        run.AreEqual("Stormwatch Summit", run.World.Ui.TextOf(card), "stage title");
        run.AreEqual("<b>03</b>", run.World.Ui.TextOf(run.Find("Heart count")), "Summit HUD reflects health");
        run.Tap("Pad.Back");
        yield return ScriptWait.Until(() => run.Time.IsPaused);
        run.AreEqual("Resume", run.World.Ui.TextOf(run.Find("Pause resume")), "pause shows both controls");
        foreach (int scale in new[] { 1, 2 })
        {
            run.World.Ui.Preferences.TextScale = scale;
            foreach (string key in new[] { "Pause board 0", "Pause controller 0", "Pause board 1", "Pause controller 1",
                "Pause board 2", "Pause controller 2", "Pause key resume", "Pause pad resume", "Pause key leave", "Pause pad leave" })
                AssertKeycap(run, key, scale);
        }
        run.Tap("P");
        yield return ScriptWait.Until(() => !run.Time.IsPaused);
        run.AreEqual("Stomp after a crash.", run.World.Ui.TextOf(run.Find("Stage tip")), "objective after resuming");
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
        run.AreEqual("Choose another slot.", run.World.Ui.TextOf(run.Find("Record")), "corrupt slot notice");
        run.Tap("Return");
        yield return ScriptWait.Frames(5);
        run.AreEqual(trailhead, run.SceneId, "unreadable slot is not opened");
        run.Tap("Tab");
        yield return ScriptWait.Frames(2);
        run.AreEqual("Stormwatch Summit", run.World.Ui.TextOf(run.Find("Selection")), "slot 2 remains selectable");
        run.AreEqual("Leg best  2.0 s", run.World.Ui.TextOf(run.Find("Record")), "Summit record has no gem score");
        run.Tap("Return");
        yield return ScriptWait.Frames(14);
        run.Assert(run.SceneId != trailhead && run.Script<TrailRunner>("Courier").Stage == 4, "valid slot enters Summit");
        run.AreEqual("[[[broken", File.ReadAllText(corrupt), "corrupt file remains intact");
    }

    [GameTest("Scenes/Trailhead.scene")]
    public IEnumerable<ScriptWait> DisplayPreferencesPersistAcrossSceneLoads(GameTestRun run)
    {
        run.Tap("F2");
        run.Tap("F3");
        yield return ScriptWait.Frames(3);
        run.AreEqual(2f, run.World.Ui.Preferences.TextScale, "maximum text size");
        run.AreEqual(0f, run.World.Ui.Preferences.ShakeScale, "gentle effects disable shake");
        run.AreEqual(0f, run.World.Ui.Preferences.FlashScale, "gentle effects disable flashing");
        run.Assert(!run.World.Ui.Preferences.Rumble, "gentle effects disable rumble");
        var settings = run.Session.Storage.LoadSettings(1)!;
        run.AreEqual(2d, settings.GetNumber("accessibility.textScale"), "text size was saved");
        var trailhead = run.SceneId;
        run.Tap("Return");
        yield return ScriptWait.Until(() => run.SceneId != trailhead);
        run.AreEqual(2f, run.World.Ui.Preferences.TextScale, "scene change kept maximum text size");
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

    [GameTest("Scenes/Trailhead.scene")]
    public IEnumerable<ScriptWait> TrailheadKeycapsAndGuideShareTheActionCenter(GameTestRun run)
    {
        string[] guides = ["Depart", "Slot", "Leg", "Text", "Motion"];
        string[] actions = ["Start", "Slot guide", "Select stage", "Accessibility", "Motion status"];
        foreach (int scale in new[] { 1, 2 })
        {
            run.World.Ui.Preferences.TextScale = scale;
            var bar = LayoutBounds(run, "Title actions");
            run.Assert(bar.Left >= 0 && bar.Right <= 384, "footer fits the viewport");
            float right = bar.Left + 5;
            for (int i = 0; i < guides.Length; i++)
            {
                string key = "Key " + guides[i];
                var bounds = LayoutBounds(run, key);
                AssertAligned(run, "Title actions", key);
                AssertAligned(run, key, actions[i]);
                AssertKeycap(run, key, scale);
                run.AreEqual(right + (i == 0 ? 0 : 5), bounds.Left, key + " has the same group spacing");
                var action = LayoutBounds(run, actions[i]);
                run.AreEqual(bounds.Right + 4, action.Left, key + " has the same caption spacing");
                right = action.Right;
            }
            run.AreEqual(bar.Right - 5, right, "last caption has the same outer padding as the first chip");

            var panel = LayoutBounds(run, "Route chip");
            var copy = LayoutBounds(run, "Route copy");
            run.Assert(Math.Abs(CenterY(panel) - CenterY(copy)) <= 2, "route copy uses the card's optical center");
            foreach (string label in new[] { "Selection", "Record" })
            {
                var bounds = LayoutBounds(run, label);
                run.AreEqual(panel.Left + 12, bounds.Left, label + " keeps the shared inset");
                run.Assert(bounds.Top >= panel.Top + 4 && bounds.Bottom <= panel.Bottom - 4, label + " clears the card's vertical edges");
            }

            WriteBounds(run, "Trailhead", scale, "Title actions", "Key Depart", "Key Depart glyph", "Start", "Key Slot",
                "Key Slot glyph", "Slot guide", "Key Leg", "Key Leg glyph", "Select stage", "Key Text", "Key Text glyph",
                "Accessibility", "Key Motion", "Key Motion glyph", "Motion status",
                "Slot card 1", "Slot card 2", "Slot card 3", "Slot label 1", "Slot label 2", "Slot label 3",
                "Route chip", "Route copy", "Selection", "Record");
        }
        yield return ScriptWait.Frames(1);
    }

    [GameTest("Scenes/Summit.scene")]
    public IEnumerable<ScriptWait> SummitResultsShareInsetAndCenterTheAdvanceButton(GameTestRun run)
    {
        run.World.Ui.SetVisible(run.Find("Results panel"), true);
        foreach (int scale in new[] { 1, 2 })
        {
            run.World.Ui.Preferences.TextScale = scale;
            run.Assert(LayoutBounds(run, "Stage card").Top >= LayoutBounds(run, "Warden panel").Bottom, "stage title clears the boss bar");
            var left = LayoutBounds(run, "Results panel").Left + 12;
            foreach (string name in new[] { "Results title", "Score route", "Score total label", "Result leg 1", "Results button" })
                run.AreEqual(left, LayoutBounds(run, name).Left, name + " shares the panel's 12px inset");
            AssertAligned(run, "Results button", "Result key");
            AssertAligned(run, "Result key", "Result pad key");
            AssertAligned(run, "Result key", "Results continue");
            AssertKeycap(run, "Result key", scale);
            AssertKeycap(run, "Result pad key", scale);
            var enter = LayoutBounds(run, "Result key");
            var start = LayoutBounds(run, "Result pad key");
            var caption = LayoutBounds(run, "Results continue");
            run.AreEqual(enter.Right + 5, start.Left, "the two keycaps have a 5px gap");
            run.AreEqual(start.Right + 5, caption.Left, "the caption follows the second keycap");
            run.AreEqual(CenterX(LayoutBounds(run, "Results button")), CenterX(LayoutBounds(run, "Result button unit")),
                "key and label stay centered together");
            ArcadeUi.RightNumber(run.World.Ui, run.Find("Results time"), "64.8s", 60, 7.5f);
            ArcadeUi.RightNumber(run.World.Ui, run.Find("Result leg 1 time"), "7.2s", 60, 7.5f);
            ArcadeUi.RightNumber(run.World.Ui, run.Find("Results haul"), "44", 34, 7.5f);
            ArcadeUi.RightNumber(run.World.Ui, run.Find("Result leg 1 tokens"), "9", 34, 7.5f);
            ArcadeUi.RightNumber(run.World.Ui, run.Find("Results gems"), "1", 28, 7.5f);
            ArcadeUi.RightNumber(run.World.Ui, run.Find("Result leg 1 gems"), "0", 28, 7.5f);
            run.AreEqual(LayoutBounds(run, "Results time").Right, LayoutBounds(run, "Result leg 1 time").Right, "time column edge");
            run.AreEqual(LayoutBounds(run, "Results haul").Right, LayoutBounds(run, "Result leg 1 tokens").Right, "token column edge");
            run.AreEqual(LayoutBounds(run, "Results gems").Right, LayoutBounds(run, "Result leg 1 gems").Right, "gem column edge");
            WriteBounds(run, "Summit", scale, "Results panel", "Results title", "Score header", "Score total",
                "Leg row 1", "Leg row 2", "Leg row 3", "Leg row 4", "Results button", "Result button unit", "Result key",
                "Result key glyph", "Result pad key", "Result pad key glyph", "Results continue", "Delivered badge", "Delivery seal", "Delivered",
                "Score time column");
        }
        yield return ScriptWait.Frames(1);
    }

    private static System.Drawing.RectangleF LayoutBounds(GameTestRun run, string name) =>
        run.World.Ui.Bounds(run.Find(name))!.Value;

    private static float CenterX(System.Drawing.RectangleF r) => r.Left + r.Width / 2;
    private static float CenterY(System.Drawing.RectangleF r) => r.Top + r.Height / 2;

    private static void AssertAligned(GameTestRun run, string first, string second) =>
        run.Assert(Math.Abs(CenterY(LayoutBounds(run, first)) - CenterY(LayoutBounds(run, second))) <= 2,
            second + " uses the same optical vertical center as " + first);

    private static void AssertKeycap(GameTestRun run, string name, int scale)
    {
        var key = LayoutBounds(run, name);
        var glyph = LayoutBounds(run, name + " glyph");
        run.AreEqual(CenterX(key), CenterX(glyph), name + " centers its glyph");
        AssertAligned(run, name, name + " glyph");
        run.Assert(glyph.Left - key.Left >= 2 * scale && key.Right - glyph.Right >= 2 * scale,
            name + " keeps at least 2px per side at the selected scale");
        run.Assert(glyph.Top >= key.Top && glyph.Bottom <= key.Bottom, name + " contains the full glyph height");
    }

    private static void WriteBounds(GameTestRun run, string scene, int scale, params string[] names)
    {
        if (Environment.GetEnvironmentVariable("MONOON_LEDGERUN_UI_BOUNDS") != "1") return;
        foreach (string name in names)
        {
            var r = LayoutBounds(run, name);
            Console.WriteLine(FormattableString.Invariant($"LEDGERUN_UI|{scene}|{scale}|{name}|{r.X}|{r.Y}|{r.Width}|{r.Height}"));
        }
    }
}
