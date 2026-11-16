using Engine.Core;

namespace Engine.Editor;

internal sealed class SceneHistory
{
    private sealed record State(string Objects, GameSettings Settings);
    private sealed record Edit(State? Before, State? After, Guid? PreviousSelection, Guid? Selection, TileChange[]? Cells = null, IProjectFileEdit? Asset=null);
    private const int Capacity = 100;
    private readonly List<Edit> edits = [];
    private int position;
    private State current = new("[]", new());
    private string savedObjects = "[]";
    private object? editGroup;

    public bool CanUndo => position > 0;
    public IProjectFileEdit? FileEdit(bool redo) => edits[redo ? position : position - 1].Asset;
    public bool CanRedo => position < edits.Count;
    internal string CurrentSnapshot => current.Objects;
    public bool IsDirty => current.Objects != savedObjects;

    public void Reset(SceneDocument scene)
    {
        edits.Clear();
        position = 0;
        current = new(scene.CaptureObjects(), scene.Settings);
        MarkSaved();
    }

    public void MarkSaved(SceneDocument scene)
    {
        var normalized = new State(scene.CaptureObjects(), scene.Settings);
        if (CanUndo && edits[position - 1].After == current)
            edits[position - 1] = edits[position - 1] with { After = normalized };
        current = normalized;
        MarkSaved();
    }

    public void MarkSaved()
    {
        savedObjects = current.Objects;
        EndGroup();
    }

    public void EndGroup() => editGroup = null;

    public bool Record(SceneDocument scene, Guid? previousSelection, Guid? selection, object? group, IProjectFileEdit? asset=null)
    {
        var next = new State(scene.CaptureObjects(), scene.Settings);
        if (asset is ProjectPathEdit move) savedObjects = move.RewriteSnapshot(savedObjects, true);
        if (next == current && asset==null) return false;
        if (CanRedo) edits.RemoveRange(position, edits.Count - position);
        if (group != null && ReferenceEquals(group, editGroup) && CanUndo)
        {
            var edit = edits[position - 1];
            if (edit.Before == next)
            {
                edits.RemoveAt(--position);
                group = null;
            }
            else edits[position - 1] = edit with { After = next, Selection = selection };
        }
        else
        {
            edits.Add(new(current, next, previousSelection, selection, Asset:asset));
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
        current = new(scene.CaptureObjects(),scene.Settings);
        EndGroup();
    }

    public Guid? Restore(SceneDocument scene, bool redo)
    {
        var edit = edits[redo ? position : position - 1];
        if (edit.Cells is { } cells)
        {
            var map = scene.Objects.Single(o => o.Id == edit.Selection).GetComponent<Tilemap2DComponent>()!;
            foreach (var change in cells) map.Set(change.Cell,redo ? change.After : change.Before);
            current = new(scene.CaptureObjects(),scene.Settings);
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
        scene.RestoreObjects(state.Objects);
        scene.Settings = state.Settings;
        if (edit.Asset != null) scene.ResolveAssets(ProjectAssets.Scan(scene.ProjectDirectory!));
        if (edit.Asset is ProjectPathEdit move) savedObjects = move.RewriteSnapshot(savedObjects, redo);
        current = new(scene.CaptureObjects(), scene.Settings);
        position += redo ? 1 : -1;
        EndGroup();
        return redo ? edit.Selection : edit.PreviousSelection;
    }
}
