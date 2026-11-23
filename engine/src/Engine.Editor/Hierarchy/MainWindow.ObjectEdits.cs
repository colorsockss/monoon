using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private void AddObject(SceneObject? parent = null, bool square = true)
    {
        if (busy || Viewport.IsPlaying || (parent != null && !Scene.Objects.Contains(parent))) return;
        if (parent == null && prefabSource != null) parent = Scene.Objects.Single(o => o.Id == prefabSource.RootId);
        string name = square ? "Square" : "GameObject";
        var number = 1;
        while (Scene.Objects.Any(obj => obj.Name == $"{name} {number}")) number++;
        var obj = new SceneObject { Name = $"{name} {number}" };
        var center = Viewport.EditCenter;
        obj.X = parent != null ? 0 : (float)Math.Round(center.X - (square ? obj.Width / 2 : 0));
        obj.Y = parent != null ? 0 : (float)Math.Round(center.Y - (square ? obj.Height / 2 : 0));
        if (square) obj.AddComponent(new SpriteRendererComponent { SquareColor = "#E6AE65" });
        Scene.Objects.Add(obj);
        if (parent != null) Scene.Reparent(obj, parent, keepWorld: false);
        FinishObjectChange(obj);
        FocusHierarchySelection();
    }

    private void DeleteObject()
    {
        if (busy || Viewport.IsPlaying || Selected is not { } obj) return;
        if (prefabSource?.RootId == obj.Id) return;
        bool resetCamera = Scene.Camera == obj;
        Scene.RemoveObject(obj);
        FinishObjectChange(obj.Parent ?? Scene.Objects.LastOrDefault());
        if (resetCamera) ReportStartCamera();
        FocusHierarchySelection();
    }

    private void ReportStartCamera() => Status.Text = Scene.Camera is { } camera ? $"Start camera: {camera.Name}" : "Add a camera with Scene → Create Camera before playing.";

    private void FinishObjectChange(SceneObject? selection,PrefabFileEdit? asset=null)
    {
        if (prefabSource != null)
        {
            var root = Scene.Objects.Single(o => o.Id == prefabSource.RootId);
            foreach (var child in Scene.Objects.Where(o => o != root && o.Parent == null).ToArray())
                Scene.Reparent(child, root);
        }
        var previous = Selected?.Id;
        RebuildHierarchy(selection);
        RecordEdit(previous,asset:asset);
    }

    private void MarkDirty(object? group = null) => RecordEdit(Selected?.Id, group);

    private void RecordEdit(Guid? previous, object? group = null, IProjectFileEdit? asset=null, Guid[]? multiSelection=null)
    {
        Viewport.InvalidateUi();
        Scene.NormalizeCamera();
        if (!history.Record(Scene, previous, Selected?.Id, group,asset,multiSelection)) return;
        QueueCheckpoint();
        UpdateMenuState();
        UpdateTitle();
        Status.Text = history.IsDirty ? "Unsaved changes" : "No unsaved changes";
    }
}
