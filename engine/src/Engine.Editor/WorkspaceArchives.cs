using System.IO.Compression;
using Engine.Core;
using VYaml.Annotations;

namespace Engine.Editor;

[YamlObject]
internal sealed partial record ArchiveManifest(int Version, string Kind, string Name);
[YamlObject]
internal sealed partial record EditorCommands(int Version, EditorCommand[] Commands);
[YamlObject]
internal sealed partial record EditorCommand(string Label, string Prefab);

internal static class WorkspaceArchives
{
    internal const string Template = "template", Package = "package";
    private const string Manifest = "MoNoonArchive";
    private static bool PackedImage(string path)
    {
        const string prefix = ".monoon-atlas/";
        return path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(".png", StringComparison.Ordinal) &&
            path.Length == prefix.Length + 64 + 4 && path[prefix.Length..^4].All(char.IsAsciiHexDigit);
    }

    private static bool Allowed(string relative, string kind)
    {
        if (PackedImage(relative)) return true;
        var parts = relative.Split('/');
        if (!parts.All(ProjectImport.ValidName)) return false;
        if (parts.Length == 1 && relative == GameSettings.FileName) return kind == Template;
        string source = relative.EndsWith(".meta", StringComparison.Ordinal) ? relative[..^5] : relative;
        if (parts.Any(ProjectScripts.ReservedFolder)) return false;
        if (relative.EndsWith(".license", StringComparison.Ordinal)) return GameLocalization.IsFont(relative[..^8]);
        if (source.EndsWith(".cs", StringComparison.Ordinal)) return source != "Program.cs";
        if (source.EndsWith(ProjectAssets.Scene)) return parts[0] == "Scenes";
        return ProjectAssets.IsAsset(source) || parts[0] == "Editor" && relative.EndsWith(".tools", StringComparison.Ordinal);
    }

    internal static IEnumerable<EditorCommand> Commands(string root)
    {
        var folder = Path.Combine(root, "Editor");
        if (!Directory.Exists(folder)) yield break;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(folder, "*.tools", Walk).Order(StringComparer.Ordinal))
        {
            var doc = ProjectYaml.Read<EditorCommands>(path);
            if (doc.Version != 1 || doc.Commands == null) throw new InvalidDataException($"Unsupported editor extension: {path}");
            foreach (var command in doc.Commands)
            {
                if (command == null || string.IsNullOrWhiteSpace(command.Label) || !seen.Add(command.Label) ||
                    string.IsNullOrEmpty(command.Prefab) || !command.Prefab.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !command.Prefab.EndsWith(ProjectAssets.Prefab, StringComparison.Ordinal) || !Allowed(command.Prefab, Package))
                    throw new InvalidDataException($"Invalid or duplicate editor command in {path}.");
                yield return command;
            }
        }
    }

    private static readonly EnumerationOptions Walk = new() {
        RecurseSubdirectories = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.ReparsePoint, IgnoreInaccessible = false
    };

    internal static string[] PackageFiles(string root)
    {
        var folder = Path.Combine(root, "Editor");
        var extensions = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*.tools", Walk).Select(p => Path.GetRelativePath(root, p).Replace(Path.DirectorySeparatorChar, '/'))
            : Enumerable.Empty<string>();
        return ProjectCatalog.Files(root).Concat(extensions).Where(p => Allowed(p, Package)).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static IReadOnlySet<string> Dependencies(string root, IEnumerable<string> selected)
    {
        var catalog = ProjectCatalog.Inspect(root);
        var pending = new Queue<string>(selected.Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/')));
        var result = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var path))
        {
            if (!Allowed(path, Package) || path.EndsWith(".meta") || !File.Exists(Path.Combine(root, path)))
                throw new InvalidDataException($"Choose an asset, script or .tools file inside this project: {path}");
            if (!result.Add(path)) continue;
            if (ProjectAssets.IsAsset(path) && !PackedImage(path)) result.Add(path + ".meta");
            if (GameLocalization.IsFont(path) && File.Exists(Path.Combine(root, path + ".license"))) result.Add(path + ".license");
            if (path.EndsWith(".tools", StringComparison.Ordinal))
            {
                var extension = ProjectYaml.Read<EditorCommands>(Path.Combine(root, path));
                foreach (var command in extension.Commands) pending.Enqueue(command.Prefab);
            }
            if (path.EndsWith(".cs", StringComparison.Ordinal))
            {
                // Shared C# types can be referenced without a serialized asset link.
                foreach (var script in new ProjectScripts(root).Locations().Values)
                {
                    result.Add(script);
                    if (File.Exists(Path.Combine(root, script + ".meta"))) result.Add(script + ".meta");
                }
            }
            foreach (var link in catalog.Uses(path))
            {
                if (link.Problem != null) throw new InvalidDataException($"{path}: {link.Problem} ({link.Target})");
                pending.Enqueue(link.Target);
            }
        }
        return result;
    }

    internal static void Export(string root, string destination, string kind, IReadOnlySet<string>? selection = null)
    {
        ProjectAssets.Scan(root); // Include the stable sidecars for every document and sprite.
        var temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                using (var manifest = zip.CreateEntry(Manifest).Open())
                    manifest.Write(ProjectYaml.Encode(new ArchiveManifest(1, kind, Path.GetFileName(root))));
                var packedImages = new HashSet<string>(StringComparer.Ordinal);
                foreach (var relative in PackageFiles(root))
                {
                    if (selection != null && !selection.Contains(relative)) continue;
                    var path = Path.Combine(root, relative);
                    zip.CreateEntryFromFile(path, relative);
                    if (File.Exists(path + ".meta")) zip.CreateEntryFromFile(path + ".meta", relative + ".meta");
                    if (GameLocalization.IsFont(relative) && File.Exists(path + ".license"))
                        zip.CreateEntryFromFile(path + ".license", relative + ".license");
                    if (relative.EndsWith(SpriteAtlas.Extension)) packedImages.Add(SpriteAtlas.Load(path).ImagePath());
                }
                foreach (var image in packedImages)
                    zip.CreateEntryFromFile(Path.Combine(root, image), image);
                if (kind == Template) zip.CreateEntryFromFile(Path.Combine(root, GameSettings.FileName), GameSettings.FileName);
            }
            File.Move(temp, destination, true);
        }
        finally { File.Delete(temp); }
    }

    internal static string[] Install(string archive, string root, string kind)
    {
        if (kind == Template && Directory.EnumerateFileSystemEntries(root).Any(p => Path.GetFileName(p) != ".DS_Store"))
            throw new IOException("Choose an empty folder for the project.");
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > 20000) throw new InvalidDataException("Too many files in archive.");
        var header = zip.GetEntry(Manifest) ?? throw new InvalidDataException("Missing MoNoon archive manifest.");
        if (header.Length > 16384) throw new InvalidDataException("Invalid archive manifest.");
        using var bytes = new MemoryStream();
        using (var input = header.Open()) input.CopyTo(bytes);
        var info = ProjectYaml.Decode<ArchiveManifest>(bytes.ToArray());
        if (info.Version != 1 || info.Kind != kind) throw new InvalidDataException("Wrong archive type or version.");

        var stage = Directory.CreateDirectory(Path.Combine(root, ".monoon-install-" + Guid.NewGuid().ToString("N"))).FullName;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Manifest };
        var added = new List<string>();
        var createdFolders = new Stack<string>();
        void CreateFolders(string directory)
        {
            if (Directory.Exists(directory)) return;
            CreateFolders(Path.GetDirectoryName(directory)!);
            Directory.CreateDirectory(directory);
            createdFolders.Push(directory);
        }
        var pending = new List<(string Staged, string Target)>();
        long size = 0;
        try
        {
            foreach (var entry in zip.Entries.Where(e => e != header))
            {
                var relative = entry.FullName;
                if (!Allowed(relative, kind) || !names.Add(relative)) throw new InvalidDataException($"Invalid archive path: {relative}");
                size = checked(size + entry.Length);
                if (size > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("Archive exceeds 2 GiB.");
                string target = root;
                foreach (var part in relative.Split('/'))
                {
                    target = Path.Combine(target, part);
                    if (new FileInfo(target).LinkTarget != null) throw new IOException($"Cannot install through a link: {relative}");
                }
                var staged = Path.Combine(stage, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                entry.ExtractToFile(staged);
                if (File.Exists(target))
                {
                    if (!Identical(target, staged)) throw new IOException($"A different file already exists: {relative}");
                }
                else pending.Add((staged, target));
            }
            if (kind == Template)
            {
                if (!names.Contains(GameSettings.FileName) || !names.Any(p => p.StartsWith("Scenes/") && p.EndsWith(ProjectAssets.Scene)))
                    throw new InvalidDataException("A project template needs settings and at least one scene.");
                var settings = GameSettings.Load(stage) with { ProjectId = Guid.NewGuid() };
                settings.Save(stage);
            }
            var existing = (kind == Template ? ProjectAssets.ReadManifest(root) : ProjectAssets.Scan(root)).Entries.ToDictionary(e => e.Id, e => e.Path);
            foreach (string file in names.Where(n => n.EndsWith(".meta", StringComparison.Ordinal)))
            {
                string owner = file[..^5];
                if (owner.EndsWith(".cs", StringComparison.Ordinal))
                {
                    if (!names.Contains(owner)) throw new InvalidDataException($"Missing script for {file}.");
                    continue; // ScriptLocation is validated by ProjectScripts below.
                }
                var id = ProjectYaml.Read<AssetMeta>(Path.Combine(stage, file)).Id;
                if (!names.Contains(owner) || id == Guid.Empty || existing.TryGetValue(id, out var other) && other != owner)
                    throw new InvalidDataException($"Conflicting asset identity: {file}");
                existing[id] = owner;
            }
            foreach (var asset in names.Where(n => ProjectAssets.IsAsset(n) && !PackedImage(n)))
                if (!names.Contains(asset + ".meta")) throw new InvalidDataException($"Missing asset identity: {asset}");
            foreach (var license in names.Where(n => n.EndsWith(".license", StringComparison.Ordinal)))
                if (!names.Contains(license[..^8])) throw new InvalidDataException($"Missing font for {license}.");
            foreach (var atlas in names.Where(n => n.EndsWith(SpriteAtlas.Extension)))
            {
                var packed = SpriteAtlas.Load(Path.Combine(stage, atlas)).ImagePath();
                if (!names.Contains(packed)) throw new InvalidDataException($"Missing packed image: {packed}");
            }
            foreach (var source in names.Where(n => n.EndsWith(ProjectAssets.Scene) || n.EndsWith(ProjectAssets.Prefab)))
                SceneDocument.ReadAsset(Path.Combine(stage, source));
            foreach (var source in names.Where(n => n.EndsWith(".cs", StringComparison.Ordinal) && !n.StartsWith("Scripts/", StringComparison.Ordinal)))
                if (!names.Contains(source + ".meta")) throw new InvalidDataException($"Missing script metadata: {source}");
            _ = new ProjectScripts(stage).Locations();
            // Validate the declared menu actions and resolve their assets before publishing.
            var commands = Commands(stage).ToArray();
            foreach (var command in commands)
                if (!names.Contains(command.Prefab) && !File.Exists(Path.Combine(root, command.Prefab)))
                    throw new InvalidDataException($"Missing extension prefab: {command.Prefab}");
            foreach (var (staged, target) in pending)
            {
                CreateFolders(Path.GetDirectoryName(target)!);
                File.Move(staged, target);
                added.Add(target);
            }
            _ = Commands(root).ToArray();
            _ = new ProjectScripts(root).Locations();
            ProjectAssets.Scan(root);
            return added.ToArray();
        }
        catch
        {
            foreach (var path in added.AsEnumerable().Reverse()) File.Delete(path);
            foreach (var folder in createdFolders)
                if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            throw;
        }
        finally { Directory.Delete(stage, true); }
    }

    private static bool Identical(string left, string right)
    {
        using var a = File.OpenRead(left);
        using var b = File.OpenRead(right);
        return a.Length == b.Length && System.Security.Cryptography.SHA256.HashData(a)
            .AsSpan().SequenceEqual(System.Security.Cryptography.SHA256.HashData(b));
    }
}
