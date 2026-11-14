using Avalonia;
using Avalonia.Controls.Primitives;

namespace Engine.Editor;

public sealed class ComponentFoldout
{
    public bool IsExpanded { get; set; } = true;
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

    public static readonly StyledProperty<bool> HasSettingsProperty =
        AvaloniaProperty.Register<ComponentSection, bool>(nameof(HasSettings), true);
    public bool HasSettings
    {
        get => GetValue(HasSettingsProperty);
        set => SetValue(HasSettingsProperty, value);
    }
}
