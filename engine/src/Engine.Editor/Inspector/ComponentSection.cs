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

    // The header's on/off check (SceneComponent.Enabled) for components that can be turned off.
    public static readonly StyledProperty<bool> CanToggleProperty =
        AvaloniaProperty.Register<ComponentSection, bool>(nameof(CanToggle));
    public bool CanToggle
    {
        get => GetValue(CanToggleProperty);
        set => SetValue(CanToggleProperty, value);
    }

    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<ComponentSection, bool>(nameof(IsOn), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    // 13.6: the component has prefab overrides on this instance (a dot left of the header).
    public static readonly StyledProperty<bool> IsOverriddenProperty =
        AvaloniaProperty.Register<ComponentSection, bool>(nameof(IsOverridden));
    public bool IsOverridden
    {
        get => GetValue(IsOverriddenProperty);
        set => SetValue(IsOverriddenProperty, value);
    }

    public static readonly StyledProperty<bool> HasSettingsProperty =
        AvaloniaProperty.Register<ComponentSection, bool>(nameof(HasSettings), true);
    public bool HasSettings
    {
        get => GetValue(HasSettingsProperty);
        set => SetValue(HasSettingsProperty, value);
    }
}
