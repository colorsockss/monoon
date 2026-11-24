using VYaml.Annotations;
using System.Security.Cryptography;

namespace Engine.Core;

[YamlObject]
public sealed partial record AssetEntry(Guid Id, string Path, string Hash);

public sealed class ProjectAssets
{
    internal static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name==name.Trim() &&
        !name.StartsWith('.') && !name.EndsWith('.') && !name.Any(c=>char.IsControl(c)||"<>:\"/\\|?*".Contains(c)) &&
        !WindowsDeviceName(name);

    // CON, PRN, AUX, NUL, COM1-9 and LPT1-9 (ASCII, any case), alone or before the first '.': "con", "COM1.txt".
    // Hand-written instead of a Regex so the player does not link System.Text.RegularExpressions.
    internal static bool WindowsDeviceName(string name)
    {
        var stem = name.AsSpan(0, name.IndexOf('.') is >= 0 and var dot ? dot : name.Length);
        return stem.Length == 3 && (System.Text.Ascii.EqualsIgnoreCase(stem, "CON") || System.Text.Ascii.EqualsIgnoreCase(stem, "PRN")
                || System.Text.Ascii.EqualsIgnoreCase(stem, "AUX") || System.Text.Ascii.EqualsIgnoreCase(stem, "NUL"))
            || stem.Length == 4 && stem[3] is >= '1' and <= '9'
                && (System.Text.Ascii.EqualsIgnoreCase(stem[..3], "COM") || System.Text.Ascii.EqualsIgnoreCase(stem[..3], "LPT"));
    }

    public const string Manifest = ".monoon-assets";

    // Native AOT players only run exported games, so the kit sets this switch and trimming drops the loose-project
    // paths (prefab refresh, tile baking, asset scanning) that re-encode scene objects with VYaml.
    private const string ExportedGameSwitch = "MoNoon.ExportedGameOnly";
    [System.Diagnostics.CodeAnalysis.FeatureSwitchDefinition(ExportedGameSwitch)]
    internal static bool ExportedGameOnly => AppContext.TryGetSwitch(ExportedGameSwitch, out bool on) && on;
    internal static bool Exported(string root) => ExportedGameOnly || File.Exists(Path.Combine(root, ".monoon-player"));
    public const string Scene = ".scene", Prefab = ".prefab", Clip = ".clip", Tileset = ".tileset", Data = ".data";
    public static bool IsAsset(string path) => Path.GetExtension(path) is Scene or Prefab or Clip or Tileset or Data or CutsceneSequence.Extension or SkeletonRig.Extension or ShaderClip.Extension or ParticleEffect.Extension or GameTable.Extension or SpriteAtlas.Extension or AnimationController.Extension || GameLocalization.IsFont(path) || SpriteAssets.IsPng(path) || AudioFile.IsSupported(path);
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
        return shaders[id]=ShaderClip.Load(Path.Combine(root,path), this, root);
    }
    private readonly Dictionary<Guid, ParticleEffect> particles = [];
    internal ParticleEffect LoadParticles(string root, Guid id) => particles.TryGetValue(id,out var cached) ? cached : ReloadParticles(root,id);
    // Reads the file again (editor Scene Play reload); later loads share the new effect.
    internal ParticleEffect ReloadParticles(string root, Guid id)
    {
        if (PathFor(id) is not {} path || !path.EndsWith(ParticleEffect.Extension))
            throw new InvalidDataException($"Missing particle effect {id}.");
        return particles[id]=ParticleEffect.Load(Path.Combine(root,path)).WithTexture(root,this);
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
            if (Exported(root)) return new(previous);
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
                    ProjectYaml.Write(sidecar, existingMeta == null ? new AssetMeta(id) : existingMeta with { Id = id });
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

    internal static IEnumerable<string> Enumerate(string root, string folder, Func<string, bool>? includeExtra = null)
    {
        foreach (var item in new DirectoryInfo(folder).EnumerateFileSystemInfos())
        {
            if (item.Name.StartsWith('.') || item.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            if (item is DirectoryInfo directory)
            {
                if (directory.Name is "bin" or "obj" or "build" or "Builds" || directory.Name.EndsWith(".app")
                    || File.Exists(Path.Combine(directory.FullName, ".monoon-player"))
                    || File.Exists(Path.Combine(directory.FullName, "Game", ".monoon-player"))) continue;
                foreach (var child in Enumerate(root, directory.FullName, includeExtra)) yield return child;
            }
            else if (IsAsset(item.Name)
                     || includeExtra?.Invoke(item.Name) == true)
                yield return Path.GetRelativePath(root, item.FullName).Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
