using System.Numerics;
using System.Text.Json;

namespace Engine.Core;

public sealed record ScriptField(string Name, Type Type, object? Default, string? TargetScript = null, Type? TargetBuiltIn = null, string[]? EnumNames = null)
{
    public string[] FormerNames { get; init; } = [];
    // 13.8. A flag enum saves a comma list of names ("Fire, Ice"; "" is no flag).
    public bool Flags { get; init; }
    // A colour (a Color field, or a string with [ColorUsage]) saved as #RRGGBB[AA]; Type is string and Default the hex text.
    public bool? ColorAlpha { get; init; }
    // Array, List<T> (IsList) or dictionary (Key: string or int) of Element (int, long, uint, float, double, bool or string),
    // or a one-level [Serializable] struct of Members. Type is JsonElement and Default the initializer's JSON (null when none).
    public Type? Element { get; init; }
    public Type? Key { get; init; }
    public bool IsList { get; init; }
    public ScriptField[]? Members { get; init; }
    public bool IsJson => Type == typeof(JsonElement);
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
        if (IsJson) return Members == null || stored.ValueKind != JsonValueKind.Object ? stored : StructValue(stored);
        try { var value = stored.Deserialize(Type) ?? Default; return value is Guid id ? component.InstanceId(id) : value; }
        catch (JsonException) { return Default; } // An override may predate a field type change.
    }

    // The default members with the saved ones over them: an older save may lack a member added since.
    private JsonElement StructValue(JsonElement stored)
    {
        var merged = System.Text.Json.Nodes.JsonNode.Parse(((JsonElement)Default!).GetRawText())!.AsObject();
        foreach (var member in Members!)
            if (stored.TryGetProperty(member.Name, out var value)) merged[member.Name] = System.Text.Json.Nodes.JsonNode.Parse(value.GetRawText());
        return JsonElements.Of(merged);
    }
}

public sealed record ScriptDefinition(string Source, IReadOnlyList<ScriptField> Fields)
{
    public string[] Events { get; init; } = [];
    public string[] Handlers { get; init; } = [];
}
