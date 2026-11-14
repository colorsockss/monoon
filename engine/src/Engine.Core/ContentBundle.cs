using System.IO.Compression;
using System.Security.Cryptography;

namespace Engine.Core;

// Bundle paths mirror the project folders. The build manifest owns the asset GUIDs and hashes.
public sealed class ContentBundle : IDisposable
{
    private const string Catalog = "Catalog";
    private readonly DirectoryInfo? mounted;
    public string Root { get; }

    private ContentBundle(string root, DirectoryInfo? mounted = null)
    {
        Root = root;
        this.mounted = mounted;
    }

    internal static void ValidateFolders(string[] folders)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string folder in folders.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(folder) || folder.Split('/') is not ["Assets" or "Scenes", ..] parts
                || !parts.All(ProjectImport.ValidName))
                throw new InvalidDataException($"Expected an Assets or Scenes folder for the content bundle: '{folder}'.");
            if (!seen.Add(folder) || seen.Any(parent => folder.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Duplicate or overlapping content bundle folder: {folder}.");
        }
    }

    private static Dictionary<string, AssetEntry> Members(string folder, IEnumerable<AssetEntry> manifest)
    {
        var members = new Dictionary<string, AssetEntry>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in manifest.Where(a => a.Path.StartsWith(folder + "/", StringComparison.Ordinal)))
        {
            if (!asset.Path.Split('/').All(ProjectImport.ValidName) || !ProjectAssets.IsAsset(asset.Path) || !paths.Add(asset.Path))
                throw new InvalidDataException($"Invalid or duplicate asset in bundle {folder}: {asset.Path}");
            members.Add(asset.Path, asset);
        }
        if (members.Count == 0) throw new InvalidDataException($"Content bundle {folder} contains no exported assets.");
        return members;
    }

    internal static void Pack(string game, string[] folders, AssetEntry[] manifest)
    {
        if (folders.Length == 0) return;
        ValidateFolders(folders);
        string directory = BundleDirectory(game);
        var catalog = new GameData();
        foreach (string folder in folders.Order(StringComparer.Ordinal))
        {
            var members = Members(folder, manifest);
            string archive = Path.Combine(directory, folder + ".bundle");
            Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                foreach (string path in members.Keys.Order(StringComparer.Ordinal))
                {
                    var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
                    entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using var output = entry.Open();
                    using var input = File.OpenRead(Path.Combine(game, path));
                    input.CopyTo(output);
                }
            }
            using var file = File.OpenRead(archive);
            catalog.Set(folder, Convert.ToHexString(SHA256.HashData(file)));
            foreach (string path in members.Keys) File.Delete(Path.Combine(game, path));
        }
        ProjectYaml.Write(Path.Combine(directory, Catalog), catalog);
    }

    public static ContentBundle Open(string game, GameSettings settings, GameMods? mods = null) =>
        Open(game, settings, mods, null);

    internal static ContentBundle Open(string game, GameSettings settings, GameMods? mods, Dictionary<Guid, byte[]>? updates)
    {
        var enabled = mods?.Enabled() ?? [];
        var folders = settings.ContentBundles ?? [];
        if (folders.Length == 0 && enabled.Length == 0) return new(game);
        ValidateFolders(folders);
        string directory = BundleDirectory(game);
        var catalog = folders.Length == 0 ? new GameData() : GameData.Load(Path.Combine(directory, Catalog));
        if (catalog.Version != 1 || !catalog.Values.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(folders))
            throw new InvalidDataException("Content bundle catalog does not match game settings.");

        var manifest = ProjectAssets.ReadManifest(game);
        var mounted = Directory.CreateTempSubdirectory("monoon-content-");
        try
        {
            // Existing image, audio and scene readers need real paths. Each player owns its mount.
            CopyLoose(game, mounted.FullName);
            foreach (string folder in folders)
            {
                string archive = Path.Combine(directory, folder + ".bundle");
                using var file = File.OpenRead(archive);
                if (Convert.ToHexString(SHA256.HashData(file)) != catalog.GetText(folder))
                    throw new InvalidDataException($"Content bundle checksum mismatch: {folder}");
                file.Position = 0;
                using var zip = new ZipArchive(file, ZipArchiveMode.Read);
                var expected = Members(folder, manifest.Entries);
                foreach (var entry in zip.Entries)
                {
                    if (!expected.Remove(entry.FullName, out var asset))
                        throw new InvalidDataException($"Unexpected file in {folder}.bundle: {entry.FullName}");
                    string path = Path.Combine(mounted.FullName, asset.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    using var input = entry.Open();
                    using var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                    input.CopyTo(output);
                    output.Position = 0;
                    if (Convert.ToHexString(SHA256.HashData(output)) != asset.Hash)
                        throw new InvalidDataException($"Content bundle asset checksum mismatch: {asset.Path}");
                }
                if (expected.Count != 0) throw new InvalidDataException($"Missing file in {folder}.bundle: {expected.Keys.First()}");
            }
            if (updates != null)
            {
                WriteOverrides(mounted.FullName, manifest, updates);
                manifest = ProjectAssets.ReadManifest(mounted.FullName);
            }
            if (enabled.Length != 0)
            {
                var overrides = mods!.ReadOverrides(enabled, manifest);
                WriteOverrides(mounted.FullName, manifest, overrides);
            }
            return new(mounted.FullName, mounted);
        }
        catch { mounted.Delete(true); throw; }
    }

    private static void WriteOverrides(string root, ProjectAssets manifest, Dictionary<Guid, byte[]> overrides)
    {
        var entries = manifest.Entries.ToArray();
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (!overrides.TryGetValue(entry.Id, out var bytes)) continue;
            string path = Path.Combine(root, entry.Path);
            using (var original = File.OpenRead(path))
                if (Convert.ToHexString(SHA256.HashData(original)) != entry.Hash)
                    throw new InvalidDataException($"Original asset checksum mismatch: {entry.Path}");
            ProjectYaml.WriteBytes(path, bytes);
            entries[i] = entry with { Hash = Convert.ToHexString(SHA256.HashData(bytes)) };
        }
        ProjectYaml.Write(Path.Combine(root, ProjectAssets.Manifest), entries);
    }

    private static string BundleDirectory(string game) =>
        Path.Combine(Directory.GetParent(Path.GetFullPath(game))!.FullName, "ContentBundles");

    private static void CopyLoose(string source, string target)
    {
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException($"Unexpected link in built game content: {entry.FullName}");
            string path = Path.Combine(target, entry.Name);
            if (entry is DirectoryInfo folder) CopyLoose(folder.FullName, Directory.CreateDirectory(path).FullName);
            else File.Copy(entry.FullName, path);
        }
    }

    public void Dispose() => mounted?.Delete(true);
}
