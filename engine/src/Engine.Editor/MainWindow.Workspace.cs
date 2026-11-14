using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private SceneRecovery? recovery;
    private readonly NativeMenu extensionMenu = new();
    private readonly NativeMenuItem extensionItem = new("Extensions");
    private readonly List<NativeMenuItem> projectCommands = [];
    private readonly DispatcherTimer checkpointTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private string? pendingSnapshot;
    private string? migrationReport;

    private void InitializeWorkspace(NativeMenu fileMenu, NativeMenu projectMenu)
    {
        NativeMenuItem Item(string label, Func<Task> action)
        {
            var item = new NativeMenuItem(label);
            item.Click += (_, _) => RunAction(action);
            projectCommands.Add(item);
            return item;
        }
        fileMenu.Add(new NativeMenuItemSeparator());
        var fromTemplate = Item("New from Template…", CreateFromTemplate);
        projectCommands.Remove(fromTemplate);
        fileMenu.Add(fromTemplate);
        fileMenu.Add(Item("Export Project Template…", () => ExportArchive(WorkspaceArchives.Template)));
        projectMenu.Add(new NativeMenuItemSeparator());
        projectMenu.Add(Item("Import Package…", ImportPackage));
        projectMenu.Add(Item("Export Package…", () => ExportArchive(WorkspaceArchives.Package)));
        projectMenu.Add(Item("Recover Scene Edits…", PromptRecovery));
        extensionItem.Menu = extensionMenu;
        projectMenu.Add(extensionItem);
        projectMenu.Add(Item("Reload Extensions", () => { RefreshExtensions(); return Task.CompletedTask; }));
        checkpointTimer.Tick += (_, _) => FlushCheckpoint();
        Closed += (_, _) => { FlushCheckpoint(); recovery?.Dispose(); };
    }

    private void RefreshExtensions()
    {
        extensionMenu.Items.Clear();
        if (project != null)
        {
            try
            {
                foreach (var command in WorkspaceArchives.Commands(project))
                {
                    var item = new NativeMenuItem(command.Label);
                    item.Click += (_, _) => RunAction(() => {
                        var center = Viewport.EditCenter;
                        FinishObjectChange(Scene.PlacePrefab(command.Prefab, new((float)center.X, (float)center.Y)));
                        Status.Text = command.Label;
                        return Task.CompletedTask;
                    });
                    extensionMenu.Add(item);
                }
            }
            catch (Exception ex) { LogError(ex.Message); Status.Text = ex.Message; }
        }
        UpdateMenuState();
    }

    private void QueueCheckpoint()
    {
        if (recovery == null) return;
        pendingSnapshot = history.CurrentSnapshot;
        if (!checkpointTimer.IsEnabled) checkpointTimer.Start();
    }

    private void FlushCheckpoint()
    {
        checkpointTimer.Stop();
        if (recovery == null || pendingSnapshot == null) return;
        try
        {
            recovery.Checkpoint(pendingSnapshot);
            pendingSnapshot = null;
        }
        catch (Exception ex) { LogError(ex.Message); Status.Text = $"Recovery checkpoint failed: {ex.Message}"; }
    }

    private void StartWorkspace(string? root)
    {
        checkpointTimer.Stop();
        pendingSnapshot = null;
        recovery?.ClearOwn();
        recovery = root == null ? null : new SceneRecovery(root, "Scenes/" + sceneName + Suffix, Scene.CaptureObjects());
        RefreshExtensions();
    }

    private void PrepareMigration(string root)
    {
        migrationReport = null;
        LegacyProject.Upgrade(root, paths => migrationReport = MigrationBackup.Create(root, paths));
    }

    private async Task<string?> Choice(string title, string description, params string[] choices)
    {
        var dialog = new Window { Title = title, Width = 510, MinWidth = 360, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var label in choices)
        {
            var button = new Button { Content = label, Margin = new Thickness(4, 0, 0, 0), IsCancel = label == "Cancel" };
            button.Click += (_, _) => dialog.Close(label);
            buttons.Children.Add(button);
        }
        dialog.Content = new StackPanel { Margin = new Thickness(14), Spacing = 10, Children = {
            new ScrollViewer { MaxHeight = 240, Content = new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap } }, buttons } };
        return await dialog.ShowDialog<string?>(this);
    }

    private async Task<string?> MergeWithDisk(string baseline, string edited, string disk)
    {
        var result = SceneMerge.Combine(baseline, edited, disk, ConflictChoice.Local);
        if (result.Conflicts.Count == 0) return result.Snapshot;
        var answer = await Choice("Scene merge conflicts",
            "Both copies changed these fields. Choose which values to keep for the listed conflicts. Other edits will be combined.\n\n" +
            string.Join("\n", result.Conflicts), "Cancel", "Use Disk", "Use My Edits");
        return answer switch {
            "Use My Edits" => result.Snapshot,
            "Use Disk" => SceneMerge.Combine(baseline, edited, disk, ConflictChoice.Disk).Snapshot,
            _ => null
        };
    }

    private void ApplySnapshot(string snapshot)
    {
        var selected = Selected?.Id;
        Scene.RestoreObjects(snapshot);
        Scene.ResolveAssets(ProjectAssets.Scan(project!));
        RebuildHierarchy(Scene.Objects.FirstOrDefault(o => o.Id == selected) ?? Scene.Objects.FirstOrDefault());
        RecordEdit(selected);
        Viewport.ClearSprites();
        Viewport.RefreshScene();
    }

    private async Task<bool> SaveWorkspaceScene()
    {
        if (recovery == null) { Scene.Save(ScenePath); return true; }
        var onDisk = File.Exists(ScenePath) ? File.ReadAllText(ScenePath) : null;
        string? merged = null;
        if (onDisk != recovery.Disk)
        {
            if (onDisk == null)
            {
                if (await Choice("Scene removed on disk", "Recreate the scene from the open editor?", "Cancel", "Recreate") != "Recreate") return false;
            }
            else
            {
                var incoming = SceneDocument.OpenForEditing(ScenePath).CaptureObjects();
                merged = await MergeWithDisk(recovery.Baseline, Scene.CaptureObjects(), incoming);
                if (merged == null) return false;
                SceneMerge.Normalize(merged); // Hierarchy and component validation before modifying the open scene.
            }
            if ((File.Exists(ScenePath) ? File.ReadAllText(ScenePath) : null) != onDisk)
                throw new IOException("The scene changed again during merge. Save to retry.");
        }
        if (merged != null) ApplySnapshot(merged);
        Scene.Save(ScenePath);
        return true;
    }

    private async Task PromptRecovery()
    {
        if (project == null) return;
        var root = project;
        var draft = SceneRecovery.Pending(root, LogError).FirstOrDefault();
        if (draft.Data == null) { Status.Text = "No interrupted scene edits."; return; }
        var data = draft.Data;
        var answer = await Choice("Recover scene edits", $"An interrupted session contains unsaved changes to {data.Scene}.",
            "Later", "Discard Draft", "Recover");
        if (answer == "Discard Draft") { SceneRecovery.Remove(draft.Path); return; }
        if (answer != "Recover") return;
        var name = data.Scene[7..^Suffix.Length];
        if (name != sceneName)
        {
            if (!await CanLeave()) return;
            var path = Path.Combine(root, data.Scene);
            var doc = File.Exists(path) ? SceneDocument.OpenForEditing(path) : SceneDocument.Empty(GameSettings.Load(root));
            OpenScene(root, name, doc);
        }
        var incoming = Scene.CaptureObjects();
        string? result = recovery?.Disk == data.Disk && !history.IsDirty ? data.Edited :
            await MergeWithDisk(data.Baseline, data.Edited, incoming);
        if (result == null) return;
        ApplySnapshot(result);
        recovery!.Checkpoint(Scene.CaptureObjects()); // Publish replacement before removing the interrupted session.
        SceneRecovery.Remove(draft.Path);
        Status.Text = "Recovered scene edits. Save to write the scene.";
    }

    private async Task<string?> PickArchive(string title, string extension)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = title, FileTypeFilter = [new FilePickerFileType(extension) { Patterns = ["*." + extension] }]
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<string[]?> PickPackageContents(string root)
    {
        var dialog = new Window { Title = "Package Contents", Width = 490, Height = 380,
            MinWidth = 310, MinHeight = 230, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var layout = new Grid { Margin = new Thickness(12), RowDefinitions = new("Auto,*,Auto"), RowSpacing = 6 };
        var search = new TextBox { Watermark = "Search project…" };
        Avalonia.Automation.AutomationProperties.SetName(search, "Find package files");
        layout.Children.Add(search);
        var items = new StackPanel();
        var scroll = new ScrollViewer { Content = items };
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);
        var next = new Button { Content = "Export…", IsEnabled = false };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6, Children = { cancel, next } };
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);
        var files = WorkspaceArchives.PackageFiles(root);
        var selected = new HashSet<string>(StringComparer.Ordinal);
        void Draw()
        {
            items.Children.Clear();
            foreach (var path in ProjectCatalog.Search(files, search.Text ?? ""))
            {
                var item = new CheckBox { Content = new TextBlock { Text = path, TextTrimming = TextTrimming.CharacterEllipsis },
                    HorizontalAlignment = HorizontalAlignment.Stretch, IsChecked = selected.Contains(path) };
                Avalonia.Automation.AutomationProperties.SetName(item, path);
                ToolTip.SetTip(item, path);
                item.IsCheckedChanged += (_, _) => {
                    if (item.IsChecked == true) selected.Add(path); else selected.Remove(path);
                    next.IsEnabled = selected.Count > 0;
                };
                items.Children.Add(item);
            }
        }
        next.Click += (_, _) => dialog.Close(selected.Select(p => Path.Combine(root, p)).ToArray());
        cancel.Click += (_, _) => dialog.Close();
        search.TextChanged += (_, _) => Draw();
        dialog.Content = layout;
        Draw();
        return await dialog.ShowDialog<string[]?>(this);
    }

    private async Task ExportArchive(string kind)
    {
        if (project == null || !await Save()) return;
        var root = project;
        string extension = kind == WorkspaceArchives.Template ? "monotemplate" : "monopackage";
        IReadOnlySet<string>? selection = null;
        if (kind == WorkspaceArchives.Package)
        {
            var picked = await PickPackageContents(root);
            if (picked == null) return;
            selection = await Task.Run(() => WorkspaceArchives.Dependencies(root, picked));
        }
        var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "Export " + kind, SuggestedFileName = Path.GetFileName(root) + "." + extension, DefaultExtension = extension
        });
        if (destination?.TryGetLocalPath() is not {} path) return;
        await Task.Run(() => WorkspaceArchives.Export(root, path, kind, selection));
        Status.Text = $"Exported {Path.GetFileName(path)}.";
    }

    private async Task ImportPackage()
    {
        if (await PickArchive("Import package", "monopackage") is {} file) await InstallPackage(file);
    }

    private async Task InstallPackage(string file)
    {
        if (project == null) return;
        var root = project;
        var added = await Task.Run(() => WorkspaceArchives.Install(file, root, WorkspaceArchives.Package));
        catalogFiles = null;
        RefreshProjectFiles(true);
        RefreshExtensions();
        RefreshComponentList();
        Status.Text = $"Imported package · {added.Length} new files.";
    }

    private async Task CreateFromTemplate()
    {
        if (await PickArchive("Open project template", "monotemplate") is not {} template) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose an empty folder for the new project" });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not {} root || !await CanLeave()) return;
        await Task.Run(() => WorkspaceArchives.Install(template, root, WorkspaceArchives.Template));
        LoadProject(root);
    }
}
