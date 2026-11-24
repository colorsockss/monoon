using System.Buffers;
using System.Text;
using Engine.Core;

namespace Engine.Editor;

// A state is the scene YAML cut at its object entries ("- id: …" blocks under "objects:"). Entry arrays are never
// mutated and are shared by reference between the current state, the saved state and the undo records, so an edit keeps
// only the entries it changed plus one reference per object instead of a copy of the whole scene. Undo and redo decode
// only the entries that differ from the open scene and keep the other SceneObject instances.
internal sealed class SceneHistory
{
    private sealed class State(byte[] header, byte[][] objects, byte[] footer, GameSettings settings)
    {
        public readonly byte[] Header = header, Footer = footer;
        public readonly byte[][] Objects = objects;
        public readonly GameSettings Settings = settings;

        // Same scene text (Settings are compared separately, as the former whole-text states did).
        public bool SameText(State other) => ReferenceEquals(this, other) || Header.AsSpan().SequenceEqual(other.Header) && SameEntries(other);

        // Same objects and start camera; the header holds only the schema version, which restoring leaves as it is.
        public bool SameEntries(State other)
        {
            if (Objects.Length != other.Objects.Length || !Footer.AsSpan().SequenceEqual(other.Footer)) return false;
            for (int i = 0; i < Objects.Length; i++)
                if (!ReferenceEquals(Objects[i], other.Objects[i]) && !Objects[i].AsSpan().SequenceEqual(other.Objects[i])) return false;
            return true;
        }

        public bool Same(State? other) => other != null && SameText(other) && Equals(Settings, other.Settings);

        public byte[] Bytes()
        {
            var bytes = new byte[Header.Length + Footer.Length + Objects.Sum(o => o.Length)];
            int at = 0;
            void Put(byte[] part) { part.CopyTo(bytes, at); at += part.Length; }
            Put(Header);
            foreach (var entry in Objects) Put(entry);
            Put(Footer);
            return bytes;
        }

        // The same text SceneDocument.CaptureObjects returns.
        public string Text() => Encoding.UTF8.GetString(Bytes());
    }

    private sealed record Edit(State? Before, State? After, Guid? PreviousSelection, Guid? Selection, TileChange[]? Cells = null, IProjectFileEdit? Asset=null, Guid[]? MultiSelection=null);
    private const int Capacity = 100;
    private readonly List<Edit> edits = [];
    private readonly ArrayBufferWriter<byte> buffer = new();
    private readonly List<Range> ranges = [];
    private int position;
    private State current = new([], [], [], new());
    private State saved;
    private object? editGroup;

    public SceneHistory() => saved = current;

    public bool CanUndo => position > 0;
    public IProjectFileEdit? FileEdit(bool redo) => edits[redo ? position : position - 1].Asset;
    public Guid[]? MultiSelection(bool redo) => edits[redo ? position : position - 1].MultiSelection;
    public bool CanRedo => position < edits.Count;
    // The recovery checkpoint composes the text when it is written, not on every recorded edit.
    internal Func<string> CurrentSnapshot => current.Text;
    public bool IsDirty => !current.SameText(saved);

    public void Reset(SceneDocument scene)
    {
        edits.Clear();
        position = 0;
        current = Capture(scene, null);
        MarkSaved();
    }

    public void MarkSaved(SceneDocument scene)
    {
        var normalized = Capture(scene, current);
        if (CanUndo && edits[position - 1].After is { } after && after.Same(current))
            edits[position - 1] = edits[position - 1] with { After = normalized };
        current = normalized;
        MarkSaved();
    }

    public void MarkSaved()
    {
        saved = current;
        EndGroup();
    }

    public void EndGroup() => editGroup = null;

    public bool Record(SceneDocument scene, Guid? previousSelection, Guid? selection, object? group, IProjectFileEdit? asset=null, Guid[]? multiSelection=null)
    {
        var next = Capture(scene, current);
        if (asset is ProjectPathEdit move) saved = Rewrite(saved, move, true);
        if (next.Same(current) && asset==null) return false;
        if (CanRedo) edits.RemoveRange(position, edits.Count - position);
        if (group != null && ReferenceEquals(group, editGroup) && CanUndo)
        {
            var edit = edits[position - 1];
            if (next.Same(edit.Before))
            {
                edits.RemoveAt(--position);
                group = null;
            }
            else edits[position - 1] = edit with { After = next, Selection = selection };
        }
        else
        {
            edits.Add(new(current, next, previousSelection, selection, Asset:asset, MultiSelection:multiSelection));
            if (edits.Count > Capacity) edits.RemoveAt(0);
            position = edits.Count;
        }
        current = next;
        editGroup = group;
        return true;
    }

    public void RecordStroke(SceneDocument scene, Guid owner, TileChange[] cells)
    {
        if (cells.Length == 0) return;
        if (CanRedo) edits.RemoveRange(position, edits.Count-position);
        edits.Add(new(null,null,owner,owner,cells));
        if (edits.Count > Capacity) edits.RemoveAt(0);
        position = edits.Count;
        current = Capture(scene, current);
        EndGroup();
    }

    public Guid? Restore(SceneDocument scene, bool redo)
    {
        var edit = edits[redo ? position : position - 1];
        if (edit.Cells is { } cells)
        {
            var map = scene.Objects.Single(o => o.Id == edit.Selection).GetComponent<Tilemap2DComponent>()!;
            foreach (var change in cells) map.Set(change.Cell,redo ? change.After : change.Before);
            current = Capture(scene, current);
            position += redo ? 1 : -1;
            EndGroup();
            return edit.Selection;
        }
        if (edit.Asset is {} asset) asset.Restore(redo);
        var state = (redo ? edit.After : edit.Before)!;
        if (edit.Asset is not ProjectPathEdit && state.Settings != scene.Settings && scene.ProjectDirectory is { } project)
        {
            var disk = GameSettings.Load(project);
            if (!disk.SerializedSettings().AsSpan().SequenceEqual(scene.Settings.SerializedSettings()))
                throw new IOException("Project settings changed on disk. Reopen the project before restoring settings.");
            state.Settings.Save(project);
        }
        Apply(scene, state, current);
        scene.Settings = state.Settings;
        if (edit.Asset != null) scene.ResolveAssets(ProjectAssets.Scan(scene.ProjectDirectory!));
        var restored = Capture(scene, state);
        if (!restored.SameEntries(state))
        {
            // A kept object differed from the recorded state (a change the history never saw), or normalising changed the
            // text: decode every entry, as SceneDocument.RestoreObjects did.
            Apply(scene, state, null);
            if (edit.Asset != null) scene.ResolveAssets(ProjectAssets.Scan(scene.ProjectDirectory!));
            restored = Capture(scene, state);
        }
        if (edit.Asset is ProjectPathEdit move) saved = Rewrite(saved, move, redo);
        current = restored;
        position += redo ? 1 : -1;
        EndGroup();
        return redo ? edit.Selection : edit.PreviousSelection;
    }

    // SceneDocument.RestoreObjects for a state. Entries that `open` (the state recorded for the open scene, or null) shares
    // with the target keep their SceneObject; the others are decoded. Restore checks the result against the target text.
    private static void Apply(SceneDocument scene, State target, State? open)
    {
        var objects = scene.Objects;
        var live = open?.Objects.Length == objects.Count ? open.Objects : [];
        Dictionary<byte[], int>? liveAt = null;
        var reuse = new int[target.Objects.Length];
        var missing = new List<byte[]>();
        for (int i = 0; i < reuse.Length; i++)
        {
            var entry = target.Objects[i];
            reuse[i] = -1;
            if (i < live.Length && ReferenceEquals(live[i], entry)) { reuse[i] = i; continue; }
            if (liveAt == null)
            {
                liveAt = new(ReferenceEqualityComparer.Instance);
                for (int j = 0; j < live.Length; j++) liveAt.TryAdd(live[j], j);
            }
            if (liveAt.TryGetValue(entry, out int at)) reuse[i] = at;
            else missing.Add(entry);
        }
        var restored = ProjectYaml.Decode<SceneDocument>(new State(target.Header, [..missing], target.Footer, target.Settings).Bytes());
        var decoded = restored.Objects ?? [];
        // A state without cut entries (an empty scene, or text the cut did not recognise) holds every object in its header.
        if (target.Objects.Length != 0)
        {
            var list = new List<SceneObject>(reuse.Length);
            int next = 0;
            foreach (int at in reuse) list.Add(at >= 0 ? objects[at] : decoded[next++]);
            restored.Objects = list;
        }
        else restored.Objects = decoded;
        try { restored.Validate(); }
        catch
        {
            // Validate links parents of the kept instances first; point them back at the open scene's objects.
            new SceneDocument { Objects = objects }.Validate();
            throw;
        }
        scene.Objects = restored.Objects;
        scene.DefaultCameraId = restored.DefaultCameraId;
        scene.NormalizeScriptFields();
    }

    private State Rewrite(State state, ProjectPathEdit move, bool forward) =>
        Cut(Encoding.UTF8.GetBytes(move.RewriteSnapshot(state.Text(), forward)), state, state.Settings);

    private State Capture(SceneDocument scene, State? like)
    {
        buffer.ResetWrittenCount();
        ProjectYaml.EncodeTo(buffer, scene);
        return Cut(buffer.WrittenSpan, like, scene.Settings);
    }

    // Cuts YAML into header, object entries and footer, reusing `like`'s arrays for unchanged bytes (matched from both ends,
    // then by the entry's first line, its id, for moved entries).
    private State Cut(ReadOnlySpan<byte> yaml, State? like, GameSettings settings)
    {
        ranges.Clear();
        int headerEnd = -1, footer = yaml.Length;
        for (int line = 0; line < yaml.Length;)
        {
            int end = yaml[line..].IndexOf((byte)'\n') is var at and >= 0 ? line + at + 1 : yaml.Length;
            var text = yaml[line..end];
            if (headerEnd < 0) { if (text.SequenceEqual("objects:\n"u8)) headerEnd = end; }
            else if (text.StartsWith("- "u8)) ranges.Add(new(line, end));
            else if (text[0] is not ((byte)' ' or (byte)'\n') || ranges.Count == 0) { footer = line; break; }
            else ranges[^1] = new(ranges[^1].Start, end);
            line = end;
        }
        if (ranges.Count == 0) { headerEnd = yaml.Length; footer = yaml.Length; }
        byte[] Part(ReadOnlySpan<byte> bytes, byte[]? old) => old != null && bytes.SequenceEqual(old) ? old : bytes.ToArray();
        var previous = like?.Objects ?? [];
        var objects = new byte[ranges.Count][];
        int n = objects.Length, m = previous.Length, front = 0, back = 0;
        while (front < n && front < m && yaml[ranges[front]].SequenceEqual(previous[front])) objects[front] = previous[front++];
        while (back < n - front && back < m - front && yaml[ranges[n - 1 - back]].SequenceEqual(previous[m - 1 - back]))
        {
            objects[n - 1 - back] = previous[m - 1 - back];
            back++;
        }
        Dictionary<string, byte[]>? byId = null;
        for (int i = front; i < n - back; i++)
        {
            var entry = yaml[ranges[i]];
            if (byId == null)
            {
                byId = [];
                for (int j = front; j < m - back; j++) byId.TryAdd(FirstLine(previous[j]), previous[j]);
            }
            objects[i] = Part(entry, byId.GetValueOrDefault(FirstLine(entry)));
        }
        return new(Part(yaml[..headerEnd], like?.Header), objects, Part(yaml[footer..], like?.Footer), settings);
    }

    private static string FirstLine(ReadOnlySpan<byte> entry) =>
        Encoding.UTF8.GetString(entry.IndexOf((byte)'\n') is var n and >= 0 ? entry[..n] : entry);
}
