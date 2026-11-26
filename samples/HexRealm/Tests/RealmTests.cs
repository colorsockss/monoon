using System;
using System.Collections.Generic;
using System.Linq;
using Engine.Core;

public sealed class RealmTests
{
    [GameTest("Scenes/Realm.scene")]
    public IEnumerable<ScriptWait> LoadingAnEmptySlotKeepsTheCurrentCampaign(GameTestRun run)
    {
        var board = run.Script<RealmBoard>("Board controller");
        run.Tap("B");
        yield return ScriptWait.Frames(2);
        run.Tap("Return");
        yield return ScriptWait.Frames(2);
        ulong hash = board.StateHash;

        run.Tap("F9");
        yield return ScriptWait.Frames(5);

        var current = run.Script<RealmBoard>("Board controller");
        run.AreEqual(2, current.Turn, "current turn");
        run.AreEqual(2, current.CityCount, "existing cities");
        run.AreEqual(hash, current.StateHash, "unsaved state preserved");
        run.AreEqual("No saved campaign. Current campaign kept.", Notice(run), "empty-slot message");
    }

    [GameTest("Scenes/Realm.scene", MaxFrames = 360)]
    public IEnumerable<ScriptWait> FoundEndSaveAndReload(GameTestRun run)
    {
        var board = run.Script<RealmBoard>("Board controller");
        int before = board.CityCount;
        run.Tap("B");
        yield return ScriptWait.Frames(2);
        run.AreEqual(before + 1, board.CityCount, "settler became a city");
        for (int i = 0; i < 40; i++)
        {
            if (i % 5 == 0)
            {
                run.Tap("4");
                yield return ScriptWait.Frames(2);
            }
            run.Tap("Return");
            yield return ScriptWait.Frames(2);
        }
        run.AreEqual(41, board.Turn, "40 turns committed");
        ulong hash = board.StateHash;
        run.Tap("F5");
        yield return ScriptWait.Frames(3);
        run.Tap("F9");
        yield return ScriptWait.Frames(5);
        var reloaded = run.Script<RealmBoard>("Board controller");
        run.Assert(!ReferenceEquals(reloaded, board), "load entered a new world");
        run.AreEqual(41, reloaded.Turn, "all commands were restored");
        run.AreEqual(hash, reloaded.StateHash, "resumed state matches");
    }

    [GameTest("Scenes/Realm.scene", MaxFrames = 360)]
    public IEnumerable<ScriptWait> FinishedCampaignCanBeReloadedAndRestarted(GameTestRun run)
    {
        var board = run.Script<RealmBoard>("Board controller");
        run.Tap("B");
        yield return ScriptWait.Frames(2);
        for (int turn = 0; turn < 60; turn++)
        {
            if (turn % 5 == 0)
            {
                run.Tap("4");
                yield return ScriptWait.Frames(2);
            }
            run.Tap("Return");
            yield return ScriptWait.Frames(2);
        }
        run.AreEqual(61, board.Turn, "the campaign reaches the score limit");
        string terminal = Notice(run)!;
        run.Assert(terminal.Contains("N New campaign"), "result includes a restart control");
        run.Tap("F5");
        yield return ScriptWait.Frames(2);
        run.Tap("F9");
        yield return ScriptWait.Frames(5);
        var restored = run.Script<RealmBoard>("Board controller");
        run.AreEqual(board.StateHash, restored.StateHash, "terminal slot hash");
        run.Tap("Return");
        yield return ScriptWait.Frames(2);
        run.AreEqual(61, restored.Turn, "the result locks out additional turns");
        run.AreEqual(terminal, Notice(run), "the terminal message survives load and rejected orders");
        run.Tap("N");
        yield return ScriptWait.Frames(5);
        run.AreEqual(1, run.Script<RealmBoard>("Board controller").Turn, "new unsaved campaign");
        run.AreEqual(1, run.Script<RealmBoard>("Board controller").CityCount, "only the rival city is present");
        run.Tap("F9");
        yield return ScriptWait.Frames(5);
        run.AreEqual(terminal, Notice(run), "new campaign kept the previously saved result");
    }

    [GameTest("Scenes/Realm.scene")]
    public IEnumerable<ScriptWait> MoveHighlightAndCommandUseTheSameDetour(GameTestRun run)
    {
        yield return ScriptWait.Frames(2);
        run.Assert(run.Objects.TryGetTile(run.Find("Orders"), new(7, 7), out var blocked), "order overlay exists");
        run.Assert(blocked.Tile != 3, "the settler cannot reach through the occupied scout cell");
        run.Click(451, 218);
        yield return ScriptWait.Frames(2);
        run.Assert(run.Objects.TryGetTile(run.Find("Orders"), new(3, 7), out var detour) && detour.Tile == 3,
            "the scout can reach the destination via the alternate path");
        var board = run.Script<RealmBoard>("Board controller");
        var start = board.DrawnAt(1);
        yield return run.Replay("Tests/ScoutDetour.inputreplay");
        var saved = run.Session.Storage.LoadSlot(1, 1)!;
        run.AreEqual("0,1,3,7;", saved.GetText("orders"), "right click accepted the highlighted detour");
        ulong hash = board.StateHash;
        var gliding = board.DrawnAt(1);
        yield return ScriptWait.Frames(40);
        var end = board.DrawnAt(1);
        run.Assert(gliding != start && gliding != end, "the scout is drawn between cells while it travels");
        run.Assert(System.Numerics.Vector2.Distance(start, end) > System.Numerics.Vector2.Distance(start, gliding), "and keeps going to its cell");
        run.AreEqual(hash, board.StateHash, "the move itself was instant");
    }

    [GameTest("Scenes/Realm.scene")]
    public void CapturingTheFirstCityAwayFromStartEndsTheCampaign(GameTestRun run)
    {
        var rules = NewRules();
        run.Assert(rules.Apply(new(OrderKind.Move, 0, 5, 6)) && rules.Apply(new(OrderKind.Found)), "first city founded at (5,6)");
        run.Assert(rules.Apply(new(OrderKind.Move, 3, 4, 9)), "worker leaves attack range");
        var capital = rules.CityAt(new(5, 6))!;
        // Set up the original capture with two rival units in range.
        rules.Units[4].Cell = new(5, 5);
        rules.Units[5].Cell = new(4, 6);
        for (int i = 0; i < 3 && capital.Side == 1; i++) rules.Apply(new(OrderKind.End));
        run.AreEqual(2, capital.Side, "the rival captured the capital");
        run.AreEqual(2, rules.Winner, "capital capture ended the campaign");
    }

    [GameTest("Scenes/Realm.scene")]
    public void TrainingReusesACasualtyAtTheRosterLimit(GameTestRun run)
    {
        var rules = NewRules();
        run.Assert(rules.Apply(new(OrderKind.Found)), "first city founded");
        while (rules.Units.Count < RealmRules.UnitLimit)
            rules.Units.Add(new(new(6, 7), 1, UnitKind.Scout) { Health = 0 });
        var city = rules.CityAt(RealmRules.Start)!;
        city.Industry = RealmRules.Prices[(int)UnitKind.Spear];
        run.Assert(rules.Apply(new(OrderKind.Train, 1, (int)UnitKind.Spear)), "can queue production with casualties");
        run.Assert(rules.Apply(new(OrderKind.End)), "can resolve production");
        run.AreEqual(RealmRules.UnitLimit, rules.Units.Count, "storage remains bounded");
        run.Assert(rules.Units[0] is { Alive: true, Side: 1, Kind: UnitKind.Spear }, "production reused the departed settler slot");
    }

    private static string? Notice(GameTestRun run) => run.World.Ui.TextOf(run.Find("Notice"));

    private static RealmRules NewRules()
    {
        var terrain = RealmRules.Generate(18427);
        var costs = terrain.Select(value => value switch { 4 or 5 => 0, 1 => 1, _ => 2 }).ToArray();
        return new(18427, terrain, costs);
    }
}
