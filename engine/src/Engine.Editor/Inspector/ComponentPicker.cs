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
