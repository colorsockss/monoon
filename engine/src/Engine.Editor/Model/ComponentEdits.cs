using System.Text;
using Engine.Core;

namespace Engine.Editor;

// Component gear-menu edits (roadmap 13.4) without UI, so Engine.Checks runs them through the Undo history. Each returns
// false when nothing changed; the caller records one edit otherwise. A copy is the component's YAML entry, as the scene
// file stores it.
internal static class ComponentEdits
{
    internal static string Copy(SceneComponent component) => Encoding.UTF8.GetString(ProjectYaml.Encode<SceneComponent>(component));

    internal static SceneComponent? Read(string? copied)
    {
        if (copied == null) return null;
        try { return ProjectYaml.Decode<SceneComponent>(Encoding.UTF8.GetBytes(copied)); }
        catch (Exception ex) when (ex is VYaml.YamlException or FormatException or InvalidDataException) { return null; }
    }

    // The same component type (and script) as the target.
    internal static bool CanPasteValues(SceneComponent target, string? copied) => Read(copied) is { } c && c.GetType() == target.GetType() && c.Kind == target.Kind;

    // The copied values replace the target's; it keeps its place in the list and its on/off state.
    internal static bool PasteValues(SceneObject owner, SceneComponent target, string copied)
    {
        if (Read(copied) is not { } values || values.GetType() != target.GetType() || values.Kind != target.Kind) return false;
        values.Enabled = target.Enabled;
        return Replace(owner, target, values);
    }

    // Built-ins get a new component's values; a script loses its Inspector overrides (its game event links stay).
    internal static bool Reset(SceneObject owner, SceneComponent target)
    {
        var fresh = target is ScriptComponent script ? script with { Values = [] } : (SceneComponent)Activator.CreateInstance(target.GetType())!;
        fresh.Enabled = target.Enabled;
        return Replace(owner, target, fresh);
    }

    internal static bool Move(SceneObject owner, SceneComponent component, int delta)
    {
        int from = owner.Components.IndexOf(component), to = from + delta;
        if (from < 0 || to < 0 || to >= owner.Components.Length) return false;
        owner.Components = owner.Components.RemoveAt(from).Insert(to, component);
        return true;
    }

    private static bool Replace(SceneObject owner, SceneComponent old, SceneComponent next)
    {
        int index = owner.Components.IndexOf(old);
        if (index < 0 || Copy(old) == Copy(next)) return false;
        owner.Components = owner.Components.SetItem(index, next);
        return true;
    }
}
