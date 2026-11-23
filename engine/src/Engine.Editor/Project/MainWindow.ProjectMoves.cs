using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private async Task ChangeProjectPath(ProjectEntry entry, bool rename)
    {
        if (project == null) return;
        var dialog = new Window { Title = rename ? "Rename" : "Move to Folder", Width = 390, MinWidth = 290,
            SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        var name = new TextBox { Text = entry.Name };
        var folders = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        if (!rename)
        {
            var paths = new List<string> { "." };
            void AddFolders(DirectoryInfo parent)
            {
                foreach (var child in parent.EnumerateDirectories().Where(ProjectScripts.ContentFolder).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var path = Path.GetRelativePath(project, child.FullName).Replace(Path.DirectorySeparatorChar, '/');
                    if (ProjectPathEdit.Within(path, entry.RelativePath)) continue;
                    paths.Add(path);
                    AddFolders(child);
                }
            }
            AddFolders(new DirectoryInfo(project));
            folders.ItemsSource = paths;
            folders.SelectedItem = entry.Directory == "" ? "." : entry.Directory;
        }
        string label = rename ? "Name" : "Destination folder";
        Control input = rename ? name : folders;
        AutomationProperties.SetName(input, label);
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var apply = new Button { Content = rename ? "Rename" : "Move", IsDefault = true };
        var error = new TextBlock { IsVisible = false, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        void Submit()
        {
            string value = rename ? name.Text ?? "" : (string?)folders.SelectedItem ?? ".";
            if (rename && !ProjectImport.ValidName(value)) { error.Text = "Enter a valid file or folder name."; error.IsVisible = true; return; }
            var parent = rename ? entry.Directory : value == "." ? "" : value;
            var next = (parent.Length == 0 ? "" : parent + "/") + (rename ? value : entry.Name);
            if (next == entry.RelativePath) { dialog.Close(); return; }
            try { MoveProjectEntry(entry, next); dialog.Close(); }
            catch (Exception ex) { error.Text = ex.Message; error.IsVisible = true; }
        }
        cancel.Click += (_, _) => dialog.Close();
        apply.Click += (_, _) => Submit();
        dialog.Content = new StackPanel { Margin = new Thickness(12), Spacing = 6, Children = {
            new TextBlock { Text = label }, input, error,
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6, Children = { cancel, apply } } } };
        dialog.Opened += (_, _) => {
            input.Focus();
            if (rename) { name.SelectionStart = 0; name.SelectionEnd = entry.IsFolder ? entry.Name.Length : Path.GetFileNameWithoutExtension(entry.Name).Length; }
        };
        if (clipEditor is {} clip && !await clip.RequestClose()) return;
        await dialog.ShowDialog(this);
    }

    private void MoveProjectEntry(ProjectEntry entry, string destination)
    {
        var selected = Selected?.Id;
        var snapshot = Scene.CaptureObjects();
        var edit = ProjectPathEdit.Move(project!, entry.RelativePath, destination);
        try
        {
            var rewritten = edit.RewriteSnapshot(snapshot, true);
            if (rewritten != snapshot) Scene.RestoreObjects(rewritten);
            Scene.ResolveAssets(ProjectAssets.Scan(project!));
            Scene.Settings = edit.RemapSettings(Scene.Settings, true);
        }
        catch
        {
            edit.Restore(false);
            Scene.RestoreObjects(snapshot);
            throw;
        }
        RebuildHierarchy(Scene.Objects.FirstOrDefault(o => o.Id == selected));
        RecordEdit(selected, asset: edit);
        ShowMovedEntry(edit, true);
        Status.Text = $"Moved {entry.RelativePath} to {destination}.";
    }

    private void ShowMovedEntry(ProjectPathEdit edit, bool forward)
    {
        var before = "Scenes/" + sceneName + Suffix;
        var after = edit.Remap(before, forward);
        sceneName = after["Scenes/".Length..^Suffix.Length];
        if (recovery != null)
            recovery.Relocate(project!, after, edit, forward);
        var path = forward ? edit.To : edit.From;
        catalogFiles = null;
        BrowseFolder(ParentPath(path), path);
        clipEditor?.SetCatalog(ProjectAssets.Scan(project!));
        Viewport.ClearSprites();
        RefreshExtensions();
        UpdateTitle();
    }
}
