using System.Diagnostics;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

public enum ReplayInputKind { Key, Pointer, Wheel, Pad, Compose, Commit, Reset }

[YamlObject]
public readonly partial record struct ReplayInput(
    [property: YamlRequired] ReplayInputKind Kind, string? Control = null, bool? Down = null,
    bool? Reverse = null, float? X = null, float? Y = null, float? Value = null, string? Text = null)
{
    internal void Validate()
    {
        bool position = X is float x && float.IsFinite(x) && Y is float y && float.IsFinite(y);
        bool noPosition = X == null && Y == null;
        bool plain = Down == null && Reverse == null && Value == null;
        bool valid = Kind switch
        {
            ReplayInputKind.Key => !string.IsNullOrEmpty(Control) && Down.HasValue && noPosition && Value == null && Text == null,
            ReplayInputKind.Pointer => position && Reverse == null && Value == null && Text == null &&
                (Control == null && Down == null || Control is "Mouse.Left" or "Mouse.Middle" or "Mouse.Right" && Down.HasValue),
            ReplayInputKind.Wheel => position && Control == null && Down == null && Reverse == null && Text == null &&
                Value is float delta && float.IsFinite(delta),
            ReplayInputKind.Pad => InputControls.Supported(Control) && InputControls.DeviceOf(Control!) == InputDevice.Gamepad &&
                Value is >= 0 and <= 1 && Down == null && Reverse == null && noPosition && Text == null,
            ReplayInputKind.Compose or ReplayInputKind.Commit => Text != null && Control == null && noPosition && plain,
            ReplayInputKind.Reset => Text == null && Control == null && noPosition && plain,
            _ => false
        };
        if (!valid) throw new InvalidDataException($"Invalid {Kind} input in recording.");
    }

    public void Dispatch(GameInputRouter router, GameLoop loop, long timestamp)
    {
        switch (Kind)
        {
            case ReplayInputKind.Key: router.Key(Control!, Down!.Value, Reverse == true, timestamp); break;
            case ReplayInputKind.Pointer: router.Pointer(new Vector2(X!.Value, Y!.Value), Control, Down == true, timestamp); break;
            case ReplayInputKind.Wheel: router.Wheel(new Vector2(X!.Value, Y!.Value), Value!.Value, timestamp); break;
            case ReplayInputKind.Pad: router.Pad(Control!, Value!.Value, timestamp); break;
            case ReplayInputKind.Compose: router.Compose(Text!); break;
            case ReplayInputKind.Commit: router.CommitText(Text!); break;
            case ReplayInputKind.Reset: router.Reset(); loop.ResetElapsed(); break;
            default: throw new InvalidDataException($"Unsupported input kind {Kind}.");
        }
    }
}

[YamlObject]
public sealed partial record RecordedInput([property: YamlRequired] int Frame,
    [property: YamlRequired] long TimeTicks, [property: YamlRequired] ReplayInput Input);

[YamlObject]
public sealed partial record InputReplay([property: YamlRequired] int Version, [property: YamlRequired] Guid Project,
    [property: YamlRequired] Guid StartScene, [property: YamlRequired] long[] FrameTicks,
    [property: YamlRequired] RecordedInput[] Events)
{
    public const int CurrentVersion = 1;
    public static InputReplay Load(string path) => ProjectYaml.Read<InputReplay>(path).Validate();

    internal InputReplay Validate()
    {
        if (Version != CurrentVersion || Project == Guid.Empty || StartScene == Guid.Empty ||
            FrameTicks == null || Events == null || FrameTicks.Any(t => t < 0))
            throw new InvalidDataException("Unsupported or incomplete input recording.");
        int previous = -1;
        foreach (var e in Events)
        {
            if (e == null || e.Frame < 0 || e.Frame < previous || e.Frame >= FrameTicks.Length)
                throw new InvalidDataException("Input event refers to a missing or out-of-order frame.");
            e.Input.Validate();
            previous = e.Frame;
        }
        return this;
    }

    public void Play(GameSession game, Guid project)
    {
        Validate();
        if (Project != project || StartScene != game.SceneId)
            throw new InvalidDataException("Recording belongs to a different project or start scene.");
        var input = new GameInputRouter(game.Actions, () => game.InputUi, game.LogicalSize);
        var loop = new GameLoop(game, game.Actions);
        long origin = Stopwatch.GetTimestamp();
        int next = 0;
        for (int frame = 0; frame < FrameTicks.Length; frame++)
        {
            while (next < Events.Length && Events[next].Frame == frame)
            {
                var e = Events[next++];
                long timestamp = checked(origin + (long)((Int128)e.TimeTicks * Stopwatch.Frequency / TimeSpan.TicksPerSecond));
                e.Input.Dispatch(input, loop, timestamp);
            }
            loop.Advance(TimeSpan.FromTicks(FrameTicks[frame]).TotalSeconds);
        }
    }
}

public sealed class InputRecorder(Guid project, Guid startScene)
{
    private readonly long origin = Stopwatch.GetTimestamp();
    private readonly List<long> frames = [];
    private readonly List<RecordedInput> events = [];
    private int committedEvents;

    public void Observe(ReplayInput input, long timestamp) =>
        events.Add(new(frames.Count, Stopwatch.GetElapsedTime(origin, timestamp).Ticks, input));

    public void EndFrame(TimeSpan elapsed)
    {
        frames.Add(elapsed.Ticks);
        committedEvents = events.Count;
    }

    public InputReplay Save(string path)
    {
        // Closing the window may interrupt event polling before the next update.
        var replay = new InputReplay(InputReplay.CurrentVersion, project, startScene, frames.ToArray(),
            events.GetRange(0, committedEvents).ToArray()).Validate();
        ProjectYaml.WriteBytes(path, ProjectYaml.Encode(replay), overwrite: false);
        return replay;
    }
}
