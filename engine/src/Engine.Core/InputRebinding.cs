using System.Text;
using System.Text.Json;

namespace Engine.Core;

/// <summary>Outcome of a player rebind. <see cref="Control"/> is null when the capture was cancelled.</summary>
public sealed record InputRebindResult(string Action, InputDevice Device, string? Control, string? Previous, string[] Swapped)
{
    public bool Cancelled => Control == null;
}

public sealed partial class InputActions
{
    public const string SettingsKey = "inputBindings";
    private readonly Dictionary<string,string[]> defaults = new(StringComparer.OrdinalIgnoreCase);
    private (string Action, InputDevice Device)? capture;
    public string? CaptureAction => capture?.Action;
    public InputDevice? CaptureDevice => capture?.Device;
    public InputRebindResult? LastRebind { get; private set; }

    public string[] ControlsFor(string action) =>
        bindings.TryGetValue(action, out var controls) ? [..controls] : throw new ArgumentException($"Unknown input action '{action}'.", nameof(action));

    /// <summary>The next fresh press of <paramref name="device"/> replaces the action's first control on that device.</summary>
    public void BeginCapture(string action, InputDevice device)
    {
        if (!bindings.ContainsKey(action)) throw new ArgumentException($"Unknown input action '{action}'.", nameof(action));
        capture = (action, device);
    }

    public bool CancelCapture()
    {
        if (capture is not { } c) return false;
        capture = null;
        LastRebind = new(c.Action, c.Device, null, BindingFor(c.Action, c.Device), []);
        return true;
    }

    // The router offers fresh presses here before the UI and the actions see them.
    internal bool TryCapture(string control)
    {
        if (capture is not { } c) return false;
        bool supported = InputControls.Supported(control);
        if (supported && InputControls.DeviceOf(control) == c.Device) { capture = null; Swap(c.Action, c.Device, control); }
        else if (supported || control == "Escape") CancelCapture();
        else return true; // Keys MoNoon cannot bind, such as Caps Lock, keep waiting.
        if (!supported) return true;
        suppressed.Add(control); // Holding or releasing the captured control must not trigger an action.
        device = InputControls.DeviceOf(control); // Prompts switch to the device the player just used.
        return true;
    }

    /// <summary>Binds <paramref name="control"/> in place of the action's first control on its device.
    /// Other actions that used it receive the replaced control, or lose it when there was none.</summary>
    public InputRebindResult Swap(string action, InputDevice device, string control)
    {
        if (!bindings.TryGetValue(action, out var current)) throw new ArgumentException($"Unknown input action '{action}'.", nameof(action));
        if (!InputControls.Supported(control) || InputControls.DeviceOf(control) != device)
            throw new ArgumentException($"'{control}' is not a supported {device} control.", nameof(control));
        var previous = BindingFor(action, device);
        if (string.Equals(previous, control, StringComparison.OrdinalIgnoreCase)) return LastRebind = new(action, device, previous, previous, []);
        var list = new List<string>(current);
        int slot = previous == null ? -1 : IndexOf(list, previous), existing = IndexOf(list, control);
        if (existing >= 0) (list[slot], list[existing]) = (list[existing], list[slot]);
        else if (slot >= 0) list[slot] = control;
        else list.Add(control);
        var swapped = new List<string>();
        foreach (var (name, sources) in bindings.ToArray())
        {
            int k = IndexOf(sources, control);
            if (k < 0 || string.Equals(name, action, StringComparison.OrdinalIgnoreCase)) continue;
            var other = new List<string>(sources);
            if (previous != null && IndexOf(other, previous) < 0) other[k] = previous;
            else other.RemoveAt(k);
            Rebind(name, [..other]);
            swapped.Add(name);
        }
        Rebind(action, [..list]);
        return LastRebind = new(action, device, control, previous, [..swapped]);
    }

    public void RestoreDefaults()
    {
        capture = null;
        LastRebind = null;
        foreach (var (name, controls) in defaults)
            if (!Same(bindings[name], controls)) Rebind(name, controls);
    }

    /// <summary>Writes the actions that differ from the project defaults, or removes the key when none do.</summary>
    public void SaveTo(GameData settings)
    {
        var json = new StringBuilder("{");
        foreach (var (name, controls) in bindings)
        {
            if (!defaults.TryGetValue(name, out var original) || Same(controls, original)) continue;
            // Action and control names are validated ASCII identifiers, so they need no escaping.
            if (json.Length > 1) json.Append(',');
            json.Append('"').Append(name).Append("\":[").AppendJoin(',', controls.Select(c => $"\"{c}\"")).Append(']');
        }
        if (json.Length == 1) { settings.Values.Remove(SettingsKey); return; }
        using var document = JsonDocument.Parse(json.Append('}').ToString());
        settings.Values[SettingsKey] = document.RootElement.Clone();
    }

    /// <summary>Applies saved bindings. Missing, unknown or invalid entries fall back to the project defaults.</summary>
    public void LoadFrom(GameData settings)
    {
        capture = null;
        var saved = new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
        if (settings.Values.TryGetValue(SettingsKey, out var map) && map.ValueKind == JsonValueKind.Object)
            foreach (var entry in map.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Array) continue;
                var controls = entry.Value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : "").ToArray();
                try { InputBindings.ValidateControls(entry.Name, controls); saved[entry.Name] = controls; }
                catch (InvalidDataException) { } // A hand-edited or outdated entry keeps the default binding.
            }
        foreach (var (name, original) in defaults)
        {
            var target = saved.GetValueOrDefault(name) ?? original;
            if (!Same(bindings[name], target)) Rebind(name, target);
        }
    }

    private static bool Same(string[] a, string[] b) => a.AsSpan().SequenceEqual(b, StringComparer.OrdinalIgnoreCase);
    private static int IndexOf(IReadOnlyList<string> list, string control)
    {
        for (int i = 0; i < list.Count; i++) if (string.Equals(list[i], control, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}

public sealed partial class SessionState
{
    private InputActions Input => input ?? throw new InvalidOperationException("Start a game before rebinding.");
    public string[] ControlsFor(string action) => Input.ControlsFor(action);
    public void BeginRebind(string action, InputDevice device) => Input.BeginCapture(action, device);
    public bool CancelRebind() => input?.CancelCapture() ?? false;
    public string? RebindingAction => input?.CaptureAction;
    public InputDevice? RebindingDevice => input?.CaptureDevice;
    public InputRebindResult? LastRebind => input?.LastRebind;
    public InputRebindResult SwapBinding(string action, InputDevice device, string control) => Input.Swap(action, device, control);
    public void RestoreDefaultBindings() => Input.RestoreDefaults();
    public void LoadInputBindings(GameData settings) => Input.LoadFrom(settings);
    public void SaveInputBindings(GameData settings) => Input.SaveTo(settings);
}
