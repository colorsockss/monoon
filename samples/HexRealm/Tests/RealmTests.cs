using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Engine.Core;

public sealed class RealmTests
{
    [GameTest("Scenes/Realm.scene")]
    public IEnumerable<ScriptWait> TurnReportsSeparateTrainingHarvestAndResearch(GameTestRun run)
    {
        var board = run.Script<RealmBoard>("Board controller");
        var turn = run.Find("Turn value");
        var food = run.Find("Command advice");
        var lore = run.Find("Lore value");
        var ui = run.World.Ui;
        run.Tap("N");
        yield return ScriptWait.Frames(32);
        run.Tap("B");
        yield return ScriptWait.Frames(4);
        run.Tap("R");
        yield return ScriptWait.Frames(4);
        run.Tap("4");
        yield return ScriptWait.Frames(4);
        run.Tap("Return");
        yield return ScriptWait.Frames(110);
        var previousFood = ui.TextOf(food);

        run.Tap("Return");
        yield return ScriptWait.Frames(12);
        run.AreEqual("02 / 25", ui.TextOf(turn), "turn counter waits for rival motion");
        run.AreEqual(previousFood, ui.TextOf(food), "harvest waits for the new turn report");
        yield return ScriptWait.Until(() => board.Status == "Haven trained Spearguard");
        run.AreEqual("03 / 25", ui.TextOf(turn), "training follows the turn stamp");
        run.AreEqual(previousFood, ui.TextOf(food), "training precedes harvest");
        run.AreEqual("Terraces", ui.TextOf(lore), "training precedes research");
        yield return ScriptWait.Until(() => ui.TextOf(food) != previousFood);
        string? firstHarvest = ui.TextOf(food);
        yield return ScriptWait.Until(() => ui.TextOf(lore) == "Fletching");
        run.Assert(ui.TextOf(food) != firstHarvest, "food counts through intermediate values");
    }

    [GameTest("Scenes/Realm.scene")]
    public IEnumerable<ScriptWait> BoardActionsRequestAudibleEffects(GameTestRun run)
    {
        var heard = new List<string>();
        run.World.SoundRequested += path => heard.Add(System.IO.Path.GetFileNameWithoutExtension(path));
        var pcm = new float[1600];

        run.Tap("N");
        yield return ScriptWait.Frames(32);
        run.Click(350, 330); // The starting scout.
        yield return ScriptWait.Frames(3);
        run.AreEqual("Select", heard.LastOrDefault(), "select cue");
        run.World.Mixer.Render(pcm);
        run.Assert(pcm.Any(sample => Math.Abs(sample) > .01f), "SDL_mixer produced audible samples");

        run.Click(288, 330); // The starting settler.
        yield return ScriptWait.Frames(3);
        run.Tap("B");
        yield return ScriptWait.Frames(3);
        run.AreEqual("Build", heard.LastOrDefault(), "founding cue");
        run.AreEqual(1f, run.Session.Screen.For(run.Find("Atlas camera")).Current.Zoom,
            "founding does not jump the camera scale");
        run.Tap("Return");
        yield return ScriptWait.Frames(3);
        run.AreEqual("Turn", heard.LastOrDefault(), "rival turn cue");
    }

    [GameTest("Scenes/Realm.scene")]
    public IEnumerable<ScriptWait> EmptySlotPreservesTheCurrentTurn(GameTestRun run)
    {
        var board = run.Script<RealmBoard>("Board controller");
        run.Assert(!board.InCampaign, "new profile opens at the title");
        run.Tap("F9");
        yield return ScriptWait.Frames(3);
        run.Assert(!board.InCampaign, "empty load leaves the title visible");
        run.Tap("N");
        yield return ScriptWait.Frames(32);
        run.Tap("B");
        yield return ScriptWait.Frames(3);
        run.Tap("Return");
        yield return ScriptWait.Frames(110);
        ulong hash = board.StateHash;
        run.Tap("F9");
        yield return ScriptWait.Frames(75);
        run.AreEqual(2, board.Turn, "current turn");
        run.AreEqual(2, board.CityCount, "existing cities");
        run.AreEqual(hash, board.StateHash, "unsaved state preserved");
        run.AreEqual("No saved campaign.", board.Status, "empty-slot message");
    }

    [GameTest("Scenes/Realm.scene")]
    public IEnumerable<ScriptWait> OlderSlotOpensAFreshCampaignAndCanBeOverwritten(GameTestRun run)
    {
        var legacy = new GameData { Version = 1 };
        legacy.Set("orders", "old-campaign");
        run.Session.Storage.SaveSlot(1, legacy);
        var board = run.Script<RealmBoard>("Board controller");

        run.Tap("F9");
        yield return ScriptWait.Frames(32);
        run.Assert(board.InCampaign && board.Turn == 1, "the older format starts a new campaign");
        run.AreEqual("Older save: new campaign", board.Status, "version fallback message");
        run.AreEqual("old-campaign", run.Session.Storage.LoadSlot(1, 1)!.GetText("orders"), "load alone did not write");
        run.Tap("F5");
        yield return ScriptWait.Frames(4);
        run.AreEqual(4, run.Session.Storage.LoadSlot(1, 4)!.Version, "F5 wrote the fresh campaign in slot 1");
    }

    [GameTest("Scenes/Realm.scene")]
    public void RivalFoundsAndBuildsAWorkingFront(GameTestRun run)
    {
        var rules = NewRules();
        for (int i = 0; i < 7; i++) rules.Apply(new(OrderKind.End));
        run.Assert(rules.CityAt(RealmRules.Greyharbor) is { Side: 2 }, "second rival city");
        run.Assert(rules.Units.Any(unit => unit is { Alive: true, Side: 2, Kind: UnitKind.Worker }), "working rival economy");
        run.Assert(rules.Improvements.Any(value => value != 0), "worker improved a rival tile");
        run.Assert(rules.Units.Any(unit => unit is { Alive: true, Side: 2, Kind: UnitKind.Spear } &&
                   RealmRules.Distance(unit.Cell, RealmRules.Start) <= 4), "rival pressed the coast");
    }

    [GameTest("Scenes/Realm.scene")]
    public void FullGrowthGaugeClaimsTheSurveyedTile(GameTestRun run)
    {
        var rules = NewRules();
        run.Assert(rules.Apply(new(OrderKind.Found)), "Haven founded");
        var city = rules.CityAt(RealmRules.Start)!;
        rules.Apply(new(OrderKind.End));
        var forecast = rules.Forecast(city);
        run.Assert(forecast.Next != null && forecast.Turns > 1, "next claim and harvest forecast available");
        Point next = forecast.Next!.Value;
        int before = rules.Territory(1), population = city.Population;
        Point? marked = null;
        rules.Claimed = (town, cause, cells) =>
        {
            if (town != city || cause != ClaimCause.Growth) return;
            run.AreEqual(1, cells.Length, "each growth adds one parcel");
            marked = cells[0];
        };
        for (int i = 1; i < forecast.Turns; i++)
        {
            rules.Apply(new(OrderKind.End));
            run.AreEqual(before, rules.Territory(1), "border waits for the food threshold");
            run.AreEqual(next, rules.Forecast(city).Next!.Value, "survey remains until harvest matures");
        }
        var ripe = rules.Forecast(city);
        run.AreEqual(1, ripe.Turns, "gauge is one harvest away");
        rules.Apply(new(OrderKind.End));
        run.AreEqual(population + 1, city.Population, "food increases population");
        run.AreEqual(ripe.Food + ripe.Harvest - ripe.Goal, city.Food, "surplus food carries over");
        run.AreEqual(before + 1, rules.Territory(1), "land count rises by exactly one");
        run.Assert(marked == next && rules.Owner[RealmRules.Index(next)] == 1, "the ink event and actual claim both use the previewed parcel");
    }

    [GameTest("Scenes/Realm.scene")]
    public IEnumerable<ScriptWait> FoundProduceSaveAndReload(GameTestRun run)
    {
        var board = run.Script<RealmBoard>("Board controller");
        run.Tap("N");
        yield return ScriptWait.Frames(32);
        run.Tap("B");
        yield return ScriptWait.Frames(3);
        run.Tap("4");
        yield return ScriptWait.Frames(3);
        run.AreEqual(2, board.CityCount, "settler became a city");
        run.Tap("Return");
        yield return ScriptWait.Frames(110);
        run.Tap("F5");
        yield return ScriptWait.Frames(3);
        ulong hash = board.StateHash;
        run.Tap("Return");
        yield return ScriptWait.Frames(110);
        run.Assert(board.StateHash != hash, "later turn changes the state");
        run.Tap("F9");
        yield return ScriptWait.Frames(32);
        run.AreEqual(2, board.Turn, "turn rewound to the save");
        run.AreEqual(hash, board.StateHash, "replayed orders match");
    }

    [GameTest("Scenes/Realm.scene", MaxFrames = 3300)]
    public IEnumerable<ScriptWait> FinishedCampaignSurvivesSaveTitleAndReload(GameTestRun run)
    {
        var board = run.Script<RealmBoard>("Board controller");
        run.Tap("N");
        yield return ScriptWait.Frames(32);
        run.Tap("B");
        yield return ScriptWait.Frames(3);
        for (int turn = 0; turn < RealmRules.LastTurn && board.Winner == 0; turn++)
        {
            if (turn % 2 == 0)
            {
                run.Tap("4");
                yield return ScriptWait.Frames(3);
            }
            run.Tap("Return");
            yield return ScriptWait.Frames(110);
        }
        int winner = board.Winner;
        int end = board.Turn;
        run.Assert(winner > 0 && end <= RealmRules.LastTurn + 1, "campaign reaches a result within 25 turns");
        yield return ScriptWait.Frames(105); // Let the claimed land and the charter finish presenting.
        ulong hash = board.StateHash;
        run.Tap("F5");
        yield return ScriptWait.Frames(3);
        run.Tap("N");
        yield return ScriptWait.Frames(32);
        run.Assert(!board.InCampaign, "back to the title from the result");
        run.Tap("F9");
        yield return ScriptWait.Frames(32);
        run.AreEqual(hash, board.StateHash, "result survives load");
        run.AreEqual(winner, board.Winner, "winner survives load");
        run.Tap("Return");
        yield return ScriptWait.Frames(110);
        run.AreEqual(end, board.Turn, "terminal state rejects further turns");
    }

    [GameTest("Scenes/Realm.scene")]
    public void PathPreviewAndOrderBothTakeTheDetour(GameTestRun run)
    {
        var rules = NewRules();
        var scout = rules.Units[1];
        var target = new Point(2, 5);
        var path = new Point[RealmRules.Count];
        var reached = new GridReach[RealmRules.Count];
        int count = rules.Reachable(scout, reached);
        run.Assert(reached.Take(count).Any(item => item.Cell == target), "movement outline includes the detour");
        int length = rules.PreviewPath(scout, target, path, out long cost);
        run.Assert(length > 3 && !path.Take(length).Contains(RealmRules.Start), "occupied direct path was excluded");
        run.Assert(rules.Apply(new(OrderKind.Move, 1, target.X, target.Y)), "previewed route was executable");
        run.AreEqual(target, scout.Cell, "scout arrived at the preview destination");
        run.AreEqual(40L - cost, scout.Moves, "preview and order charge the same cost");
    }

    [GameTest("Scenes/Realm.scene")]
    public void AttackForecastAndDamageAgree(GameTestRun run)
    {
        var rules = NewRules();
        var spear = rules.Units[2];
        var rival = rules.Units[4];
        rival.Cell = new(4, 6);
        var forecast = rules.Attack(spear, rival.Cell)!.Value;
        run.Assert(rules.Apply(new(OrderKind.Move, 2, rival.Cell.X, rival.Cell.Y)), "attack applied");
        run.AreEqual(forecast.Health - forecast.Damage, rival.Health, "damage matched forecast");
        run.AreEqual(7 - forecast.Retaliation, spear.Health, "retaliation matched forecast");
        run.AreEqual(0L, spear.Moves, "attack exhausts the actor");
    }

    [GameTest("Scenes/Realm.scene")]
    public void CapturingTheFirstCityAwayFromStartEndsTheCampaign(GameTestRun run)
    {
        var rules = NewRules();
        var location = new Point(3, 4);
        run.Assert(rules.Apply(new(OrderKind.Move, 0, location.X, location.Y)) &&
                   rules.Apply(new(OrderKind.Found)), "first city founded away from the start");
        var capital = rules.CityAt(location)!;
        int beforeCapture = rules.Territory(1);
        foreach (var piece in rules.Units) piece.Health = 0;
        rules.Units[4].Health = 7;
        rules.Units[4].Cell = new(3, 3);
        capital.Health = 3;
        capital.Food = capital.FoodGoal;
        var claims = new List<ClaimCause>(2);
        rules.Claimed = (town, cause, _) =>
        {
            if (town == capital) claims.Add(cause);
        };
        rules.Apply(new(OrderKind.End));
        run.Assert(claims.SequenceEqual([ClaimCause.Growth, ClaimCause.Capture]),
            "a capture on the harvest turn transfers freshly claimed land as well");
        run.AreEqual(2, capital.Side, "the rival captured the capital");
        run.AreEqual(2, rules.Winner, "capturing the settled capital ended the campaign");
        run.Assert(beforeCapture > 0 && rules.CoastResult.Land == rules.Territory(1),
            "final tally uses the territory after the capital changes hands");
        run.AreEqual(0, rules.CoastResult.Land, "the occupied city transfers all its surveyed holdings");
    }

    [GameTest("Scenes/Realm.scene")]
    public void TrainingReusesACasualtyAndCapsStoredProduction(GameTestRun run)
    {
        var rules = NewRules();
        run.Assert(rules.Apply(new(OrderKind.Found)), "first city founded");
        while (rules.Units.Count < RealmRules.UnitLimit)
            rules.Units.Add(new(RealmRules.Start, 1, UnitKind.Scout) { Health = 0 });
        var city = rules.CityAt(RealmRules.Start)!;
        city.Industry = 99;
        run.Assert(rules.Apply(new(OrderKind.Train, 1, (int)UnitKind.Spear)), "can queue production with casualties");
        run.Assert(rules.Apply(new(OrderKind.End)), "production resolves");
        run.AreEqual(RealmRules.UnitLimit, rules.Units.Count, "roster storage remains bounded");
        run.Assert(rules.Units[0] is { Alive: true, Side: 1, Kind: UnitKind.Spear }, "production reused the departed settler slot");
        run.Assert(city.Industry < 24 && city.Queue == null, "stockpile is bounded after the completed order");
    }

    [GameTest("Scenes/Realm.scene")]
    public void AQuietCampaignGoesToTheScoreLimit(GameTestRun run)
    {
        var rules = NewRules();
        for (int i = 0; i < RealmRules.LastTurn; i++) run.Assert(rules.Apply(new(OrderKind.End)), "turn accepted");
        run.AreEqual(RealmRules.LastTurn + 1, rules.Turn, "score limit");
        run.AreEqual(2, rules.Winner, "no settlements loses the reckoning");
        run.Assert(!rules.Apply(new(OrderKind.End)), "result locks the clock");
    }

    private static RealmRules NewRules()
    {
        var terrain = RealmRules.Generate(18427);
        return new(18427, terrain, Array.ConvertAll(terrain, RealmRules.EntryCost));
    }
}
