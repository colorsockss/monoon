using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Numerics;
using Engine.Core;

public sealed class MazeRun : GameBehaviour
{
    public int InitialSeed = 104729;
    public PrefabReference Pawn;
    public Guid Readout, Status, TurnReadout;
    private const int Cell = 24, Columns = 12, Rows = 7;
    private const string Rules = "SeedLab/walk-v1";
    private const int PlaybackTicks = 24;
    private Guid pawn;
    private Point exit;
    private GridTurnRun run;
    private bool automatic;
    private int ticksUntilTurn;
    private readonly System.Collections.Generic.Queue<GridCommand> pending = new();
    private bool spawning;
    private static Vector2 Center(Point p) => new((p.X + .5f) * Cell, (p.Y + .5f) * Cell);

    public override void OnCreate(ScriptLife life) =>
        Start(life.ObjectId, life.Objects, life.Ui, unchecked((uint)InitialSeed), null);

    public void ApplySeed(Guid layer, IGameObjects objects, IGameUi ui, string text)
    {
        if (uint.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out uint value))
        {
            Start(layer, objects, ui, value, null);
            ui.ClearFocus();
        }
        else
            ui.SetText(Status, "Enter a seed from 0 to 4294967295.");
    }

    private static GridCommand? Direction(GameInput input)
    {
        // Stable priority when multiple direction edges land in a single tick.
        if (input.Pressed("MoveUp")) return GridCommand.Up;
        if (input.Pressed("MoveRight")) return GridCommand.Right;
        if (input.Pressed("MoveDown")) return GridCommand.Down;
        if (input.Pressed("MoveLeft")) return GridCommand.Left;
        if (input.Pressed("WaitTurn")) return GridCommand.Wait;
        return null;
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        uint seed = (uint)run.Setup.Seed;
        if (tick.Input.Pressed("NextSeed")) Start(tick.ObjectId, tick.Objects, tick.Ui, unchecked(seed + 1), null);
        else if (tick.Input.Pressed("PreviousSeed")) Start(tick.ObjectId, tick.Objects, tick.Ui, unchecked(seed - 1), null);
        else if (tick.Input.Pressed("Rebuild")) Start(tick.ObjectId, tick.Objects, tick.Ui, seed, null);
        else if (tick.Input.Pressed("SaveReplay")) Save(tick);
        else if (tick.Input.Pressed("Replay")) Load(tick, true);
        else if (tick.Input.Pressed("StepReplay") && run.Mode != GridTurnMode.Playback) Load(tick, false);
        else if (run.Mode == GridTurnMode.Playback)
        {
            if (tick.Input.Pressed("StepReplay"))
            {
                automatic = false;
                Advance(tick);
            }
            else if (automatic && --ticksUntilTurn <= 0)
            {
                ticksUntilTurn = PlaybackTicks;
                Advance(tick);
            }
        }
        else if (run.Mode == GridTurnMode.Recording)
        {
            var input = Direction(tick.Input);
            if (input.HasValue) pending.Enqueue(input.Value);
            // The runtime prefab joins collision queries at the end of its spawn tick.
            if (!spawning && pending.TryDequeue(out var command)) Perform(tick, command);
        }
        spawning = false;
    }

    private void Perform(ScriptTick tick, GridCommand command)
    {
        if (run.Position == exit) return;
        var before = run.Position;
        if (!run.PlayTurn(command, target => Enter(tick, target)))
        {
            tick.Ui.SetText(Status, "Turn limit reached. K saves this run.");
            return;
        }
        string status = run.Position == exit ? "Exit reached!  K saves this run." :
            command == GridCommand.Wait ? "Waited." : run.Position == before ? "Blocked by a wall." : "Find the gold exit.";
        Show(tick.Ui, status);
    }

    private bool Enter(ScriptTick tick, Point target)
    {
        Vector2 step = Vector2.TransformNormal(Center(target) - Center(run.Position), tick.WorldMatrix);
        return tick.Objects.TryMoveStep(pawn, step);
    }

    private void Advance(ScriptTick tick)
    {
        run.ReplayTurn(target => Enter(tick, target));
        string status;
        if (run.Mode == GridTurnMode.Diverged)
            status = $"Mismatch at turn {run.Turn}. Expected ({run.LastExpected.X}, {run.LastExpected.Y}).";
        else if (run.Mode == GridTurnMode.Completed)
            status = run.Turn == 1 ? "Replay complete. One turn matched." : $"Replay complete. All {run.Turn} turns matched.";
        else status = automatic ? "Replaying." : "Replay paused. J advances one turn.";
        Show(tick.Ui, status);
    }

    private void Save(ScriptTick tick)
    {
        if (run.Mode != GridTurnMode.Recording || run.Turn == 0)
        {
            tick.Ui.SetText(Status, "Play a turn before saving.");
            return;
        }
        try
        {
            tick.Session.Storage.SaveReplay(run.Snapshot());
            Show(tick.Ui, run.Turn == 1 ? "Saved one turn. T replays." : $"Saved {run.Turn} turns. T replays.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { tick.Ui.SetText(Status, "Could not save the replay."); }
    }

    private void Load(ScriptTick tick, bool autoPlay)
    {
        try
        {
            var replay = tick.Session.Storage.LoadReplay();
            if (replay == null) { tick.Ui.SetText(Status, "No saved replay. K saves your turns."); return; }
            var setup = replay.Setup;
            if (setup.Rules != Rules || setup.GeneratorVersion != MazeLayout.Version ||
                setup.Columns != Columns || setup.Rows != Rows || setup.Seed > uint.MaxValue ||
                setup.StartX != 1 || setup.StartY != 1)
            { tick.Ui.SetText(Status, "This replay uses different maze rules."); return; }
            Start(tick.ObjectId, tick.Objects, tick.Ui, (uint)setup.Seed, replay);
            automatic = autoPlay;
            ticksUntilTurn = PlaybackTicks;
            Show(tick.Ui, automatic ? "Replaying." : "Replay paused. J advances one turn.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        { tick.Ui.SetText(Status, "Could not read the saved replay."); }
    }

    private void Show(IGameUi ui, string status)
    {
        string number = run.Mode == GridTurnMode.Recording ? run.Turn.ToString() : $"{run.Turn}/{run.Length}";
        ui.SetText(TurnReadout, $"Turn {number}   ({run.Position.X}, {run.Position.Y})");
        ui.SetText(Status, status);
    }

    private void Start(Guid layer, IGameObjects objects, IGameUi ui, uint seed, GridReplay replay)
    {
        var maze = MazeLayout.Generate(Columns, Rows, seed);
        int[] tiles = new int[maze.Width * maze.Height];
        for (int y = 0; y < maze.Height; y++)
        for (int x = 0; x < maze.Width; x++)
            tiles[y * maze.Width + x] = maze.IsPassage(x, y) ? 2 : 1;
        tiles[maze.Entrance.Y * maze.Width + maze.Entrance.X] = 3;
        tiles[maze.Exit.Y * maze.Width + maze.Exit.X] = 4;
        if (!objects.ReplaceTiles(layer, maze.Width, maze.Height, tiles) ||
            !objects.TryGetWorldMatrix(layer, out var world))
            throw new InvalidOperationException("MazeRun needs a non-streamed tilemap.");
        if (pawn != Guid.Empty) objects.Destroy(pawn);
        var spawn = Vector2.Transform(Center(maze.Entrance), world);
        pawn = objects.Instantiate(Pawn, spawn - new Vector2(7));
        exit = maze.Exit;
        run = replay == null
            ? GridTurnRun.Record(new GridRunRules(Rules, MazeLayout.Version, seed, Columns, Rows, maze.Entrance.X, maze.Entrance.Y))
            : GridTurnRun.Replay(replay);
        pending.Clear();
        spawning = true;
        automatic = false;
        ui.SetText(Readout, seed.ToString(CultureInfo.InvariantCulture));
        Show(ui, "Find the gold exit.");
    }
}
