using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow : Window
{
    private const string Suffix = ProjectAssets.Scene;
    private string? project;
    private ProjectScripts? scripts;
    private string sceneName = "Untitled";
    private readonly SceneHistory history = new();
    private readonly Dictionary<string, ComponentFoldout> foldouts = [];
    private bool refreshing;

    private bool busy;
    private bool closingApproved;
    private SceneDocument Scene => Viewport.Scene;
    private SceneObject? Selected => Viewport.Selected;
    private string ScenePath => Path.Combine(project!, "Scenes", sceneName + Suffix);

    public MainWindow() : this(null) { }

    public MainWindow(string? initialProject) : this(initialProject, null, null) { }

    private MainWindow(string? initialProject, SceneDocument.PrefabSource? source, Action<SceneDocument.PrefabSource>? saveSource)
    {
        prefabSource = source;
        savePrefabSource = saveSource;
        InitializeComponent();
        InitializeDocking();
        InitializeMenus();
        OverridesButton.Click += (_, _) => RunAction(ShowPrefabOverrides);
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
        PlayButton.Click += (_, _) => { if (prefabSource == null) TogglePlay(); else RunAction(Save); };
        AddButton.Click += (_, _) => AddObject();
        InitializeHierarchy();
        Viewport.Picked += Select;
        Viewport.TogglePicked += ToggleSelection;
        InitializeGameView();
        GizmoSnap.IsCheckedChanged+=(_,_)=>Viewport.SnapEnabled=GizmoSnap.IsChecked==true;
        Viewport.Moved += RefreshInspector;
        Viewport.MoveFinished += () => MarkDirty();
        Viewport.PathEdited += RefreshComponentList;
        Viewport.RuntimeTick += SyncPlayInspector;
        Viewport.RuntimeTick += SyncTimeControls;
        Viewport.PlayFailed += message => { SyncPlayInspector(); SetControls(); LogError(message); Status.Text = message; };
        InitializeProjectBrowser();
        InitializeSpriteDrops();
        foreach (var field in new[] { PosX, PosY, ObjWidth, ObjHeight, ObjRotation, ObjScaleX, ObjScaleY, ObjPivotX, ObjPivotY })
            field.ValueChanged += (_, _) => ChangeBounds(field);
        InitializeObjectRoles();
        ObjectName.LostFocus += (_, _) => CommitText();
        ObjectName.TextChanged += (_, _) => UpdateMenuState();
        AddHandler(GotFocusEvent, (_, _) => history.EndGroup(), RoutingStrategies.Tunnel);
        ObjectName.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { CommitText(); Viewport.Focus(); }
        };
        AddHandler(KeyDownEvent, GameKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, GameKeyUp, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, Shortcut, RoutingStrategies.Tunnel);
        Closing += OnClosing;
        Deactivated += (_, _) => { Viewport.ResetInput(); EndFileDrag(); };
        Activated += (_, _) =>
        {
            RefreshChangedScripts();
            refreshSceneReferences = true;
            spriteMonitor?.Request();
        };
        scriptRefresh.Tick += (_, _) => RefreshChangedScripts();
        Closed += (_, _) => { scriptRefresh.Stop(); Viewport.Stop(); UnloadExtensions(); };
        if (source != null)
        {
            OpenScene(source.Document.ProjectDirectory, initialProject!, source.Document);
            Viewport.PrefabFraming = true;
            Viewport.FrameCamera();
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            return;
        }
        OpenScene(null, "Untitled", SceneDocument.Starter());
        var start = initialProject ?? FindSample();
        if (start != null)
        {
            try { LoadProject(Path.GetFullPath(start)); }
            catch (Exception ex) { LogError(ex.Message); Status.Text = ex.Message; }
        }
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

    private async Task<bool> Save()
    {
        if (prefabSource != null)
        {
            Scene.NormalizeScriptFields();
            if (history.IsDirty || Scene.MigratedScriptFields)
            {
                savePrefabSource!(prefabSource);
                prefabSource = SceneDocument.OpenPrefabSource(project!, sceneName) with { Document = Scene };
                history.MarkSaved(Scene);
                UpdateTitle();
            }
            Status.Text = $"Saved {sceneName}";
            return true;
        }
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
        // Open editor panels close first; one with unsaved changes asks, and leaving waits for the next attempt.
        foreach (var kind in EditorKinds)
            if (Editor(kind) is {} editor && !await editor.RequestClose()) return false;
        if (!history.IsDirty) return true;
        var dialog = new Window { Title = prefabSource == null ? "Unsaved scene" : "Unsaved prefab", SizeToContent = SizeToContent.WidthAndHeight,
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

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closingApproved) return;
        Viewport.EndDrag();
        CommitText();
        if (!history.IsDirty && !busy && EditorKinds.All(kind => Editor(kind) == null)) return;
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

    private void UpdateTitle()
    {
        SceneCaption.Text = sceneName + (history.IsDirty ? " *" : "");
        Title = prefabSource == null ? "MoNoon" : $"Prefab · {Path.GetFileName(sceneName)}";
    }
}
