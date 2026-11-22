using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Engine.Core;

public readonly record struct BundleReadSample(string Folder, long ArchiveBytes, int AssetCount, long InflatedBytes, TimeSpan ReadTime);

// Bundle paths mirror the project folders. The build manifest owns the asset GUIDs and hashes.
public sealed class ContentBundle : IDisposable
{
    private const string Catalog = "Catalog";
    private readonly DirectoryInfo? mounted;
    private readonly BundleMountCache? cache;
    public string Root { get; }
    public string CacheStatus { get; }
    public bool IsTemporary => mounted != null;

    private ContentBundle(string root, DirectoryInfo? mounted = null, BundleMountCache? cache = null, string status = "loose")
    {
        Root = root;
        this.mounted = mounted;
        this.cache = cache;
        CacheStatus = status;
    }

    internal static void ValidateFolders(string[] folders)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string folder in folders.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(folder) || folder.Split('/') is not ["Assets" or "Scenes", ..] parts
                || !parts.All(ProjectAssets.ValidName))
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
            if (!asset.Path.Split('/').All(ProjectAssets.ValidName) || !ProjectAssets.IsAsset(asset.Path) || !paths.Add(asset.Path))
                throw new InvalidDataException($"Invalid or duplicate asset in bundle {folder}: {asset.Path}");
            members.Add(asset.Path, asset);
        }
        if (members.Count == 0) throw new InvalidDataException($"Content bundle {folder} contains no exported assets.");
        return members;
    }

    internal static void Pack(string game, string[] folders, AssetEntry[] manifest,
        Action<string, Dictionary<string, AssetEntry>, string, Action<string>>? cook = null)
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
            void Write(string pathToZip)
            {
                using var zip = ZipFile.Open(pathToZip, ZipArchiveMode.Create);
                foreach (string path in members.Keys.Order(StringComparer.Ordinal))
                {
                    var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
                    entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using var output = entry.Open();
                    using var input = File.OpenRead(Path.Combine(game, path));
                    input.CopyTo(output);
                }
            }
            if (cook == null) Write(archive); else cook(folder, members, archive, Write);
            using var file = File.OpenRead(archive);
            catalog.Set(folder, Convert.ToHexString(SHA256.HashData(file)));
            foreach (string path in members.Keys) File.Delete(Path.Combine(game, path));
        }
        ProjectYaml.Write(Path.Combine(directory, Catalog), catalog);
    }

    public static ContentBundle Open(string game, GameSettings settings, GameMods? mods = null) =>
        Open(game, settings, mods, null);

    internal static ContentBundle Open(string game, GameSettings settings, GameMods? mods, Dictionary<Guid, byte[]>? updates,
        string? cacheDirectory = null, Action<string>? report = null, Action<BundleReadSample>? observe = null)
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
        var members = folders.ToDictionary(folder => folder, folder => Members(folder, manifest.Entries));
        string status = enabled.Length > 0 || updates != null ? "overlay" : "off";
        if (cacheDirectory != null && status != "overlay")
        {
            BundleMountCache? cache = null;
            try
            {
                cache = BundleMountCache.TryAcquire(cacheDirectory);
                if (cache != null)
                {
                    string key = BundleMountCache.Digest(Path.Combine(game, ProjectAssets.Manifest)) +
                        BundleMountCache.Digest(Path.Combine(directory, Catalog));
                    bool hit = cache.Matches(game, key, members.Values.SelectMany(m => m.Values));
                    if (!hit)
                    {
                        cache.Reset();
                        CopyLoose(game, cache.Mount.FullName);
                    }
                    ReadArchives(directory, catalog, members, hit ? null : cache.Mount.FullName, observe);
                    if (!hit) cache.Commit(key);
                    var result = new ContentBundle(cache.Mount.FullName, cache: cache, status: hit ? "hit" : "miss");
                    cache = null; // The running game owns the lease.
                    return result;
                }
                status = "busy";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                report?.Invoke($"Bundle cache unavailable, mounting privately: {ex.Message}");
                status = "unavailable";
            }
            finally { cache?.Dispose(); }
        }

        var mounted = Directory.CreateTempSubdirectory("monoon-content-");
        try
        {
            CopyLoose(game, mounted.FullName);
            ReadArchives(directory, catalog, members, mounted.FullName, observe);
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
            return new(mounted.FullName, mounted, status: status);
        }
        catch { mounted.Delete(true); throw; }
    }

    // A valid cached mount skips inflation, but the shipped archive must still exist
    // and match its catalog even on a hit. Extract and verify using the same open handle.
    private static void ReadArchives(string directory, GameData catalog,
        Dictionary<string, Dictionary<string, AssetEntry>> members, string? destination, Action<BundleReadSample>? observe)
    {
        foreach (var (folder, assets) in members)
        {
            long start = observe != null ? Stopwatch.GetTimestamp() : 0;
            using var file = File.OpenRead(Path.Combine(directory, folder + ".bundle"));
            if (Convert.ToHexString(SHA256.HashData(file)) != catalog.GetText(folder))
                throw new InvalidDataException($"Content bundle checksum mismatch: {folder}");
            long inflated = destination == null ? 0 : Extract(file, folder, assets, destination);
            observe?.Invoke(new(folder, file.Length, assets.Count, inflated, Stopwatch.GetElapsedTime(start)));
        }
    }

    private static long Extract(FileStream file, string folder, Dictionary<string, AssetEntry> assets, string destination)
    {
        long bytes = 0;
        file.Position = 0;
        using var zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        var expected = new Dictionary<string, AssetEntry>(assets);
        foreach (var entry in zip.Entries)
        {
            if (!expected.Remove(entry.FullName, out var asset))
                throw new InvalidDataException($"Unexpected file in {folder}.bundle: {entry.FullName}");
            string path = Path.Combine(destination, asset.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var input = entry.Open();
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            input.CopyTo(output);
            bytes += output.Length;
            output.Position = 0;
            if (Convert.ToHexString(SHA256.HashData(output)) != asset.Hash)
                throw new InvalidDataException($"Content bundle asset checksum mismatch: {asset.Path}");
        }
        if (expected.Count != 0) throw new InvalidDataException($"Missing file in {folder}.bundle: {expected.Keys.First()}");
        return bytes;
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
        foreach (string relative in Files(source))
        {
            string path = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(Path.Combine(source, relative), path);
        }
    }

    internal static IEnumerable<string> Files(string root)
    {
        return Walk(new DirectoryInfo(root));
        IEnumerable<string> Walk(DirectoryInfo directory)
        {
            if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException($"Unexpected directory link in game content: {directory.FullName}");
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new InvalidDataException($"Unexpected link in game content: {entry.FullName}");
                if (entry is DirectoryInfo folder)
                    foreach (var path in Walk(folder)) yield return path;
                else yield return Path.GetRelativePath(root, entry.FullName).Replace(Path.DirectorySeparatorChar, '/');
            }
        }
    }

    public void Dispose()
    {
        try { mounted?.Delete(true); }
        finally { cache?.Dispose(); }
    }
}
