using Microsoft.CodeAnalysis;

namespace Engine.Core;

// Editor-only presentation of one script's Inspector fields (roadmap 13.3), read from Roslyn source symbols.
// Min and Max: [Range] gives both, [Min] only Min. Space is in pixels. Hidden: [HideInInspector] (still saved and loaded).
public sealed record ScriptFieldUi(string? Tooltip, string? Header, float? Space, float? Min, float? Max, int? Lines, bool Hidden)
{
    // 13.13: attribute class names without "Attribute" (also [Conditional] ones from game scripts) and the C# type, for field drawers.
    public string[] Attributes { get; init; } = [];
    public string TypeName { get; init; } = "";
}

// A field the Inspector does not show. Expected: public or [SerializeField], so the author probably meant it to show.
public sealed record ScriptFieldNote(string Name, string Type, string Reason, string? Initializer, bool Expected);

public sealed class ScriptInspectorInfo
{
    public Dictionary<string, ScriptFieldUi> Fields { get; } = new(StringComparer.Ordinal);
    public List<ScriptFieldNote> Skipped { get; } = [];
    internal Dictionary<string, IFieldSymbol> Declaring { get; } = new(StringComparer.Ordinal);
    // 13.8 field kinds: (key expression, member expression) -> the generated factory statement (ScriptValues readers).
    internal Dictionary<string, Func<string, string, string>> Reads { get; } = new(StringComparer.Ordinal);
}
