using System.Text.Json;

namespace Engine.Editor;

public enum DockZone { Center, Left, Right, Top, Bottom }

// A tab group (Panels) or a split (Row: left to right, Column: top to bottom).
public sealed class DockNode
{
    public string? Split { get; set; }
    public List<DockNode>? Children { get; set; }
    public List<string>? Panels { get; set; }
    public string? Active { get; set; }
    // Pixels along the parent split's axis.
    public double Size { get; set; }

    public static DockNode Tabs(double size, params string[] panels) => new() { Panels = [.. panels], Active = panels[0], Size = size };
    public static DockNode Row(double size, params DockNode[] children) => new() { Split = "Row", Children = [.. children], Size = size };
    public static DockNode Column(double size, params DockNode[] children) => new() { Split = "Column", Children = [.. children], Size = size };
    public DockNode Clone() => JsonSerializer.Deserialize<DockNode>(JsonSerializer.Serialize(this))!;
    internal IEnumerable<DockNode> Descendants() => (Children ?? []).SelectMany(c => c.Descendants()).Prepend(this);
}

// A panel shown in its own window: position in screen pixels, size in window units.
public sealed class FloatingPanel
{
    public string Panel { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
