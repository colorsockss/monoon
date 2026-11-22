using System.ComponentModel;
using System.Drawing;
using VYaml.Annotations;

namespace Engine.Core;

[DisplayName("UI Canvas"), YamlObject]
public sealed partial record UiCanvasComponent : SceneComponent
{
    public int Order { get; set; }
}

[DisplayName("UI Rect"), YamlObject]
public sealed partial record UiRectComponent : SceneComponent
{
    public UiRectComponent() { }
    [YamlConstructor] public UiRectComponent(string anchor = "TopLeft", string showIf = "")
        => (Anchor, ShowIf) = (anchor, showIf);
    public string Anchor { get; set; } = "TopLeft";
    public string ShowIf { get; set; } = "";
    public float? Left { get; set; }
    public float? Right { get; set; }
    public float? Top { get; set; }
    public float? Bottom { get; set; }
    internal bool HasPins => Left.HasValue || Right.HasValue || Top.HasValue || Bottom.HasValue;
    public bool InputModeRoot { get; set; }
    public bool ScaleHorizontalWithText { get; set; }
}

[DisplayName("UI Image"), YamlObject]
public sealed partial record UiImageComponent : SceneComponent
{
    public UiImageComponent() { }
    [YamlConstructor] public UiImageComponent(string color = "#FFFFFF") => Color = color;
    public UiBorders? Borders { get; set; }
    public Guid? Sprite { get; set; }
    public string Color { get; set; } = "#FFFFFF";
}

[DisplayName("UI Text"), YamlObject]
public sealed partial record UiTextComponent : SceneComponent
{
    public UiTextComponent() { }
    [YamlConstructor] public UiTextComponent(string text = "Text", float size = 16, string color = "#FFFFFF")
        => (Text, Size, Color) = (text, size, color);
    public string Text { get; set; } = "Text";
    public float Size { get; set; } = 16;
    public string Color { get; set; } = "#FFFFFF";
    public string? TextKey { get; set; }
    public bool Wrap {get;set;}
    public Dictionary<string,string>? Translations { get;set; }
    public float InsetX { get; set; }
    public float InsetY { get; set; }
}

[DisplayName("UI Button"), YamlObject]
public sealed partial record UiButtonComponent : UiControlComponent
{
    public UiButtonComponent() { }
    [YamlConstructor] public UiButtonComponent(bool interactable = true) => Interactable = interactable;
}

public sealed partial class SceneDocument
{
    [System.Text.Json.Serialization.JsonIgnore, YamlIgnore]
    public bool IsUiOnly => Objects.Any(ScreenUi.IsWidget) &&
        !Objects.Any(o => !ScreenUi.IsWidget(o) && (o.GetComponent<Tilemap2DComponent>() != null || o.GetComponent<SpriteRendererComponent>() != null || o.GetComponent<Collider2DComponent>() != null));

    public SceneObject CreateUi(string kind, SceneObject? parent)
    {
        var obj = new SceneObject { Name = kind, X = 12, Y = 12, Width = 112, Height = 32 };
        if (kind == "Canvas") { obj.X = obj.Y = 0; obj.AddComponent(new UiCanvasComponent()); }
        else
        {
            obj.AddComponent(new UiRectComponent());
            switch (kind)
            {
                case "Slider": obj.AddComponent(new UiSliderComponent()); break;
                case "Toggle": obj.AddComponent(new UiToggleComponent()); break;
                case "Dropdown": obj.AddComponent(new UiDropdownComponent()); break;
                case "Button": obj.AddComponent(new UiButtonComponent()); obj.AddComponent(new UiTextComponent { Text = "Button", InsetX = 8, InsetY = 5 }); break;
                case "Text": obj.AddComponent(new UiTextComponent()); break;
                case "Image": obj.AddComponent(new UiImageComponent()); break;
                case "Layout": case "Scroll View":
                    obj.Width=240;obj.Height=180;obj.AddComponent(new UiLayoutComponent());
                    if(kind=="Scroll View")obj.AddComponent(new UiScrollComponent());
                    break;
                case "Text Field": obj.AddComponent(new UiTextFieldComponent());obj.AddComponent(new UiTextComponent { Text="Enter text",InsetX=6,InsetY=3 });break;
                case "Group": break;
                default: throw new ArgumentException("Unknown UI element.", nameof(kind));
            }
        }
        obj.SetParent(parent);
        Objects.Add(obj);
        return obj;
    }
}

public interface IGameUi
{
    UiPreferences Preferences {get;}
    string Translate(string key, string? fallback = null);
    bool SetTextKey(Guid widget, string key);
    bool SetTextKey(Guid widget, string key, params string[] argumentKeys);
    string? Value(Guid widget);
    float? SliderValue(Guid widget);
    bool? ToggleValue(Guid widget);
    int? SelectedIndex(Guid widget);
    bool SetSliderValue(Guid widget, float value);
    bool SetToggleValue(Guid widget, bool value);
    bool SetSelectedIndex(Guid widget, int index);
    bool FocusWidget(Guid widget);
    float ScrollOffset(Guid widget);
    Guid? ActiveInputMode { get; }
    bool PushInputMode(Guid root);
    bool PopInputMode(Guid root);
    Guid? Focused {get;}
    void ClearFocus();
    string Preedit {get;}
    bool SetText(Guid widget, string text);
    /// <summary>The label text currently drawn for a text widget, or the value of a text field.</summary>
    string? TextOf(Guid widget);
    bool SetVisible(Guid widget, bool visible);
    bool SetInteractable(Guid widget, bool interactable);
    bool BindList(Guid list, IUiListSource source);
    bool RefreshList(Guid list);
    string? SelectedItem(Guid list);
    bool SelectItem(Guid list, string itemId);
    int ListRowCount(Guid list);
}
public readonly record struct UiValueChanged(Guid ObjectId, float Value, SessionState Session, IGameUi Ui, IGameObjects Objects);
public readonly record struct UiClick(Guid ObjectId, SessionState Session, IGameUi Ui, IGameObjects Objects);

/// <summary>One layout for painting, picking and keyboard navigation, in game resolution units.</summary>
public sealed partial class ScreenUi : IGameUi
{
    public static IReadOnlyList<string> Anchors { get; } = Array.AsReadOnly(new[] {
        "TopLeft", "TopCenter", "TopRight", "MiddleLeft", "Center", "MiddleRight", "BottomLeft", "BottomCenter", "BottomRight" });
    public static bool IsWidget(SceneObject o) => o.Components.Any(c => c is UiRectComponent or UiImageComponent or UiTextComponent or UiControlComponent or UiTextFieldComponent or UiLayoutComponent or UiScrollComponent);
    private sealed record Node(SceneObject Object, Node? Parent, int Order, string? Image)
    {
        internal Guid Source = Object.Id; // the authored object behind a pooled list row
        internal UiNineSlice? Slice;
        internal ButtonPaint[]? Palette;
        internal RectangleF Box,Clip;
        internal float Content,Offset,Measured,MeasuredWidth,Cursor;
        internal int Placed;
        internal float AllocatedWidth,LineWidth,LineSize;
        internal string? LineSource;
        internal Type? FontBackend;
        internal string[] Lines=[];
        internal readonly List<Node> Children=[];
        internal readonly UiLayoutComponent? Layout=Object.GetComponent<UiLayoutComponent>();
        internal readonly bool Scroll=Object.GetComponent<UiScrollComponent>()!=null;
        internal readonly UiTextFieldComponent? Field=Object.GetComponent<UiTextFieldComponent>();
        internal readonly bool Canvas=Object.GetComponent<UiCanvasComponent>()!=null;
        internal readonly UiTextComponent? Label = Object.GetComponent<UiTextComponent>();
        internal readonly UiImageComponent? Picture = Object.GetComponent<UiImageComponent>();
        internal readonly UiControlComponent? Control = Object.GetComponent<UiControlComponent>();
        internal float ControlValue;
        internal readonly UiRectComponent? Rect = Object.GetComponent<UiRectComponent>();
        internal readonly bool ScaleHorizontal = Object.GetComponent<UiRectComponent>()?.ScaleHorizontalWithText == true;
        internal readonly string Flag = Object.GetComponent<UiRectComponent>()?.ShowIf ?? "";
    }
    private Node[] treeOrder=[];
    private Node[] nodes = [];
    private Dictionary<Guid, Node> byId = [];
    private readonly Dictionary<Guid, string> textOverrides = [];
    private readonly Dictionary<Guid, bool> interactOverrides = [];
    private readonly HashSet<Guid> hidden = [];
    private readonly RectangleF screen;
    private readonly Func<string,bool>? flag;
    private readonly Func<Guid,bool>? active;
    private readonly UiTheme theme;
    private Guid? focus, pressed, hovered, activationOwner;
    private string? activationKey;
    private PointF? pointer;
    private readonly Queue<(Guid Id, float? Value)> controlEvents = [];
    private readonly HashSet<string> held = [];
    public IEnumerable<string> Images => nodes.Select(n => n.Image).OfType<string>().Distinct();

    public ScreenUi(SceneDocument scene, ProjectAssets? assets, Func<string,bool>? flag = null, Func<Guid,bool>? active = null,UiPreferences? preferences=null)
    {
        Language = assets != null && scene.ProjectDirectory is {} root ? GameLocalization.Load(root, assets, scene.Settings.Localization) : GameLocalization.Empty;
        theme = assets != null && scene.ProjectDirectory is {} dir ? UiTheme.Load(dir, assets, scene.Settings.UiTheme) : UiTheme.Empty;
        Preferences=preferences??new(scene.Settings.Accessibility);
        this.flag = flag;
        this.active = active;
        screen = new(0, 0, scene.Settings.Width, scene.Settings.Height);
        Rebuild(scene, assets);
    }

    internal void Rebuild(SceneDocument scene, ProjectAssets? assets)
    {
        uiOnly=scene.IsUiOnly;
        var tree = new Dictionary<Guid,Node>();
        var ordered = new List<Node>();
        modeOwners.Clear();
        foreach (var obj in scene.ParentFirst())
        {
            var owner = obj.GetComponent<UiRectComponent>()?.InputModeRoot == true
                ? obj.Id : obj.ParentId is Guid parentId ? modeOwners.GetValueOrDefault(parentId) : (Guid?)null;
            if (owner.HasValue) modeOwners.Add(obj.Id, owner);
            var canvas = obj.GetComponent<UiCanvasComponent>();
            if (canvas == null && !IsWidget(obj)) continue;
            Node? parent = null;
            for (var p = obj.Parent; p != null; p = p.Parent) if (tree.TryGetValue(p.Id, out parent)) break;
            string? image = null;
            if (obj.GetComponent<UiImageComponent>()?.Sprite is Guid id && assets?.PathFor(id) is { } relative && scene.ProjectDirectory != null)
                image = Path.Combine(scene.ProjectDirectory, relative);
            var node = new Node(obj, parent, canvas?.Order ?? parent?.Order ?? 0, image);
            if (node.Control is {} control)
            {
                node.Palette = theme.Find(control.Style);
                var old = byId.GetValueOrDefault(obj.Id);
                node.ControlValue = control.Normalize(old?.Control?.GetType() == control.GetType() ? old.ControlValue : control.DefaultValue);
            }
            if (node.Picture?.Borders is {} borders && image != null) node.Slice = new(borders, SpriteRegion.ReadPngSize(image));
            if(byId.TryGetValue(obj.Id,out var prior))node.Offset=prior.Offset;
            parent?.Children.Add(node);tree.Add(obj.Id, node); ordered.Add(node);
        }
        InsertListRows(ordered, tree);
        treeOrder=ordered.ToArray();
        nodes = ordered.OrderBy(n => n.Order).ToArray();
        byId = tree;
        foreach(var id in values.Keys.Where(id=>!tree.ContainsKey(id)).ToArray())values.Remove(id);
        if(edit is Guid editing && !tree.ContainsKey(editing)) EndEditing();
        foreach (var id in keyOverrides.Keys.Where(id => !tree.ContainsKey(id)).ToArray()) keyOverrides.Remove(id);
        hidden.RemoveWhere(id => !tree.ContainsKey(id));
        foreach (var id in textOverrides.Keys.Where(id => !tree.ContainsKey(id)).ToArray()) textOverrides.Remove(id);
        foreach (var id in interactOverrides.Keys.Where(id => !tree.ContainsKey(id)).ToArray()) interactOverrides.Remove(id);
        if (focus is Guid f && !tree.ContainsKey(f)) focus = null;
        if (pressed is Guid capture && !tree.ContainsKey(capture)) pressed = null;
        if (dropdownId is Guid open)
        {
            if (tree.GetValueOrDefault(open)?.Control is UiDropdownComponent menu)
                optionCursor = (int)menu.Normalize(optionCursor);
            else CloseOptions();
        }
        ValidateInputModes();
        OrderModeLayers();
    }
    internal static int AnchorIndex(string anchor)
    {
        for (int i = 0; i < Anchors.Count; i++) if (Anchors[i] == anchor) return i;
        throw new InvalidDataException($"Unknown UI anchor '{anchor}'.");
    }
}
