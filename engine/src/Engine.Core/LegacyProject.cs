using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Engine.Core;

// Editor-only import boundary. Players only read current project names.
public static class LegacyProject
{
    internal static bool IsDocument(string path) => Path.GetExtension(path) is ".yaml" or ".json"
        && ProjectAssets.IsDocument(Path.ChangeExtension(path, null));
    internal static bool IsDocumentSidecar(string path) => path.EndsWith(".meta", StringComparison.Ordinal) && IsDocument(path[..^5]);
    private static string NewName(string path) => IsDocument(path) ? Path.ChangeExtension(path, null) : path;
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public static void Upgrade(string root) => Upgrade(root, null);

    internal static void Upgrade(string root, Action<IEnumerable<string>>? backup)
    {
        root = Path.GetFullPath(root);
        if (File.Exists(Path.Combine(root, ".monoon-player"))) return;
        var files = ProjectAssets.Enumerate(root, root, includeLegacy: true).Order(StringComparer.Ordinal).ToArray();
        var writes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var remove = new List<string>();
        var inPlace = new HashSet<string>(StringComparer.Ordinal);
        var yamlManifest = Path.Combine(root, ProjectAssets.Manifest + ".yaml");
        var jsonManifest = Path.Combine(root, ProjectAssets.Manifest + ".json");
        if (File.Exists(yamlManifest) && File.Exists(jsonManifest))
            throw new IOException($"Cannot migrate: both {yamlManifest} and {jsonManifest} exist.");
        var oldManifest = File.Exists(yamlManifest) ? yamlManifest : jsonManifest;
        var manifest = File.Exists(yamlManifest) ? ProjectYaml.Read<AssetEntry[]>(yamlManifest)
            : File.Exists(jsonManifest) ? JsonSerializer.Deserialize(File.ReadAllBytes(jsonManifest), GameJson.Default.AssetEntryArray)
                ?? throw new InvalidDataException($"Invalid {jsonManifest}.") : [];
        var entries = manifest.ToDictionary(e => e.Path, StringComparer.Ordinal);
        var scripts = new Dictionary<string, string>();
        var documents = new Dictionary<string, SceneDocument>();
        var prefabRevisions = new Dictionary<Guid, (string Before, string After)>();

        void ConvertFile(string source, string target, byte[] bytes)
        {
            if (!writes.TryAdd(target, bytes) && !writes[target].AsSpan().SequenceEqual(bytes))
                throw new InvalidDataException($"Conflicting migration targets: {target}");
            if (source != target) remove.Add(source);
        }

        Guid? Identity(string relative)
        {
            var sidecar = Path.Combine(root, relative + ".meta");
            if (!File.Exists(sidecar)) return entries.GetValueOrDefault(relative)?.Id;
            var id = Guid.TryParse(File.ReadAllText(sidecar).Trim(), out var bare) ? bare : ProjectYaml.Read<AssetMeta>(sidecar).Id;
            if (id == Guid.Empty) throw new InvalidDataException($"Empty asset ID: {sidecar}");
            return id;
        }

        void MigrateMeta(string source, string target)
        {
            if (!File.Exists(source)) return;
            if (Guid.TryParse(File.ReadAllText(source).Trim(), out var bare))
            {
                if (bare == Guid.Empty) throw new InvalidDataException($"Empty asset ID: {source}");
                if (source == target) inPlace.Add(target);
                ConvertFile(source, target, ProjectYaml.Encode(new AssetMeta(bare)));
            }
            else if (source != target)
            {
                var bytes = File.ReadAllBytes(source);
                if (ProjectYaml.Decode<AssetMeta>(bytes).Id == Guid.Empty) throw new InvalidDataException($"Empty asset ID: {source}");
                ConvertFile(source, target, bytes);
            }
        }

        foreach (var relative in files)
        {
            var path = Path.Combine(root, relative);
            var target = Path.Combine(root, NewName(relative));
            if (IsDocumentSidecar(relative))
            {
                // The asset may be temporarily absent. Its identity still moves with its name.
                MigrateMeta(path, Path.Combine(root, NewName(relative[..^5]) + ".meta"));
                continue;
            }
            if (IsDocument(relative) && relative.EndsWith(".yaml", StringComparison.Ordinal))
                ConvertFile(path, target, File.ReadAllBytes(path)); // Keep user formatting and prefab revision hashes.
            else if (IsDocument(relative))
            {
                if (relative.EndsWith(".clip.json"))
                {
                    var clip = JsonSerializer.Deserialize(File.ReadAllBytes(path), GameJson.Default.SpriteClip)
                        ?? throw new InvalidDataException($"Invalid clip: {path}");
                    clip.Validate(path);
                    ConvertFile(path, target, ProjectYaml.Encode(clip));
                }
                else
                {
                    var node = JsonNode.Parse(File.ReadAllBytes(path), new JsonNodeOptions { PropertyNameCaseInsensitive = true }) as JsonObject
                        ?? throw new InvalidDataException($"Invalid scene or prefab: {path}");
                    documents.Add(relative, SceneDocument.ImportLegacy(node, root, scripts));
                }
            }

            if (path == target) MigrateMeta(path + ".meta", target + ".meta");
        }

        // ExportPrefab writes standalone templates. Merely changing their encoding must
        // not trigger a prefab refresh that would discard an instance's local edits.
        foreach (var (relative, doc) in documents.OrderBy(p => !p.Key.EndsWith(".prefab.json")))
        {
            foreach (var obj in doc.Objects)
                if (obj.Prefab is { } link && prefabRevisions.TryGetValue(link.AssetId, out var rev) && link.Revision == rev.Before)
                    obj.Prefab = link with { Revision = rev.After };
            var data = ProjectYaml.Encode(doc);
            var source = Path.Combine(root, relative);
            ConvertFile(source, Path.Combine(root, NewName(relative)), data);
            if (relative.EndsWith(".prefab.json") && Identity(relative) is Guid prefabId)
                prefabRevisions.Add(prefabId, (Hash(File.ReadAllBytes(source)), Hash(data)));
        }
        foreach (var (name, source) in scripts)
            writes.Add(new ProjectScripts(root).SourcePath(name), Encoding.UTF8.GetBytes(source));

        var yamlSettings = Path.Combine(root, GameSettings.FileName + ".yaml");
        if (File.Exists(yamlSettings))
            ConvertFile(yamlSettings, Path.Combine(root, GameSettings.FileName), File.ReadAllBytes(yamlSettings));
        var oldSettings = Path.Combine(root, GameSettings.FileName + ".json");
        if (File.Exists(oldSettings))
        {
            var settings = JsonSerializer.Deserialize(File.ReadAllBytes(oldSettings), GameJson.Default.GameSettings)
                ?? throw new InvalidDataException($"Invalid settings: {oldSettings}");
            settings.Validate();
            ConvertFile(oldSettings, Path.Combine(root, GameSettings.FileName), ProjectYaml.Encode(settings));
        }
        if (File.Exists(oldManifest))
        {
            var updated = manifest.Select(e => {
                var name = NewName(e.Path);
                return e with { Path = name, Hash = writes.TryGetValue(Path.Combine(root, name), out var data) ? Hash(data) : e.Hash };
            }).OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
            if (updated.DistinctBy(e => e.Path, StringComparer.Ordinal).Count() != updated.Length)
                throw new InvalidDataException($"Conflicting asset paths in {oldManifest}.");
            ConvertFile(oldManifest, Path.Combine(root, ProjectAssets.Manifest), ProjectYaml.Encode(updated));
        }

        // Check every destination before touching originals. Identical output is safe
        // to reuse if the process was interrupted during a previous conversion.
        var pending = writes.Where(p => !File.Exists(p.Key) || !File.ReadAllBytes(p.Key).AsSpan().SequenceEqual(p.Value)).ToArray();
        foreach (var (path, _) in pending)
            if (Directory.Exists(path) || File.Exists(path) && !inPlace.Contains(path))
                throw new IOException($"Cannot migrate: destination already exists: {path}");
        bool backedUp = pending.Length != 0 || remove.Count != 0;
        if (backedUp)
            backup?.Invoke(pending.Select(p => p.Key).Concat(remove).Append(Path.Combine(root, GameSettings.FileName)));
        foreach (var (path, bytes) in pending)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            ProjectYaml.WriteBytes(path, bytes, overwrite: inPlace.Contains(path));
        }
        foreach (var path in remove) File.Delete(path);
        if (Directory.Exists(Path.Combine(root, "Scenes")))
        {
            var settingsPath = Path.Combine(root, GameSettings.FileName);
            var current = File.Exists(settingsPath) ? ProjectYaml.Read<GameSettings>(settingsPath) : new GameSettings();
            if (current.ProjectId == null)
            {
                if (!backedUp && File.Exists(settingsPath)) backup?.Invoke([settingsPath]);
                (current with { ProjectId = Guid.NewGuid() }).Save(root);
            }
        }
    }
}
