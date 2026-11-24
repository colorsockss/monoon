using System.Drawing;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record AnimationParameter(string Name, string Kind, float Default = 0);

// Directions: 4 clips (N, E, S, W) or 8 (N, NE, E, SE, S, SW, W, NW), picked by the Vector2 parameter named in Direction
// (world +Y is down, so N is up on screen). Clip repeats the first so lists and older readers still see one clip.
[YamlObject]
public sealed partial record AnimationState(string Name, Guid Clip)
{
    public Guid[]? Directions { get; init; }
    public string? Direction { get; init; }
    public static readonly string[] Compass = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
    public Guid[] Clips() => Directions ?? [Clip];
}

[YamlObject]
public sealed partial record AnimationCondition(string Parameter, string Test, float Value = 0);

// The first condition is inline (older files); And adds more that must all hold. An empty Parameter means no inline
// condition, which needs ExitTime or And. ExitTime is the fraction of the source clip played since entering it (1 = once).
[YamlObject]
public sealed partial record AnimationTransition(string From, string To, string Parameter, string Test, float Value = 0)
{
    public const string AnyState = "Any State";
    public AnimationCondition[]? And { get; init; }
    public float? ExitTime { get; init; }
    public IEnumerable<AnimationCondition> Conditions() =>
        (Parameter.Length == 0 ? [] : new[] { new AnimationCondition(Parameter, Test, Value) }).Concat(And ?? []);
}

[YamlObject]
public sealed partial record AnimationController(string Entry, AnimationState[] States, AnimationParameter[] Parameters, AnimationTransition[] Transitions)
{
    public const string Extension = ".animator";
    public static string[] Tests(string kind) => kind switch
    {
        "Bool" => ["True", "False"],
        "Float" or "Int" => ["Greater", "Less", "Equal", "AtLeast", "AtMost"],
        "Trigger" => ["Triggered"],
        _ => [] // Vector2 has no tests: it only picks a state's direction clip
    };

    public static AnimationController Read(string file) => ProjectYaml.Read<AnimationController>(file);

    public void Save(string file, bool overwrite)
    {
        Validate(file);
        ProjectYaml.WriteBytes(file, ProjectYaml.Encode(this), overwrite);
    }

    internal static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) &&
        name == name.Trim() && !name.Any(char.IsControl);

    public void Validate(string file)
    {
        void Fail(string message) => throw new InvalidDataException($"{file}: {message}");
        if (Parameters == null || States == null || Transitions == null) throw new InvalidDataException($"{file}: missing controller lists.");
        var states = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in States)
            if (state == null || !ValidName(state.Name) || state.Clip == Guid.Empty || !states.Add(state.Name))
                Fail("Every state needs a unique name and a clip.");
        if (!states.Contains(Entry)) Fail("Select a valid entry state.");
        if (states.Contains(AnimationTransition.AnyState)) Fail($"\"{AnimationTransition.AnyState}\" is reserved for transitions from every state.");
        var parameters = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var p in Parameters)
            if (p == null || !ValidName(p.Name) || !parameters.TryAdd(p.Name,p.Kind) ||
                Tests(p.Kind).Length == 0 && p.Kind != "Vector2" || !float.IsFinite(p.Default) ||
                (p.Kind == "Bool" && p.Default is not (0 or 1)) || (p.Kind is "Trigger" or "Vector2" && p.Default != 0) ||
                (p.Kind == "Int" && p.Default != MathF.Round(p.Default)))
                Fail("Invalid parameter name, type or default.");
        foreach (var state in States)
        {
            if (state.Directions == null && state.Direction == null) continue;
            bool valid = state.Directions is { Length: 4 or 8 } clips && clips[0] == state.Clip &&
                parameters.TryGetValue(state.Direction ?? "", out var kind) && kind == "Vector2";
            foreach (var id in state.Directions ?? []) valid &= id != Guid.Empty;
            if (!valid) Fail($"{state.Name}: a direction set needs 4 or 8 clips (the first is the state clip) and a Vector2 parameter.");
        }
        foreach (var t in Transitions)
        {
            if (t == null || t.Parameter == null || t.Test == null || !(states.Contains(t.From) || t.From == AnimationTransition.AnyState) ||
                !states.Contains(t.To) || t.From == t.To || t.ExitTime is { } exit && !(exit is >= 0 and <= 1000) ||
                t.Parameter.Length == 0 && (t.Test.Length != 0 || t.ExitTime == null && t.And is not { Length: > 0 }))
                Fail("Each transition needs two different states and a condition or an exit time.");
            foreach (var c in t.Conditions())
                if (c == null || !parameters.TryGetValue(c.Parameter ?? "",out var kind) || !Tests(kind).Contains(c.Test) ||
                    !float.IsFinite(c.Value) || kind == "Int" && c.Value != MathF.Round(c.Value))
                    Fail("Each condition needs a parameter and a test matching its type.");
        }
    }
}

public readonly record struct AnimationMarker(string State, Guid Clip, int Frame, string Name);

public interface IGameAnimator
{
    string State { get; }
    int Frame { get; }
    /// <summary>Direction of the last direction-set clip shown: 0 N, 1 NE, 2 E … 7 NW (<see cref="AnimationState.Compass"/>). Starts at 4 (S).</summary>
    int Facing { get; }
    void Play();
    void PlayThenShowSprite();
    void SetBool(string name, bool value);
    void SetFloat(string name, float value);
    void SetInt(string name, int value);
    void SetVector2(string name, Vector2 value);
    void SetTrigger(string name);
}

// One instance per object or preview. Authored state, frame positions and event boundaries
// use the same clock regardless of which renderer is displaying the current frame.
public sealed class AnimationPlayer : IGameAnimator
{
    private sealed class Track(Guid id, SpriteClip clip, SceneAppearance[] images)
    {
        public readonly Guid Id = id;
        public readonly SpriteClip Clip = clip;
        public readonly SceneAppearance[] Images = images;
        public readonly SpriteClipTimeline Axis = new(clip.Frames);
        public readonly ComponentCurves? Curves = clip.Tracks is { Length: > 0 } ? new(clip.Tracks, new SpriteClipTimeline(clip.Frames).Duration) : null;
        public readonly int[] Markers = Enumerable.Range(0,clip.Frames.Length).Where(i => clip.Frames[i].Event != null).ToArray();
    }

    // Transitions compiled per state (list order, Any State rules included) so a tick checks only the current state's
    // conditions, by index, without allocating.
    private readonly struct Test(int parameter, int op, float value)
    {
        public readonly int Parameter = parameter, Op = op;
        public readonly float Value = value;
    }
    private readonly struct Rule(int to, int first, int count, double exitTime)
    {
        public readonly int To = to, First = first, Count = count;
        public readonly double ExitTime = exitTime;
    }
    private readonly AnimationController controller;
    private readonly Dictionary<string,AnimationState> states;
    private readonly Dictionary<Guid,Track> tracks = [];
    private readonly Dictionary<string,int> parameters;
    private readonly string[] kinds;
    private readonly float[] values, valuesY;
    private readonly string[] names;
    // Per state: its clip, or its 4/8 direction clips, and the Vector2 parameter that picks one (-1: none).
    private readonly Track[][] sets;
    private readonly int[] directionOf;
    private readonly Test[] tests;
    private readonly Rule[] rules;
    private readonly int[][] outgoing;
    private int state, slot, facing = 4;
    private Track Current => sets[state][slot];
    private bool started, entering, returnToSprite;
    private long playRevision;
    private double time, elapsed;
    public string State => names[state];
    public int Facing => facing;
    public int Frame => started ? Current.Axis.FrameAt(time) : -1;
    public SceneAppearance? Appearance => started && Current.Images[Frame].Image != null ? Current.Images[Frame] : null;
    public ComponentPose Pose => started ? Current.Curves?.Sample(time) ?? default : default;
    internal IEnumerable<string> Images => tracks.Values.SelectMany(t => t.Images).Select(image => image.Image).OfType<string>().Distinct();

    public AnimationPlayer(AnimationController controller, string root, ProjectAssets assets)
        : this(controller,root,assets,new()) { }

    internal AnimationPlayer(AnimationController controller, string root, ProjectAssets assets, Dictionary<string,Size> dimensions)
    {
        controller.Validate("Animation controller");
        this.controller = controller;
        states = controller.States.ToDictionary(s => s.Name);
        names = controller.States.Select(s => s.Name).ToArray();
        state = Array.IndexOf(names, controller.Entry);
        parameters = new(StringComparer.Ordinal);
        kinds = new string[controller.Parameters.Length];
        values = new float[controller.Parameters.Length];
        valuesY = new float[controller.Parameters.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            var p = controller.Parameters[i];
            parameters.Add(p.Name, i); kinds[i] = p.Kind; values[i] = p.Default;
        }
        sets = new Track[names.Length][];
        directionOf = new int[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var s = controller.States[i];
            var ids = s.Clips();
            sets[i] = new Track[ids.Length];
            directionOf[i] = s.Direction == null ? -1 : parameters[s.Direction];
            for (int d = 0; d < ids.Length; d++)
            {
                if (!tracks.TryGetValue(ids[d], out var track))
                {
                    var path = assets.PathFor(ids[d]);
                    if (path == null || !path.EndsWith(ProjectAssets.Clip,StringComparison.Ordinal)) throw new InvalidDataException($"Missing animation clip {ids[d]}.");
                    var file = Path.Combine(root,path);
                    var clip = SpriteClip.Load(file);
                    tracks.Add(ids[d], track = new(ids[d],clip,clip.ResolveFrames(file,root,assets,dimensions)));
                }
                sets[i][d] = track;
            }
        }
        string[] ops = ["True", "False", "Greater", "Less", "Equal", "AtLeast", "AtMost"];
        rules = new Rule[controller.Transitions.Length];
        tests = new Test[controller.Transitions.Sum(t => t.Conditions().Count())];
        for (int r = 0, n = 0; r < rules.Length; r++)
        {
            var t = controller.Transitions[r];
            int first = n;
            foreach (var c in t.Conditions())
                tests[n++] = new(parameters[c.Parameter], c.Test == "Triggered" ? 0 : Array.IndexOf(ops, c.Test), c.Value);
            rules[r] = new(Array.IndexOf(names, t.To), first, n - first, t.ExitTime ?? -1);
        }
        outgoing = new int[names.Length][];
        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i];
            outgoing[i] = Enumerable.Range(0, rules.Length).Where(r => controller.Transitions[r].From == n ||
                controller.Transitions[r].From == AnimationTransition.AnyState && controller.Transitions[r].To != n).ToArray();
        }
    }

    internal static AnimationController? Configuration(SpriteAnimatorComponent animator, string root, ProjectAssets assets)
    {
        if (animator.Controller is Guid id)
        {
            var path = assets.PathFor(id);
            if (path?.EndsWith(AnimationController.Extension,StringComparison.Ordinal) != true) throw new InvalidDataException($"Missing animator controller {id}.");
            var controller = AnimationController.Read(Path.Combine(root,path)); controller.Validate(path); return controller;
        }
        return animator.Clip is Guid clip ? new("Clip",[new("Clip",clip)],[],[]) : null;
    }

    private void Set(string name, string kind, float value)
    {
        if (!parameters.TryGetValue(name,out var index) || kinds[index] != kind)
            throw new ArgumentException($"Animator has no {kind} parameter named '{name}'.");
        values[index] = value;
    }
    public void SetInt(string name, int value) => Set(name,"Int",value);
    public void SetBool(string name, bool value) => Set(name,"Bool",value ? 1 : 0);
    public void SetFloat(string name, float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Set(name,"Float",value);
    }
    public void SetTrigger(string name) => Set(name,"Trigger",1);
    public void SetVector2(string name, Vector2 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)) throw new ArgumentOutOfRangeException(nameof(value));
        Set(name,"Vector2",value.X);
        valuesY[parameters[name]] = value.Y;
    }

    public void Play()
    {
        playRevision++;
        returnToSprite = false;
        Enter(controller.Entry);
    }

    public void PlayThenShowSprite()
    {
        Play();
        returnToSprite = true;
    }
    private void Enter(string name) => Enter(Array.IndexOf(names, name));
    private void Enter(int next)
    {
        state = next; started = entering = true; time = elapsed = 0;
        slot = facing * sets[next].Length / 8;
    }

    // Keep the current direction while the vector stays within 30 degrees of it (8 directions; 60 for 4), otherwise take the
    // nearest. Compares squared cosines with products and sums only, so every CPU and Native AOT pick the same clip.
    // A zero vector keeps the facing; a 4-direction set shows a diagonal as the axis before it clockwise (NE as N).
    private void Face()
    {
        int count = sets[state].Length, p = directionOf[state];
        slot = facing * count / 8;
        if (p < 0) return;
        float x = values[p], y = valuesY[p], length = x * x + y * y;
        if (length > 0 && Score(slot, count, x, y) < (count == 8 ? .75f : .25f) * length)
        {
            float best = -1;
            for (int i = 0; i < count; i++)
            {
                float score = Score(i, count, x, y);
                if (score > best) { best = score; slot = i; }
            }
        }
        facing = slot * 8 / count;
    }
    private static ReadOnlySpan<sbyte> CompassX => [0, 1, 1, 1, 0, -1, -1, -1];
    private static ReadOnlySpan<sbyte> CompassY => [-1, -1, 0, 1, 1, 1, 0, -1];
    // Squared cosine times the vector's squared length; -1 behind.
    private static float Score(int slot, int count, float x, float y)
    {
        int d = slot * 8 / count, dx = CompassX[d], dy = CompassY[d];
        float dot = x * dx + y * dy;
        return dot > 0 ? dot * dot / (dx * dx + dy * dy) : -1;
    }

    // All conditions hold (AND) and, with an exit time, the clip has played that far since the state was entered.
    private bool Matches(in Rule rule)
    {
        if (rule.ExitTime >= 0 && elapsed < rule.ExitTime * Current.Axis.Duration) return false;
        for (int i = rule.First; i < rule.First + rule.Count; i++)
        {
            var t = tests[i];
            float value = values[t.Parameter];
            bool ok = t.Op switch
            {
                0 => value != 0, 1 => value == 0, 2 => value > t.Value, 3 => value < t.Value,
                4 => value == t.Value, 5 => value >= t.Value, _ => value <= t.Value
            };
            if (!ok) return false;
        }
        return true;
    }

    public void Step(double seconds, Action<AnimationMarker>? notify)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!started) return;
        // A script may restart its animator from an event. Leave that new entry for
        // the next tick instead of overwriting its cursor with the outgoing clip time.
        long revision = playRevision;
        bool Emit(int index)
        {
            if (notify != null && Current.Clip.Frames[index].Event is {} name)
                notify(new(State,Current.Id,index,name));
            return revision == playRevision;
        }
        if (entering)
        {
            entering = false;
            if (!Emit(0)) return;
        }
        foreach (int index in outgoing[state])
        {
            ref readonly var rule = ref rules[index];
            if (!Matches(rule)) continue;
            for (int i = rule.First; i < rule.First + rule.Count; i++)
                if (kinds[tests[i].Parameter] == "Trigger") values[tests[i].Parameter] = 0;
            Enter(rule.To);
            entering = false;
            if (!Emit(0)) return;
            break;
        }
        Face();
        var track = Current;
        double end = time + seconds;
        if (notify != null && track.Markers.Length != 0)
        {
            // Events are in (previous, end], including frame zero at each loop boundary.
            double duration = track.Axis.Duration;
            var loops = track.Clip.Loop ? Math.Floor(end / duration) : 0;
            if (loops * track.Markers.Length > 65536) throw new InvalidOperationException("Animation exceeds 65536 frame events per tick.");
            for (int cycle = 0; cycle <= (int)loops; cycle++)
                foreach (int index in track.Markers)
                {
                    double at = cycle * duration + track.Axis.Start(index);
                    if (at > time && at <= end && !Emit(index)) return;
                }
        }
        time = track.Axis.Position(end,track.Clip.Loop);
        elapsed += seconds;
        if (returnToSprite && !track.Clip.Loop && end >= track.Axis.Duration) started = false;
    }
}
