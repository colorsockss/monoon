using System.Diagnostics;

namespace Engine.Core;

// Inspector field attributes with Unity's names. The editor reads them from the script source (Roslyn symbols); with
// [Conditional] the compiler leaves every use out of the game assembly, so shipped scripts carry no attribute data.

/// <summary>Inspector tooltip for a field (a <c>///</c> summary comment is used when there is none).</summary>
[Conditional("MONOON_EDITOR"), AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class TooltipAttribute(string tooltip) : Attribute { public string Tooltip { get; } = tooltip; }

/// <summary>Shows an int or float field as a slider between min and max.</summary>
[Conditional("MONOON_EDITOR"), AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class RangeAttribute(float min, float max) : Attribute { public float Min { get; } = min; public float Max { get; } = max; }

/// <summary>The Inspector does not store an int or float below this value.</summary>
[Conditional("MONOON_EDITOR"), AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class MinAttribute(float min) : Attribute { public float Min { get; } = min; }

/// <summary>A bold caption above the field.</summary>
[Conditional("MONOON_EDITOR"), AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class HeaderAttribute(string header) : Attribute { public string Header { get; } = header; }

/// <summary>Empty space above the field, in pixels.</summary>
[Conditional("MONOON_EDITOR"), AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class SpaceAttribute(float height = 8) : Attribute { public float Height { get; } = height; }

/// <summary>A string field edited in a box of several lines.</summary>
[Conditional("MONOON_EDITOR"), AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class MultilineAttribute(int lines = 3) : Attribute { public int Lines { get; } = lines; }

/// <summary>The field is saved and loaded as usual but not shown in the Inspector.</summary>
[Conditional("MONOON_EDITOR"), AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class HideInInspectorAttribute : Attribute;

/// <summary>Edits a <c>string</c> field as a #RRGGBB[AA] colour (a <c>Color</c> field is one already); false hides alpha.</summary>
[Conditional("MONOON_EDITOR"), AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class ColorUsageAttribute(bool showAlpha = true) : Attribute { public bool ShowAlpha { get; } = showAlpha; }

/// <summary>Shows and saves a private or protected field like a public one.</summary>
[Conditional("MONOON_EDITOR"), AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class SerializeFieldAttribute : Attribute;
