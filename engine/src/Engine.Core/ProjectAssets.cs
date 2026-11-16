using VYaml.Annotations;
using System.Security.Cryptography;

namespace Engine.Core;

[YamlObject]
public sealed partial record AssetEntry(Guid Id, string Path, string Hash);

public sealed class ProjectAssets
{
    public const string Manifest = ".monoon-assets";
    public const string Scene = ".scene", Prefab = ".prefab", Clip = ".clip", Tileset = ".tileset", Data = ".data";
    public static bool IsAsset(string path) => Path.GetExtension(path) is Scene or Prefab or Clip or Tileset or Data or CutsceneSequence.Extension or SkeletonRig.Extension or ShaderClip.Extension or GameTable.Extension or SpriteAtlas.Extension or AnimationController.Extension || GameLocalization.IsFont(path) || SpriteAssets.IsPng(path) || AudioFile.IsSupported(path);
    internal static bool IsDocument(string path) => Path.GetExtension(path) is Scene or Prefab or Clip;
    private readonly Dictionary<Guid, CutsceneSequence> sequences = [];
    internal CutsceneSequence LoadSequence(string root, Guid id)
    {
        if (sequences.TryGetValue(id, out var sequence)) return sequence;
        if (PathFor(id) is not {} path || !path.EndsWith(CutsceneSequence.Extension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Missing cutscene sequence {id}.");
        return sequences[id] = CutsceneSequence.Load(Path.Combine(root, path));
    }
    private readonly Dictionary<Guid,SkeletonRig> rigs=[];
    internal SkeletonRig LoadRig(string root,Guid id)
    {
        if (rigs.TryGetValue(id,out var rig)) return rig;
        if (PathFor(id) is not {} path || !path.EndsWith(SkeletonRig.Extension))
            throw new InvalidDataException($"Missing skeleton rig {id}.");
        return rigs[id]=SkeletonRig.Load(Path.Combine(root,path));
    }
    private readonly Dictionary<Guid, ShaderClip> shaders = [];
    public ShaderClip LoadShader(string root, Guid id)
    {
        if (shaders.TryGetValue(id,out var cached)) return cached;
        if (PathFor(id) is not {} path || !path.EndsWith(ShaderClip.Extension))
            throw new InvalidDataException($"Missing shader {id}.");
        return shaders[id]=ShaderClip.Load(Path.Combine(root,path));
    }
    private SpriteLibrary? spriteLibrary;
    public SpriteLibrary Sprites(string root) => spriteLibrary ??= new(root,this);
    private readonly AssetEntry[] entries;
    public IReadOnlyList<AssetEntry> Entries => entries;
    public string? PathFor(Guid id) => entries.FirstOrDefault(e => e.Id == id)?.Path;
    public Guid? IdFor(string path) => entries.FirstOrDefault(e => e.Path == path)?.Id;
    private ProjectAssets(AssetEntry[] entries) => this.entries = entries;
    private static readonly object gate = new();

    public static SheetSlice[] ReadSheetSlices(string png)
    {
        var meta = png + ".meta";
        return File.Exists(meta) ? ProjectYaml.Read<AssetMeta>(meta).Slices ?? [] : [];
    }

    public static ProjectAssets ReadManifest(string root)
    {
        var file = Path.Combine(root, Manifest);
        return new(File.Exists(file) ? ProjectYaml.Read<AssetEntry[]>(file) : []);
    }

    public static ProjectAssets Scan(string root)
    {
        lock (gate)
        {
            var manifest = Path.Combine(root, Manifest);
            var previous = File.Exists(manifest) ? ProjectYaml.Read<AssetEntry[]>(manifest) : [];
            if (File.Exists(Path.Combine(root, ".monoon-player"))) return new(previous);
            var files = Enumerate(root, root).Order(StringComparer.Ordinal).ToArray();
            var present = files.ToHashSet(StringComparer.Ordinal);
            var missing = previous.Where(e => !present.Contains(e.Path)).ToList();
            var next = new List<AssetEntry>();
            var used = new HashSet<Guid>();
            var knownPaths = previous.Where(e => present.Contains(e.Path)).ToDictionary(e => e.Id, e => e.Path);
            foreach (string relative in files)
            {
                using var input = File.OpenRead(Path.Combine(root, relative));
                var hash = Convert.ToHexString(SHA256.HashData(input));
                var match = previous.FirstOrDefault(e => e.Path == relative);
                var sidecar = Path.Combine(root, relative + ".meta");
                var existingMeta = File.Exists(sidecar) ? ProjectYaml.Read<AssetMeta>(sidecar) : null;
                Guid? metadata = existingMeta?.Id;
                if (metadata == Guid.Empty) throw new InvalidDataException($"Empty asset ID in {sidecar}.");
                if (metadata is Guid stable && !used.Contains(stable) && (!knownPaths.TryGetValue(stable, out var owner) || owner == relative))
                {
                    match = new(stable, relative, hash);
                    missing.RemoveAll(e => e.Id == stable);
                }
                if (match == null)
                {
                    var moved = missing.Where(e => e.Hash == hash && Path.GetExtension(e.Path) == Path.GetExtension(relative)).ToArray();
                    if (moved.Length == 1) { match = moved[0]; missing.Remove(match); }
                }
                var id = match?.Id ?? Guid.NewGuid();
                if (!used.Add(id)) throw new InvalidDataException($"Duplicate asset ID on {relative}.");
                if (metadata != id)
                    ProjectYaml.Write(sidecar, new AssetMeta(id, existingMeta?.Slices));
                next.Add(new(id, relative, hash));
            }
            // Retain an absent reference so restoring the original file restores its identity.
            next.AddRange(missing);
            var result = next.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
            if (!previous.SequenceEqual(result)) Write(manifest, result);
            return new(result);
        }
    }

    public static void Write<T>(string path, T value) => ProjectYaml.Write(path, value);

    internal static IEnumerable<string> Enumerate(string root, string folder, bool includeLegacy = false)
    {
        foreach (var item in new DirectoryInfo(folder).EnumerateFileSystemInfos())
        {
            if (item.Name.StartsWith('.') || item.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            if (item is DirectoryInfo directory)
            {
                if (directory.Name is "bin" or "obj" or "build" or "Builds" || directory.Name.EndsWith(".app")
                    || File.Exists(Path.Combine(directory.FullName, ".monoon-player"))
                    || File.Exists(Path.Combine(directory.FullName, "Game", ".monoon-player"))) continue;
                foreach (var child in Enumerate(root, directory.FullName, includeLegacy)) yield return child;
            }
            else if (IsAsset(item.Name)
                     || includeLegacy && (LegacyProject.IsDocument(item.Name) || LegacyProject.IsDocumentSidecar(item.Name)))
                yield return Path.GetRelativePath(root, item.FullName).Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
