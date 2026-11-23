using System.Text;
using System.Text.Json.Nodes;
using Engine.Core;

namespace Engine.Editor;

// One history entry owns the filesystem move and all serialized references affected by it.
internal sealed class ProjectPathEdit : IProjectFileEdit
{
    private readonly string root;
    public string From { get; }
    public string To { get; }
    private readonly bool directory;
    private readonly Dictionary<string, string> newScriptMeta = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (byte[] Before, byte[] After)> originals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> identities = new(StringComparer.Ordinal);

    private ProjectPathEdit(string root, string from, string to)
    {
        this.root = Path.GetFullPath(root);
        From = from;
        To = to;
        directory = Directory.Exists(Physical(from));
    }

    public static ProjectPathEdit Move(string root, string from, string to)
    {
        var edit = new ProjectPathEdit(root, from, to);
        edit.Apply(forward: true, first: true);
        return edit;
    }

    public void Restore(bool redo) => Apply(forward: redo, first: false);

    internal static bool Within(string path, string parent) =>
        path == parent || path.StartsWith(parent + "/", StringComparison.Ordinal);

    public string Remap(string path, bool forward) =>
        Within(path, forward ? From : To) ? (forward ? To : From) + path[(forward ? From : To).Length..] : path;

    internal static bool CanMove(string path, bool folder) =>
        path.Split('/').All(ProjectImport.ValidName) && (folder ? path is not ("Scenes" or "Assets" or "Editor")
        : ProjectAssets.IsAsset(path) || path.EndsWith(".cs", StringComparison.Ordinal) && path != "Program.cs");

    private string Physical(string relative)
    {
        if (!relative.Split('/').All(ProjectImport.ValidName)) throw new IOException($"Invalid project path: {relative}");
        string path = root;
        foreach (var part in relative.Split('/'))
        {
            path = Path.Combine(path, part);
            if (new FileInfo(path).LinkTarget != null) throw new IOException($"Cannot move through a symbolic link: {relative}");
        }
        return path;
    }

    private IEnumerable<DirectoryInfo> Ancestors(string path)
    {
        for (var dir = new DirectoryInfo(path); dir.FullName != root && dir.Parent != null; dir = dir.Parent)
            yield return dir;
    }

    private static void Vacant(string source, string destination)
    {
        var parent = new DirectoryInfo(Path.GetDirectoryName(destination)!);
        var clash = parent.EnumerateFileSystemInfos().FirstOrDefault(f =>
            f.Name.Equals(Path.GetFileName(destination), StringComparison.OrdinalIgnoreCase) && f.FullName != source);
        if (clash != null) throw new IOException($"A file or folder named {clash.Name} already exists.");
    }

    private void Apply(bool forward, bool first)
    {
        string from = forward ? From : To, to = forward ? To : From;
        var source = Physical(from);
        var dest = Physical(to);
        if (!CanMove(from, directory) || !CanMove(to, directory) || from == to ||
            (directory && to.StartsWith(from + "/", StringComparison.OrdinalIgnoreCase)) ||
            (!directory && Path.GetExtension(from) != Path.GetExtension(to)))
            throw new IOException("Choose a different project path and keep the original asset type.");
        if (to.Split('/').Any(ProjectScripts.ReservedFolder))
            throw new IOException("Choose a project content folder.");
        foreach (var ancestor in Ancestors(Path.GetDirectoryName(dest)!))
            if (!ProjectScripts.ContentFolder(ancestor)) throw new IOException("Choose a project content folder.");
        if (!Path.Exists(source) || Directory.Exists(source) != directory) throw new IOException($"Missing {from}.");
        Vacant(source, dest);
        string[] sidecars = !directory && GameLocalization.IsFont(from) ? [".meta", ".license"] : [".meta"];
        foreach (var suffix in sidecars) Vacant(source + suffix, dest + suffix);

        // The catalog keeps missing asset IDs so restoring a deleted file restores its references.
        // A move must not claim a destination reserved by a different asset.
        var scripts = new ProjectScripts(root).Locations();
        var assets = ProjectAssets.Scan(root);
        foreach (var asset in assets.Entries.Where(a => Within(a.Path, from) && File.Exists(Physical(a.Path))))
        {
            var next = Remap(asset.Path, forward);
            if (assets.IdFor(next) is Guid owner && owner != asset.Id) throw new IOException($"Asset identity already registered at {next}.");
            if (next.EndsWith(ProjectAssets.Scene) && !next.StartsWith("Scenes/", StringComparison.Ordinal))
                throw new IOException("Move scenes within Scenes.");
            if (first) identities.Add(asset.Path, asset.Id);
        }
        foreach (var (original, id) in identities)
            if (assets.IdFor(forward ? original : Remap(original, true)) != id)
                throw new IOException("The asset was replaced after this edit.");
        foreach (var (id, path) in scripts.Where(p => Within(p.Value, from)))
        {
            if (File.Exists(Physical(path) + ".meta")) continue;
            if (first) newScriptMeta.Add(path, id);
            else if (!forward || !newScriptMeta.ContainsKey(path))
                throw new IOException($"Script contents of the folder changed after the move: {path}");
        }
        if (!directory && from.EndsWith(".cs", StringComparison.Ordinal) && !scripts.ContainsValue(from))
            throw new IOException("Select a game script to move.");
        if (directory) CheckTree(new DirectoryInfo(source));
        else _ = Physical(from + ".meta");

        // Prepare changes without modifying the project. Read the current documents again on Undo
        // so saving or editing unrelated fields after the move does not overwrite those changes.
        var changes = new List<(string Path, byte[]? Before, byte[]? After)>();
        foreach (var (original, id) in newScriptMeta)
        {
            string path = forward ? original : Remap(original, true);
            if (!scripts.TryGetValue(id, out var actual) || actual != path) throw new IOException($"Script changed: {path}");
            byte[]? before = File.Exists(Physical(path) + ".meta") ? File.ReadAllBytes(Physical(path) + ".meta") : null;
            if (forward ? before != null : before == null || ProjectYaml.Decode<ScriptLocation>(before).ScriptId != id)
                throw new IOException($"Script metadata changed: {path}");
            changes.Add((path + ".meta", before, forward ? ProjectYaml.Encode(new ScriptLocation(id)) : null));
        }

        foreach (string relative in assets.Entries.Select(a => a.Path).Where(p => p.EndsWith(ProjectAssets.Scene) || p.EndsWith(ProjectAssets.Prefab)))
        {
            var path = Physical(relative);
            if (!File.Exists(path)) continue;
            byte[] before = File.ReadAllBytes(path);
            if (RewriteDocument(before, forward) is {} after) changes.Add((relative, before, after));
        }
        string settingsFile = Path.Combine(root, GameSettings.FileName);
        if (File.Exists(settingsFile))
        {
            var before = File.ReadAllBytes(settingsFile);
            var settings = ProjectYaml.Decode<GameSettings>(before);
            var next = RemapSettings(settings, forward);
            if (next != settings) { next.Validate(); changes.Add((GameSettings.FileName, before, ProjectYaml.Encode(next))); }
        }

        for (int i = 0; i < changes.Count; i++)
        {
            var (relative, before, after) = changes[i];
            if (before == null || after == null) continue;
            string original = forward ? relative : Remap(relative, false);
            if (first) originals[original] = (before, after);
            else if (originals.TryGetValue(original, out var pair) && before.AsSpan().SequenceEqual(forward ? pair.Before : pair.After))
                changes[i] = (relative, before, forward ? pair.After : pair.Before);
        }
        string temp = Path.Combine(root, ".monoon-move-" + Guid.NewGuid().ToString("N"));
        void MoveItem(string a, string b) { if (directory) Directory.Move(a,b); else File.Move(a,b); }
        var rollback = new Stack<Action>();
        void Step(Action action, Action inverse) { action(); rollback.Push(inverse); }
        try
        {
            foreach (var (relative, before, after) in changes)
            {
                var path = Physical(relative);
                if (before == null ? Path.Exists(path) : !File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(before))
                    throw new IOException($"File changed during move: {relative}");
                void Write(byte[]? data) { if (data == null) File.Delete(path); else ProjectYaml.WriteBytes(path, data); }
                Step(() => Write(after), () => Write(before));
            }
            // Staging also makes case-only renames work on case-insensitive volumes.
            Step(() => MoveItem(source, temp), () => MoveItem(temp, source));
            var companions = sidecars.Where(suffix => File.Exists(source + suffix)).ToArray();
            foreach (var suffix in companions)
                Step(() => File.Move(source + suffix, temp + suffix), () => File.Move(temp + suffix, source + suffix));
            Step(() => MoveItem(temp, dest), () => MoveItem(dest, temp));
            foreach (var suffix in companions)
                Step(() => File.Move(temp + suffix, dest + suffix), () => File.Move(dest + suffix, temp + suffix));
            _ = new ProjectScripts(root).Locations();
            var updated = ProjectAssets.Scan(root);
            foreach (var (original, id) in identities)
                if (updated.IdFor(forward ? Remap(original, true) : original) != id) throw new IOException($"Asset identity conflict: {original}");
        }
        catch
        {
            foreach (var undo in rollback) undo();
            if (rollback.Count > 0) ProjectAssets.Scan(root);
            throw;
        }
    }

    private static void CheckTree(DirectoryInfo dir)
    {
        foreach (var item in dir.EnumerateFileSystemInfos())
        {
            if (item.LinkTarget != null) throw new IOException($"Folder contains a symbolic link: {item.Name}");
            if (item is DirectoryInfo child)
            {
                if (!ProjectScripts.ContentFolder(child)) throw new IOException($"Folder contains non-project data: {child.Name}");
                CheckTree(child);
            }
        }
    }

    internal GameSettings RemapSettings(GameSettings settings, bool forward)
    {
        if (settings.ContentBundles is not {} bundles) return settings;
        var next = bundles.Select(p => Remap(p, forward)).ToArray();
        return next.SequenceEqual(bundles) ? settings : settings with { ContentBundles = next };
    }

    internal string RewriteSnapshot(string snapshot, bool forward)
    {
        var bytes = Encoding.UTF8.GetBytes(snapshot);
        return RewriteDocument(bytes, forward) is {} rewritten ? Encoding.UTF8.GetString(rewritten) : snapshot;
    }

    internal string RewriteDisk(string relative, string disk, bool forward)
    {
        var original = forward ? relative : Remap(relative, false);
        if (originals.TryGetValue(original, out var pair) && disk == Encoding.UTF8.GetString(forward ? pair.Before : pair.After))
            return Encoding.UTF8.GetString(forward ? pair.After : pair.Before);
        return RewriteSnapshot(disk, forward);
    }

    private byte[]? RewriteDocument(byte[] input, bool forward)
    {
        var doc = ProjectYaml.Decode<SceneDocument>(input);
        bool changed = false;
        foreach (var obj in doc.Objects)
        {
            bool UpdateSprite(SceneObject item)
            {
                if (item.GetComponent<SpriteRendererComponent>() is not { Asset: {} path } sprite) return false;
                var next = Remap(path, forward);
                if (next == path) return false;
                sprite.Asset = next;
                return true;
            }
            changed |= UpdateSprite(obj);
            if (obj.Prefab?.Baseline is not {} baseline) continue;
            foreach (var (key, value) in baseline.ToArray())
            {
                var copy = PrefabMerge.Restore(JsonNode.Parse(value.GetRawText())!);
                if (!UpdateSprite(copy)) continue;
                baseline[key] = PrefabMerge.Snapshot(copy);
                changed = true;
            }
        }
        return changed ? ProjectYaml.Encode(doc) : null;
    }
}
