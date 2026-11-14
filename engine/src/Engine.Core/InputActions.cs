using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record ActionBinding(string Name, string[] Controls);

[YamlObject]
public sealed partial record InputBindings
{
    public ActionBinding[]? Actions { get; init; }
    // Read the former five key lists. New saves contain only the action map.
    public string? Left { get; init; }
    public string? Right { get; init; }
    public string? Up { get; init; }
    public string? Down { get; init; }
    public string? Interact { get; init; }

    public static string[] Keys(string group) => group.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    public InputBindings AsMap() => Actions != null ? this with { Left=null, Right=null, Up=null, Down=null, Interact=null } :
        new() { Actions = [
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
        }
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
}

public sealed class InputActions
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
            if (Presses.Count != 0 || Releases.Count != 0) Snapshot = null;
            Times.Clear();
            Presses.Clear();
            Releases.Clear();
            Peaks.Clear();
        }
    }
    private readonly InputHistory gameHistory = new();
    private readonly InputHistory uiHistory = new();
    private InputDevice device;
    private Vector2 pointer;
    private bool inside;
    public InputActions(InputBindings map)
    {
        map.Validate();
        foreach (var a in map.AsMap().Actions!) bindings.Add(a.Name,[..a.Controls]);
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
        values.Clear();
        gameHistory.Clear();
        uiHistory.Clear();
        gameHistory.Snapshot = uiHistory.Snapshot = null;
        inside = false;
    }

    public GameInput Take() => Take(gameHistory);
    internal GameInput TakeUi() => Take(uiHistory);
    internal void DiscardGameEdges() => gameHistory.Clear();

    private GameInput Take(InputHistory history)
    {
        if (history.Snapshot == null)
        {
            history.Snapshot = new(StringComparer.OrdinalIgnoreCase);
            foreach (var name in bindings.Keys)
                history.Snapshot[name] = new(values.GetValueOrDefault(name), history.Presses.Contains(name), history.Releases.Contains(name), history.Times.GetValueOrDefault(name)?.ToArray());
        }
        float Axis(string name) => Math.Max(values.GetValueOrDefault(name), history.Peaks.GetValueOrDefault(name));
        var move = new Vector2(Axis("MoveRight") - Axis("MoveLeft"), Axis("MoveDown") - Axis("MoveUp"));
        var frame = new GameInput(move.LengthSquared() > 1 ? Vector2.Normalize(move) : move, history.Presses.Contains("Interact"))
        {
            Samples = history.Snapshot, ActiveDevice = device, PointerInside = inside, PointerPosition = pointer
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
}
