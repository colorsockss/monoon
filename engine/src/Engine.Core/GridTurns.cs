using System.Drawing;
using VYaml.Annotations;

namespace Engine.Core;

public enum GridCommand { Up, Right, Down, Left, Wait }
public enum GridTurnMode { Recording, Playback, Completed, Diverged }

[YamlObject]
public sealed partial record GridRunRules(
    [property: YamlRequired] string Rules,
    [property: YamlRequired] int GeneratorVersion,
    [property: YamlRequired] ulong Seed,
    [property: YamlRequired] int Columns,
    [property: YamlRequired] int Rows,
    [property: YamlRequired] int StartX,
    [property: YamlRequired] int StartY);

[YamlObject]
public sealed partial record RecordedTurn(
    [property: YamlRequired] GridCommand Command,
    [property: YamlRequired] int X,
    [property: YamlRequired] int Y);

[YamlObject]
public sealed partial record GridReplay(
    [property: YamlRequired] int Version,
    [property: YamlRequired] GridRunRules Setup,
    [property: YamlRequired] RecordedTurn[] Turns)
{
    public const int CurrentVersion = 1;
    public const int MaxTurns = 32768;

    internal void Validate()
    {
        if (Version != CurrentVersion || Setup == null || string.IsNullOrWhiteSpace(Setup.Rules) ||
            Setup.GeneratorVersion < 1 || Setup.Columns < 1 || Setup.Rows < 1 || Turns is not { Length: <= MaxTurns })
            throw new InvalidDataException("Unsupported or incomplete grid replay.");
        var cell = new Point(Setup.StartX, Setup.StartY);
        foreach (var turn in Turns)
        {
            if (turn == null || !Enum.IsDefined(turn.Command))
                throw new InvalidDataException("Invalid turn command.");
            var result = new Point(turn.X, turn.Y);
            if (result != cell && result != GridTurnRun.Destination(cell, turn.Command))
                throw new InvalidDataException("Recorded turn exceeds one cell or uses the wrong direction.");
            cell = result;
        }
    }
}

/// <summary>One input edge commits one command. Playback runs the same move callback and
/// compares its outcome with the recorded checkpoint before accepting the next command.</summary>
public sealed class GridTurnRun
{
    private readonly List<RecordedTurn> recorded = [];
    private readonly RecordedTurn[] playback;
    public GridRunRules Setup { get; }
    public Point Position { get; private set; }
    public int Turn { get; private set; }
    public int Length => Mode == GridTurnMode.Recording ? recorded.Count : playback.Length;
    public GridTurnMode Mode { get; private set; }
    public RecordedTurn? LastExpected { get; private set; }

    private GridTurnRun(GridRunRules setup, RecordedTurn[] playback, GridTurnMode mode)
    {
        Setup = setup;
        this.playback = playback;
        Position = new(setup.StartX, setup.StartY);
        Mode = mode;
    }

    public static GridTurnRun Record(GridRunRules setup)
    {
        new GridReplay(GridReplay.CurrentVersion, setup, []).Validate();
        return new(setup, [], GridTurnMode.Recording);
    }

    public static GridTurnRun Replay(GridReplay replay)
    {
        replay.Validate();
        return new(replay.Setup, (RecordedTurn[])replay.Turns.Clone(),
            replay.Turns.Length == 0 ? GridTurnMode.Completed : GridTurnMode.Playback);
    }

    public GridReplay Snapshot()
    {
        if (Mode != GridTurnMode.Recording) throw new InvalidOperationException("Only a live run can be recorded.");
        return new(GridReplay.CurrentVersion, Setup, recorded.ToArray());
    }

    public bool PlayTurn(GridCommand command, Func<Point,bool> tryEnter)
    {
        if (Mode != GridTurnMode.Recording || Turn == GridReplay.MaxTurns) return false;
        Resolve(command, tryEnter);
        recorded.Add(new(command, Position.X, Position.Y));
        return true;
    }

    public bool ReplayTurn(Func<Point,bool> tryEnter)
    {
        if (Mode != GridTurnMode.Playback) return false;
        var expected = playback[Turn];
        Resolve(expected.Command, tryEnter);
        LastExpected = expected;
        if (Position.X != expected.X || Position.Y != expected.Y) Mode = GridTurnMode.Diverged;
        else if (Turn == playback.Length) Mode = GridTurnMode.Completed;
        return true;
    }

    private void Resolve(GridCommand command, Func<Point,bool> tryEnter)
    {
        var target = Destination(Position, command);
        if (target != Position && tryEnter(target)) Position = target;
        Turn++;
    }

    internal static Point Destination(Point cell, GridCommand command) => command switch
    {
        GridCommand.Up => cell with { Y = checked(cell.Y - 1) },
        GridCommand.Right => cell with { X = checked(cell.X + 1) },
        GridCommand.Down => cell with { Y = checked(cell.Y + 1) },
        GridCommand.Left => cell with { X = checked(cell.X - 1) },
        GridCommand.Wait => cell,
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };
}
