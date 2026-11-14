using System.Drawing;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record AnimationParameter(string Name, string Kind, float Default = 0);

[YamlObject]
public sealed partial record AnimationState(string Name, Guid Clip);

[YamlObject]
public sealed partial record AnimationTransition(string From, string To, string Parameter, string Test, float Value = 0);

[YamlObject]
public sealed partial record AnimationController(string Entry, AnimationState[] States, AnimationParameter[] Parameters, AnimationTransition[] Transitions)
{
    public const string Extension = ".animator";
    public static string[] Tests(string kind) => kind switch
    {
        "Bool" => ["True", "False"],
        "Float" => ["Greater", "Less", "Equal", "AtLeast", "AtMost"],
        "Trigger" => ["Triggered"],
        _ => []
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
        var parameters = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var p in Parameters)
            if (p == null || !ValidName(p.Name) || !parameters.TryAdd(p.Name,p.Kind) ||
                Tests(p.Kind).Length == 0 || !float.IsFinite(p.Default) ||
                (p.Kind == "Bool" && p.Default is not (0 or 1)) || (p.Kind == "Trigger" && p.Default != 0))
                Fail("Invalid parameter name, type or default.");
        foreach (var t in Transitions)
            if (t == null || !states.Contains(t.From) || !states.Contains(t.To) || t.From == t.To ||
                !parameters.TryGetValue(t.Parameter,out var kind) || !Tests(kind).Contains(t.Test) || !float.IsFinite(t.Value))
                Fail("Each transition needs two different states and a condition matching its parameter type.");
    }
}

public readonly record struct AnimationMarker(string State, Guid Clip, int Frame, string Name);

public interface IGameAnimator
{
    string State { get; }
    int Frame { get; }
    void Play();
    void SetBool(string name, bool value);
    void SetFloat(string name, float value);
    void SetTrigger(string name);
}

// One instance per object or preview. Authored state, frame positions and event boundaries
// use the same clock regardless of which renderer is displaying the current frame.
public sealed class AnimationPlayer : IGameAnimator
{
    private sealed class Track(SpriteClip clip, SceneAppearance[] images)
    {
        public readonly SpriteClip Clip = clip;
        public readonly SceneAppearance[] Images = images;
        public readonly SpriteClipTimeline Axis = new(clip.Frames);
        public readonly int[] Markers = Enumerable.Range(0,clip.Frames.Length).Where(i => clip.Frames[i].Event != null).ToArray();
    }

    private readonly AnimationController controller;
    private readonly Dictionary<string,AnimationState> states;
    private readonly Dictionary<Guid,Track> tracks = [];
    private readonly Dictionary<string,AnimationParameter> parameters;
    private readonly Dictionary<string,float> values;
    private Track Current => tracks[states[State].Clip];
    private bool started, entering;
    private long playRevision;
    private double time;
    public string State { get; private set; }
    public int Frame => started ? Current.Axis.FrameAt(time) : -1;
    public SceneAppearance? Appearance => started ? Current.Images[Frame] : null;
    internal IEnumerable<string> Images => tracks.Values.SelectMany(t => t.Images).Select(image => image.Image!).Distinct();

    public AnimationPlayer(AnimationController controller, string root, ProjectAssets assets)
        : this(controller,root,assets,new()) { }

    internal AnimationPlayer(AnimationController controller, string root, ProjectAssets assets, Dictionary<string,Size> dimensions)
    {
        controller.Validate("Animation controller");
        this.controller = controller;
        State = controller.Entry;
        states = controller.States.ToDictionary(s => s.Name);
        parameters = controller.Parameters.ToDictionary(p => p.Name);
        values = controller.Parameters.ToDictionary(p => p.Name,p => p.Default);
        foreach (var id in controller.States.Select(s => s.Clip).Distinct())
        {
            var path = assets.PathFor(id);
            if (path == null || !path.EndsWith(ProjectAssets.Clip,StringComparison.Ordinal)) throw new InvalidDataException($"Missing animation clip {id}.");
            var file = Path.Combine(root,path);
            var clip = SpriteClip.Load(file);
            tracks.Add(id,new(clip,clip.ResolveFrames(file,root,assets,dimensions)));
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
        if (!parameters.TryGetValue(name,out var parameter) || parameter.Kind != kind)
            throw new ArgumentException($"Animator has no {kind} parameter named '{name}'.");
        values[name] = value;
    }
    public void SetBool(string name, bool value) => Set(name,"Bool",value ? 1 : 0);
    public void SetFloat(string name, float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Set(name,"Float",value);
    }
    public void SetTrigger(string name) => Set(name,"Trigger",1);

    public void Play()
    {
        playRevision++;
        Enter(controller.Entry);
    }
    private void Enter(string state)
    {
        State = state; started = entering = true; time = 0;
    }

    private bool Matches(AnimationTransition rule)
    {
        float value = values[rule.Parameter];
        return rule.Test switch
        {
            "True" or "Triggered" => value != 0, "False" => value == 0,
            "Greater" => value > rule.Value, "Less" => value < rule.Value, "Equal" => value == rule.Value,
            "AtLeast" => value >= rule.Value, "AtMost" => value <= rule.Value, _ => false
        };
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
                notify(new(State,states[State].Clip,index,name));
            return revision == playRevision;
        }
        if (entering)
        {
            entering = false;
            if (!Emit(0)) return;
        }
        foreach (var rule in controller.Transitions)
        {
            if (rule.From != State || !Matches(rule)) continue;
            if (rule.Test == "Triggered") values[rule.Parameter] = 0;
            Enter(rule.To);
            entering = false;
            if (!Emit(0)) return;
            break;
        }
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
    }
}
