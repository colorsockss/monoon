using System.Text;
using System.Text.Json;
using VYaml.Annotations;

namespace Engine.Core;

public readonly record struct DataReference(Guid Id);

// The YAML mapping supports nested records and lists as well as scalar settings.
[YamlObject]
public sealed partial class GameData
{
    [YamlRequired] public int Version { get; set; } = 1;
    [YamlRequired] public Dictionary<string, JsonElement> Values { get; set; } = new(StringComparer.Ordinal);

    public bool GetBool(string key, bool fallback = false) =>
        Values.TryGetValue(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;
    public string GetText(string key, string fallback = "") =>
        Values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : fallback;
    public double GetNumber(string key, double fallback = 0) =>
        Values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n) && double.IsFinite(n) ? n : fallback;
    public void Set(string key, bool value) => Values[key] = JsonSerializer.SerializeToElement(value, GameJson.Default.Boolean);
    public void Set(string key, string value) => Values[key] = JsonSerializer.SerializeToElement(value, GameJson.Default.String);
    public void Set(string key, double value) => Values[key] = JsonSerializer.SerializeToElement(value, GameJson.Default.Double);

    internal GameData Validate()
    {
        if (Version < 1 || Values == null || Values.Keys.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Data needs a positive version and a values mapping with nonempty keys.");
        return this;
    }

    public static JsonElement ParseValue(string yaml)
    {
        try { return ProjectYaml.Decode<JsonElement>(Encoding.UTF8.GetBytes(yaml)); }
        catch (Exception ex) when (ex is VYaml.YamlException or FormatException or ArgumentException)
        { throw new InvalidDataException(ex.Message, ex); }
    }
    public static string FormatValue(JsonElement value) => Encoding.UTF8.GetString(ProjectYaml.Encode(value)).TrimEnd('\n','\r');

    public static GameData Parse(string yaml)
    {
        try { return ProjectYaml.Decode<GameData>(Encoding.UTF8.GetBytes(yaml)).Validate(); }
        catch (Exception ex) when (ex is VYaml.YamlException or FormatException or ArgumentException)
        { throw new InvalidDataException(ex.Message, ex); }
    }

    public static void SaveYaml(string path, string yaml, bool overwrite)
    {
        Parse(yaml);
        ProjectYaml.WriteBytes(path, Encoding.UTF8.GetBytes(yaml), overwrite);
    }
    public string ToYaml() => Encoding.UTF8.GetString(ProjectYaml.Encode(Validate()));
    public static GameData Load(string path)
    {
        string yaml = File.ReadAllText(path);
        try { return Parse(yaml); }
        catch (InvalidDataException ex) { throw new InvalidDataException($"{path}: {ex.Message}", ex); }
    }

    public GameData UpgradeTo(int version, Action<int,GameData>? migrate = null)
    {
        Validate();
        if (version < Version) throw new InvalidDataException($"Data version {Version} is newer than supported version {version}.");
        if (version == Version) return this;
        if (migrate == null) throw new InvalidDataException($"Missing migration from data version {Version} to {version}.");
        // Migrate a copy so a failed step cannot leave a partially upgraded document.
        var next = new GameData { Version = Version, Values = new(Values, StringComparer.Ordinal) };
        for (int from = Version; from < version; from++)
        {
            migrate(from, next);
            next.Version = from + 1;
            next.Validate();
        }
        return next;
    }

    internal static string AssetPath(string project, ProjectAssets assets, DataReference reference)
    {
        var path = assets.PathFor(reference.Id);
        if (path == null || !path.EndsWith(ProjectAssets.Data, StringComparison.Ordinal))
            throw new InvalidDataException($"Missing data asset {reference.Id}.");
        return Path.Combine(project, path);
    }
}
