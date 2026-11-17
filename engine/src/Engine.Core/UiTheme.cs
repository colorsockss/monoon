using System.Drawing;

namespace Engine.Core;

internal enum ButtonState { Normal, Hover, Focused, Pressed, Disabled }
internal readonly record struct ButtonPaint(Color Fill, Color Ink);

internal sealed class UiTheme
{
    private readonly Dictionary<string, ButtonPaint[]> styles = new(StringComparer.Ordinal);
    public IEnumerable<string> Names => styles.Keys;
    public static readonly UiTheme Empty = new();

    public ButtonPaint[]? Find(string? name)
    {
        if (styles.Count == 0) return null;
        return styles.TryGetValue(name ?? "button", out var paint) ? paint :
            throw new InvalidDataException($"UI theme has no button style '{name}'.");
    }

    public static UiTheme Load(string project, ProjectAssets assets, Guid? reference, ISet<string>? dependencies = null)
    {
        if (reference is not Guid id) return Empty;
        var path = GameTable.AssetPath(assets, new(id));
        dependencies?.Add(path);
        var table = GameTable.Load(Path.Combine(project, path));
        int Column(string name) => Array.FindIndex(table.Columns, c => c.Name == name && c.Type == "Text");
        int fill = Column("fill"), ink = Column("text");
        if (fill < 0 || ink < 0) throw new InvalidDataException($"{path}: a UI theme needs fill and text columns of type Text.");
        var theme = new UiTheme();
        foreach (var row in table.Rows)
        {
            int dot = row.Key.LastIndexOf('.');
            if (dot <= 0 || !Enum.TryParse<ButtonState>(row.Key[(dot+1)..], true, out var state) ||
                !Enum.IsDefined(state) || !string.Equals(row.Key[(dot+1)..], state.ToString(), StringComparison.OrdinalIgnoreCase) ||
                !SceneObject.TryColor(row.Cells[fill].GetString(), out var background) ||
                !SceneObject.TryColor(row.Cells[ink].GetString(), out var foreground))
                throw new InvalidDataException($"{path}: '{row.Key}' needs a style.state key and two #RRGGBB colors.");
            var name = row.Key[..dot];
            if (!theme.styles.TryGetValue(name, out var colors)) theme.styles[name] = colors = new ButtonPaint[5];
            if (!colors[(int)state].Fill.IsEmpty) throw new InvalidDataException($"{path}: duplicate {row.Key}.");
            colors[(int)state] = new(background, foreground);
        }
        if (!theme.styles.ContainsKey("button")) throw new InvalidDataException($"{path}: missing default 'button' style.");
        foreach (var (name, colors) in theme.styles)
            foreach (var state in Enum.GetValues<ButtonState>())
                if (colors[(int)state].Fill.IsEmpty) throw new InvalidDataException($"{path}: missing {name}.{state}.");
        return theme;
    }
}
