using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private Guid? editSelection;
    private SceneGame? inspectedWorld;
    private long inspectedRevision, lastInspectorTick;

    private void SyncPlayInspector()
    {
        var world = Viewport.PlayWorld;
        if (world != inspectedWorld || (world != null && world.HierarchyRevision != inspectedRevision))
        {
            var id = world == null ? editSelection : Selected?.Id;
            bool sameWorld = inspectedWorld == world;
            if (!sameWorld) playEdits.Clear();
            else if (world != null)
                foreach (var key in playEdits.Keys.Where(k => !world.Objects.Contains(k.Owner)).ToArray()) playEdits.Remove(key);
            inspectedWorld = world;
            inspectedRevision = world?.HierarchyRevision ?? 0;
            var objects = world?.Objects ?? Scene.Objects;
            var selected = objects.FirstOrDefault(o => o.Id == id);
            if (sameWorld && world != null && RefreshHierarchyRows(world)) Select(selected);
            else RebuildHierarchy(selected);
        }
        else if (world != null && Selected != null && Environment.TickCount64 - lastInspectorTick >= 100)
        {
            lastInspectorTick = Environment.TickCount64;
            RefreshInspector();
        }
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
            try {
                if (project != null && (scriptDebugger != null || ScriptIdeProject.Exists(project))) ScriptIdeProject.Generate(project, scriptDebugger);
                Viewport.Play(scriptDebugger); ReloadPlayParticles(); WriteLog(new(GameLogLevel.Info,$"Play started: {sceneName}",null,0)); }
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
        ObjectName.IsReadOnly = ObjectTag.IsReadOnly = ObjectGroups.IsReadOnly = !edit;
        AffineRows.IsEnabled = PositionRow.IsEnabled = SizeRow.IsEnabled = edit;
        AddComponentButton.IsVisible = edit;
        AssetSearch.IsEnabled = ProjectBrowser.IsEnabled = ProjectActions.IsEnabled = FolderTree.IsEnabled = ProjectPath.IsEnabled = edit;
        ParentFolderButton.IsEnabled = edit && browserFolder.Length > 0;
        AddButton.IsEnabled = edit;
        NewSceneButton.IsEnabled = edit && project != null;
        NewDialogueButton.IsEnabled = NewDataButton.IsEnabled = NewScriptButton.IsEnabled = NewSharedButton.IsEnabled = edit;
        UpdateMenuState();
        SyncTimeControls();
        GizmoControls.IsEnabled=edit;
        ProjectActions.IsVisible = PauseButton.IsVisible = StepButton.IsVisible = prefabSource == null;
        ShowGameView(!edit);
        LockEditors();
        PlayButton.Content = prefabSource != null ? "Save" : new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = EditorTokens.Gap,
            Children = { EditorTokens.Icon(edit ? "IconPlay" : "IconStop"), new TextBlock { Text = edit ? "Play" : "Stop", VerticalAlignment = VerticalAlignment.Center } },
        };
        // Keep the accessible names that UI tests and assistive tools already use.
        Avalonia.Automation.AutomationProperties.SetName(PlayButton, prefabSource != null ? "Save" : edit ? "▶  Play" : "■  Stop");
    }
}
