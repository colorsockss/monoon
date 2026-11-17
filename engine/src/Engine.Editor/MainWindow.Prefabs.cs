using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private readonly NativeMenuItem createPrefabItem=new("Create Prefab");
    private readonly NativeMenuItem createVariantItem=new("Create Prefab Variant");
    private readonly NativeMenuItem inspectPrefabItem=new("Inspect Overrides…");
    private readonly NativeMenuItem applyPrefabItem=new("Apply to Prefab");
    private readonly NativeMenuItem revertPrefabItem=new("Revert Prefab Overrides");

    private void InitializePrefabMenus()
    {
        createPrefabItem.Click+=(_,_)=>RunAction(()=>SavePrefab(false));
        createVariantItem.Click+=(_,_)=>RunAction(()=>SavePrefab(true));
        inspectPrefabItem.Click+=(_,_)=>RunAction(ShowPrefabOverrides);
        applyPrefabItem.Click+=(_,_)=>RunAction(ApplyPrefab);
        revertPrefabItem.Click+=(_,_)=>RunAction(RevertPrefab);
        UpdatePrefabMenus();
    }

    private void UpdatePrefabMenus()
    {
        bool editable=project!=null && !busy && !Viewport.IsPlaying && Viewport.Selection.Count==1;
        createPrefabItem.IsEnabled=editable;
        inspectPrefabItem.IsEnabled=applyPrefabItem.IsEnabled=revertPrefabItem.IsEnabled=createVariantItem.IsEnabled=editable && Selected?.Prefab!=null;
    }

    private Task SavePrefab(bool variant)
    {
        if(project==null || Selected is not {} root)return Task.CompletedTask;
        var folder=CreationFolder("Assets");
        Directory.CreateDirectory(Path.Combine(project,folder));
        var invalid=Path.GetInvalidFileNameChars();
        var baseName=string.Concat(root.Name.Select(c=>invalid.Contains(c)||c=='/'||c=='\\'?'_':c)).Trim().Trim('.');
        var stem=(baseName.Length==0?"Prefab":baseName)+(variant?" Variant":"");
        string path=Path.Combine(project,folder,stem+ProjectAssets.Prefab);
        for(int i=2;File.Exists(path);i++)path=Path.Combine(project,folder,$"{stem} {i}{ProjectAssets.Prefab}");
        WritePrefabEdit(path,root,variant,false);
        return Task.CompletedTask;
    }

    private SceneDocument.PrefabSource? prefabSource;
    private readonly Action<SceneDocument.PrefabSource>? savePrefabSource;

    private Task EditPrefabSource(string relative)
    {
        var source = SceneDocument.OpenPrefabSource(project!, relative);
        var path = Path.Combine(project!, relative);
        var window = new MainWindow(relative, source, edited =>
            RecordPrefabAssetChange(path, () =>
            {
                SceneDocument.SavePrefabSource(edited);
                Scene.RefreshPrefabs(ProjectAssets.Scan(project!));
            }));
        return window.ShowDialog(this);
    }

    private void RecordPrefabAssetChange(string path, Action write)
    {
        var before = PrefabFileEdit.Read(path);
        var snapshot = Scene.CaptureObjects();
        var selected = Selected?.Id;
        try
        {
            write();
        }
        catch
        {
            var failed = new PrefabFileEdit(path, before, PrefabFileEdit.Read(path));
            if (failed.HasChanges) { failed.Restore(false); ProjectAssets.Scan(project!); }
            Scene.RestoreObjects(snapshot);
            RebuildHierarchy(Scene.Objects.FirstOrDefault(o => o.Id == selected));
            throw;
        }
        var edit = new PrefabFileEdit(path, before, PrefabFileEdit.Read(path));
        Viewport.ClearSprites();
        FinishObjectChange(Scene.Objects.FirstOrDefault(o => o.Id == selected), edit.HasChanges ? edit : null);
    }

    private Task ShowPrefabOverrides()
    {
        if (Selected?.Prefab is not {} link || project == null) return Task.CompletedTask;
        var rootId = Selected.Id;
        var relative = ProjectAssets.Scan(project).PathFor(link.AssetId) ?? throw new IOException("Missing prefab source.");
        var grid = new Grid { RowDefinitions = new("Auto,*,Auto,Auto,Auto"), RowSpacing = 6, Margin = new Thickness(10) };
        var path = new TextBlock { Text = relative, TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTip.SetTip(path, relative);
        var list = new ListBox
        {
            ItemTemplate = new FuncDataTemplate<object>((item, _) =>
            {
                var label = item is PropertyOverride change ? $"{change.Property.Label} · {change.Owner}" : item as string;
                var text = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis };
                ToolTip.SetTip(text, label);
                return text;
            })
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        var details = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxHeight = 64 };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var apply = new Button { Content = "Apply to Source" };
        var revert = new Button { Content = "Revert Property" };
        var done = new Button { Content = "Done" };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right, Children = { revert, apply, done } };
        void Add(Control control, int row) { Grid.SetRow(control, row); grid.Children.Add(control); }
        Add(path, 0); Add(list, 1); Add(details, 2); Add(status, 3); Add(buttons, 4);
        var window = new Window { Title = "Prefab Overrides", Width = 435, Height = 315, MinWidth = 350, MinHeight = 235,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = grid };
        SceneObject Root() => Scene.Objects.Single(o => o.Id == rootId);
        void Describe()
        {
            var change = list.SelectedItem as PropertyOverride;
            revert.IsEnabled = apply.IsEnabled = change != null;
            details.Text = change == null ? (list.ItemCount == 0 ? "No overrides" : "Use the Scene menu for whole-instance changes.")
                : $"Source   {change.SourceValue}\nInstance   {change.LocalValue}";
            ToolTip.SetTip(details, details.Text);
        }
        void Reload()
        {
            var root = Root();
            list.ItemsSource = Scene.PropertyOverrides(root).Cast<object>().Concat(Scene.StructuralOverrides(root)).ToArray();
            list.SelectedIndex = list.ItemCount > 0 ? 0 : -1;
            Describe();
        }
        void Edit(bool toSource)
        {
            if (list.SelectedItem is not PropertyOverride change) return;
            try
            {
                if (toSource)
                    RecordPrefabAssetChange(Path.Combine(project, relative), () => Scene.EditPrefabProperty(Root(), change, true));
                else
                {
                    Scene.EditPrefabProperty(Root(), change, false);
                    FinishObjectChange(Root());
                }
                status.IsVisible = false;
                Reload();
            }
            catch (Exception ex) { LogError(ex.Message); status.Text = ex.Message; status.IsVisible = true; }
        }
        list.SelectionChanged += (_, _) => Describe();
        apply.Click += (_, _) => Edit(true);
        revert.Click += (_, _) => Edit(false);
        done.Click += (_, _) => window.Close();
        Reload();
        return window.ShowDialog(this);
    }

    private Task ApplyPrefab()
    {
        if(project!=null && Selected?.Prefab is {} link)
        {
            var relative=ProjectAssets.Scan(project).PathFor(link.AssetId)??throw new IOException("Missing prefab source.");
            WritePrefabEdit(Path.Combine(project,relative),Selected,false,true);
        }
        return Task.CompletedTask;
    }

    private Task RevertPrefab()
    {
        if(Selected?.Prefab!=null)FinishObjectChange(Scene.RevertPrefab(Selected));
        return Task.CompletedTask;
    }

    private void WritePrefabEdit(string path,SceneObject root,bool variant,bool apply)
    {
        var before=PrefabFileEdit.Read(path);
        var snapshot=Scene.CaptureObjects();
        try
        {
            if(variant)Scene.CreatePrefabVariant(root,path);
            else Scene.ExportPrefab(root,path,apply);
            if(apply)Scene.RefreshPrefabs(ProjectAssets.Scan(project!));
        }
        catch
        {
            PrefabFileEdit.RestoreFiles(path,before);
            Scene.RestoreObjects(snapshot);
            ProjectAssets.Scan(project!);
            RebuildHierarchy(Scene.Objects.FirstOrDefault(o=>o.Id==root.Id));
            throw;
        }
        var relative=Path.GetRelativePath(project!,path);
        BrowseFolder(Path.GetDirectoryName(relative)??"",relative);
        var assetEdit=new PrefabFileEdit(path,before,PrefabFileEdit.Read(path));
        FinishObjectChange(Scene.Objects.First(o=>o.Id==root.Id),assetEdit.HasChanges?assetEdit:null);
        Status.Text=$"{(apply?"Applied":"Created")} {relative}";
    }
}
