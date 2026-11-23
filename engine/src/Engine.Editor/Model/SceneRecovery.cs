using Engine.Core;
using VYaml.Annotations;

namespace Engine.Editor;

[YamlObject]
internal sealed partial record SceneDraft(int Version, string Scene, string? Disk, string Baseline, string Edited);

// Each editing session owns its draft. An open lease distinguishes a live editor from a crashed one.
internal sealed class SceneRecovery : IDisposable
{
    private readonly string directory;
    private string relative;
    private readonly string draft;
    private FileStream? lease;
    internal string? Disk { get; private set; }
    internal string Baseline { get; private set; }
    internal string ScenePath { get; private set; }

    public SceneRecovery(string root, string scene, string snapshot)
    {
        directory = Path.Combine(root, ".monoon", "recovery");
        relative = scene;
        ScenePath = Path.Combine(root, scene);
        draft = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".draft");
        Disk = File.Exists(ScenePath) ? File.ReadAllText(ScenePath) : null;
        Baseline = snapshot;
    }

    internal void Relocate(string root, string scene, ProjectPathEdit edit, bool forward)
    {
        if (Disk != null) Disk = edit.RewriteDisk(relative, Disk, forward);
        relative = scene;
        ScenePath = Path.Combine(root, scene);
        Baseline = edit.RewriteSnapshot(Baseline, forward);
    }

    public void Checkpoint(string edited)
    {
        if (edited == Baseline) { ClearOwn(); return; }
        Directory.CreateDirectory(directory);
        lease ??= new FileStream(draft + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        ProjectYaml.Write(draft, new SceneDraft(1, relative, Disk, Baseline, edited));
    }

    public void Saved(string snapshot)
    {
        Disk = File.ReadAllText(ScenePath);
        Baseline = snapshot;
        ClearOwn();
    }

    internal void ClearOwn()
    {
        if (lease == null) return;
        File.Delete(draft);
        Dispose();
    }

    public void Dispose()
    {
        if (lease == null) return;
        lease.Dispose();
        lease = null;
        File.Delete(draft + ".lock");
    }

    internal static IEnumerable<(string Path, SceneDraft Data)> Pending(string root, Action<string>? report = null)
    {
        var folder = Path.Combine(root, ".monoon", "recovery");
        if (!Directory.Exists(folder)) yield break;
        foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*.draft").OrderByDescending(f => f.LastWriteTimeUtc))
        {
            var lockPath = file.FullName + ".lock";
            if (File.Exists(lockPath))
            {
                try { using var test = File.Open(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) { continue; }
            }
            SceneDraft data;
            try { data = ProjectYaml.Read<SceneDraft>(file.FullName); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                report?.Invoke($"Cannot read recovery file {file.Name}: {ex.Message}");
                continue;
            }
            if (data.Version != 1 || data.Scene == null || !data.Scene.StartsWith("Scenes/", StringComparison.Ordinal) ||
                !data.Scene.EndsWith(ProjectAssets.Scene, StringComparison.Ordinal) ||
                !data.Scene.Split('/').All(ProjectImport.ValidName)) continue;
            yield return (file.FullName, data);
        }
    }

    internal static void Remove(string file)
    {
        File.Delete(file);
        File.Delete(file + ".lock");
    }
}
