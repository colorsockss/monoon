using Avalonia.Controls;
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

    private Task ShowPrefabOverrides()
    {
        if(Selected?.Prefab is not {} link || project==null)return Task.CompletedTask;
        var items=Scene.PrefabOverrides(Selected).ToArray();
        var source=ProjectAssets.ReadManifest(project).PathFor(link.AssetId);
        var grid=new Avalonia.Controls.Grid {RowDefinitions=new("Auto,*"),RowSpacing=6,Margin=new Avalonia.Thickness(10)};
        grid.Children.Add(new TextBlock {Text=source,TextTrimming=Avalonia.Media.TextTrimming.CharacterEllipsis});
        var list=new ListBox {ItemsSource=items.Length>0?items:["No overrides"]};
        Grid.SetRow(list,1);grid.Children.Add(list);
        var window=new Window {Title="Prefab Overrides",Width=380,Height=260,MinWidth=260,MinHeight=160,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=grid};
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
