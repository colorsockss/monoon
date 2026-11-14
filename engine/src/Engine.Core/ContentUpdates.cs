using System.IO.Compression;
using System.Security.Cryptography;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
internal sealed partial record UpdateCatalog(int Version, Guid GameId, string BaseManifest, AssetEntry[] Assets);

// Each update is a complete delta against the shipped manifest, shared by compatible player builds.
public sealed class ContentUpdates
{
    private const string Catalog = "Update";
    private const int MaxAsset = 16 * 1024 * 1024, MaxTotal = 64 * 1024 * 1024, MaxCatalog = 1024 * 1024;
    private readonly string game;
    private readonly GameSettings settings;
    private readonly Guid gameId;
    private readonly string active;

    public ContentUpdates(string game, GameSettings settings)
    {
        this.game = game;
        this.settings = settings;
        gameId = settings.ProjectId ?? settings.StartScene ?? throw new InvalidDataException("Missing game ID.");
        active = Path.Combine(PlayerStorage.ForGame(gameId, false).DirectoryPath, "ContentUpdates", "Active.bundle");
    }

    private string BaseHash()
    {
        using var file = File.OpenRead(Path.Combine(game, ProjectAssets.Manifest));
        return Convert.ToHexString(SHA256.HashData(file));
    }

    private Dictionary<string, AssetEntry> Targets()
    {
        var folders = settings.ContentBundles ?? [];
        ContentBundle.ValidateFolders(folders);
        return ProjectAssets.ReadManifest(game).Entries.Where(e =>
            Path.GetExtension(e.Path) is ProjectAssets.Data or GameTable.Extension
            && e.Path.Split('/').All(ProjectImport.ValidName)
            && folders.Any(f => e.Path.StartsWith(f + "/", StringComparison.Ordinal)))
            .ToDictionary(e => e.Path, StringComparer.Ordinal);
    }

    // Publishing reads only the exported baseline and the replacement documents.
    public string Publish(string replacements, string destination)
    {
        var targets = Targets();
        var changes = new List<AssetEntry>();
        long total = 0;
        destination = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string stage = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var files = Directory.GetFiles(replacements, "*", new EnumerationOptions {
                RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden,
                IgnoreInaccessible = false
            }).Order(StringComparer.Ordinal).ToArray();
            using (var zip = ZipFile.Open(stage, ZipArchiveMode.Create))
            {
                foreach (string file in files)
                {
                    string path = Path.GetRelativePath(replacements, file).Replace(Path.DirectorySeparatorChar, '/');
                    if (!targets.TryGetValue(path, out var target))
                        throw new InvalidDataException($"Expected a bundled .data or .table asset from this build: {path}");
                    using var input = File.OpenRead(file);
                    if (input.Length > MaxAsset || (total += input.Length) > MaxTotal)
                        throw new InvalidDataException("Update documents exceed the size limit.");
                    byte[] bytes = ReadBytes(input, input.Length);
                    ValidateDocument(path, bytes);
                    string hash = Convert.ToHexString(SHA256.HashData(bytes));
                    if (hash == target.Hash) continue;
                    changes.Add(target with { Hash = hash });
                    WriteEntry(zip, path, bytes);
                }
                if (changes.Count == 0) throw new InvalidDataException("No changed bundled documents.");
                byte[] catalog = ProjectYaml.Encode(new UpdateCatalog(1, gameId, BaseHash(), changes.ToArray()));
                if (catalog.Length > MaxCatalog) throw new InvalidDataException("Update catalog exceeds the size limit.");
                WriteEntry(zip, Catalog, catalog);
            }
            using var output = File.OpenRead(stage);
            string digest = Convert.ToHexString(SHA256.HashData(output));
            output.Close();
            File.Move(stage, destination, true);
            return digest;
        }
        finally { File.Delete(stage); }
    }

    private static void WriteEntry(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var output = entry.Open();
        output.Write(bytes);
    }

    private static byte[] ReadBytes(Stream input, long length)
    {
        var bytes = new byte[checked((int)length)];
        input.ReadExactly(bytes);
        if (input.ReadByte() != -1) throw new InvalidDataException("Update entry length mismatch.");
        return bytes;
    }

    private static void ValidateDocument(string path, byte[] bytes)
    {
        if (Path.GetExtension(path) == ProjectAssets.Data) ProjectYaml.Decode<GameData>(bytes).Validate();
        else ProjectYaml.Decode<GameTable>(bytes).Validate();
    }

    private Dictionary<Guid, byte[]> ReadChanges(FileStream package)
    {
        using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        var header = zip.GetEntry(Catalog);
        if (header == null || header.Length > MaxCatalog) throw new InvalidDataException("Missing or oversized update catalog.");
        UpdateCatalog catalog;
        using (var stream = header.Open()) catalog = ProjectYaml.Decode<UpdateCatalog>(ReadBytes(stream, header.Length));
        if (catalog.Version != 1 || catalog.GameId != gameId || catalog.BaseManifest != BaseHash())
            throw new InvalidDataException("Content update does not match this game's shipped manifest.");
        if (catalog.Assets is not { Length: > 0 } || zip.Entries.Count != catalog.Assets.Length + 1)
            throw new InvalidDataException("Content update entry count mismatch.");

        var targets = Targets();
        var changes = new Dictionary<Guid, byte[]>();
        long total = 0;
        foreach (var asset in catalog.Assets)
        {
            if (asset?.Path == null || !targets.Remove(asset.Path, out var target) || asset.Id != target.Id)
                throw new InvalidDataException($"Unknown or repeated update target: {asset?.Path}");
            var entry = zip.GetEntry(asset.Path);
            if (entry == null || entry.Length > MaxAsset || (total += entry.Length) > MaxTotal)
                throw new InvalidDataException($"Missing or oversized update entry: {asset.Path}");
            using var input = entry.Open();
            byte[] bytes = ReadBytes(input, entry.Length);
            if (Convert.ToHexString(SHA256.HashData(bytes)) != asset.Hash)
                throw new InvalidDataException($"Content update checksum mismatch: {asset.Path}");
            ValidateDocument(asset.Path, bytes);
            changes.Add(asset.Id, bytes);
        }
        return changes;
    }

    // The rename is the commit point. Existing sessions keep their independent mounted files.
    public void Apply(string source, string expectedHash, Action<string> checkStartScene)
    {
        if (expectedHash.Length != 64 || !expectedHash.All(char.IsAsciiHexDigit))
            throw new InvalidDataException("Expected the publisher's SHA-256 (64 hex digits).");
        string directory = Directory.CreateDirectory(Path.GetDirectoryName(active)!).FullName;
        string stage = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var input = File.OpenRead(source))
            using (var output = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (input.Length > MaxTotal + MaxCatalog * 2) throw new InvalidDataException("Update package exceeds the size limit.");
                input.CopyTo(output);
                output.Flush(flushToDisk: true);
            }
            using (var file = File.OpenRead(stage))
            {
                if (!Convert.ToHexString(SHA256.HashData(file)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Content update package SHA-256 mismatch.");
                file.Position = 0;
                var changes = ReadChanges(file);
                using var mounted = ContentBundle.Open(game, settings, null, changes);
                checkStartScene(mounted.Root);
            }
            File.Move(stage, active, true);
        }
        finally { File.Delete(stage); }
    }

    public ContentBundle Open(GameMods? mods, Action<string> report)
    {
        Dictionary<Guid, byte[]>? changes = null;
        try
        {
            using var file = new FileStream(active, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            changes = ReadChanges(file);
        }
        catch (FileNotFoundException ex) when (ex.FileName == active) { }
        catch (DirectoryNotFoundException) when (!Directory.Exists(Path.GetDirectoryName(active))) { }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or VYaml.YamlException or FormatException or ArgumentException or OverflowException)
        {
            report($"Content update unavailable; using shipped content: {ex.Message}");
        }
        return ContentBundle.Open(game, settings, mods, changes);
    }

    public void Reset()
    {
        if (Directory.Exists(Path.GetDirectoryName(active))) File.Delete(active);
    }
}
