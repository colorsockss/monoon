using System.ComponentModel;
using System.Reflection;
using VYaml.Annotations;
using Engine.Core;

namespace Engine.Editor;

internal sealed record ComponentChoice(Type Type, string Name)
{
    // Offer exactly the component types the scene format can save and reopen.
    public static IReadOnlyDictionary<Type, ComponentChoice> Registered { get; } = typeof(SceneComponent)
        .GetCustomAttributes<YamlObjectUnionAttribute>()
        .Select(attribute => attribute.SubType)
        .Where(type => type != typeof(ScriptComponent) && type != typeof(HudComponent))
        .ToDictionary(type => type, type => new ComponentChoice(type,
            type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name));

    public SceneComponent Create() => (SceneComponent)Activator.CreateInstance(Type)!;
}

internal sealed record AttachedComponent(SceneComponent Component, string? Project, SceneObject Owner, ComponentFoldout Foldout, Action Changed,
    Func<ScriptComponent, Avalonia.Controls.Control> ScriptEditor, Func<SceneComponent, Avalonia.Controls.Control> PresentationEditor, bool Editable)
{
    public string Name => Component is ScriptComponent script ? script.Script : ComponentChoice.Registered[Component.GetType()].Name;
    public bool FieldsEnabled => Editable || Component is ScriptComponent;
    public object? Fields { get; } = Component switch
    {
        SpriteRendererComponent sprite => new SpriteFields(sprite, Project, Changed),
        ScriptComponent script => ScriptEditor(script),
        _ => PresentationEditor(Component)
    };
}

internal sealed record SpriteFields(SpriteRendererComponent Sprite, string? Project, Action Changed) : INotifyPropertyChanged
{
    public decimal Layer { get => Sprite.SortingLayer; set { if ((int)value != Sprite.SortingLayer) { Sprite.SortingLayer = (int)value; Changed(); } } }
    public decimal Order { get => Sprite.Order; set { if ((int)value != Sprite.Order) { Sprite.Order = (int)value; Changed(); } } }
    public bool SortByY
    {
        get => Sprite.SortByY;
        set
        {
            if (value == Sprite.SortByY) return;
            Sprite.SortByY = value;
            PropertyChanged?.Invoke(this, new(nameof(SortByY)));
            Changed();
        }
    }
    public decimal FootOffset
    {
        get => decimal.CreateSaturating(Sprite.FootOffset);
        set { if ((float)value != Sprite.FootOffset) { Sprite.FootOffset = (float)value; Changed(); } }
    }
    public string AssetLabel => Sprite.Asset is { } path
        ? Project != null && File.Exists(Path.Combine(Project, path)) ? path : $"Missing: {path}"
        : "Square";
    public event PropertyChangedEventHandler? PropertyChanged;
    public void RefreshColor() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorBrush)));
    public string ColorText => Sprite.Tint ?? (IsSquare ? Sprite.SquareColor : "#FFFFFF");
    public string MaskLabel { get; } = Sprite.Mask is Guid id && Project != null ? ProjectAssets.ReadManifest(Project).PathFor(id) ?? $"Missing: {id}" : "None";
    public bool FlipX { get => Sprite.FlipX; set { if (value!=Sprite.FlipX) { Sprite.FlipX=value; Changed(); } } }
    public bool FlipY { get => Sprite.FlipY; set { if (value!=Sprite.FlipY) { Sprite.FlipY=value; Changed(); } } }
    public bool PixelSnap { get => Sprite.PixelSnap; set { if (value!=Sprite.PixelSnap) { Sprite.PixelSnap=value; Changed(); } } }
    public bool IsSquare => Sprite.Asset == null;
    public Avalonia.Media.IBrush ColorBrush => Avalonia.Media.Brush.Parse(ColorText);
}
