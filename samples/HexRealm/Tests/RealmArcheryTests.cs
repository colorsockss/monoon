using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Engine.Core;

public sealed class RealmArcheryTests
{
    [GameTest("Scenes/Realm.scene", MaxFrames = 180)]
    public IEnumerable<ScriptWait> LongShotLandsBeforeDamageIsPresented(GameTestRun run) => Shoot(run, 2);

    [GameTest("Scenes/Realm.scene", MaxFrames = 180)]
    public IEnumerable<ScriptWait> AdjacentRetaliationFollowsArrowImpact(GameTestRun run) => Shoot(run, 1);

    private static IEnumerable<ScriptWait> Shoot(GameTestRun run, int range)
    {
        var ground = new int[RealmRules.Count];
        Array.Fill(ground, 1);
        var campaign = new RealmRules(18427, ground, Array.ConvertAll(ground, RealmRules.EntryCost));
        campaign.Units.Clear();
        var archer = new RealmRules.Unit(new Point(4, 5), 1, UnitKind.Archer);
        var defender = new RealmRules.Unit(new Point(4 + range, 5), 2, UnitKind.Spear);
        campaign.Units.Add(archer);
        campaign.Units.Add(defender);
        var board = run.Script<RealmBoard>("Board controller");
        board.Install(campaign, true);

        var bow = run.Find("Pieces/1-Archer");
        var animator = run.Objects.Animator(bow)!;
        var launchFrames = new List<int>();
        var hitFrames = new List<int>();
        int DamageMarkers() => run.World.Objects.Count(o => o.Name is "Damage" or "Arrow damage" && run.Objects.IsActive(o.Id));
        Guid Arrow() => run.Objects.FindByGroup("bow-arrow").FirstOrDefault(run.Objects.IsActive);
        run.World.SoundRequested += path =>
        {
            if (path.EndsWith("Bow.wav", StringComparison.Ordinal)) launchFrames.Add(run.Frame);
            if (path.EndsWith("Hit.wav", StringComparison.Ordinal)) hitFrames.Add(run.Frame);
        };

        run.Replay(range == 1 ? "Tests/ArcherNear.inputreplay" : "Tests/ArcherFar.inputreplay");
        yield return ScriptWait.Until(() => animator.State == "Shoot");
        run.AreEqual(4, defender.Health, "combat resolves independently of the drawing");
        var tile = board.DrawnAt(0);
        run.AreEqual(Guid.Empty, Arrow(), "drawing the bow precedes launch");
        run.AreEqual(0, DamageMarkers(), "no early damage label");

        yield return ScriptWait.Until(() => Arrow() != Guid.Empty);
        var arrow = Arrow();
        run.AreEqual(2, animator.Frame, "release marker spawns the arrow on the release frame");
        run.AreEqual(1, launchFrames.Count, "one bow sound at launch");
        run.AreEqual(0, hitFrames.Count, "the impact sound waits for arrival");
        run.Assert(run.Objects.TryGetLocalTransform(arrow, out var launch), "arrow has a launch transform");
        yield return ScriptWait.Frames(9);
        run.AreEqual(tile, board.DrawnAt(0), "the archer stays in place");
        run.AreEqual(arrow, Arrow(), "the same projectile is still flying");
        run.AreEqual(0, DamageMarkers(), "no damage label in flight");
        run.Assert(run.Objects.TryGetLocalTransform(arrow, out var middle) && middle.Rotation > launch.Rotation,
            "the arrow rotates along its arc");

        yield return ScriptWait.Until(() => DamageMarkers() == 1);
        run.AreEqual(Guid.Empty, Arrow(), "arrow returns to the pool at impact");
        run.AreEqual(1, hitFrames.Count, "one impact cue arrives with the damage label");
        run.AreEqual(range == 1 ? 18 : 26, hitFrames[0] - launchFrames[0], "flight length in 60Hz ticks");
        yield return ScriptWait.Frames(12);
        run.AreEqual(range == 1 ? 2 : 1, DamageMarkers(), "only a legal reply follows the hit");
    }
}
