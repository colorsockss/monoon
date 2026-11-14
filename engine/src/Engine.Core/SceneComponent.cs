using VYaml.Annotations;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Engine.Core;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CutsceneDirectorComponent), "cutsceneDirector")]
[YamlObjectUnion("cutsceneDirector", typeof(CutsceneDirectorComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(PositionTween2DComponent), "positionTween2D")]
[YamlObjectUnion("positionTween2D", typeof(PositionTween2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(SkeletonAnimator2DComponent), "skeletonAnimator2D")]
[YamlObjectUnion("skeletonAnimator2D", typeof(SkeletonAnimator2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(FootIK2DComponent), "footIK2D")]
[YamlObjectUnion("footIK2D", typeof(FootIK2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(ParticleEmitter2DComponent), "particleEmitter2D")]
[YamlObjectUnion("particleEmitter2D", typeof(ParticleEmitter2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(PointLight2DComponent), "pointLight2D")]
[YamlObjectUnion("pointLight2D", typeof(PointLight2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(SpriteShader2DComponent), "spriteShader2D")]
[YamlObjectUnion("spriteShader2D", typeof(SpriteShader2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(NavigationRegion2DComponent), "navigationRegion2D")]
[YamlObjectUnion("navigationRegion2D", typeof(NavigationRegion2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(Rigidbody2DComponent), "rigidbody2D")]
[YamlObjectUnion("rigidbody2D", typeof(Rigidbody2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(DistanceJoint2DComponent), "distanceJoint2D")]
[YamlObjectUnion("distanceJoint2D", typeof(DistanceJoint2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(CharacterMotor2DComponent), "characterMotor2D")]
[YamlObjectUnion("characterMotor2D", typeof(CharacterMotor2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(Tilemap2DComponent), "tilemap2D")]
[YamlObjectUnion("tilemap2D", typeof(Tilemap2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(Camera2DComponent), "camera2D")]
[YamlObjectUnion("camera2D", typeof(Camera2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(SpriteRendererComponent), "spriteRenderer")]
[YamlObjectUnion("spriteRenderer", typeof(SpriteRendererComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(CircleCollider2DComponent), "circleCollider2D")]
[YamlObjectUnion("circleCollider2D", typeof(CircleCollider2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(PolygonCollider2DComponent), "polygonCollider2D")]
[YamlObjectUnion("polygonCollider2D", typeof(PolygonCollider2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(BoxCollider2DComponent), "boxCollider2D")]
[YamlObjectUnion("boxCollider2D", typeof(BoxCollider2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(SpriteAnimatorComponent), "spriteAnimator")]
[YamlObjectUnion("spriteAnimator", typeof(SpriteAnimatorComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(AudioListener2DComponent), "audioListener2D")]
[YamlObjectUnion("audioListener2D", typeof(AudioListener2DComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(AudioSourceComponent), "audioSource")]
[YamlObjectUnion("audioSource", typeof(AudioSourceComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(UiLayoutComponent), "uiLayout")]
[YamlObjectUnion("uiLayout", typeof(UiLayoutComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(UiScrollComponent), "uiScroll")]
[YamlObjectUnion("uiScroll", typeof(UiScrollComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(UiTextFieldComponent), "uiTextField")]
[YamlObjectUnion("uiTextField", typeof(UiTextFieldComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(UiCanvasComponent), "uiCanvas")]
[YamlObjectUnion("uiCanvas", typeof(UiCanvasComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(UiRectComponent), "uiRect")]
[YamlObjectUnion("uiRect", typeof(UiRectComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(UiImageComponent), "uiImage")]
[YamlObjectUnion("uiImage", typeof(UiImageComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(UiTextComponent), "uiText")]
[YamlObjectUnion("uiText", typeof(UiTextComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(UiButtonComponent), "uiButton")]
[YamlObjectUnion("uiButton", typeof(UiButtonComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(HudComponent), "hud")]
[YamlObjectUnion("hud", typeof(HudComponent), DiscriminatorKey = "type")]
[JsonDerivedType(typeof(ScriptComponent), "script")]
[YamlObjectUnion("script", typeof(ScriptComponent), DiscriminatorKey = "type")]
[YamlObject]
public abstract partial record SceneComponent
{
    internal virtual string Kind => GetType().Name;
}

public abstract record Collider2DComponent : SceneComponent
{
    // Compound colliders are authored as children of a moving parent.
    internal override string Kind => "Collider2D";
    public bool IsTrigger { get; set; }
    public int Layer { get; set; }
    public uint Mask { get; set; } = uint.MaxValue;
}

[DisplayName("Box Collider 2D"), YamlObject]
public sealed partial record BoxCollider2DComponent : Collider2DComponent
{
    public BoxCollider2DComponent() { }
    [YamlConstructor] public BoxCollider2DComponent(uint mask = uint.MaxValue) => Mask = mask;
}

[DisplayName("Circle Collider 2D"), YamlObject]
public sealed partial record CircleCollider2DComponent : Collider2DComponent
{
    public CircleCollider2DComponent() { }
    [YamlConstructor] public CircleCollider2DComponent(uint mask = uint.MaxValue) => Mask = mask;
    // Zero fits the smaller local dimension.
    public float Radius { get; set; }
}

[YamlObject]
public readonly partial record struct ColliderVertex(float X, float Y);

[DisplayName("Polygon Collider 2D"), YamlObject]
public sealed partial record PolygonCollider2DComponent : Collider2DComponent
{
    public PolygonCollider2DComponent() { }
    [YamlConstructor] public PolygonCollider2DComponent(uint mask = uint.MaxValue) => Mask = mask;
    private ColliderVertex[] points = [new(0,1),new(.5f,0),new(1,1)];
    public ColliderVertex[] Points { get => (ColliderVertex[])points.Clone(); set => points = value is null ? [] : (ColliderVertex[])value.Clone(); }
}

[DisplayName("Sprite Renderer")]
[YamlObject]
public sealed partial record SpriteRendererComponent : SceneComponent
{
    public int SortingLayer { get; set; }
    public int Order { get; set; }
    public bool FlipX { get; set; }
    public bool FlipY { get; set; }
    public bool PixelSnap { get; set; }
    public string? Tint { get; set; }
    public Guid? Mask { get; set; }
    // A null asset selects the engine's white square sprite.
    public SpriteRendererComponent() { }
    [YamlConstructor] public SpriteRendererComponent(string squareColor = "#FFFFFF") => SquareColor = squareColor;
    public string SquareColor { get; set; } = "#FFFFFF";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? AssetId { get; set; }
    public string? Asset { get; set; }
}

[DisplayName("Camera 2D")]
[YamlObject]
public sealed partial record Camera2DComponent : SceneComponent
{
    public const float MinZoom = 0.05f, MaxZoom = 32f;
    public Camera2DComponent() { }
    [YamlConstructor] public Camera2DComponent(float zoom = 1,string output = Primary,bool showViews=true) => (Zoom,Output,ShowViews) = (zoom,output,showViews);
    public float Zoom { get; set; } = 1;
}
