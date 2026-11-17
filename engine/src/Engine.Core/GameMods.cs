using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
internal sealed partial record ModAsset(Guid Asset, GameData? Data = null, GameTable? Table = null);

[YamlObject]
internal sealed partial record ModDocument(int Version, Guid GameId, string Name, ModAsset[] Replacements);

public sealed class GameMods
{
    private readonly string game;
    private readonly Guid projectId;
    private readonly string bundled;
    public string UserDirectory { get; }
    private string SelectionFile => Path.Combine(UserDirectory, "Enabled");

    internal GameMods(string game, Guid projectId, string profile)
    {
        this.game = game;
        this.projectId = projectId;
        bundled = Path.Combine(Directory.GetParent(game)!.FullName, "Mods");
        UserDirectory = Path.Combine(profile, "Mods");
    }

    public static GameMods ForPlayer(string game, GameSettings settings)
    {
        var id = settings.ProjectId ?? settings.StartScene ?? throw new InvalidDataException("Missing game ID.");
        return new(game, id, PlayerStorage.ForGame(id, false).DirectoryPath);
    }

    private static void ValidateId(string id)
    {
        if (!ProjectAssets.ValidName(id) || !id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '_'))
            throw new InvalidDataException($"Invalid mod ID '{id}'. Use lowercase letters, digits, hyphens or underscores.");
    }

    private static IEnumerable<string> Files(string folder) => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder, "*.mod").Order(StringComparer.Ordinal)
        : [];

    private string Find(string id)
    {
        ValidateId(id);
        string file = id + ".mod";
        string user = Path.Combine(UserDirectory, file), included = Path.Combine(bundled, file);
        if (File.Exists(user) && File.Exists(included)) throw new InvalidDataException($"Duplicate mod ID: {id}");
        if (File.Exists(user)) return user;
        if (File.Exists(included)) return included;
        throw new FileNotFoundException($"Missing mod: {id}");
    }

    internal string[] Enabled()
    {
        if (!File.Exists(SelectionFile)) return [];
        var ids = ProjectYaml.Read<string[]>(SelectionFile);
        foreach (var id in ids) ValidateId(id);
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new InvalidDataException($"Duplicate mod in {SelectionFile}.");
        return ids;
    }

    public IEnumerable<string> List()
    {
        var enabled = Enabled().ToHashSet(StringComparer.Ordinal);
        var ids = Files(bundled).Concat(Files(UserDirectory)).Select(f => Path.GetFileNameWithoutExtension(f))
            .Concat(enabled).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            string status = enabled.Contains(id) ? "on" : "off";
            if (!File.Exists(Path.Combine(bundled, id + ".mod")) && !File.Exists(Path.Combine(UserDirectory, id + ".mod")))
                yield return $"{status}  {id}  (missing)";
            else
                yield return $"{status}  {id}  {Read(Find(id), projectId).Name}";
        }
    }

    public void SetEnabled(string id, bool enable)
    {
        ValidateId(id);
        Directory.CreateDirectory(UserDirectory);
        using var writer = new FileStream(Path.Combine(UserDirectory, ".writer.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var ids = Enabled().ToList();
        if (enable)
        {
            if (!ids.Contains(id)) ids.Add(id);
            ReadOverrides(ids, ProjectAssets.ReadManifest(game));
        }
        else if (!ids.Remove(id)) return;
        ProjectYaml.Write(SelectionFile, ids.ToArray());
    }

    private static ModDocument Read(string file, Guid expectedGame)
    {
        var mod = ProjectYaml.Read<ModDocument>(file);
        if (mod.Version != 1 || mod.GameId != expectedGame || string.IsNullOrWhiteSpace(mod.Name) || mod.Replacements is not { Length: > 0 })
            throw new InvalidDataException($"{file}: expected a version 1 mod for {expectedGame} with a name and replacements.");
        return mod;
    }

    internal Dictionary<Guid, byte[]> ReadOverrides(IEnumerable<string> ids, ProjectAssets manifest)
    {
        var changes = new Dictionary<Guid, byte[]>();
        foreach (var id in ids)
            AddOverrides(Find(id), projectId, manifest.Entries, changes);
        return changes;
    }

    private static void AddOverrides(string file, Guid gameId, IReadOnlyList<AssetEntry> manifest, Dictionary<Guid, byte[]> changes)
    {
        foreach (var change in Read(file, gameId).Replacements)
        {
            var entry = manifest.FirstOrDefault(e => e.Id == change?.Asset)
                ?? throw new InvalidDataException($"{file}: unknown exported asset {change?.Asset}.");
            if (!entry.Path.Split('/').All(ProjectAssets.ValidName))
                throw new InvalidDataException($"Invalid mod target in game manifest: {entry.Path}");
            byte[] content = (Path.GetExtension(entry.Path), change.Data, change.Table) switch
            {
                (ProjectAssets.Data, {} data, null) => ProjectYaml.Encode(data.Validate()),
                (GameTable.Extension, null, {} table) => ProjectYaml.Encode(table.Validate()),
                _ => throw new InvalidDataException($"{file}: supply a matching data or table replacement for {entry.Path}.")
            };
            if (!changes.TryAdd(entry.Id, content))
                throw new InvalidDataException($"{file}: another replacement already targets {entry.Path}.");
        }
    }

    internal static void Export(string project, string bin, Guid gameId, AssetEntry[] manifest)
    {
        foreach (string source in Files(Path.Combine(project, "Mods")))
        {
            ValidateId(Path.GetFileNameWithoutExtension(source));
            string destination = Path.Combine(Directory.CreateDirectory(Path.Combine(bin, "Mods")).FullName, Path.GetFileName(source));
            File.Copy(source, destination);
            AddOverrides(destination, gameId, manifest, []);
        }
    }
}
