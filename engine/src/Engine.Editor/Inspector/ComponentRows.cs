using System.ComponentModel;
using Engine.Core;

namespace Engine.Editor;

internal sealed record AttachedComponent(SceneComponent Component, string? Project, SceneObject Owner, ComponentFoldout Foldout, Action Changed,
    Func<ScriptComponent, Avalonia.Controls.Control> ScriptEditor, Func<SceneComponent, Avalonia.Controls.Control> PresentationEditor, bool Editable,
    Func<SceneComponent, Avalonia.Controls.Control>? DebugEditor = null,
    Func<SpriteRendererComponent, Action, SpriteFields>? SpriteEditor = null) : INotifyPropertyChanged
{
    // The yellow line above the section (13.14), updated after every edit.
    private string? warning;
    public string? Warning
    {
        get => warning;
        set
        {
            if (warning == value) return;
            warning = value;
            PropertyChanged?.Invoke(this, new(nameof(Warning)));
            PropertyChanged?.Invoke(this, new(nameof(HasWarning)));
        }
    }
    public bool HasWarning => warning != null;

    // The header's prefab override dot (13.6), updated after every committed edit.
    private bool overridden;
    public bool Overridden
    {
        get => overridden;
        set { if (overridden == value) return; overridden = value; PropertyChanged?.Invoke(this, new(nameof(Overridden))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name => Component is ScriptComponent script ? script.Script : ComponentChoice.Registered[Component.GetType()].Name;
    public Avalonia.Media.Geometry? Icon => EditorTokens.ComponentIcon(Component);
    public bool FieldsEnabled => Editable || Component is ScriptComponent;
    public bool CanToggle => Editable && Component.CanDisable;
    public Action? Toggled { get; init; }
    public bool Enabled
    {
        get => Component.Enabled;
        set { if (value == Component.Enabled || !CanToggle) return; Component.Enabled = value; Toggled?.Invoke(); }
    }
    public object? Fields { get; } = DebugEditor != null ? DebugEditor(Component) : Component switch
    {
        SpriteRendererComponent sprite => SpriteEditor?.Invoke(sprite, Changed) ?? new SpriteFields(sprite, Project, Changed),
        ScriptComponent script => EditorTokens.TrimComboBoxes(ScriptEditor(script)),
        _ => EditorTokens.TrimComboBoxes(PresentationEditor(Component))
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
    public event PropertyChangedEventHandler? PropertyChanged;
    // Code-built rows the XAML template hosts: the tint ColorField (13.9).
    public Avalonia.Controls.Control? ColorEditor { get; init; }
    // The Sprite and Mask object fields (13.7/13.17).
    public Avalonia.Controls.Control? SpriteEditor { get; init; }
    public Avalonia.Controls.Control? MaskEditor { get; init; }
    // Draw mode and 9-slice borders (16.2).
    public Avalonia.Controls.Control? DrawEditor { get; init; }
    public string ColorText => Sprite.Tint ?? (IsSquare ? Sprite.SquareColor : "#FFFFFF");
    public bool FlipX { get => Sprite.FlipX; set { if (value!=Sprite.FlipX) { Sprite.FlipX=value; Changed(); } } }
    public bool FlipY { get => Sprite.FlipY; set { if (value!=Sprite.FlipY) { Sprite.FlipY=value; Changed(); } } }
    public bool PixelSnap { get => Sprite.PixelSnap; set { if (value!=Sprite.PixelSnap) { Sprite.PixelSnap=value; Changed(); } } }
    public bool IsSquare => Sprite.Asset == null;
}
