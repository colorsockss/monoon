using Engine.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Engine.Editor;

public partial class MainWindow
{
    private readonly NativeMenuItem mixerItem=new("Audio Mixer…");
    private readonly NativeMenuItem inputItem = new() { Header = "Input Actions…" };
    private readonly NativeMenuItem newProjectItem = new() { Header = "New Project…" };
    private readonly NativeMenuItem openProjectItem = new() { Header = "Open Project…" };
    private readonly NativeMenuItem saveSceneItem = new() { Header = "Save Scene" };
    private readonly NativeMenuItem undoItem = new() { Header = "Undo" };
    private readonly NativeMenuItem redoItem = new() { Header = "Redo" };
    private readonly NativeMenuItem preferencesItem = new() { Header = "Preferences…" };
    private readonly NativeMenuItem playItem = new() { Header = "Play" };
    private readonly NativeMenuItem runGameItem = new() { Header = "Save & Run" };

    private readonly NativeMenuItem buildItem = new("Build Game…");
    private readonly NativeMenuItem resolutionItem = new() { Header = "Game Resolution…" };
    private readonly NativeMenuItem createCameraItem = new() { Header = "Create Camera" };
    private readonly NativeMenuItem frameCameraItem = new() { Header = "Frame Camera" };

    private void InitializeMenus()
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        BindMenuAction(newProjectItem, new KeyGesture(Key.N, modifier), () => ChooseProject(true));
        BindMenuAction(openProjectItem, new KeyGesture(Key.O, modifier), () => ChooseProject(false));
        BindMenuAction(saveSceneItem, new KeyGesture(Key.S, modifier), Save);
        BindMenuAction(undoItem, new KeyGesture(Key.Z, modifier), () => RestoreEdit(false));
        BindMenuAction(redoItem, new KeyGesture(Key.Z, modifier | KeyModifiers.Shift), () => RestoreEdit(true));
        BindMenuAction(preferencesItem, new KeyGesture(Key.OemComma, modifier), EditPreferences);
        playItem.Click += (_, _) => TogglePlay();
        runGameItem.Click += (_, _) => RunAction(RunGame);

        resolutionItem.Click += (_, _) => RunAction(EditGameSettings);
        createCameraItem.Click += (_, _) => CreateCamera();
        frameCameraItem.Click += (_, _) => Viewport.FrameCamera();
        var menus = new NativeMenu();
        menus.Add(new NativeMenuItem("File") { Menu = new NativeMenu
        {
            newProjectItem, openProjectItem, new NativeMenuItemSeparator(), saveSceneItem
        } });
        var editMenu = new NativeMenu { undoItem, redoItem };
        menus.Add(new NativeMenuItem("Edit") { Menu = editMenu });
        if (OperatingSystem.IsMacOS())
        {
            var app = Application.Current!;
            var applicationMenu = NativeMenu.GetMenu(app) ?? new NativeMenu();
            applicationMenu.Items.Insert(0, preferencesItem);
            NativeMenu.SetMenu(app, applicationMenu);
        }
        else
        {
            editMenu.Add(new NativeMenuItemSeparator());
            editMenu.Add(preferencesItem);
        }
        mixerItem.Click+=(_,_)=>RunAction(EditMixer);
        inputItem.Click += (_, _) => RunAction(EditInput);
        var access = new NativeMenuItem("UI Accessibility…");
        access.Click+=(_,_)=>RunAction(EditUiDefaults);
        var console = new NativeMenuItem("Console…");
        console.Click += (_, _) => ShowConsole();
        var projectMenu = new NativeMenu { resolutionItem, inputItem, mixerItem, access, console };
        InitializeWorkspace(((NativeMenuItem)menus.Items[0]).Menu!, projectMenu);
        menus.Add(new NativeMenuItem("Project") { Menu = projectMenu });
        InitializePrefabMenus();
        menus.Add(new NativeMenuItem("Scene") { Menu = new NativeMenu { createCameraItem, frameCameraItem,new NativeMenuItemSeparator(),createPrefabItem,createVariantItem,inspectPrefabItem,applyPrefabItem,revertPrefabItem } });
        var profiler=new NativeMenuItem("Frame Profiler");
        profiler.Click+=(_,_)=>SetProfiler(FrameDebug.IsChecked!=true);
        menus.Add(new NativeMenuItem("Run") { Menu = new NativeMenu { playItem, runGameItem, new NativeMenuItemSeparator(),profiler } });
        buildItem.Click += (_, _) => RunAction(BuildGame);
        menus.Add(new NativeMenuItem("Build") { Menu = new NativeMenu { buildItem } });
        NativeMenu.SetMenu(this, menus);
    }

    private async Task RestoreEdit(bool redo)
    {
        Viewport.Focus();
        if (redo ? history.CanRedo : history.CanUndo)
        {
            var group=Viewport.Selection.Select(o=>o.Id).ToArray();
            var fileEdit = history.FileEdit(redo);
            if (fileEdit is ProjectPathEdit moveClip && clipEditor?.RelativePath is {} clipPath &&
                moveClip.Remap(clipPath, redo) != clipPath && !await clipEditor.RequestClose()) return;
            var selection = history.Restore(Scene, redo);
            if (fileEdit is ProjectPathEdit move) ShowMovedEntry(move, redo);
            Viewport.InvalidateUi();
            RebuildHierarchy(Scene.Objects.FirstOrDefault(obj => obj.Id == selection));
            if(group.Length>1 && selection==group[^1]) ShowSelection(group.Select(id=>Scene.Objects.FirstOrDefault(o=>o.Id==id)).OfType<SceneObject>().ToList());
            RefreshProjectFiles(force:true);
            UpdateResolution();
            UpdateTitle();
            QueueCheckpoint();
            Status.Text = redo ? "Redid edit." : "Undid edit.";
        }
    }

    private void BindMenuAction(NativeMenuItem item, KeyGesture gesture, Func<Task> action)
    {
        item.Gesture = gesture;
        item.Click += (_, _) => RunAction(action);
        // NativeMenuBar displays gestures in its in-window menu; handle them there as well.
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (NativeMenu.GetIsNativeMenuExported(this) || !item.IsEnabled || !gesture.Matches(e)) return;
            e.Handled = true;
            RunAction(action);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void UpdateMenuState()
    {
        UpdatePrefabMenus();
        bool workspaceEnabled = !busy && !Viewport.IsPlaying && project != null;
        foreach (var command in projectCommands) command.IsEnabled = workspaceEnabled;
        extensionItem.IsEnabled = workspaceEnabled && extensionMenu.Items.Count != 0;
        bool edit = !busy && !Viewport.IsPlaying;
        bool editingName = ObjectName.IsFocused && Selected is { } obj &&
            !string.IsNullOrWhiteSpace(ObjectName.Text) && ObjectName.Text.Trim() != obj.Name;
        undoItem.IsEnabled = edit && (history.CanUndo || editingName);
        redoItem.IsEnabled = edit && history.CanRedo && !editingName;
        inputItem.IsEnabled = edit;
        newProjectItem.IsEnabled = openProjectItem.IsEnabled = saveSceneItem.IsEnabled = edit;
        preferencesItem.IsEnabled = runGameItem.IsEnabled = resolutionItem.IsEnabled = edit;
        buildItem.IsEnabled = createCameraItem.IsEnabled = edit;
        mixerItem.IsEnabled=!busy;
        frameCameraItem.IsEnabled = edit && Scene.Camera != null;
        playItem.Header = Viewport.IsPlaying ? "Stop" : "Play";
        playItem.IsEnabled = PlayButton.IsEnabled = !busy;
    }
}
