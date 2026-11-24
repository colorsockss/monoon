using System.ComponentModel;
using System.Reflection;
using VYaml.Annotations;
using Engine.Core;

namespace Engine.Editor;

internal sealed record ComponentChoice(Type Type, string Name)
{
    // Add Component groups, in menu order. Project scripts form the last group.
    internal static readonly string[] Categories = ["Rendering", "Physics", "UI", "Audio", "Animation", "Navigation", "Scripts"];

    // Offer exactly the component types the scene format can save and reopen.
    public static IReadOnlyDictionary<Type, ComponentChoice> Registered { get; } = typeof(SceneComponent)
        .GetCustomAttributes<YamlObjectUnionAttribute>()
        .Select(attribute => attribute.SubType)
        .Where(type => type != typeof(ScriptComponent) && type != typeof(HudComponent))
        .ToDictionary(type => type, type => new ComponentChoice(type,
            type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name));

    public string Category => Type.Name.StartsWith("Ui", StringComparison.Ordinal) ? "UI" : Type.Name switch
    {
        nameof(Rigidbody2DComponent) or nameof(CharacterMotor2DComponent) => "Physics",
        _ when typeof(Collider2DComponent).IsAssignableFrom(Type) || typeof(Joint2DComponent).IsAssignableFrom(Type) => "Physics",
        nameof(AudioSourceComponent) or nameof(AudioListener2DComponent) => "Audio",
        nameof(SpriteAnimatorComponent) or nameof(SkeletonAnimator2DComponent) or nameof(FootIK2DComponent) or nameof(PositionTween2DComponent)
            or nameof(PathFollower2DComponent) or nameof(CutsceneDirectorComponent) => "Animation",
        nameof(NavigationRegion2DComponent) => "Navigation",
        _ => "Rendering", // sprites, sorting, lines, trails, particles, lights, shaders, tiles, cameras
    };

    public SceneComponent Create() => (SceneComponent)Activator.CreateInstance(Type)!;
}
