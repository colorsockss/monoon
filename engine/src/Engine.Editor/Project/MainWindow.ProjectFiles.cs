using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private sealed record ProjectEntry(string RelativePath, bool IsFolder, bool SearchResult=false, string? Root=null)
    {
        public Avalonia.Media.Imaging.Bitmap? Thumbnail => Root != null && !IsFolder && SpriteAssets.IsPng(RelativePath) ? ProjectThumbnails.Get(Path.Combine(Root, RelativePath)) : null;
        public bool HasThumbnail => Thumbnail != null;
        public string Directory => ParentPath(RelativePath);
        public string Name => Path.GetFileName(RelativePath);
        public bool IsSource => !IsFolder && Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        public Avalonia.Media.Geometry Icon => EditorTokens.AssetIcon(RelativePath, IsFolder);
        public override string ToString() => Name;
    }

    private sealed record FilePress(ProjectEntry Entry, IPointer Pointer, Point Origin, bool DoubleClick) { public string? ScriptToAttach { get; set; } public bool Checked { get; set; } }
    private FilePress? filePress;
    private bool draggingFile;
    private string browserFolder = "";
    private DateTime? browserStamp;
    private static readonly Cursor CopyFileCursor = new(StandardCursorType.DragCopy);
    private static readonly Cursor MoveFileCursor = new(StandardCursorType.DragMove);
    private static readonly Cursor NoDropCursor = new(StandardCursorType.No);

    private void InitializeProjectBrowser()
    {
        AssetSearch.TextChanged += (_,_) => RefreshProjectFiles(force:true);
        AssetSearch.KeyDown += (_,e) => {
            if(e.Key==Key.Escape){AssetSearch.Text="";e.Handled=true;}
            else if(e.Key is Key.Down or Key.Enter && ProjectBrowser.ItemCount>0){
                ProjectBrowser.SelectedIndex=0;ProjectBrowser.Focus();
                if(e.Key==Key.Enter && ProjectBrowser.SelectedItem is ProjectEntry entry) OpenEntry(entry);
                e.Handled=true;
            }
        };
        ProjectBrowser.SelectionChanged += (_, _) => ShowProjectPreview();
        Closed += (_, _) => { catalogWatcher?.Dispose(); spriteMonitor?.Dispose(); previewBitmap?.Dispose(); };
        ProjectBrowser.AddHandler(PointerPressedEvent, (_,e)=>{
            if(prefabSource != null || busy || Viewport.IsPlaying || !e.GetCurrentPoint(ProjectBrowser).Properties.IsRightButtonPressed || e.Source is not Visual v)return;
            var item=v as ListBoxItem??v.FindAncestorOfType<ListBoxItem>();
            if(item?.DataContext is not ProjectEntry entry)return;
            ProjectBrowser.SelectedItem=entry;
            var menu = new ContextMenu();
            if (ProjectPathEdit.CanMove(entry.RelativePath, entry.IsFolder))
            {
                var rename = new MenuItem { Header = "Rename…" };
                rename.Click += (_,_) => RunAction(() => ChangeProjectPath(entry, rename: true));
                var move = new MenuItem { Header = "Move to Folder…" };
                move.Click += (_,_) => RunAction(() => ChangeProjectPath(entry, rename: false));
                menu.Items.Add(rename);
                menu.Items.Add(move);
            }
            if (!entry.IsFolder)
            {
                var refs = new MenuItem { Header = "Asset References…" };
                refs.Click += (_,_) => RunAction(() => InspectAssets(false));
                var reveal = new MenuItem { Header = "Show in Folder" };
                reveal.Click += (_,_) => BrowseFolder(ParentPath(entry.RelativePath),entry.RelativePath);
                menu.Items.Add(refs);
                menu.Items.Add(reveal);
            }
            if (menu.Items.Count != 0) menu.Open(item);
            e.Handled=true;
        },RoutingStrategies.Tunnel);
        ParentFolderButton.Click += (_, _) => UpOneFolder();
        InitializeProjectView();
        ProjectBrowser.KeyDown += (_, e) =>
        {
            if (prefabSource == null && e.Key == Key.F2 && ProjectBrowser.SelectedItem is ProjectEntry selected && ProjectPathEdit.CanMove(selected.RelativePath, selected.IsFolder))
                RunAction(() => ChangeProjectPath(selected, rename: true));
            else if (e.Key == Key.Enter && ProjectBrowser.SelectedItem is ProjectEntry entry) OpenEntry(entry);
            else if (e.Key == Key.Back || (e.Key == Key.Up && e.KeyModifiers == KeyModifiers.Alt)) UpOneFolder();
            else return;
            e.Handled = true;
        };
        ProjectBrowser.AddHandler(PointerPressedEvent, FilePressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        ProjectBrowser.PointerMoved += FileMoved;
        ProjectBrowser.PointerReleased += FileReleased;
        ProjectBrowser.PointerCaptureLost += (_, e) =>
        {
            if (e.Source == ProjectBrowser && filePress?.Pointer == e.Pointer) EndFileDrag();
        };
    }

    private static string ParentPath(string path) => Path.GetDirectoryName(path)?.Replace(Path.DirectorySeparatorChar, '/') ?? "";

    private string CreationFolder(string category) =>
        browserFolder.StartsWith(category + "/", StringComparison.Ordinal) ? browserFolder : category;

    private void UpOneFolder()
    {
        if (browserFolder.Length != 0) BrowseFolder(ParentPath(browserFolder), browserFolder);
    }

    private void BrowseFolder(string folder, string? select = null)
    {
        browserFolder = folder;
        AssetSearch.Text="";
        RefreshProjectFiles(force: true);
        ProjectBrowser.SelectedItem = ProjectBrowser.Items.OfType<ProjectEntry>().FirstOrDefault(entry => entry.RelativePath == select);
        if (ProjectBrowser.SelectedItem is { } selected) ProjectBrowser.ScrollIntoView(selected);
        else if (ProjectBrowser.ItemCount > 0) ProjectBrowser.ScrollIntoView(0);
    }

    private void RefreshProjectFiles(bool force = false)
    {
        if (project == null)
        {
            browserStamp = null;
            ProjectBrowser.ItemsSource = null;
            ParentFolderButton.IsVisible = false;
            ShowProjectPath(null);
            FolderTree.Items.Clear();
            ProjectName.Text = "Unsaved";
            ToolTip.SetTip(ProjectName, null);
            EmptyFolder.IsVisible = false;
            ParentFolderButton.IsEnabled = false;
            return;
        }

        try
        {
            var directory = new DirectoryInfo(Path.Combine(project, browserFolder));
            while (browserFolder.Length > 0 && (!directory.Exists || directory.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            {
                browserFolder = ParentPath(browserFolder);
                directory = new DirectoryInfo(Path.Combine(project, browserFolder));
                force = true;
            }
            var stamp = directory.LastWriteTimeUtc;
            var query=AssetSearch.Text?.Trim()??"";
            var changed=Interlocked.Exchange(ref catalogDirty,0)!=0;
            if (changed || FolderTree.Items.Count == 0) RebuildFolderTree();
            if (!force && stamp == browserStamp && !(changed && query.Length>0)) return;
            if(query.Length>0)
            {
                if(changed || catalogFiles==null) catalogFiles=ProjectCatalog.Files(project);
                var hits=ProjectCatalog.Search(catalogFiles,query).Select(p=>new ProjectEntry(p,false,true,project)).ToArray();
                var old=ProjectBrowser.SelectedItem as ProjectEntry;
                ProjectBrowser.ItemsSource=hits;ProjectBrowser.SelectedItem=hits.FirstOrDefault(p=>p==old);
                EmptyFolder.Text="No matching assets";EmptyFolder.IsVisible=hits.Length==0;
                ProjectName.Text=$"{hits.Length} found";ToolTip.SetTip(ProjectName,"All project assets and scripts");
                ShowProjectPath(null);
                ParentFolderButton.IsVisible=false;browserStamp=stamp;return;
            }
            if(changed) catalogFiles=null;
            EmptyFolder.Text="Empty folder";
            var selected = ProjectBrowser.SelectedItem as ProjectEntry;
            var options = new EnumerationOptions { AttributesToSkip = FileAttributes.Hidden | FileAttributes.ReparsePoint, IgnoreInaccessible = false };
            var files = directory.GetFileSystemInfos("*", options);
            bool dotnetRoot = browserFolder.Length == 0 && files.Any(file => file.Extension == ".csproj");
            var entries = files.Where(file => !file.Name.StartsWith('.') && !file.Name.EndsWith(".meta") &&
                    !(dotnetRoot && file is DirectoryInfo && file.Name is "bin" or "obj"))
                .Select(file => new ProjectEntry(Path.GetRelativePath(project, file.FullName).Replace(Path.DirectorySeparatorChar, '/'), file is DirectoryInfo, Root: project))
                .OrderByDescending(entry => entry.IsFolder).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal).ToArray();
            ProjectBrowser.ItemsSource = entries;
            ProjectBrowser.SelectedItem = entries.FirstOrDefault(entry => entry == selected);
            browserStamp = stamp;
            ShowProjectPath(browserFolder);
            ParentFolderButton.IsVisible = browserFolder.Length != 0;
            ParentFolderButton.IsEnabled = ParentFolderButton.IsVisible && !Viewport.IsPlaying;
            EmptyFolder.IsVisible = entries.Length == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ProjectBrowser.ItemsSource = null;
            browserStamp = null;
            ShowProjectPath(browserFolder);
            ParentFolderButton.IsVisible = browserFolder.Length > 0;
            ParentFolderButton.IsEnabled = ParentFolderButton.IsVisible && !Viewport.IsPlaying;
            EmptyFolder.IsVisible = false;
            Status.Text = $"Could not list project files: {ex.Message}";
        }
    }

    private void OpenEntry(ProjectEntry entry)
    {
        if (busy || Viewport.IsPlaying || project == null) return;
        if (entry.IsFolder) BrowseFolder(entry.RelativePath);
        else if (prefabSource != null) return;
        else if (entry.Name.EndsWith(ProjectAssets.Prefab)) RunAction(() => EditPrefabSource(entry.RelativePath));
        else if (entry.Name.EndsWith(".monopackage", StringComparison.OrdinalIgnoreCase))
            RunAction(() => InstallPackage(Path.Combine(project, entry.RelativePath)));
        else if (entry.Name.EndsWith(SpriteAtlas.Extension)) RunAction(() => EditAtlas(entry.RelativePath));
        else if (entry.Name.EndsWith(GameTable.Extension)) RunAction(() => EditValues(entry.RelativePath,true));
        else if (entry.Name.EndsWith(ProjectAssets.Data)) RunAction(() => EditData(entry.RelativePath));
        else if (entry.Name.EndsWith(ProjectAssets.Tileset)) RunAction(() => EditTileset(entry.RelativePath));
        else if (entry.Name.EndsWith(AnimationController.Extension)) RunAction(() => EditAnimator(entry.RelativePath));
        else if (entry.Name.EndsWith(ProjectAssets.Clip)) RunAction(() => EditClip(entry.RelativePath));
        else if (entry.Name.EndsWith(CutsceneSequence.Extension) || entry.Name.EndsWith(SkeletonRig.Extension) || entry.Name.EndsWith(ShaderClip.Extension) || entry.IsSource) RunAction(() => OpenProjectSource(entry.RelativePath));
        else if (entry.RelativePath.StartsWith("Scenes/", StringComparison.Ordinal) && entry.Name.EndsWith(Suffix, StringComparison.Ordinal))
        {
            var name = entry.RelativePath[7..^Suffix.Length];
            if (name != sceneName) RunAction(() => SwitchScene(name));
        }
    }

    private async Task OpenProjectSource(string relativePath)
    {
        if (project == null) return;
        await ExternalScriptEditor.Open(Path.Combine(project, relativePath));
        Status.Text = $"Opened {relativePath}";
    }

    private void FilePressed(object? sender, PointerPressedEventArgs e)
    {
        if (busy || Viewport.IsPlaying || !e.GetCurrentPoint(ProjectBrowser).Properties.IsLeftButtonPressed ||
            e.Source is not Visual visual) return;
        var row = visual as ListBoxItem ?? visual.FindAncestorOfType<ListBoxItem>();
        if (row?.DataContext is not ProjectEntry entry) return;
        EndFileDrag();
        var selection = ProjectBrowser.SelectedItems!;
        if (clipEditor != null && !entry.IsFolder && SpriteAssets.IsPng(entry.Name))
        {
            bool toggle = (e.KeyModifiers & (KeyModifiers.Meta | KeyModifiers.Control)) != 0;
            if (toggle)
            {
                if (selection.Contains(entry)) selection.Remove(entry); else selection.Add(entry);
                row.Focus(); e.Handled = true; return;
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && ProjectBrowser.SelectedItem is ProjectEntry anchor)
            {
                int a = ProjectBrowser.Items.IndexOf(anchor), b = ProjectBrowser.Items.IndexOf(entry);
                selection.Clear();
                foreach (var png in ProjectBrowser.Items.OfType<ProjectEntry>().Skip(Math.Min(a,b)).Take(Math.Abs(a-b)+1)
                    .Where(p => !p.IsFolder && SpriteAssets.IsPng(p.Name))) selection.Add(png);
            }
            else if (!selection.Contains(entry)) { selection.Clear(); selection.Add(entry); }
        }
        else ProjectBrowser.SelectedItem = entry;
        row.Focus();
        filePress = new FilePress(entry, e.Pointer, e.GetPosition(this), e.ClickCount == 2);
        e.Pointer.Capture(ProjectBrowser);
        e.Handled = true;
    }

    private bool IsFileDrag(FilePress press, PointerEventArgs e) =>
        draggingFile || ((Vector)(e.GetPosition(this) - press.Origin)).Length >= 4;

    private bool OverInspector(PointerEventArgs e) => new Rect(InspectorPanel.Bounds.Size).Contains(e.GetPosition(InspectorPanel));

    private string? ScriptToAttach(FilePress press)
    {
        if (!press.Checked)
        {
            press.Checked = true;
            if (press.Entry.IsSource && scripts != null)
            {
                try
                {
                    var id = scripts.Locations().FirstOrDefault(p => p.Value == press.Entry.RelativePath).Key;
                    if (id != null && AttachableScripts().Contains(id)) press.ScriptToAttach = id;
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
                { Status.Text = ex.Message; }
            }
        }
        return press.ScriptToAttach;
    }

    private string? FolderDrop(PointerEventArgs e, ProjectEntry entry)
    {
        if (prefabSource != null || !ProjectPathEdit.CanMove(entry.RelativePath, entry.IsFolder)) return null;
        string? folder = null;
        if (ParentFolderButton.IsVisible && new Rect(ParentFolderButton.Bounds.Size).Contains(e.GetPosition(ParentFolderButton)))
            folder = ParentPath(browserFolder);
        else if (ProjectPath.Children.OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && new Rect(b.Bounds.Size).Contains(e.GetPosition(b))) is { Tag: string crumb })
            folder = crumb;
        else if (FolderTreePane.IsVisible && FolderTree.InputHitTest(e.GetPosition(FolderTree)) is Visual node &&
            (node as TreeViewItem ?? node.FindAncestorOfType<TreeViewItem>()) is { Tag: string branch })
            folder = branch;
        else if (ProjectBrowser.InputHitTest(e.GetPosition(ProjectBrowser)) is Visual hit)
        {
            var row = hit as ListBoxItem ?? hit.FindAncestorOfType<ListBoxItem>();
            if (row?.DataContext is ProjectEntry { IsFolder: true } target) folder = target.RelativePath;
        }
        return folder != null && folder != entry.Directory && !ProjectPathEdit.Within(folder, entry.RelativePath)
            ? (folder.Length == 0 ? "" : folder + "/") + entry.Name : null;
    }

    private static bool AcceptsProjectEntry(ReferenceDrop target, ProjectEntry entry) =>
        !entry.IsFolder && target.Extension is {} ext && entry.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase);

    private void FileMoved(object? sender, PointerEventArgs e)
    {
        if (filePress is not { } press || press.Pointer != e.Pointer || !IsFileDrag(press, e)) return;
        draggingFile = true;
        var reference = ReferenceTarget(e);
        if (reference?.Tag is ReferenceDrop target)
        {
            bool accepted = AcceptsProjectEntry(target, press.Entry);
            HighlightReference(accepted ? reference : null);
            HighlightSpriteTarget(null);
            InspectorPanel.Classes.Remove("scriptDrop");
            ProjectBrowser.Cursor = accepted ? CopyFileCursor : NoDropCursor;
            e.Handled = true;
            return;
        }
        HighlightReference(null);
        bool attach = OverInspector(e) && ScriptToAttach(press) is { } name && Selected is { } owner && !HasScript(owner, name);
        bool png = !press.Entry.IsFolder && SpriteAssets.IsPng(press.Entry.Name);
        if (png && ClipDrop(e,false)) { HighlightSpriteTarget(null); InspectorPanel.Classes.Remove("scriptDrop");
            ProjectBrowser.Cursor = CopyFileCursor; e.Handled = true; return; }
        var sprite = png ? SpriteSectionAt(e.GetPosition(InspectorPanel)) :
            !press.Entry.IsFolder && press.Entry.Name.EndsWith(ProjectAssets.Tileset) ? SpriteSectionAt(e.GetPosition(InspectorPanel),tiles:true) : null;
        bool place = (png || press.Entry.Name.EndsWith(ProjectAssets.Prefab)) && Viewport.CanDropPng(e.GetPosition(Viewport));
        HighlightSpriteTarget(sprite);
        InspectorPanel.Classes.Set("scriptDrop", attach);
        ProjectBrowser.Cursor = FolderDrop(e, press.Entry) != null ? MoveFileCursor :
            attach || place || sprite != null ? CopyFileCursor : NoDropCursor;
        e.Handled = true;
    }

    private void FileReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (filePress is not { } press || press.Pointer != e.Pointer) return;
        bool dragged = IsFileDrag(press, e);
        bool clipDrop = dragged && !press.Entry.IsFolder && SpriteAssets.IsPng(press.Entry.Name) && ClipDrop(e,true);
        EndFileDrag();
        if (clipDrop) { e.Handled = true; return; }
        if (busy || Viewport.IsPlaying || project == null) return;
        e.Handled = true;
        var entry = press.Entry;
        if (!dragged)
        {
            if (press.DoubleClick) OpenEntry(entry);
            return;
        }
        if (ReferenceTarget(e)?.Tag is ReferenceDrop target)
        {
            if (AcceptsProjectEntry(target, entry) && ProjectAssets.Scan(project).IdFor(entry.RelativePath) is Guid id)
                target.Insert(id);
        }
        else if (FolderDrop(e, entry) is {} destination)
        {
            RunAction(async () => {
                if (clipEditor is {} clip && !await clip.RequestClose()) return;
                MoveProjectEntry(entry, destination);
            });
        }
        else if (entry.IsSource && OverInspector(e) && Selected is { } owner)
        {
            if (!File.Exists(Path.Combine(project, entry.RelativePath))) Status.Text = $"Missing {entry.RelativePath}";
            else if (ScriptToAttach(press) is not { } name) Status.Text = "Only a project GameBehaviour can be attached.";
            else if (AttachComponent(owner, new ScriptComponent { Script = name }))
                Status.Text = $"Added {name} to {owner.Name}.";
            else Status.Text = $"{owner.Name} already has {name}.";
        }
        else if (!entry.IsFolder && entry.Name.EndsWith(ProjectAssets.Tileset) &&
            SpriteSectionAt(e.GetPosition(InspectorPanel),tiles:true)?.DataContext is AttachedComponent { Component: Tilemap2DComponent map })
            AssignTileset(map,ProjectAssets.Scan(project).IdFor(entry.RelativePath));
        else if (!entry.IsFolder && entry.Name.EndsWith(ProjectAssets.Prefab) && Viewport.CanDropPng(e.GetPosition(Viewport)))
        {
            var p = Viewport.ToWorld(e.GetPosition(Viewport));
            RunAction(() => { FinishObjectChange(Scene.PlacePrefab(entry.RelativePath, new((float)p.X, (float)p.Y))); return Task.CompletedTask; });
        }
        else if (!entry.IsFolder && SpriteAssets.IsPng(entry.Name))
        {
            var path = Path.Combine(project, entry.RelativePath);
            if (SpriteSectionAt(e.GetPosition(InspectorPanel)) is { } sprite) DropOnSprite(sprite, path);
            else Viewport.DropPng(path, e.GetPosition(Viewport));
        }
    }

    private bool ClipDrop(PointerEventArgs e, bool release) => clipEditor != null &&
        clipEditor.ProjectDrop(this.PointToScreen(e.GetPosition(this)),
            ProjectBrowser.Items.OfType<ProjectEntry>().Where(p => ProjectBrowser.SelectedItems!.Contains(p))
                .Select(p => p.RelativePath).ToArray(), release);

    private void EndFileDrag()
    {
        if (filePress is not { } press) return;
        filePress = null;
        draggingFile = false;
        clipEditor?.ClearDrop();
        HighlightReference(null);
        HighlightSpriteTarget(null);
        InspectorPanel.Classes.Remove("scriptDrop");
        ProjectBrowser.Cursor = null;
        if (press.Pointer.Captured == ProjectBrowser) press.Pointer.Capture(null);
    }
}
