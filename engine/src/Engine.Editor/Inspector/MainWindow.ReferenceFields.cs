using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Engine.Core;

namespace Engine.Editor;

// Object fields (13.7/13.17): asset and scene-object references as ReferenceFields. Choices come from the Inspector's one
// shared ProjectAssets scan; Ping and Select reach the Project panel and the Hierarchy.
public partial class MainWindow : IReferenceHost
{
    internal static Func<string, bool> EndsWith(string suffix) => path => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    // An asset field: accepts says which project paths fit (the picker lists them and drops are checked with it).
    private ReferenceField AssetField(string name, Func<string, bool> accepts, Guid? current, Action<Guid?> set, string none = "None")
    {
        var empty = None with { Label = none };
        var assets = project == null ? null : InspectorAssets(project);
        string? path = current is Guid id ? assets?.PathFor(id) : null;
        string root = project ?? "";
        var shown = current is not Guid value || value == Guid.Empty ? empty
            : path != null && File.Exists(Path.Combine(root, path)) ? AssetChoice(root, value, path, Path.GetFileName(path))
            : new ReferenceChoice(value, $"Missing: {path ?? value.ToString()}", null, EditorTokens.Geometry("IconWarning"));
        IReadOnlyList<ReferenceChoice> Choices() => project == null ? [empty] :
            [empty, .. InspectorAssets(project).Entries.Where(e => accepts(e.Path) && File.Exists(Path.Combine(root, e.Path)))
                .Select(e => AssetChoice(root, e.Id, e.Path, e.Path))];
        var field = new ReferenceField(name, asset: true, shown, Choices, set, accepts);
        field.IsEnabled = !Viewport.IsPlaying;
        return field;
    }

    // A scene-object field; accepts filters the objects offered and dropped from the Hierarchy.
    private ReferenceField ObjectField(string name, Func<SceneObject, bool> accepts, Guid? current, Action<Guid?> set, string none = "None")
    {
        var empty = None with { Label = none };
        ReferenceChoice Shown() => current is not Guid id || id == Guid.Empty ? empty
            : (Viewport.PlayWorld?.Objects ?? Scene.Objects).FirstOrDefault(o => o.Id == id) is { } obj ? new ReferenceChoice(id, ObjectPath(obj), null, EditorTokens.ObjectIcon(obj))
            : new ReferenceChoice(id, "Missing object or component", null, EditorTokens.Geometry("IconWarning"));
        var shown = Shown();
        inspectorObjectFields.Add(() => Shown() == shown);
        IReadOnlyList<ReferenceChoice> Choices() =>
            [empty, .. (Viewport.PlayWorld?.Objects ?? Scene.Objects).Where(accepts).Select(o => new ReferenceChoice(o.Id, ObjectPath(o), null, EditorTokens.ObjectIcon(o)))];
        var field = new ReferenceField(name, asset: false, shown, Choices, id =>
        {
            // A Hierarchy drop of an object the field does not take is refused, as the picker never offers it.
            if (id is Guid picked && Scene.Objects.FirstOrDefault(o => o.Id == picked) is { } target && !accepts(target)) { Status.Text = $"{target.Name} does not fit {name}."; return; }
            set(id);
        });
        field.IsEnabled = !Viewport.IsPlaying;
        return field;
    }

    private static readonly ReferenceChoice None = new(Guid.Empty, "None", null, null);

    // Finder files dragged over the Inspector or the Particle panel. An object field takes the files it accepts (one, or all in
    // order for an array); files outside the project are imported into Assets/ first (PNGs through the sprite importer, other
    // types through Import Assets) and then linked: one Undo step for the link.
    private void ReferenceFileDrag(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray() ?? [];
        bool dropped = e.RoutedEvent == DragDrop.DropEvent;
        var target = files.Length > 0 && sender is Visual host && !busy && !Viewport.IsPlaying && TopLevel.GetTopLevel(host) is { } top
            && top.PointToScreen(e.GetPosition(top)) is var screen ? ReferenceTarget(screen) : null;
        if (target?.Tag is ReferenceDrop drop)
        {
            var fits = drop.Asset is { } accepts ? files.Where(f => accepts(f)).Take(drop.Many ? int.MaxValue : 1).ToArray() : [];
            HighlightReference(dropped ? null : target, fits.Length > 0);
            HighlightSpriteTarget(null);
            e.DragEffects = fits.Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
            if (!dropped) return;
            if (fits.Length == 0) { Status.Text = $"{Path.GetFileName(files[0])} is not the kind of asset this field takes."; return; }
            RunAction(async () => { if (await ImportForField(fits) is { Length: > 0 } ids) drop.Insert(ids); });
            return;
        }
        HighlightReference(null);
        if (sender == InspectorPanel) SpriteFileDrag(sender, e);
    }

    private async Task<Guid[]> ImportForField(string[] files)
    {
        if (project == null && !await Save()) return [];
        string root = project!, pngFolder = CreationFolder(SpriteAssets.Folder), folder = CreationFolder("Assets");
        string inside = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var relative = await Task.Run(() =>
        {
            var result = new string[files.Length];
            var outside = new List<int>();
            for (int i = 0; i < files.Length; i++)
            {
                var full = Path.GetFullPath(files[i]);
                if (SpriteAssets.IsPng(full)) result[i] = PngImporter.Import(root, full, pngFolder).Asset;
                else if (full.StartsWith(inside, StringComparison.Ordinal)) result[i] = Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/');
                else outside.Add(i);
            }
            if (outside.Count > 0)
                foreach (var (i, copied) in outside.Zip(ProjectImport.Copy(root, folder, outside.Select(i => files[i]))))
                    result[i] = copied;
            return result;
        });
        catalogFiles = null;
        inspectorAssets = null;
        var assets = ProjectAssets.Scan(root);
        if (clipEditor is { } clip) clip.SetCatalog(assets);
        BrowseFolder(ParentPath(relative[0]), relative[0]);
        foreach (var png in relative.Where(SpriteAssets.IsPng)) Viewport.InvalidateSprite(Path.Combine(root, png));
        Status.Text = relative.Length == 1 ? $"Linked {relative[0]}" : $"Linked {relative.Length} assets";
        return relative.Select(r => assets.IdFor(r)).OfType<Guid>().ToArray();
    }

    private static ReferenceChoice AssetChoice(string root, Guid id, string path, string label) =>
        new(id, label, SpriteAssets.IsPng(path) ? Path.Combine(root, path) : null, EditorTokens.AssetIcon(path, false));

    void IReferenceHost.Ping(Guid id, bool asset, bool select)
    {
        if (asset)
        {
            if (project == null || ProjectAssets.Scan(project).PathFor(id) is not { } path) { Status.Text = "The asset is not in the project."; return; }
            if (!Dock.IsOpen("Project")) OpenPanel("Project");
            Dock.Activate("Project");
            BrowseFolder(ParentPath(path), path);
            if (select) ProjectBrowser.Focus();
            Dispatcher.UIThread.Post(() => Flash(ProjectBrowser.ContainerFromItem(ProjectBrowser.SelectedItem!)), DispatcherPriority.Background);
            Status.Text = path;
            return;
        }
        if (Scene.Objects.FirstOrDefault(o => o.Id == id) is not { } obj || !treeItems.TryGetValue(obj, out var node)) { Status.Text = "The object is not in this scene."; return; }
        if (select) { Select(obj); return; }
        if (!Dock.IsOpen("Hierarchy")) OpenPanel("Hierarchy");
        Dock.Activate("Hierarchy");
        if (ExpandParents(node)) ShowHierarchyRows();
        // A panel opened just now has no rows yet; scroll and tint once it is laid out.
        Dispatcher.UIThread.Post(() =>
        {
            if (!node.Listed) return;
            Hierarchy.ScrollIntoView(node);
            Flash(Hierarchy.ContainerFromItem(node));
        }, DispatcherPriority.Loaded);
        Status.Text = ObjectPath(obj);
    }

    // The pinged row is tinted for a second; only its background changes.
    private static void Flash(Control? row)
    {
        if (row == null) return;
        row.Classes.Add("ping");
        DispatcherTimer.RunOnce(() => row.Classes.Remove("ping"), TimeSpan.FromSeconds(1));
    }
}
