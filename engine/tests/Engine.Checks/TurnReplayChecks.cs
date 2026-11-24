using System.Drawing;
using System.Numerics;
using Engine.Core;

internal static class TurnReplayChecks
{
    [Check(2300, StopAfter = "turn")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    internal static void Run(string repository, string scratch)
    {
        var setup = new GridRunRules("test/v1", 1, 42, 3, 2, 1, 1);
        var run = GridTurnRun.Record(setup);
        bool Enter(Point target) => target.X > 0;
        run.PlayTurn(GridCommand.Left, Enter);
        run.PlayTurn(GridCommand.Right, Enter);
        run.PlayTurn(GridCommand.Wait, Enter);
        var tape = run.Snapshot();
        Check(tape.Turns.SequenceEqual(new RecordedTurn[] {new(GridCommand.Left,1,1), new(GridCommand.Right,2,1), new(GridCommand.Wait,2,1)}),
            "blocked, open and wait commands each occupy exactly one turn");
        var replay = GridTurnRun.Replay(tape);
        run.PlayTurn(GridCommand.Down, Enter);
        while (replay.ReplayTurn(Enter)) { }
        Check(replay.Mode == GridTurnMode.Completed && replay.Turn == 3 && replay.Position == new Point(2,1), "frozen recording replays every checkpoint");
        replay = GridTurnRun.Replay(tape);
        replay.ReplayTurn(_ => true);
        Check(replay.Mode == GridTurnMode.Diverged && replay.Turn == 1 && replay.Position == new Point(0,1) &&
            !replay.ReplayTurn(Enter), "changed movement rules stop at the first divergent turn");
        bool invalid = false;
        try { GridTurnRun.Replay(tape with { Turns = [new(GridCommand.Right, 9, 1)] }); } catch (InvalidDataException) { invalid = true; }
        Check(invalid, "a replay with an impossible checkpoint is rejected");

        var storage = new PlayerStorage(Path.Combine(scratch, "turn-player"));
        var scene = SceneDocument.Load(Path.Combine(repository,"samples/SeedLab/Scenes/Survey.scene"));
        var commands = new[] {"Left","Space","Right","Right","Down","Left","Up","Space","Right"};
        var positions = new List<Point>();
        var maze = MazeLayout.Generate(12,7,104729);
        static Point Cell(GameSession s)
        {
            var obj = s.Current.Objects.Single(o=>o.Name=="Explorer");
            if (!s.Current.TryGetPosition(obj.Id, out Vector2 p)) throw new Exception("Missing explorer");
            var x=(p.X+7-24-12)/24; var y=(p.Y+7-64-12)/24;
            if (x!=MathF.Truncate(x) || y!=MathF.Truncate(y)) throw new Exception($"Off-center pawn at {p}");
            return new((int)x,(int)y);
        }
        static void Tick(GameSession s) => s.FixedUpdate(s.Actions.Take(),1f/60);
        static void Press(GameSession s,string key)
        { s.Actions.Press(key);Tick(s);s.Actions.Release(key);Tick(s); }
        using (var live = new GameSession(scene,null,null,storage))
        {
            Tick(live);
            var expected=maze.Entrance;
            foreach (var key in commands)
            {
                Point to = key switch {
                    "Right"=>expected with {X=expected.X+1}, "Left"=>expected with {X=expected.X-1},
                    "Down"=>expected with {Y=expected.Y+1}, "Up"=>expected with {Y=expected.Y-1}, _=>expected };
                if (maze.IsPassage(to.X,to.Y)) expected=to;
                live.Actions.Press(key);
                for (int i=0;i<21;i++) Tick(live);
                live.Actions.Release(key);Tick(live);
                if (Cell(live)!=expected) throw new Exception($"Held {key}: {Cell(live)} vs {expected}");
                positions.Add(expected);
            }
            Press(live,"K");
            var saved=storage.LoadReplay();
            Check(saved?.Setup.Seed==104729 && saved.Turns.Select(t=>new Point(t.X,t.Y)).SequenceEqual(positions),
                "one press is one grid turn even across 21 ticks, and YAML contains the observed positions");
        }
        using (var fresh = new GameSession(scene,null,null,storage))
        {
            Tick(fresh);Press(fresh,"N");Press(fresh,"J");
            Check(Cell(fresh)==maze.Entrance,"new session loads the original seed and spawn at turn zero");
            foreach (var expected in positions)
            {
                Press(fresh,"Right"); Press(fresh,"J");
                if (Cell(fresh)!=expected) throw new Exception($"Manual replay: {Cell(fresh)} vs {expected}");
            }
            Check(true,"manual replay uses saved commands and ignores gameplay inputs at each turn");
            Press(fresh,"T");
            for (int i=0;i<positions.Count*24+10;i++) Tick(fresh);
            Check(Cell(fresh)==positions[^1] && storage.LoadReplay()!.Turns.Length==positions.Count,
                "automatic replay finishes at the recorded location without replacing the saved run");
        }
    }

    private static void Check(bool passed,string label)
    {
        if (!passed) throw new Exception("Turn replay: "+label);
        Console.WriteLine("PASS turn replay: "+label);
    }
}
