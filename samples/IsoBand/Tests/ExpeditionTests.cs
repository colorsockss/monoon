using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Engine.Core;

public sealed class ExpeditionTests
{
    private const string Marsh = "Scenes/Marsh.scene";
    private const string Haven = "Scenes/Haven.scene";
    private static readonly Guid MarshId = Guid.Parse("a1b92a57-febd-5582-87cc-8700e056ac09");
    private static readonly Guid CryptId = Guid.Parse("fd96a88e-4577-5ef2-ac84-c67c9df6467d");

    [GameTest(Marsh)]
    public void IsoWaterShapesAreBakedIntoNavigation(GameTestRun run)
    {
        var layer = run.Find("Diamond terrain");
        var area = Expedition.BakeNavigation(run.Objects, layer);
        foreach (var cell in new[] { new Point(2, 5), new Point(3, 6), new Point(6, 8) })
        {
            var center = run.Objects.TileCellCenter(layer, cell)!.Value;
            run.AreEqual<Point?>(cell, run.Objects.TileCellAt(layer, center), "world-to-cell round trip");
            run.Assert(!area.IsOpen(center), "solid water is closed at its world center");
        }
        Vector2 land = run.Objects.TileCellCenter(layer, new(4, 4))!.Value;
        run.Assert(area.IsOpen(land), "land along the diamond shore is walkable");
        Span<Vector2> route = stackalloc Vector2[64];
        run.Assert(area.FindPath(run.PositionOf("Captain"), run.PositionOf("Gate footprint"), route) > 1,
            "the authored land connects the start to the gate");
    }

    [GameTest(Marsh, MaxFrames = 720)]
    public IEnumerable<ScriptWait> ClickMovesTheBandIntoAnimatedAutoCombat(GameTestRun run)
    {
        yield return ScriptWait.Frames(57);
        var origin = run.PositionOf("Captain");
        var follower = run.PositionOf("Companion 1");
        ClickCell(run, new(4, 4));
        yield return ScriptWait.Frames(17);
        run.AreEqual("Walk", run.Objects.Animator(run.Find("Captain motion"))!.State, "moving drives the captain's animator");
        run.Assert(run.Objects.IsActive(run.Find("Encounter focus")), "the active enemy wave joins the camera group");
        run.Assert(run.World.TryGetView(run.Find("Expedition camera"), out var view), "camera follows the group");
        var captain = view.WorldToScreen(run.PositionOf("Captain"));
        float middleX = view.Viewport.Left + view.Viewport.Width / 2f;
        float middleY = view.Viewport.Top + view.Viewport.Height / 2f;
        run.Assert(MathF.Abs(captain.X - middleX) < 61 && MathF.Abs(captain.Y - middleY) < 61,
            "camera keeps the captain near center during the encounter");
        run.Tap("Space");
        yield return ScriptWait.Until(() => run.Script<Expedition>("Captain").Defeated > 0);
        run.Assert(Vector2.Distance(run.PositionOf("Captain"), origin) > 20, "captain moved toward the selected cell");
        run.Assert(Vector2.Distance(run.PositionOf("Companion 1"), follower) > 8, "companion followed");
        run.Assert(Vector2.Distance(run.PositionOf("Companion 1"), run.PositionOf("Captain")) > 10, "party keeps its spacing");
        yield return ScriptWait.Frames(35);
        bool foundFall = false;
        for (int i = 1; i <= 3; i++) foundFall |= !run.Objects.IsActive(run.Find($"Foe {i}"));
        run.Assert(foundFall, "defeated foes leave the stage after their fall animation");
    }

    [GameTest(Haven)]
    public IEnumerable<ScriptWait> HiringAndGearPersistThroughSceneReload(GameTestRun run)
    {
        Seed(run, 0, 30, 40);
        yield return ScriptWait.Frames(4);
        run.Tap("Return");
        yield return ScriptWait.Frames(4);
        yield return WalkToTownFire(run);
        run.Tap("H");
        yield return ScriptWait.Frames(3);
        run.Assert(run.Objects.IsActive(run.Find("Companion 3")), "the hired healer enters the formation");
        run.Tap("U");
        yield return ScriptWait.Frames(3);
        run.Session.LoadScene(run.SceneId!.Value);
        yield return ScriptWait.Frames(4);

        var progress = run.Session.Storage.LoadSlot(1, 1)!;
        run.AreEqual(3d, progress.GetNumber("recruits"), "recruit count is saved");
        run.AreEqual(2d, progress.GetNumber("gear"), "gear upgrade is saved");
        run.AreEqual(26d, progress.GetNumber("coins"), "crown costs were applied once");
        run.Assert(run.Objects.IsActive(run.Find("Companion 3")), "the healer is reconstructed from the slot");
    }

    [GameTest(Haven)]
    public IEnumerable<ScriptWait> FinishedExpeditionRequiresExplicitReplay(GameTestRun run)
    {
        var haven = run.SceneId;
        Seed(run, 3, 30, 18);
        yield return ScriptWait.Frames(4);
        run.AreEqual("Expedition complete", run.World.Ui.TextOf(run.Find("Ledger heading")), "completed slot opens the ending");
        string[] values = ["9", "12", "7", "3", "3", "--"];
        for (int i = 0; i < values.Length; i++)
        {
            run.AreEqual(values[i], run.World.Ui.TextOf(run.Find($"Expedition record {i+1}")),
                "the saved tally, level, band and fallback time occupy separate rows");
            SameLine(run, Box(run, $"Record label {i+1}").Left, Box(run, "Ledger heading").Left);
            SameLine(run, Box(run, $"Expedition record {i+1}").Left, Box(run, "Expedition record 1").Left);
            SameLine(run, Box(run, $"Record label {i+1}").Top, Box(run, $"Expedition record {i+1}").Top);
        }

        run.Tap("Return");
        yield return ScriptWait.Frames(5);
        run.Press("Down");
        yield return ScriptWait.Frames(130);
        run.Release("Down");
        run.Tap("Return");
        yield return ScriptWait.Frames(26);
        run.AreEqual(haven, run.SceneId, "normal travel did not reopen the Crypt");
        run.AreEqual("Expedition complete", run.World.Ui.TextOf(run.Find("Ledger heading")), "Haven gate returns to the ending");
        yield return ScriptWait.Frames(12);
        var replay = Box(run, "New run action");
        run.Click(replay.Left + replay.Width / 2, replay.Top + replay.Height / 2);
        yield return ScriptWait.Frames(37);
        run.AreEqual<Guid?>(MarshId, run.SceneId, "explicit replay opens Marsh");
        var restarted = run.Session.Storage.LoadSlot(1, 1)!;
        run.AreEqual(0d, restarted.GetNumber("route"), "route restarted");
        run.AreEqual(0d, restarted.GetNumber("fieldTicks", -1), "new expedition starts a fresh field clock");
        run.AreEqual(30d, restarted.GetNumber("xp"), "experience preserved");
        run.AreEqual(18d, restarted.GetNumber("coins"), "crowns preserved");
    }

    [GameTest(Haven)]
    public IEnumerable<ScriptWait> BothArchersAttackAtRange(GameTestRun run)
    {
        Seed(run, 0, 30, 36);
        yield return ScriptWait.Frames(4);
        run.Tap("Return");
        yield return ScriptWait.Frames(4);
        yield return WalkToTownFire(run);
        for (int i = 0; i < 2; i++)
        {
            run.Tap("H");
            yield return ScriptWait.Frames(5);
        }
        run.Assert(run.Objects.IsActive(run.Find("Companion 4")), "both recruits were hired");
        run.Session.LoadScene(CryptId);
        yield return ScriptWait.Frames(54);
        var original = run.Objects.Animator(run.Find("Companion 2 motion"))!;
        var recruit = run.Objects.Animator(run.Find("Companion 4 motion"))!;
        bool originalHit = false, recruitHit = false, arrow = false;
        for (int tick = 0; tick < 80; tick++)
        {
            originalHit |= original.State == "Attack" && BeyondMelee(run, "Companion 2");
            recruitHit |= recruit.State == "Attack" && BeyondMelee(run, "Companion 4");
            arrow |= run.Objects.IsActive(run.Find("Arrow 1"));
            if (originalHit && recruitHit && arrow) break;
            yield return ScriptWait.Frames(1);
        }
        run.Assert(originalHit && recruitHit && arrow, "both archers fire visible arrows from beyond melee range");
    }

    [GameTest(Haven)]
    public IEnumerable<ScriptWait> LedgerAndHudShareAlignmentAtBothTextScales(GameTestRun run)
    {
        foreach (float scale in new[] { 1f, 2f })
        {
            run.World.Ui.Preferences.TextScale = scale;
            yield return ScriptWait.Frames(12);
            FitsInside(run, "Ledger title", "Ledger");
            FitsInside(run, "Muster action text", "Muster action");
            CheckButton(run, "Muster action");
            CheckButton(run, "Haven action");
            CheckButton(run, "New run action");
            SameLine(run, Box(run, "Ledger heading").Left, Box(run, "Ledger title").Left);
            SameLine(run, Box(run, "Ledger description").Left, Box(run, "Haven action").Left);
            SameLine(run, Box(run, "Ledger description").Left, Box(run, "Ledger signature").Left);
            SameLine(run, Box(run, "Haven action").Top, Box(run, "New run action").Top);

            SameLine(run, CenterX(Box(run, "Level value")), CenterX(Box(run, "Level medal")));
            SameLine(run, CenterY(Box(run, "Level value")), CenterY(Box(run, "Level medal")) + 1.6f);
            FitsInside(run, "Crowns value", "Progress panel");
            FitsInside(run, "Portrait HP 1", "Party panel");
            FitsInside(run, "Chapter subtitle row", "Chapter card");
            SameLine(run, CenterY(Box(run, "Crowns illustration")), CenterY(Box(run, "Crowns value row")));
            SameLine(run, CenterY(Box(run, "Crowns illustration")), CenterY(Box(run, "Level medal")));
            SameLine(run, Box(run, "Quartermaster heading").Left, Box(run, "Hire action row").Left);
            SameLine(run, Box(run, "Hire action row").Left, Box(run, "Gear action row").Left);
            var progress = Box(run, "Progress panel");
            SameLine(run, 472, progress.Right);
            SameLine(run, scale == 1 ? 116 : 96, progress.Width);
            SameLine(run, progress.Right - 8, Box(run, "XP track").Right);

            var pulse = Box(run, "Pulse panel");
            SameLine(run, scale == 1 ? 108 : 146, pulse.Width);
            SameLine(run, CenterY(Box(run, "Pulse medallion")), CenterY(Box(run, "Pulse caption row")));
            foreach (string prompt in new[] { "Wave panel", "Status panel" })
            {
                SameLine(run, pulse.Bottom, Box(run, prompt).Bottom);
                SameLine(run, pulse.Height, Box(run, prompt).Height);
                if (scale == 1) CheckHudBudget(run, prompt, "Pulse panel");
            }
            if (scale == 1)
            {
                CheckHudBudget(run, "Pulse panel", "Chapter card");
                CheckHudBudget(run, "Pulse panel", "Guardian panel");
            }
        }

        run.Tap("Return");
        yield return ScriptWait.Frames(12);
        run.Tap("Tab");
        yield return ScriptWait.Frames(12);
        run.AreEqual("Field notes", run.World.Ui.TextOf(run.Find("Ledger title")), "field notes available on demand");

        run.Session.LoadScene(MarshId);
        yield return ScriptWait.Frames(62);
        run.Tap("Space");
        yield return ScriptWait.Frames(5);
        run.AreEqual("Pulse 6s", run.World.Ui.TextOf(run.Find("Pulse caption")), "cooldown is a single caption");
        SameLine(run, 116, Box(run, "Pulse panel").Width);
        CheckHudBudget(run, "Pulse panel", "Guardian panel");
    }

    private static void CheckButton(GameTestRun run, string name)
    {
        var button = Box(run, name);
        var cap = Box(run, name + " key");
        var text = Box(run, name + " text");
        SameLine(run, CenterX(button), CenterX(cap));
        SameLine(run, CenterX(button), CenterX(text));
        SameLine(run, button.Top + 39, CenterY(text));
    }

    private static RectangleF Box(GameTestRun run, string name) => run.World.Ui.Bounds(run.Find(name))!.Value;
    private static float CenterX(RectangleF box) => box.Left + box.Width / 2;
    private static float CenterY(RectangleF box) => box.Top + box.Height / 2;
    private static void SameLine(GameTestRun run, float a, float b) =>
        run.Assert(MathF.Abs(a - b) <= 1, $"expected a shared guide, got {a:0.0} and {b:0.0}");

    private static bool BeyondMelee(GameTestRun run, string archer)
    {
        Vector2 position = run.PositionOf(archer);
        float nearest = float.PositiveInfinity;
        for (int i = 1; i <= 3; i++)
            if (run.Objects.IsActive(run.Find($"Foe {i}")))
                nearest = Math.Min(nearest, Vector2.Distance(position, run.PositionOf($"Foe {i}")));
        return nearest is > 36 and < 91;
    }

    private static void Seed(GameTestRun run, int route, int xp, int coins)
    {
        var saved = new GameData();
        saved.Set("route", route);
        saved.Set("xp", xp);
        saved.Set("coins", coins);
        run.Session.Storage.SaveSlot(1, saved);
        run.Session.LoadScene(run.SceneId!.Value);
    }

    private static ScriptWait WalkToTownFire(GameTestRun run)
    {
        ClickCell(run, new(3, 3));
        return ScriptWait.Frames(35);
    }

    private static void ClickCell(GameTestRun run, Point cell)
    {
        run.Assert(run.World.TryGetView(run.Find("Expedition camera"), out var camera), "expedition camera is ready");
        var point = camera.WorldToScreen(run.Objects.TileCellCenter(run.Find("Diamond terrain"), cell)!.Value);
        run.Click(point.X, point.Y);
    }

    private static void CheckHudBudget(GameTestRun run, params string[] panels)
    {
        // Use the authored party width for all five slots, even when the test starts with three.
        var party = run.World.Ui.Bounds(run.Find("Party panel"))!.Value;
        var progress = run.World.Ui.Bounds(run.Find("Progress panel"))!.Value;
        float area = 124 * party.Height + progress.Width * progress.Height;
        foreach (string panel in panels)
        {
            var box = run.World.Ui.Bounds(run.Find(panel))!.Value;
            area += box.Width * box.Height;
        }
        run.Assert(area <= 480 * 270 * .15f, $"maximum HUD area is {area} logical pixels");
    }

    private static void FitsInside(GameTestRun run, string child, string parent)
    {
        var bounds = run.World.Ui.Bounds(run.Find(child))!.Value;
        var container = run.World.Ui.Bounds(run.Find(parent))!.Value;
        run.Assert(bounds.Left >= container.Left && bounds.Right <= container.Right &&
            bounds.Top >= container.Top && bounds.Bottom <= container.Bottom, $"{child} stays inside {parent}");
    }
}
