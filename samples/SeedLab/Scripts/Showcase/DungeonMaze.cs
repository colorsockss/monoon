using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Numerics;
using Engine.Core;

// Showcase copy of MazeRun on KeyRoom's 32-unit tiles: the same MazeLayout generator and GridTurnRun record / replay.
// N / P change the seed (the dungeon is rebuilt from it), the arrow keys take one turn each, Return walks the shortest
// path to the chest, K saves the run, T replays it turn by turn. The explorer's picture faces its last step.
// Gold dots mark the route ahead: the shortest path when Return solves, the saved turns when T replays; each dot clears
// as the explorer steps on it. The status names a layout code (a hash of the passages), so a revisited seed visibly
// rebuilds the same dungeon. Raises the HUD tag flags "seed", "turns", "replay".
public sealed class DungeonMaze : GameBehaviour
{
    public int InitialSeed = 104729;
    public PrefabReference Pawn;
    public Guid Chest, Readout, Status;
    public SpriteReference FaceN, FaceE, FaceS, FaceW;
    private const int Cell = 32, Columns = 8, Rows = 5, PlaybackTicks = 3, WalkTicks = 6;   // a turn every 0.1 s, replays at double speed
    private const string Rules = "SeedLab/dungeon-v1";
    private Guid pawn, picture;
    private MazeLayout maze = null!;
    private GridTurnRun run = null!;
    private GridReplay? loaded;
    private readonly Queue<GridCommand> pending = new();
    private bool automatic, spawning;
    private int ticksUntilTurn;
    private int[] tiles = [];
    private bool[] route = [];
    private Guid board;
    private readonly HashSet<uint> seen = new();
    private static Vector2 Center(Point p) => new((p.X + .5f) * Cell, (p.Y + .5f) * Cell);

    public override void OnCreate(ScriptLife life) => Start(life.ObjectId, life.Objects, life.Ui, unchecked((uint)InitialSeed), null);

    public override void FixedUpdate(ScriptTick tick)
    {
        var input = tick.Input;
        uint seed = (uint)run.Setup.Seed;
        if (input.Pressed("NextSeed")) { Start(tick.ObjectId, tick.Objects, tick.Ui, unchecked(seed + 1), null); tick.Session.Set("seed"); }
        else if (input.Pressed("PreviousSeed")) { Start(tick.ObjectId, tick.Objects, tick.Ui, unchecked(seed - 1), null); tick.Session.Set("seed"); }
        else if (input.Pressed("SaveReplay")) Save(tick);
        else if (input.Pressed("Replay")) Load(tick);
        else if (run.Mode == GridTurnMode.Playback)
        {
            if (--ticksUntilTurn <= 0) { ticksUntilTurn = PlaybackTicks; Advance(tick); }
        }
        else if (run.Mode == GridTurnMode.Recording)
        {
            if (Direction(input) is { } command) pending.Enqueue(command);
            if (input.Pressed("Solve"))
            {
                pending.Clear();
                var cell = run.Position;
                foreach (var step in PathToExit()) { pending.Enqueue(step); cell = Step(cell, step); route[cell.Y * maze.Width + cell.X] = true; }
                automatic = true;
                Paint(tick.Objects);
            }
            // The runtime prefab joins collision queries at the end of its spawn tick; solved steps walk at a steady pace.
            if (!spawning && (!automatic || --ticksUntilTurn <= 0) && pending.TryDequeue(out var next))
            {
                ticksUntilTurn = WalkTicks;
                Perform(tick, next);
            }
            if (pending.Count == 0) automatic = false;
        }
        spawning = false;
    }

    private static GridCommand? Direction(GameInput input) =>
        input.Pressed("MoveUp") ? GridCommand.Up : input.Pressed("MoveRight") ? GridCommand.Right :
        input.Pressed("MoveDown") ? GridCommand.Down : input.Pressed("MoveLeft") ? GridCommand.Left : null;

    // Breadth-first search over the maze's passages from the explorer to the exit.
    private List<GridCommand> PathToExit()
    {
        var from = new Dictionary<Point, (Point, GridCommand)>();
        var queue = new Queue<Point>();
        queue.Enqueue(run.Position);
        from[run.Position] = (run.Position, GridCommand.Wait);
        (int, int, GridCommand)[] moves = [(0, -1, GridCommand.Up), (1, 0, GridCommand.Right), (0, 1, GridCommand.Down), (-1, 0, GridCommand.Left)];
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (p == maze.Exit) break;
            foreach (var (dx, dy, c) in moves)
            {
                var q = new Point(p.X + dx, p.Y + dy);
                if (maze.IsPassage(q.X, q.Y) && from.TryAdd(q, (p, c))) queue.Enqueue(q);
            }
        }
        var path = new List<GridCommand>();
        for (var p = maze.Exit; from.ContainsKey(p) && p != run.Position; p = from[p].Item1) path.Add(from[p].Item2);
        path.Reverse();
        return path;
    }

    private static Point Step(Point p, GridCommand c) => c switch
    {
        GridCommand.Up => p with { Y = p.Y - 1 }, GridCommand.Right => p with { X = p.X + 1 },
        GridCommand.Down => p with { Y = p.Y + 1 }, GridCommand.Left => p with { X = p.X - 1 }, _ => p,
    };

    // Rewrites the board: walls, three floor stones, and a gold dot on every cell still ahead on the route.
    private void Paint(IGameObjects objects)
    {
        for (int i = 0; i < tiles.Length; i++)
            tiles[i] = !maze.IsPassage(i % maze.Width, i / maze.Width) ? 1 : route[i] ? 5 : 2 + (i % maze.Width * 7 + i / maze.Width * 13) % 3;
        objects.ReplaceTiles(board, maze.Width, maze.Height, tiles);
    }

    private void Reached(IGameObjects objects)
    {
        int i = run.Position.Y * maze.Width + run.Position.X;
        if (route[i]) { route[i] = false; Paint(objects); }
    }

    private void Perform(ScriptTick tick, GridCommand command)
    {
        if (run.Position == maze.Exit) return;
        run.PlayTurn(command, target => Enter(tick, target, command));
        Reached(tick.Objects);
        tick.Session.Set("turns");
        Show(tick.Ui, run.Position == maze.Exit ? "Found the chest!  K saves this run." : "Find the chest.");
    }

    private bool Enter(ScriptTick tick, Point target, GridCommand command)
    {
        Face(tick.Objects, command);
        Vector2 step = Affine2D.TransformNormal(Center(target) - Center(run.Position), tick.WorldMatrix);
        return tick.Objects.TryMoveStep(pawn, step);
    }

    private void Face(IGameObjects objects, GridCommand command)
    {
        var face = command switch { GridCommand.Up => FaceN, GridCommand.Right => FaceE, GridCommand.Left => FaceW, _ => FaceS };
        if (picture != Guid.Empty) objects.SetSprite(picture, face);
    }

    private void Advance(ScriptTick tick)
    {
        var expected = loaded != null && run.Turn < loaded.Turns.Length ? loaded.Turns[run.Turn].Command : GridCommand.Wait;
        run.ReplayTurn(target => Enter(tick, target, expected));
        Reached(tick.Objects);
        Show(tick.Ui, run.Mode == GridTurnMode.Completed ? $"Replay complete. All {run.Turn} turns matched." :
            run.Mode == GridTurnMode.Diverged ? $"Mismatch at turn {run.Turn}." : "Replaying the saved run.");
    }

    private void Save(ScriptTick tick)
    {
        if (run.Mode != GridTurnMode.Recording || run.Turn == 0) { tick.Ui.SetText(Status, "Take a turn before saving."); return; }
        try { tick.Session.Storage.SaveReplay(run.Snapshot()); Show(tick.Ui, $"Saved {run.Turn} turns. T replays them."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { tick.Ui.SetText(Status, "Could not save the replay."); }
    }

    private void Load(ScriptTick tick)
    {
        try
        {
            var replay = tick.Session.Storage.LoadReplay();
            if (replay == null) { tick.Ui.SetText(Status, "No saved run. K saves your turns."); return; }
            if (replay.Setup.Rules != Rules || replay.Setup.Seed > uint.MaxValue) { tick.Ui.SetText(Status, "This replay uses other rules."); return; }
            Start(tick.ObjectId, tick.Objects, tick.Ui, (uint)replay.Setup.Seed, replay);
            loaded = replay;
            foreach (var turn in replay.Turns) route[turn.Y * maze.Width + turn.X] = true;
            Paint(tick.Objects);
            ticksUntilTurn = PlaybackTicks;
            tick.Session.Set("replay");
            Show(tick.Ui, "Replaying the saved run.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        { tick.Ui.SetText(Status, "Could not read the saved run."); }
    }

    private void Show(IGameUi ui, string status)
    {
        string turns = run.Mode == GridTurnMode.Recording ? run.Turn.ToString() : $"{run.Turn} / {run.Length}";
        ui.SetText(Status, $"Turn {turns}     {status}");
    }

    private void Start(Guid layer, IGameObjects objects, IGameUi ui, uint seed, GridReplay? replay)
    {
        maze = MazeLayout.Generate(Columns, Rows, seed);
        board = layer;
        if (tiles.Length != maze.Width * maze.Height) (tiles, route) = (new int[maze.Width * maze.Height], new bool[maze.Width * maze.Height]);
        Array.Clear(route);
        Paint(objects);
        if (!objects.TryGetWorldMatrix(layer, out var world)) throw new InvalidOperationException("DungeonMaze needs a tilemap.");
        if (pawn != Guid.Empty) objects.Destroy(pawn);
        pawn = objects.Instantiate(Pawn, Affine2D.Transform(Center(maze.Entrance), world) - new Vector2(10, 10));
        picture = objects.FindByGroup("Explorer picture") is { Count: > 0 } found ? found[^1] : Guid.Empty;
        objects.SetLocalTransform(Chest, new LocalTransform2D(Affine2D.Transform(Center(maze.Exit), world) - new Vector2(16, 16), 0, Vector2.One));
        run = replay == null
            ? GridTurnRun.Record(new GridRunRules(Rules, MazeLayout.Version, seed, Columns, Rows, maze.Entrance.X, maze.Entrance.Y))
            : GridTurnRun.Replay(replay);
        pending.Clear();
        (spawning, automatic) = (true, false);
        ui.SetText(Readout, seed.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " "));
        // FNV-1a over the passage cells: equal codes mean equal dungeons.
        uint hash = 2166136261;
        for (int i = 0; i < tiles.Length; i++) hash = (hash ^ (maze.IsPassage(i % maze.Width, i / maze.Width) ? 1u : 0u)) * 16777619;
        string layout = $"layout #{(hash ^ hash >> 16) & 0xFFFF:X4}";
        Show(ui, replay != null ? "Replaying the saved run." : seen.Add(seed) ? $"New dungeon, {layout}" : $"Seen before: same {layout}");
    }
}
