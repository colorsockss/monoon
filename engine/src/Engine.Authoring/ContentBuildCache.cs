using System.Security.Cryptography;
using System.Runtime.InteropServices;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
internal sealed partial record ContentBuildEntry(string Action, string Reason, Dictionary<string, string> Inputs, string? Hash);

// Only derived content is cached. Each export still validates the project and stages a new game.
internal sealed class ContentBuildCache
{
    private readonly Dictionary<string, ContentBuildEntry> previous;
    private readonly Dictionary<string, ContentBuildEntry> current = new(StringComparer.Ordinal);
    private readonly string blobs;
    private readonly bool full;
    private readonly string generator = string.Join("/", typeof(GameBuild).Module.ModuleVersionId,
        typeof(SceneDocument).Module.ModuleVersionId, typeof(SkiaSharp.SKBitmap).Module.ModuleVersionId,
        RuntimeInformation.OSDescription, Environment.Version);
    internal string Report { get; }
    internal string[] Decisions => current.Select(p => $"{p.Value.Action} {p.Key} — {p.Value.Reason}").ToArray();

    internal ContentBuildCache(string destination, Guid project, string target, string mode, BuildProfile profile, bool full)
    {
        this.full = full;
        var root = Path.Combine(destination, ".monoon-build-cache", project.ToString("N"),
            $"{RuntimeInformation.RuntimeIdentifier}-{target}-{mode}-{profile}");
        blobs = Directory.CreateDirectory(Path.Combine(root, "blobs")).FullName;
        Report = Path.Combine(root, "report.yaml");
        try { previous = ProjectYaml.Read<Dictionary<string, ContentBuildEntry>>(Report); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { previous = []; }
    }

    // 'path' is a fresh staging path. An optional font with no covered characters has no output.
    internal string? Generate(string name, Dictionary<string,string> inputs, string path, Action<string> generate,
        bool optional = false, bool reusable = true)
    {
        inputs.Add("generator", generator);
        previous.TryGetValue(name, out var old);
        string reason;
        if (full) reason = "full content build";
        else if (!reusable) reason = "custom generator";
        else if (old?.Inputs == null) reason = "no previous result";
        else
        {
            var changed = old.Inputs.Keys.Union(inputs.Keys, StringComparer.Ordinal)
                .Where(k => old.Inputs.GetValueOrDefault(k) != inputs.GetValueOrDefault(k)).Order(StringComparer.Ordinal).ToArray();
            if (changed.Length == 0 && ((old.Hash == null && optional) || Restore(old.Hash, path)))
            {
                current.Add(name, new("reuse", "inputs unchanged", inputs, old.Hash));
                return old.Hash;
            }
            reason = changed.Length > 0 ? "changed: " + string.Join(", ", changed) : "cached file missing or damaged";
        }
        generate(path);
        string? hash = null;
        if (!optional || File.Exists(path))
        {
            hash = BundleMountCache.Digest(path);
            var blob = Path.Combine(blobs, hash);
            if (!File.Exists(blob) || BundleMountCache.Digest(blob) != hash)
            {
                string temp = Path.Combine(blobs, Guid.NewGuid().ToString("N") + ".tmp");
                try { File.Copy(path, temp); File.Move(temp, blob, overwrite: true); }
                finally { File.Delete(temp); }
            }
        }
        current.Add(name, new("build", reason, inputs, hash));
        return hash;
    }

    private bool Restore(string? hash, string path)
    {
        if (hash is not { Length: 64 } || !hash.All(char.IsAsciiHexDigit)) return false;
        try { File.Copy(Path.Combine(blobs, hash), path); }
        catch (FileNotFoundException) { return false; }
        if (BundleMountCache.Digest(path) == hash) return true;
        File.Delete(path);
        return false;
    }

    internal void Publish() => ProjectYaml.Write(Report, current);
    internal static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
