using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Engine.Core;

namespace Engine.Editor;

// Code-built windows read the same design tokens and icons as the XAML in EditorStyles.axaml and EditorIcons.axaml.
internal static class EditorTokens
{
    private static T Get<T>(string key) =>
        Application.Current!.TryGetResource(key, ThemeVariant.Dark, out var value) && value is T typed ? typed : throw new KeyNotFoundException(key);

    public static IBrush Brush(string key) => Get<IBrush>(key);
    public static Geometry Geometry(string key) => Get<Geometry>(key);
    public static double Gap => Get<double>("EditorGap");
    public static double Gap2 => Get<double>("EditorGap2");
    public static double FieldGap => Get<double>("EditorFieldGap");
    public static double FieldLabelWidth => Get<double>("EditorFieldLabelWidth");
    public static double DialogLabelWidth => Get<double>("EditorDialogLabelWidth");
    public static double FontSize => Get<double>("EditorFontSize");
    public static double FontSizeSmall => Get<double>("EditorFontSizeSmall");
    public static Thickness WindowPadding => Get<Thickness>("EditorWindowPadding");

    // A label column of the given width followed by the remaining columns, e.g. LabelColumns(FieldLabelWidth, "*,Auto").
    public static ColumnDefinitions LabelColumns(double width, string rest = "*") =>
        new(width.ToString(CultureInfo.InvariantCulture) + "," + rest);

    public static PathIcon Icon(string key) => new() { Theme = Get<ControlTheme>("EditorIcon"), Data = Geometry(key) };

    // Icon-only buttons always carry a tooltip and an accessible name.
    public static Button IconButton(string key, string tip)
    {
        var button = new Button { Classes = { "icon" }, Content = Icon(key) };
        ToolTip.SetTip(button, tip);
        AutomationProperties.SetName(button, tip);
        return button;
    }

    public static Geometry? ComponentIcon(SceneComponent component) => component switch
    {
        ScriptComponent => Geometry("IconScript"),
        SpriteRendererComponent or SpriteShader2DComponent => Geometry("IconSpriteRenderer"),
        CircleCollider2DComponent => Geometry("IconCircleCollider"),
        Collider2DComponent => Geometry("IconBoxCollider"),
        Rigidbody2DComponent or Joint2DComponent or CharacterMotor2DComponent => Geometry("IconRigidbody"),
        Camera2DComponent => Geometry("IconCamera"),
        AudioSourceComponent or AudioListener2DComponent => Geometry("IconAudio"),
        Tilemap2DComponent or NavigationRegion2DComponent => Geometry("IconTilemap"),
        SpriteAnimatorComponent or SkeletonAnimator2DComponent or PositionTween2DComponent or PathFollower2DComponent or FootIK2DComponent => Geometry("IconAnimator"),
        ParticleEmitter2DComponent or Line2DComponent or Trail2DComponent => Geometry("IconParticle"),
        PointLight2DComponent => Geometry("IconLight"),
        _ when component.GetType().Name.StartsWith("Ui", StringComparison.Ordinal) || component is HudComponent => Geometry("IconUi"),
        _ => null,
    };

    // A long selected ComboBox value trims with an ellipsis instead of being cut off; the full value is in the tooltip.
    // Applied to built field trees, since Inspector fields are created in code. Pickers with their own item template keep it.
    private static readonly IDataTemplate TrimmedValue = new FuncDataTemplate<object?>((_, _) => new TextBlock
    {
        TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        [!TextBlock.TextProperty] = new Binding(), [!ToolTip.TipProperty] = new Binding(),
    });
    public static T TrimComboBoxes<T>(T root) where T : Control
    {
        foreach (var box in root.GetLogicalDescendants().OfType<ComboBox>().Concat(root is ComboBox self ? [self] : []))
            if (box.ItemTemplate == null) box.SelectionBoxItemTemplate = TrimmedValue;
        return root;
    }

    // Hierarchy rows: the first component with an icon, scripts last, and the Transform mark for an empty object.
    public static Geometry ObjectIcon(SceneObject obj) =>
        obj.Components.Where(c => c is not ScriptComponent).Select(ComponentIcon).FirstOrDefault(icon => icon != null)
        ?? Geometry(obj.Components.Any(c => c is ScriptComponent) ? "IconScript" : "IconTransform");

    public static Geometry AssetIcon(string path, bool folder) => Geometry(folder ? "IconFolder" :
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ProjectAssets.Scene => "IconScene",
            ProjectAssets.Prefab => "IconPrefab",
            ".png" => "IconImage",
            ".cs" => "IconCSharp",
            ProjectAssets.Data or GameTable.Extension => "IconData",
            ".wav" or ".ogg" => "IconAudio",
            ProjectAssets.Tileset => "IconTileset",
            SpriteAtlas.Extension => "IconAtlas",
            ProjectAssets.Clip or AnimationController.Extension => "IconAnimation",
            _ => "IconFile",
        });
}
