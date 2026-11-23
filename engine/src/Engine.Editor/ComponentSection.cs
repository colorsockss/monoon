using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Engine.Editor;

public sealed class ComponentFoldout
{
    private readonly string? key;
    private bool expanded = true;
    public ComponentFoldout() { }
    // A keyed foldout is remembered per user (editor-state.json), shared by every object with that component type.
    internal ComponentFoldout(string key) => (this.key, expanded) = (key, EditorUserState.Current.Folds.GetValueOrDefault(key, true));
    public bool IsExpanded
    {
        get => expanded;
        set
        {
            if (expanded == value) return;
            expanded = value;
            if (key == null) return;
            EditorUserState.Current.Folds[key] = value;
            EditorUserState.Current.Save();
        }
    }
}

// Built-in and attached components share the same chrome; Content is the property editor.
public sealed class ComponentSection : HeaderedContentControl
{
    public static readonly StyledProperty<ComponentFoldout?> FoldoutProperty =
        AvaloniaProperty.Register<ComponentSection, ComponentFoldout?>(nameof(Foldout));
    public ComponentFoldout? Foldout
    {
        get => GetValue(FoldoutProperty);
        set => SetValue(FoldoutProperty, value);
    }

    // 16px header icon; the column stays reserved without one so header text lines up.
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ComponentSection, Geometry?>(nameof(Icon));
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<bool> HasSettingsProperty =
        AvaloniaProperty.Register<ComponentSection, bool>(nameof(HasSettings), true);
    public bool HasSettings
    {
        get => GetValue(HasSettingsProperty);
        set => SetValue(HasSettingsProperty, value);
    }
}
