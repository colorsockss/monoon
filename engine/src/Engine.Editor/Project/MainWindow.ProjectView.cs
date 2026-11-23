using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Engine.Editor;

// Project panel chrome: path bar, folder tree and the list / thumbnail grid switch.
public partial class MainWindow
{
    private const double FolderTreeMinPanelWidth = 360;
    private bool syncingFolderTree;

    private void InitializeProjectView()
    {
        ProjectViewToggle.IsChecked = EditorUserState.Current.ProjectGrid;
        ApplyProjectView();
        ProjectViewToggle.IsCheckedChanged += (_, _) =>
        {
            ApplyProjectView();
            EditorUserState.Current.ProjectGrid = ProjectViewToggle.IsChecked == true;
            EditorUserState.Current.Save();
            RefreshProjectFiles(force: true);
        };
        ProjectPanel.SizeChanged += (_, _) => FolderTreePane.IsVisible = project != null && ProjectPanel.Bounds.Width >= FolderTreeMinPanelWidth;
        ProjectPathScroll.SizeChanged += (_, e) => { if (e.WidthChanged) FitProjectPath(); };
        FolderTree.SelectionChanged += (_, _) =>
        {
            if (!syncingFolderTree && FolderTree.SelectedItem is TreeViewItem { Tag: string folder } && folder != browserFolder) BrowseFolder(folder);
        };
    }

    private void ApplyProjectView()
    {
        bool grid = ProjectViewToggle.IsChecked == true;
        ProjectBrowser.Classes.Set("grid", grid);
        ProjectBrowser.ItemTemplate = (IDataTemplate)this.FindResource(grid ? "ProjectGridItem" : "ProjectListItem")!;
        ProjectBrowser.ItemsPanel = grid ? new FuncTemplate<Panel?>(() => new WrapPanel()) : new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
    }

    // One button per folder from the project root to `folder`; null shows the ProjectName text instead (search results, unsaved).
    private void ShowProjectPath(string? folder)
    {
        ProjectPath.Children.Clear();
        ProjectPathScroll.IsVisible = folder != null && project != null;
        ProjectName.IsVisible = !ProjectPathScroll.IsVisible;
        if (folder == null || project == null) return;
        var parts = folder.Length == 0 ? [] : folder.Split('/');
        for (int i = -1; i < parts.Length; i++)
        {
            var path = i < 0 ? "" : string.Join('/', parts[..(i + 1)]);
            var name = i < 0 ? new DirectoryInfo(project).Name : parts[i];
            if (i >= 0) ProjectPath.Children.Add(new PathIcon { Theme = (Avalonia.Styling.ControlTheme)this.FindResource("EditorIcon")!, Width = 12, Height = 12,
                Data = EditorTokens.Geometry("IconChevronRight"), Foreground = EditorTokens.Brush("EditorMuted") });
            var crumb = new Button { Classes = { "crumb", "quiet" }, Tag = path, Content = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 120 } };
            crumb.Classes.Set("current", i == parts.Length - 1);
            ToolTip.SetTip(crumb, i < 0 ? project : path);
            AutomationProperties.SetName(crumb, i < 0 ? "Project root" : $"Folder {path}");
            crumb.Click += (_, _) => { if (!busy && !Viewport.IsPlaying && path != browserFolder) BrowseFolder(path); };
            ProjectPath.Children.Add(crumb);
        }
        // A leading "…" stands in for the folders that do not fit; the current folder always shows.
        // 1px bottom padding: an even height keeps "…" on the crumbs' centre line at 100% scale (13px text would sit half a pixel off).
        var more = new TextBlock { Text = "…", Classes = { "label" }, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0), Padding = new Thickness(0, 0, 0, 1), IsVisible = false };
        ToolTip.SetTip(more, folder.Length == 0 ? project : folder);
        ProjectPath.Children.Insert(0, more);
        Dispatcher.UIThread.Post(FitProjectPath, DispatcherPriority.Loaded);
        SyncFolderTree();
    }

    private void FitProjectPath()
    {
        var items = ProjectPath.Children.ToList();
        if (items.Count < 2) return;
        foreach (var item in items) item.IsVisible = true;
        items[0].IsVisible = false;
        double Width() => items.Where(i => i.IsVisible).Sum(i => { i.Measure(Size.Infinity); return i.DesiredSize.Width + i.Margin.Left + i.Margin.Right; });
        // Drop whole leading crumbs (with their separators) until the rest fits.
        for (int i = 1; i < items.Count - 1 && Width() > ProjectPathScroll.Bounds.Width; i++)
        {
            items[i].IsVisible = false;
            items[0].IsVisible = true;
        }
    }

    private void RebuildFolderTree()
    {
        var expanded = FolderTree.Items.OfType<TreeViewItem>().SelectMany(Descendants).Where(i => i.IsExpanded).Select(i => (string)i.Tag!).ToHashSet();
        syncingFolderTree = true;
        FolderTree.Items.Clear();
        syncingFolderTree = false;
        FolderTreePane.IsVisible = project != null && ProjectPanel.Bounds.Width >= FolderTreeMinPanelWidth;
        if (project == null) return;
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.Hidden | FileAttributes.ReparsePoint };
        void Add(ItemCollection items, DirectoryInfo directory, string relative, int depth)
        {
            var item = FolderItem(relative, directory.Name);
            item.IsExpanded = relative.Length == 0 || expanded.Contains(relative);
            items.Add(item);
            if (depth > 16) return;
            try
            {
                bool dotnetRoot = relative.Length == 0 && directory.EnumerateFiles("*.csproj").Any();
                foreach (var child in directory.EnumerateDirectories("*", options)
                    .Where(d => !d.Name.StartsWith('.') && !(dotnetRoot && d.Name is "bin" or "obj"))
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                    Add(item.Items, child, relative.Length == 0 ? child.Name : relative + "/" + child.Name, depth + 1);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        Add(FolderTree.Items, new DirectoryInfo(project), "", 0);
        SyncFolderTree();
    }

    private static IEnumerable<TreeViewItem> Descendants(TreeViewItem item) =>
        item.Items.OfType<TreeViewItem>().SelectMany(Descendants).Prepend(item);

    private TreeViewItem FolderItem(string relative, string name) => new()
    {
        Header = name, Tag = relative,
        HeaderTemplate = new FuncDataTemplate<object>((_, _) =>
        {
            var row = new Grid { ColumnDefinitions = new("16,*"), ColumnSpacing = EditorTokens.Gap };
            row.Children.Add(new PathIcon { Theme = (Avalonia.Styling.ControlTheme)this.FindResource("EditorIcon")!, Data = EditorTokens.Geometry("IconFolder"), Foreground = EditorTokens.Brush("EditorMuted") });
            var text = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(text, relative.Length == 0 ? name : relative);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            return row;
        }, supportsRecycling: false),
    };

    // The tree follows the folder shown in the file area.
    private void SyncFolderTree()
    {
        var item = FolderTree.Items.OfType<TreeViewItem>().SelectMany(Descendants).FirstOrDefault(i => (string?)i.Tag == browserFolder);
        syncingFolderTree = true;
        for (var parent = item?.Parent as TreeViewItem; parent != null; parent = parent.Parent as TreeViewItem) parent.IsExpanded = true;
        FolderTree.SelectedItem = item;
        syncingFolderTree = false;
    }
}
