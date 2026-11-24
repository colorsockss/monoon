using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record ActionBinding(string Name, string[] Controls)
{
    // Roadmap 16.12. When GameInput.Triggered is true: null on the press; Hold once the action has been held Ticks game
    // ticks; Tap on a release within Ticks of the press; DoubleTap on a second press within Ticks of the first.
    public string? Interaction { get; init; }
    public int? Ticks { get; init; }
    // Game ticks a trigger stays available to Triggered/Consume (an input buffer); null or 0: only its own tick.
    public int? Buffer { get; init; }
    public const string Hold = "Hold", Tap = "Tap", DoubleTap = "DoubleTap";
    internal int Window => Ticks ?? Interaction switch { Hold => 30, Tap => 12, _ => 18 };
}

// A 2D vector action: four actions combined into one vector, clamped to length 1 (GameInput.Vector).
[YamlObject]
public sealed partial record ActionVector(string Name, string Left, string Right, string Up, string Down);

[YamlObject]
public sealed partial record InputBindings
{
    public ActionBinding[]? Actions { get; init; }
    // The first vector is GameInput.Move. Without any, Move combines MoveLeft/MoveRight/MoveUp/MoveDown as before.
    public ActionVector[]? Vectors { get; init; }
    internal static readonly ActionVector[] DefaultVectors = [new("Move", "MoveLeft", "MoveRight", "MoveUp", "MoveDown")];
    // Read the former five key lists. New saves contain only the action map.
    public string? Left { get; init; }
    public string? Right { get; init; }
    public string? Up { get; init; }
    public string? Down { get; init; }
    public string? Interact { get; init; }

    public static string[] Keys(string group) => group.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    public InputBindings AsMap() => Actions != null ? this with { Left=null, Right=null, Up=null, Down=null, Interact=null } :
        new() { Vectors = Vectors, Actions = [
            Direction("Left",Left,"Left,A"), Direction("Right",Right,"Right,D"),
            Direction("Up",Up,"Up,W"), Direction("Down",Down,"Down,S"),
            new("Interact",[.. Keys(Interact ?? "E,Space"), "Pad.South"]) ] };
    private static ActionBinding Direction(string name,string? keys,string defaults) =>
        new("Move"+name,[.. Keys(keys ?? defaults),"Pad.Dpad"+name,"Pad.LeftStick"+name]);

    public void Validate()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in AsMap().Actions!)
        {
            if (a?.Name == null || a.Name.Length is < 1 or > 64 || !char.IsAsciiLetter(a.Name[0]) ||
                !a.Name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') || !names.Add(a.Name))
                throw new InvalidDataException($"Invalid or duplicate action '{a?.Name}'. Use a unique name starting with a letter.");
            ValidateControls(a.Name,a.Controls);
            if (a.Interaction is not (null or ActionBinding.Hold or ActionBinding.Tap or ActionBinding.DoubleTap) ||
                a.Ticks is < 1 or > 3600 || a.Buffer is < 0 or > 3600)
                throw new InvalidDataException($"{a.Name}: interaction must be Hold, Tap or DoubleTap, ticks 1–3600 and buffer 0–3600.");
        }
        bool Known(string? action) => action != null && names.Contains(action);
        var vectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in Vectors ?? [])
            if (v?.Name is not { Length: >= 1 and <= 64 } name || !char.IsAsciiLetter(name[0]) || !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ||
                !vectors.Add(name) || !Known(v.Left) || !Known(v.Right) || !Known(v.Up) || !Known(v.Down))
                throw new InvalidDataException($"Invalid or duplicate vector '{v?.Name}'. Name four existing actions for left, right, up and down.");
    }
    internal static void ValidateControls(string name,string[]? controls)
    {
        if (controls == null) throw new InvalidDataException($"{name}: expected a list of controls.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in controls)
            if (!InputControls.Supported(c) || !seen.Add(c))
                throw new InvalidDataException($"{name}: unknown or duplicate control '{c}'. Use + Bind to capture a control.");
    }
}

public enum InputDevice { Keyboard, Pointer, Gamepad }

public static class InputControls
{
    private static readonly HashSet<string> names = new(StringComparer.OrdinalIgnoreCase) {
        "Left","Right","Up","Down","Space","Return","Tab","Backspace","Delete","Home","End","PageUp","PageDown","Insert",
        "LeftShift","RightShift","LeftCtrl","RightCtrl","LeftAlt","RightAlt","Minus","Equal","Comma","Period","Slash",
        "Mouse.Left","Mouse.Right","Mouse.Middle","Mouse.WheelUp","Mouse.WheelDown",
        "Pad.South","Pad.East","Pad.West","Pad.North","Pad.Start","Pad.Back","Pad.LeftShoulder","Pad.RightShoulder",
        "Pad.LeftStick","Pad.RightStick","Pad.DpadUp","Pad.DpadDown","Pad.DpadLeft","Pad.DpadRight",
        "Pad.LeftStickUp","Pad.LeftStickDown","Pad.LeftStickLeft","Pad.LeftStickRight",
        "Pad.RightStickUp","Pad.RightStickDown","Pad.RightStickLeft","Pad.RightStickRight","Pad.LeftTrigger","Pad.RightTrigger" };
    public static bool Supported(string? c) => c != null && (names.Contains(c) || c.Length == 1 && char.IsAsciiLetterOrDigit(c[0]) ||
        c.StartsWith('F') && int.TryParse(c.AsSpan(1),out int n) && n is >=1 and <=12 && c == $"F{n}");
    public static string KeyName(string key) => key switch {
        "Enter" => "Return", "Equals" => "Equal", "Lshift" or "ShiftLeft" => "LeftShift", "Rshift" or "ShiftRight" => "RightShift",
        "Lctrl" or "ControlLeft" => "LeftCtrl", "Rctrl" or "ControlRight" => "RightCtrl",
        "Lalt" or "AltLeft" => "LeftAlt", "Ralt" or "AltRight" => "RightAlt",
        _ when key.StartsWith("Arrow") => key[5..],
        _ when key.StartsWith("Digit", StringComparison.Ordinal) && key.Length == 6 && char.IsAsciiDigit(key[5]) => key[5..],
        _ when key.StartsWith("Num", StringComparison.Ordinal) && key.Length == 4 && char.IsAsciiDigit(key[3]) => key[3..],
        _ when key.Length==2 && (key[0] is 'D' or 'N') && char.IsAsciiDigit(key[1]) => key[1..],
        _ => key };
    public static InputDevice DeviceOf(string c) => c.StartsWith("Pad.",StringComparison.OrdinalIgnoreCase) ? InputDevice.Gamepad :
        c.StartsWith("Mouse.",StringComparison.OrdinalIgnoreCase) ? InputDevice.Pointer : InputDevice.Keyboard;
}

public readonly record struct GameInput(Vector2 Move, bool InteractPressed)
{
    internal readonly record struct Sample(float Value,bool Pressed,bool Released,long[]? PressTimes);
    public ReadOnlySpan<long> PressTimestamps(string action) => Samples?.GetValueOrDefault(action).PressTimes;
    internal IReadOnlyDictionary<string,Sample>? Samples { get; init; }
    public InputDevice ActiveDevice { get; init; }
    public Vector2 PointerPosition { get; init; }
    public bool PointerInside { get; init; }
    public float Value(string action) => Samples?.GetValueOrDefault(action).Value ?? 0;
    public bool Held(string action) => Value(action) > 0;
    public bool Pressed(string action) => Samples?.GetValueOrDefault(action).Pressed ??
        (InteractPressed && string.Equals(action,"Interact",StringComparison.OrdinalIgnoreCase));
    public bool Released(string action) => Samples?.GetValueOrDefault(action).Released ?? false;

    // Roadmap 16.12. Owner/Tick are set by InputActions.Take; Tick is -1 for UI input, which has no interactions or buffers.
    internal InputActions? Owner { get; init; }
    internal long Tick { get; init; }
    internal Vector2[]? Vectors { get; init; }
    /// <summary>The action's interaction fired on this game tick, or within its buffer and not consumed yet. Same as Pressed without a rule.</summary>
    public bool Triggered(string action) => Tick >= 0 && Owner?.Triggered(action, Tick, false) is bool b ? b : Pressed(action);
    /// <summary>Like Triggered, and uses the trigger up so later ticks (and later scripts this tick) no longer see it.</summary>
    public bool Consume(string action) => Tick >= 0 && Owner?.Triggered(action, Tick, true) is bool b ? b : Pressed(action);
    /// <summary>A 2D vector action from GameSettings (length at most 1). Move is the first vector.</summary>
    public Vector2 Vector(string name) => Owner?.VectorIndex(name) is int i and >= 0 && Vectors != null ? Vectors[i] : default;
}

public sealed partial class InputActions
{
    private readonly Dictionary<string,string[]> bindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,float> controls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,float> values = new(StringComparer.OrdinalIgnoreCase);
    private sealed class InputHistory
    {
        public readonly Dictionary<string,float> Peaks = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string,List<long>> Times = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Presses = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Releases = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string,GameInput.Sample>? Snapshot;
        public Vector2[]? Vectors;

        public void Record(string name, float previous, float current, long timestamp)
        {
            if (previous == 0)
            {
                Presses.Add(name);
                if (!Times.TryGetValue(name, out var list)) Times.Add(name, list = []);
                list.Add(timestamp);
            }
            if (current == 0) Releases.Add(name);
            if (current > previous) Peaks[name] = Math.Max(Peaks.GetValueOrDefault(name), current);
            Snapshot = null;
        }
        public void Forget(string name)
        {
            Times.Remove(name);
            Presses.Remove(name);
            Releases.Remove(name);
            Peaks.Remove(name);
            Snapshot = null;
        }
        public void Clear()
        {
            // Peaks too: a vector built from a peak must not outlive the tick it belongs to.
            if (Presses.Count != 0 || Releases.Count != 0 || Peaks.Count != 0) Snapshot = null;
            Times.Clear();
            Presses.Clear();
            Releases.Clear();
            Peaks.Clear();
        }
    }
    private readonly HashSet<string> suppressed = new(StringComparer.OrdinalIgnoreCase);
    internal bool Suppressed(string control) => suppressed.Contains(control);
    internal void ChangeInputMode()
    {
        suppressed.UnionWith(controls.Keys);
        controls.Clear();
        ClearSamples();
    }

    private readonly InputHistory gameHistory = new();
    private readonly InputHistory uiHistory = new();
    private InputDevice device;
    private Vector2 pointer;
    private bool inside;
    // Roadmap 16.12: actions with an interaction or a buffer, judged once per game tick in Take.
    private sealed class Rule(ActionBinding action)
    {
        public readonly string Name = action.Name;
        public readonly string? Interaction = action.Interaction;
        public readonly int Window = action.Window, Buffer = action.Buffer ?? 0;
        public long Down = -1, FirstPress = -1, Fired = -1;
        public bool Consumed;
        public void Reset() { Down = FirstPress = Fired = -1; Consumed = false; }
    }
    private readonly Rule[] rules;
    private readonly ActionVector[] vectors;
    private long tick;
    public InputActions(InputBindings map)
    {
        map.Validate();
        var actions = map.AsMap().Actions!;
        foreach (var a in actions) { bindings.Add(a.Name,[..a.Controls]); defaults.Add(a.Name,[..a.Controls]); }
        var judged = new List<Rule>();
        foreach (var a in actions) if (a.Interaction != null || a.Buffer > 0) judged.Add(new Rule(a));
        rules = judged.ToArray();
        vectors = map.Vectors is { Length: > 0 } declared ? declared : InputBindings.DefaultVectors;
    }
    internal int VectorIndex(string name)
    {
        for (int i = 0; i < vectors.Length; i++) if (string.Equals(vectors[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
    // null: no rule, so Triggered means Pressed.
    internal bool? Triggered(string action, long at, bool consume)
    {
        foreach (var r in rules)
        {
            if (!string.Equals(r.Name, action, StringComparison.OrdinalIgnoreCase)) continue;
            // Only the current tick's input answers: a GameInput kept from an earlier tick neither sees nor uses a trigger.
            bool live = at == tick && r.Fired >= 0 && !r.Consumed && tick - r.Fired <= r.Buffer;
            if (live && consume) r.Consumed = true;
            return live;
        }
        return null;
    }
    private void Judge(InputHistory history)
    {
        tick++;
        foreach (var r in rules)
        {
            bool pressed = history.Presses.Contains(r.Name), released = history.Releases.Contains(r.Name), held = values.GetValueOrDefault(r.Name) > 0;
            bool fire = false;
            switch (r.Interaction)
            {
                case null: fire = pressed; break;
                case ActionBinding.Hold:
                    if (pressed) r.Down = tick;
                    fire = held && r.Down >= 0 && tick - r.Down + 1 == r.Window;
                    break;
                case ActionBinding.Tap:
                    if (pressed) r.Down = tick;
                    fire = released && r.Down >= 0 && tick - r.Down < r.Window;
                    break;
                case ActionBinding.DoubleTap:
                    if (!pressed) break;
                    fire = r.FirstPress >= 0 && tick - r.FirstPress <= r.Window;
                    r.FirstPress = fire ? -1 : tick;
                    break;
            }
            if (!held) r.Down = -1;
            if (fire) { r.Fired = tick; r.Consumed = false; }
        }
    }
    internal void EnableDialogueBindings()
    {
        bindings.TryAdd("DialogueNext", ["Return", "Pad.South"]);
        bindings.TryAdd("SkipCutscene", ["X", "Pad.East"]);
    }
    public int Revision { get; private set; }
    public string? BindingFor(string action,InputDevice device)
    {
        if(bindings.TryGetValue(action,out var sources))
            foreach(var c in sources) if(InputControls.DeviceOf(c)==device) return c;
        return null;
    }
    public void Rebind(string action,params string[] sources)
    {
        if (!bindings.ContainsKey(action)) throw new ArgumentException($"Unknown input action '{action}'.",nameof(action));
        InputBindings.ValidateControls(action,sources);
        bindings[action] = [..sources]; Revision++;
        values[action] = Strength(sources);
        gameHistory.Forget(action);
        uiHistory.Forget(action);
        foreach (var r in rules) if (string.Equals(r.Name, action, StringComparison.OrdinalIgnoreCase)) r.Reset();
    }
    private float Strength(string[] sources)
    {
        float value=0;
        foreach (var source in sources) value=Math.Max(value,controls.GetValueOrDefault(source));
        return value;
    }
    public void NoteDevice(InputDevice next) => device=next;
    public void PointerAt(Vector2 point,bool inGame)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y)) return;
        if (inGame && (point!=pointer || !inside)) device=InputDevice.Pointer;
        pointer=point; inside=inGame;
    }
    public bool Press(string control, long? timestamp = null) => Set(control,1,timestamp);
    public bool Release(string control) => Set(control,0);
    public bool Set(string control,float strength, long? timestamp = null)
    {
        if (!float.IsFinite(strength)) return false;
        strength=Math.Clamp(strength,0,1);
        if (suppressed.Contains(control))
        {
            if (strength == 0) suppressed.Remove(control);
            return true;
        }
        float previous=controls.GetValueOrDefault(control);
        bool changed=previous!=strength;
        if (changed)
        {
            if (strength>0) { controls[control]=strength; if (strength>previous) device=InputControls.DeviceOf(control); }
            else controls.Remove(control);
        }
        bool bound=false;
        foreach (var (name,sources) in bindings)
        {
            bool match=false;
            foreach (var source in sources) if (string.Equals(source,control,StringComparison.OrdinalIgnoreCase)) { match=true; break; }
            if (!match) continue;
            bound=true;
            if (!changed) continue;
            var old=values.GetValueOrDefault(name);
            var current=Strength(sources);
            if (old==current) continue;
            values[name] = current;
            timestamp ??= System.Diagnostics.Stopwatch.GetTimestamp();
            gameHistory.Record(name, old, current, timestamp.Value);
            uiHistory.Record(name, old, current, timestamp.Value);
        }
        return bound;
    }
    public void Reset()
    {
        controls.Clear();
        suppressed.Clear();
        ClearSamples();
        inside = false;
    }
    private void ClearSamples()
    {
        values.Clear();
        gameHistory.Clear();
        uiHistory.Clear();
        gameHistory.Snapshot = uiHistory.Snapshot = null;
        foreach (var r in rules) r.Reset();
    }

    public GameInput Take() => Take(gameHistory);
    internal GameInput TakeUi() => Take(uiHistory);
    internal void DiscardGameEdges() => gameHistory.Clear();

    private GameInput Take(InputHistory history)
    {
        bool game = history == gameHistory;
        if (game) Judge(history);
        if (history.Snapshot == null)
        {
            history.Snapshot = new(StringComparer.OrdinalIgnoreCase);
            foreach (var name in bindings.Keys)
                history.Snapshot[name] = new(values.GetValueOrDefault(name), history.Presses.Contains(name), history.Releases.Contains(name), history.Times.GetValueOrDefault(name)?.ToArray());
            // Vectors change only with the samples, so they are rebuilt (and allocated) only when the input changed.
            history.Vectors = new Vector2[vectors.Length];
            for (int i = 0; i < vectors.Length; i++)
            {
                var v = vectors[i];
                var move = new Vector2(Axis(v.Right) - Axis(v.Left), Axis(v.Down) - Axis(v.Up));
                history.Vectors[i] = move.LengthSquared() > 1 ? Vector2.Normalize(move) : move;
            }
        }
        float Axis(string name) => Math.Max(values.GetValueOrDefault(name), history.Peaks.GetValueOrDefault(name));
        var frame = new GameInput(history.Vectors![0], history.Presses.Contains("Interact"))
        {
            Samples = history.Snapshot, ActiveDevice = device, PointerInside = inside, PointerPosition = pointer,
            Owner = this, Tick = game ? tick : -1, Vectors = history.Vectors
        };
        history.Clear();
        return frame;
    }
}

public interface IGameCameras
{
    bool TryGetView(Guid id,out CameraView view);
    Guid? ActiveCameraId { get; }
    bool TrySetActive(Guid id);
    // Parallax 2D: the world offset the active camera adds to the layer's drawn position at the last game tick.
    bool TryGetParallaxOffset(Guid layer, out System.Numerics.Vector2 offset) { offset = default; return false; }
}
