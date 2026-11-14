using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Engine.Core;
using Vector2 = System.Numerics.Vector2;

namespace Engine.Editor;

public partial class MainWindow : Window
{
    private const string Suffix = ProjectAssets.Scene;
    private string? project;
    private ProjectScripts? scripts;
    private string sceneName = "Untitled";
    private readonly SceneHistory history = new();
    private readonly Dictionary<(Guid Owner, Type? Component, string? Script), ComponentFoldout> foldouts = [];
    private Guid? editSelection;
    private SceneGame? inspectedWorld;
    private long inspectedRevision, lastInspectorTick;
    private bool refreshing;
    private void SyncPlayInspector()
    {
        var world = Viewport.PlayWorld;
        if (world != inspectedWorld || (world != null && world.HierarchyRevision != inspectedRevision))
        {
            var id = world == null ? editSelection : Selected?.Id;
            inspectedWorld = world;
            inspectedRevision = world?.HierarchyRevision ?? 0;
            var objects = world?.Objects ?? Scene.Objects;
            foreach (var key in foldouts.Keys.Where(k => !Scene.Objects.Any(o => o.Id == k.Owner) && !objects.Any(o => o.Id == k.Owner)).ToArray())
                foldouts.Remove(key);
            RebuildHierarchy(objects.FirstOrDefault(o => o.Id == id));
        }
        else if (world != null && Selected != null && Environment.TickCount64 - lastInspectorTick >= 100)
        {
            lastInspectorTick = Environment.TickCount64;
            RefreshInspector();
        }
    }

    private bool busy;
    private bool closingApproved;
    private SceneDocument Scene => Viewport.Scene;
    private SceneObject? Selected => Viewport.Selected;
    private string ScenePath => Path.Combine(project!, "Scenes", sceneName + Suffix);

    public MainWindow() : this(null) { }

    public MainWindow(string? initialProject)
    {
        InitializeComponent();
        InitializeMenus();
        InitializeDiagnostics();
        ImportAssetsButton.Click += (_,_) => RunAction(ChooseAssets);
        ReferencesButton.Click += (_,_) => RunAction(()=>InspectAssets(false));
        ProblemsButton.Click += (_,_) => RunAction(()=>InspectAssets(true));
        NewTableButton.Click += (_,_) => RunAction(()=>EditValues(null,true));
        Viewport.PngDropped += (path, world) => RunAction(() => PlaceSprite(path, world));
        NewDataButton.Click += (_, _) => RunAction(() => EditData(null));
        NewDialogueButton.Click += (_, _) => RunAction(() => EditValues(null, false, dialogue: true));
        NewTilesetButton.Click += (_,_)=>RunAction(()=>EditTileset(null));
        Viewport.TilesChanged += (id,cells) => {
            history.RecordStroke(Scene,id,cells);
            QueueCheckpoint();
            UpdateTitle(); UpdateMenuState(); Status.Text=$"Painted {cells.Length} cells.";
        };
        Viewport.TilePicked += id => { Viewport.BrushTile=id; Viewport.Tool=TileTool.Brush; RefreshComponentList(); };
        NewAtlasButton.Click += (_,_) => RunAction(() => EditAtlas(null));
        NewAnimatorButton.Click += (_,_) => RunAction(() => EditAnimator(null));
        NewClipButton.Click += (_, _) => RunAction(() => EditClip(null));
        NewSceneButton.Click += (_, _) => RunAction(NewScene);
        NewSharedButton.Click += (_, _) => RunAction(() => CreateScript(null, shared:true));
        NewScriptButton.Click += (_, _) => RunAction(() => CreateScript(null));
        AddComponentButton.Click += (_, _) => ShowComponentMenu();
        InitializeTimeControls();
        PlayButton.Click += (_, _) => TogglePlay();
        AddButton.Click += (_, _) => AddObject();
        InitializeHierarchy();
        Viewport.Picked += Select;
        Viewport.TogglePicked += ToggleSelection;
        GizmoPicker.ItemsSource=Enum.GetValues<GizmoTool>();
        GizmoPicker.SelectedItem=GizmoTool.Move;
        GizmoPicker.SelectionChanged+=(_,_)=>{if(GizmoPicker.SelectedItem is GizmoTool tool){Viewport.EndDrag();Viewport.Gizmo=tool;Viewport.RefreshScene();}};
        GizmoSnap.IsCheckedChanged+=(_,_)=>Viewport.SnapEnabled=GizmoSnap.IsChecked==true;
        Viewport.Moved += RefreshInspector;
        Viewport.MoveFinished += () => MarkDirty();
        Viewport.RuntimeTick += SyncPlayInspector;
        Viewport.RuntimeTick += SyncTimeControls;
        Viewport.PlayFailed += message => { SyncPlayInspector(); SetControls(); LogError(message); Status.Text = message; };
        InitializeProjectBrowser();
        InitializeSpriteDrops();
        foreach (var field in new[] { PosX, PosY, ObjWidth, ObjHeight, ObjRotation, ObjScaleX, ObjScaleY, ObjPivotX, ObjPivotY })
            field.ValueChanged += (_, _) => ChangeBounds(field);
        ObjectName.LostFocus += (_, _) => CommitText();
        ObjectName.TextChanged += (_, _) => UpdateMenuState();
        AddHandler(GotFocusEvent, (_, _) => history.EndGroup(), RoutingStrategies.Tunnel);
        ObjectName.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { CommitText(); Viewport.Focus(); }
        };
        // Handle game navigation before Avalonia moves Tab focus to the Play/Stop button.
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (Viewport.IsKeyboardFocusWithin && !e.KeyModifiers.HasFlag(KeyModifiers.Meta) && Viewport.RouteUiKey(e, true)) { Viewport.Focus(); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) =>
        {
            if (Viewport.RouteUiKey(e, false)) e.Handled = true;
        }, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, Shortcut, RoutingStrategies.Tunnel);
        Closing += OnClosing;
        Deactivated += (_, _) => { Viewport.ResetInput(); EndFileDrag(); };
        Activated += (_, _) =>
        {
            RefreshChangedScripts();
            if (!Viewport.IsPlaying && !busy && project != null)
            {
                try
                {
                    var assets = ProjectAssets.Scan(project);
                    clipEditor?.SetCatalog(assets);
                    if (Viewport.RefreshTileAssets(assets)) RefreshComponentList();
                    var before = Scene.CaptureObjects();
                    var selection = Selected?.Id;
                    Scene.RefreshPrefabs(assets);
                    Scene.ResolveAssets(assets);
                    if (Scene.CaptureObjects() != before)
                    {
                        RebuildHierarchy(Scene.Objects.FirstOrDefault(o => o.Id == selection));
                        MarkDirty(); Viewport.ClearSprites();
                    }
                }
                catch (Exception ex) { LogError(ex.Message); Status.Text = ex.Message; }
            }
        };
        scriptRefresh.Tick += (_, _) => RefreshChangedScripts();
        Closed += (_, _) => scriptRefresh.Stop();
        OpenScene(null, "Untitled", SceneDocument.Starter());
        var start = initialProject ?? FindSample();
        if (start != null)
        {
            try { LoadProject(Path.GetFullPath(start)); }
            catch (Exception ex) { LogError(ex.Message); Status.Text = ex.Message; }
        }
    }

    private static string? FindSample()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "engine", "MoNoonEngine.slnx")) &&
                Directory.Exists(Path.Combine(folder.FullName, "samples", "KeyRoom")))
                return Path.Combine(folder.FullName, "samples", "KeyRoom");
        }
        return null;
    }

    private async void RunAction(Func<Task> action)
    {
        if (busy || Viewport.IsPlaying) return;
        busy = true;
        UpdateMenuState();
        Viewport.AcceptFileDrops = false;
        try { Viewport.EndDrag(); CommitText(); await action(); }
        catch (Exception ex) { LogError(ex.Message); Status.Text = ex.Message; }
        finally { busy = false; Viewport.AcceptFileDrops = true; UpdateMenuState(); }
    }

    private async Task ChooseProject(bool create)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = create ? "Choose an empty folder for the new project" : "Open project folder"
        });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } root || !await CanLeave()) return;
        if (create)
        {
            CreateProject(root, SceneDocument.Starter(), "Main");
        }
        else LoadProject(root);
    }

    private static string? FirstScene(string root)
    {
        var folder = Path.Combine(root, "Scenes");
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.ReparsePoint };
        return Directory.EnumerateFiles(folder, "*" + Suffix, options)
            .Select(path => Path.GetRelativePath(folder, path)[..^Suffix.Length].Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(name => name.Contains('/')).ThenBy(name => name, StringComparer.Ordinal).FirstOrDefault();
    }

    private void LoadProject(string root)
    {
        if (!Directory.Exists(Path.Combine(root, "Scenes")))
            throw new InvalidDataException("Choose a project folder containing a Scenes directory.");
        PrepareMigration(root);
        var settings = GameSettings.Load(root);
        var start = settings.StartScene is Guid id ? ProjectAssets.Scan(root).PathFor(id) : null;
        var name = start?.StartsWith("Scenes/") == true && start.EndsWith(Suffix) && File.Exists(Path.Combine(root, start))
            ? start[7..^Suffix.Length] : FirstScene(root);
        var scene = name == null ? SceneDocument.Empty(GameSettings.Load(root)) : SceneDocument.OpenForEditing(Path.Combine(root, "Scenes", name + Suffix));
        OpenScene(root, name ?? "Untitled", scene);
        if (migrationReport != null) Status.Text = $"Migration backup: {migrationReport}";
        if (SceneRecovery.Pending(root, LogError).Any())
            DispatcherPostRecovery(root);
    }

    private void DispatcherPostRecovery(string root)
    {
        void Offer()
        {
            if (project == root) Avalonia.Threading.Dispatcher.UIThread.Post(() => RunAction(PromptRecovery));
        }
        if (IsVisible) Offer();
        else Opened += OnOpened;
        void OnOpened(object? sender, EventArgs args) { Opened -= OnOpened; Offer(); }
    }

    private void CreateProject(string root, SceneDocument document, string name, bool savingCurrent = false)
    {
        if (Directory.EnumerateFileSystemEntries(root).Any(path => Path.GetFileName(path) != ".DS_Store"))
            throw new IOException("Choose an empty folder to create a project.");
        var folder = Directory.CreateDirectory(Path.Combine(root, "Scenes"));
        Directory.CreateDirectory(Path.Combine(root, "Scripts"));
        Directory.CreateDirectory(Path.Combine(root, SpriteAssets.Folder));
        document.Settings = document.Settings with { ProjectId = Guid.NewGuid() };
        document.Settings.Save(root);
        document.Save(Path.Combine(folder.FullName, name + Suffix));
        OpenScene(root, name, document, savingCurrent);
    }

    private async Task SwitchScene(string name)
    {
        if (project == null || !await CanLeave()) return;
        var next = SceneDocument.OpenForEditing(Path.Combine(project, "Scenes", name + Suffix));
        OpenScene(project, name, next);
    }

    private async Task NewScene()
    {
        if (project == null || !await CanLeave()) return;
        var folder = CreationFolder("Scenes");
        var name = "Scene";
        for (int i = 2; File.Exists(Path.Combine(project, folder, name + Suffix)); i++) name = $"Scene {i}";
        var scene = SceneDocument.Empty(Scene.Settings);
        var relative = $"{folder}/{name}{Suffix}";
        scene.Save(Path.Combine(project, relative));
        OpenScene(project, relative["Scenes/".Length..^Suffix.Length], scene);
        BrowseFolder(folder, relative);
    }

    private void OpenScene(string? root, string name, SceneDocument document, bool savingCurrent = false)
    {
        EndFileDrag();
        if (project != root)
        {
            browserFolder = "";
            AssetSearch.Text="";
            WatchCatalog(root);
        }
        scripts = root == null ? null : document.OpenScripts(root);
        project = root;
        sceneName = name;
        document.ProjectDirectory = root;
        if (!savingCurrent) Viewport.EditTiles(null);
        Viewport.EndDrag();
        Viewport.Scene = document;
        if (!savingCurrent) { Viewport.Selected = null; Viewport.FrameCamera(); }
        UpdateResolution();
        Viewport.ClearSprites();
        if (!savingCurrent)
        {
            foldouts.Clear();
            history.Reset(document);
        }
        RefreshProjectFiles(force: true);
        scriptRefresh.IsEnabled = scripts != null;
        RebuildHierarchy(savingCurrent ? Selected : document.Objects.FirstOrDefault());
        UpdateTitle();
        SetControls();
        StartWorkspace(root);
        Status.Text = document.LoadNotice ?? "";
    }

    private async Task<bool> Save()
    {
        if (project == null)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Save project in an empty folder" });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } root) return false;
            CreateProject(root, Scene, sceneName, savingCurrent: true);
        }
        else
        {
            if (!await SaveWorkspaceScene()) return false;
            RefreshProjectFiles(force: true);
        }
        history.MarkSaved(Scene);
        recovery?.Saved(Scene.CaptureObjects());
        pendingSnapshot = null;
        UpdateMenuState();
        UpdateTitle();
        Status.Text = $"Saved {sceneName}{Suffix}";
        return true;
    }

    private async Task<bool> CanLeave()
    {
        if (clipEditor is {} clip && !await clip.RequestClose()) return false;
        if (!history.IsDirty) return true;
        var dialog = new Window { Title = "Unsaved scene", SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var choice in new[] { "Cancel", "Discard", "Save" })
        {
            var button = new Button { Content = choice };
            button.Click += (_, _) => dialog.Close(choice);
            buttons.Children.Add(button);
        }
        dialog.Content = new StackPanel { Margin = new Thickness(17), Spacing = 16,
            Children = { new TextBlock { Text = $"Save changes to {sceneName}?" }, buttons } };
        var result = await dialog.ShowDialog<string?>(this);
        return result == "Discard" || (result == "Save" && await Save());
    }

    private async Task RunGame()
    {
        if (!Scene.Cameras.Any() && !Scene.IsUiOnly) throw new InvalidOperationException("Add a camera with Scene → Create Camera before playing.");
        if (!await Save()) return;
        if (scripts != null)
            foreach (var name in Scene.Objects.SelectMany(obj => obj.Components).OfType<ScriptComponent>()
                         .Select(component => component.Script).Distinct(StringComparer.Ordinal))
                scripts.Get(name);
        var executable = Environment.ProcessPath ?? throw new IOException("Cannot locate the game host.");
        var start = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(EditorApp).Assembly.Location);
        start.ArgumentList.Add("--run-scene");
        start.ArgumentList.Add(ScenePath);
        using var process = System.Diagnostics.Process.Start(start);
        Status.Text = $"Opened {sceneName} in the SDL runtime.";
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closingApproved) return;
        Viewport.EndDrag();
        CommitText();
        if (!history.IsDirty && !busy && clipEditor == null) return;
        e.Cancel = true;
        if (busy) return;
        Viewport.Stop();
        SetControls();
        // Leave the closing event before a modal dialog or a second Close().
        Avalonia.Threading.Dispatcher.UIThread.Post(() => RunAction(async () =>
        {
            if (await CanLeave()) { pendingSnapshot = null; recovery?.ClearOwn(); closingApproved = true; Close(); }
        }));
    }

    private void ToggleSelection(SceneObject? obj)
    {
        if(obj==null)return;
        var selection=Viewport.Selection.ToList();
        if(!selection.Remove(obj))selection.Add(obj);
        ShowSelection(selection);
    }

    private void Select(SceneObject? obj) => ShowSelection(obj==null?[]:[obj]);

    private void ShowSelection(IReadOnlyList<SceneObject> selection)
    {
        var obj=selection.LastOrDefault();
        Viewport.Selection=selection;
        InspectorTitle.Text=selection.Count>1?$"Inspector · {selection.Count} selected":"Inspector";
        foreach(var (owner,row) in treeItems)row.Classes.Set("groupSelected",selection.Count>1 && selection.Contains(owner));
        refreshing = true;
        if (Viewport.TileEditOwner != null && (Viewport.TileEditOwner != obj?.Id || obj?.GetComponent<Tilemap2DComponent>() == null))
            Viewport.EditTiles(null);
        Viewport.Selected = obj;
        SelectHierarchyItem(obj);
        UpdatePrefabMenus();
        refreshing = false;
        RefreshInspector();
        RefreshComponentList();
        Viewport.RefreshScene();
    }

    private void RefreshInspector()
    {
        refreshing = true;
        RefreshLiveInspector();
        Inspector.IsVisible = Selected != null;
        EmptyInspector.IsVisible = Selected == null;
        if (Selected is { } obj)
        {
            ObjectName.Text = obj.Name;
            PositionRow.IsVisible = obj.GetComponent<UiCanvasComponent>() == null;
            PositionRowLabel.Text = ScreenUi.IsWidget(obj) ? "Offset" : obj.Parent == null ? "Position" : "Local pos.";
            SizeRow.IsVisible = obj.GetComponent<SpriteRendererComponent>() != null || obj.GetComponent<Collider2DComponent>() != null || ScreenUi.IsWidget(obj);
            AffineRows.IsVisible = !ScreenUi.IsWidget(obj) && obj.GetComponent<UiCanvasComponent>() == null;
            ObjRotation.Value = decimal.CreateSaturating(obj.Rotation);
            ObjScaleX.Value = decimal.CreateSaturating(obj.ScaleX);
            ObjScaleY.Value = decimal.CreateSaturating(obj.ScaleY);
            ObjPivotX.Value = decimal.CreateSaturating(obj.PivotX);
            ObjPivotY.Value = decimal.CreateSaturating(obj.PivotY);
            var local = new Vector2(obj.X, obj.Y);
            if (Viewport.PlayWorld is { } world && world.TryGetPosition(obj.Id, out var position))
            {
                var shift = position - obj.WorldPosition;
                if (obj.Parent is {} p && world.TryGetPosition(p.Id, out var parent))
                    shift = Vector2.TransformNormal(shift - (parent-p.WorldPosition), Affine2D.Inverse(p.WorldMatrix));
                local += shift;
            }
            PosX.Value = decimal.CreateSaturating(local.X);
            PosY.Value = decimal.CreateSaturating(local.Y);
            ObjWidth.Value = decimal.CreateSaturating(obj.Width);
            ObjHeight.Value = decimal.CreateSaturating(obj.Height);
        }
        refreshing = false;
    }

    private void RefreshComponentList()
    {
        componentMenu?.Hide();
        var owner = Selected;
        inspectorSourceStamp = null;
        TransformSection.Foldout = owner == null ? null : FoldoutFor(owner, null);
        AttachedComponents.ItemsSource = owner?.Components.Select(c => new AttachedComponent(c, project, owner, FoldoutFor(owner, c), () =>
        {
            MarkDirty(c is Camera2DComponent ? c : null);
            Viewport.RefreshScene();
        }, ScriptInspector, PresentationFields, !Viewport.IsPlaying)).ToArray();
    }

    private void SetStartCamera(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Selected?.GetComponent<Camera2DComponent>() == null || Viewport.IsPlaying) return;
        Scene.DefaultCameraId = Selected.Id;
        MarkDirty();
        RefreshComponentList();
        Viewport.RefreshScene();
    }

    private ComponentFoldout FoldoutFor(SceneObject owner, SceneComponent? component)
    {
        var key = (owner.Id, component?.GetType(), (component as ScriptComponent)?.Script);
        if (!foldouts.TryGetValue(key, out var foldout)) foldouts.Add(key, foldout = new());
        return foldout;
    }

    private static bool HasScript(SceneObject owner, string name) => owner.Components.OfType<ScriptComponent>()
        .Any(script => StringComparer.OrdinalIgnoreCase.Equals(script.Script, name));

    private bool AttachComponent(SceneObject owner, SceneComponent component)
    {
        if (Viewport.IsPlaying || Selected != owner || !owner.AddComponent(component)) return false;
        if (component is UiImageComponent or UiTextComponent or UiButtonComponent or UiLayoutComponent or UiScrollComponent or UiTextFieldComponent) owner.AddComponent(new UiRectComponent());
        Select(owner);
        AddComponentButton.Focus();
        MarkDirty();
        return true;
    }

    private MenuFlyout? componentMenu;

    private void ShowComponentMenu()
    {
        if (busy || Viewport.IsPlaying || Selected is not { } owner) return;
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        var builtIns = new MenuItem { Header = "Built-in components" };
        foreach (var choice in ComponentChoice.Registered.Values.OrderBy(c => c.Name, StringComparer.Ordinal))
            AddChoice(builtIns, choice.Name, owner.Components.Any(c => c.GetType() == choice.Type || c is Collider2DComponent && typeof(Collider2DComponent).IsAssignableFrom(choice.Type)), choice.Create);

        var projectScripts = new MenuItem { Header = "Project scripts" };
        foreach (var name in AttachableScripts())
            AddChoice(projectScripts, name, HasScript(owner, name), () => new ScriptComponent { Script = name });
        projectScripts.IsEnabled = projectScripts.Items.Count > 0;

        var create = new MenuItem { Header = "New C# Component…" };
        create.Click += (_, _) => { if (Selected == owner) RunAction(() => CreateScript(owner)); };
        menu.Items.Add(builtIns);
        menu.Items.Add(projectScripts);
        menu.Items.Add(new Separator());
        menu.Items.Add(create);
        componentMenu = menu;
        menu.Closed += (_, _) => { if (componentMenu == menu) componentMenu = null; };
        menu.ShowAt(AddComponentButton);

        void AddChoice(MenuItem group, string name, bool attached, Func<SceneComponent> make)
        {
            var item = new MenuItem { Header = attached ? $"{name} (Added)" : name, IsEnabled = !attached };
            item.Click += (_, _) => AttachComponent(owner, make());
            group.Items.Add(item);
        }
    }

    private void ComponentSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (busy || Viewport.IsPlaying || sender is not Button button) return;
        var row = button.DataContext as AttachedComponent;
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        bool Current() => row != null && !Viewport.IsPlaying && Selected == row.Owner &&
            row.Owner.Components.Contains(row.Component);
        if (row?.Component is ScriptComponent script)
        {
            var edit = new MenuItem { Header = "Edit Script" };
            edit.Click += (_, _) => { if (Current()) RunAction(() => OpenProjectSource($"Scripts/{script.Script}.cs")); };
            menu.Items.Add(edit);
        }
        var remove = new MenuItem { Header = "Remove", IsEnabled = row != null };
        remove.Click += (_, _) =>
        {
            if (!Current()) return;
            bool resetCamera = row!.Owner == Scene.Camera && row.Component is Camera2DComponent;
            if (!row.Owner.RemoveComponent(row.Component)) return;
            Select(row.Owner);
            AddComponentButton.Focus();
            MarkDirty();
            if (resetCamera) ReportStartCamera();
        };
        menu.Items.Add(remove);
        menu.ShowAt(button);
    }

    private void CommitText()
    {
        if (refreshing || Viewport.IsPlaying || Selected is not { } obj) return;
        var name = ObjectName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            RefreshInspector();
            Status.Text = "Enter an object name.";
            return;
        }
        if (obj.Name == name) return;
        obj.Name = name;
        RebuildHierarchy(obj);
        MarkDirty();
    }

    private void ChangeBounds(NumericUpDown field)
    {
        if (refreshing || Viewport.IsPlaying || Selected is not { } obj || field.Value is not { } number) return;
        var value = (float)number;
        if (field == ObjWidth || field == ObjHeight)
            Scene.Resize(obj, field == ObjWidth ? value : obj.Width, field == ObjHeight ? value : obj.Height);
        else if (field == PosX) obj.X = value;
        else if (field == PosY) obj.Y = value;
        else if (field == ObjRotation) obj.Rotation = value;
        else if (field == ObjPivotX) obj.PivotX = value;
        else if (field == ObjPivotY) obj.PivotY = value;
        else if (field == ObjScaleX || field == ObjScaleY)
        {
            if (value == 0) { RefreshInspector(); Status.Text = "Scale must be nonzero."; return; }
            if (field == ObjScaleX) obj.ScaleX = value; else obj.ScaleY = value;
        }
        MarkDirty(field);
        RefreshInspector();
        Viewport.RefreshScene();
    }

    private void AddObject(SceneObject? parent = null, bool square = true)
    {
        if (busy || Viewport.IsPlaying || (parent != null && !Scene.Objects.Contains(parent))) return;
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
        bool resetCamera = Scene.Camera == obj;
        Scene.RemoveObject(obj);
        FinishObjectChange(obj.Parent ?? Scene.Objects.LastOrDefault());
        if (resetCamera) ReportStartCamera();
        FocusHierarchySelection();
    }

    private void ReportStartCamera() => Status.Text = Scene.Camera is { } camera ? $"Start camera: {camera.Name}" : "Add a camera with Scene → Create Camera before playing.";

    private void FinishObjectChange(SceneObject? selection,PrefabFileEdit? asset=null)
    {
        var previous = Selected?.Id;
        RebuildHierarchy(selection);
        RecordEdit(previous,asset:asset);
    }

    private void MarkDirty(object? group = null) => RecordEdit(Selected?.Id, group);

    private void RecordEdit(Guid? previous, object? group = null, PrefabFileEdit? asset=null)
    {
        Viewport.InvalidateUi();
        Scene.NormalizeCamera();
        if (!history.Record(Scene, previous, Selected?.Id, group,asset)) return;
        QueueCheckpoint();
        UpdateMenuState();
        UpdateTitle();
        Status.Text = history.IsDirty ? "Unsaved changes" : "No unsaved changes";
    }

    private void UpdateTitle()
    {
        SceneCaption.Text = sceneName + (history.IsDirty ? " *" : "");
        Title = "MoNoon";
    }

    private void TogglePlay()
    {
        if (busy) return;
        componentMenu?.Hide();
        if (Viewport.IsPlaying) { Viewport.Stop(); WriteLog(new(GameLogLevel.Info,"Play stopped.",null,0)); RefreshChangedScripts(); }
        else
        {
            Viewport.EndDrag();
            CommitText();
            editSelection = Selected?.Id;
            try { Viewport.Play(scripts); WriteLog(new(GameLogLevel.Info,$"Play started: {sceneName}",null,0)); }
            catch (Exception ex) { LogError(ex.Message); Status.Text = ex.Message; return; }
        }
        SyncPlayInspector();
        SetControls();
        if (Viewport.IsPlaying) Viewport.Focus();
        Status.Text = Viewport.IsPlaying ? "Playing. Esc stops playback." : "Returned to the edited scene.";
    }

    private void SetControls()
    {
        RefreshDiagnostics(true);
        bool edit = !Viewport.IsPlaying;
        ObjectName.IsReadOnly = !edit;
        AffineRows.IsEnabled = PositionRow.IsEnabled = SizeRow.IsEnabled = edit;
        AddComponentButton.IsVisible = edit;
        AssetSearch.IsEnabled = ProjectBrowser.IsEnabled = ProjectActions.IsEnabled = edit;
        ParentFolderButton.IsEnabled = edit && browserFolder.Length > 0;
        AddButton.IsEnabled = edit;
        NewSceneButton.IsEnabled = edit && project != null;
        NewDialogueButton.IsEnabled = NewDataButton.IsEnabled = NewScriptButton.IsEnabled = NewSharedButton.IsEnabled = edit;
        UpdateMenuState();
        SyncTimeControls();
        GizmoControls.IsVisible=edit;
        ViewMode.Text = edit ? "Scene" : "Game";
        PlayButton.Content = edit ? "▶  Play" : "■  Stop";
    }

    private void Shortcut(object? sender, KeyEventArgs e)
    {
        if (!busy && !Viewport.IsPlaying && Selected != null &&
            (Hierarchy.IsKeyboardFocusWithin || Viewport.IsKeyboardFocusWithin || FocusManager?.GetFocusedElement() == null))
        {
            if (DuplicateGesture.Matches(e))
            {
                RunAction(DuplicateObject);
                e.Handled = true;
                return;
            }
            if (IsDeleteObjectShortcut(e))
            {
                DeleteObject();
                e.Handled = true;
                return;
            }
        }
        if(!Viewport.IsPlaying && Viewport.IsKeyboardFocusWithin && e.KeyModifiers==KeyModifiers.None)
        {
            if(e.Key==Key.Escape && Viewport.CancelGesture()){e.Handled=true;return;}
            if(e.Key is Key.W or Key.E or Key.R){
                GizmoPicker.SelectedItem=e.Key==Key.W?GizmoTool.Move:e.Key==Key.E?GizmoTool.Rotate:GizmoTool.Scale;
                e.Handled=true;return;
            }
        }
        if (e.Key==Key.Escape && Viewport.CancelStroke()) { e.Handled=true; return; }
        if (e.Key == Key.Escape && filePress != null)
        {
            EndFileDrag();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && Viewport.IsPlaying) { TogglePlay(); e.Handled = true; }
    }
}
