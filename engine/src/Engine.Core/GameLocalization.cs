using System.Buffers.Binary;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record LocalizationSettings
{
    public LocalizationSettings() { }
    [YamlConstructor] public LocalizationSettings(string fallbackLocale = "en") => FallbackLocale = fallbackLocale;
    public Guid? Table { get; init; }
    public string FallbackLocale { get; init; } = "en";
    public Guid? DefaultFont { get; init; }
    public Guid[]? FallbackFonts { get; init; }

    internal void Validate()
    {
        if (!GameLocalization.IsLanguageTag(FallbackLocale)) throw new InvalidDataException("Invalid fallback language tag.");
        var seen = new HashSet<Guid>();
        if (DefaultFont is Guid primary) seen.Add(primary);
        if (FallbackFonts is { Length: > 0 } fallbacks && (DefaultFont == null || fallbacks.Any(id => id == Guid.Empty || !seen.Add(id))))
            throw new InvalidDataException("Fallback fonts need a default font and distinct asset IDs.");
    }
}

// Immutable project data. The locale is passed by the caller so editor previews and
// simultaneous game sessions can share the asset without sharing a language preference.
internal sealed class GameLocalization
{
    public static readonly GameLocalization Empty = new();
    private readonly Dictionary<string, int> languages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string[]> messages = new(StringComparer.Ordinal);
    private readonly string fallback;
    internal IEnumerable<string> Messages => messages.Values.SelectMany(values => values);
    public IReadOnlyList<string> Fonts { get; }
    private GameLocalization(string fallback = "en", string[]? fonts = null) => (this.fallback, Fonts) = (fallback, fonts ?? []);
    internal static bool IsLanguageTag(string? tag) => tag is { Length: > 0 and <= 64 } &&
        tag.Split('-').All(part => part.Length is > 0 and <= 8 && part.All(char.IsAsciiLetterOrDigit));
    public static bool IsFont(string path) => Path.GetExtension(path).ToLowerInvariant() is ".ttf" or ".otf";

    // Validate the sfnt directory before a font enters a project or is shipped by a headless build.
    internal static void ValidateFont(string path)
    {
        using var file = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        if (file.Length < header.Length) throw new InvalidDataException($"Invalid font header: {path}");
        file.ReadExactly(header);
        var tag = BinaryPrimitives.ReadUInt32BigEndian(header);
        int tables = BinaryPrimitives.ReadUInt16BigEndian(header[4..]);
        if (tag is not (0x00010000 or 0x4f54544f or 0x74727565) || tables == 0 || file.Length < 12L + 16L * tables)
            throw new InvalidDataException($"Invalid OpenType font directory: {path}");
        Span<byte> entry = stackalloc byte[16];
        for (int i = 0; i < tables; i++)
        {
            file.ReadExactly(entry);
            long offset = BinaryPrimitives.ReadUInt32BigEndian(entry[8..]);
            long size = BinaryPrimitives.ReadUInt32BigEndian(entry[12..]);
            if (offset + size > file.Length) throw new InvalidDataException($"Truncated font table: {path}");
        }
    }

    public static GameLocalization Load(string root, ProjectAssets assets, LocalizationSettings? config, ISet<string>? dependencies = null)
    {
        if (config == null) return Empty;
        config.Validate();
        string Resolve(Guid id, Func<string,bool> supported, string kind)
        {
            if (assets.PathFor(id) is not {} relative || !supported(relative) || !File.Exists(Path.Combine(root, relative)))
                throw new InvalidDataException($"Missing {kind} asset {id}.");
            dependencies?.Add(relative);
            return relative;
        }
        var fonts = new List<string>();
        void Font(Guid id)
        {
            var path = Resolve(id, IsFont, "font");
            string file = Path.Combine(root, path);
            ValidateFont(file);
            fonts.Add(file);
            if (dependencies != null && File.Exists(Path.Combine(root, path + ".license"))) dependencies.Add(path + ".license");
        }
        if (config.DefaultFont is Guid primary) Font(primary);
        foreach (var id in config.FallbackFonts ?? []) Font(id);
        var result = new GameLocalization(config.FallbackLocale, fonts.ToArray());
        if (config.Table is not Guid table) return result;
        var relative = Resolve(table, p => p.EndsWith(GameTable.Extension, StringComparison.Ordinal), "translation table");
        var source = GameTable.Load(Path.Combine(root, relative));
        for (int i = 0; i < source.Columns.Length; i++)
        {
            var col = source.Columns[i];
            if (col.Type != "Text" || !IsLanguageTag(col.Name) || !result.languages.TryAdd(col.Name, i))
                throw new InvalidDataException($"{relative}: use unique language tags for Text columns.");
        }
        if (!result.languages.ContainsKey(config.FallbackLocale)) throw new InvalidDataException($"{relative}: missing fallback language {config.FallbackLocale}.");
        foreach (var row in source.Rows) result.messages.Add(row.Key, row.Cells.Select(value => value.GetString()!).ToArray());
        return result;
    }

    public string Resolve(string locale, string key, string? literal = null)
    {
        if (!messages.TryGetValue(key, out var cells)) return literal ?? key;
        string? Lookup(string tag)
        {
            while (true)
            {
                if (languages.TryGetValue(tag, out int index) && cells[index].Length > 0) return cells[index];
                int cut = tag.LastIndexOf('-');
                if (cut < 0) return null;
                tag = tag[..cut];
            }
        }
        return Lookup(locale) ?? Lookup(fallback) ?? literal ?? key;
    }
}
