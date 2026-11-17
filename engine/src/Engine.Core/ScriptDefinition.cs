using System.Numerics;
using System.Text.Json;

namespace Engine.Core;

public sealed record ScriptField(string Name, Type Type, object? Default, string? TargetScript = null, Type? TargetBuiltIn = null, string[]? EnumNames = null)
{
    public bool TypedReference => TargetScript != null || TargetBuiltIn != null;
    public bool IsObjectList => Type == typeof(Guid[]);
    internal static readonly Dictionary<Type, string> AssetExtensions = new()
    {
        [typeof(SpriteReference)] = ".png", [typeof(PrefabReference)] = ProjectAssets.Prefab,
        [typeof(SceneReference)] = ProjectAssets.Scene, [typeof(DataReference)] = ProjectAssets.Data,
        [typeof(TableReference)] = GameTable.Extension
    };
    public string? AssetExtension => AssetExtensions.GetValueOrDefault(Type.GetElementType() ?? Type);
    public bool IsAssetList => Type.IsArray && AssetExtension != null;
    internal IEnumerable<(string Label, Guid Id)> AssetIds(ScriptComponent script)
    {
        if (!script.Values.TryGetValue(Name, out var stored)) yield break;
        if (IsAssetList)
        {
            var ids = script.ReadReferences(Name, false);
            for (int i = 0; i < ids.Length; i++) yield return ($"{Name}[{i}]", ids[i]);
        }
        else
        {
            if (stored.ValueKind != JsonValueKind.Object ||
                !(stored.TryGetProperty("Id", out var value) || stored.TryGetProperty("id", out value)) ||
                value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out var id))
                throw new InvalidDataException("Malformed reference");
            yield return (Name, id);
        }
    }

    public bool Accepts(SceneObject target) => TargetScript != null
        ? target.Components.OfType<ScriptComponent>().Any(c => c.Script == TargetScript)
        : TargetBuiltIn == null || target.Components.Any(c => c.GetType() == TargetBuiltIn);

    public object? Value(ScriptComponent component)
    {
        if (IsObjectList || IsAssetList) return component.ReadReferences(Name, IsObjectList);
        if (Type == typeof(Vector2)) return component.ReadVector(Name, (Vector2)Default!);
        if (!component.Values.TryGetValue(Name, out var stored)) return Default;
        try { var value = stored.Deserialize(Type) ?? Default; return value is Guid id ? component.InstanceId(id) : value; }
        catch (JsonException) { return Default; } // An override may predate a field type change.
    }
}

public sealed record ScriptDefinition(string Source, IReadOnlyList<ScriptField> Fields)
{
    public string[] Events { get; init; } = [];
    public string[] Handlers { get; init; } = [];
}
