using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private FileSystemWatcher? catalogWatcher;
    private string[]? catalogFiles;
    private int catalogDirty=1;
    private ProjectSpriteMonitor? spriteMonitor;
    private bool refreshSceneReferences;

    private void WatchCatalog(string? root)
    {
        catalogWatcher?.Dispose();
        spriteMonitor?.Dispose();
        catalogWatcher = null;
        spriteMonitor = null;
        catalogFiles = null;
        catalogDirty = 1;
        if (root == null) return;
        var monitor = new ProjectSpriteMonitor(root, ApplyImportedAssets, message => { LogError(message); Status.Text = message; });
        spriteMonitor = monitor;
        var watch = new FileSystemWatcher(root) { IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
        bool Relevant(string? path)
        {
            if (path == null) return false;
            var parts = path.Replace(Path.DirectorySeparatorChar, '/').Split('/');
            if (parts.Any(p => p.StartsWith('.') || p is "bin" or "obj" or "build" or "Builds")) return false;
            return SpriteAssets.IsPng(path) || path.EndsWith(".png.meta", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(SpriteAtlas.Extension, StringComparison.OrdinalIgnoreCase) || !Path.HasExtension(path);
        }
        void Changed(object? sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed) Interlocked.Exchange(ref catalogDirty, 1);
            if (Relevant(e.Name) || e is RenamedEventArgs renamed && Relevant(renamed.OldName)) monitor.Request();
        }
        watch.Changed += Changed;
        watch.Created += Changed;
        watch.Deleted += Changed;
        watch.Renamed += Changed;
        watch.Error += (_, _) => { Interlocked.Exchange(ref catalogDirty, 1); monitor.Request(); };
        catalogWatcher = watch;
        watch.EnableRaisingEvents = true;
        monitor.Request();
    }

    private void ApplyImportedAssets(ProjectAssets assets)
    {
        clipEditor?.SetCatalog(assets);
        RefreshProjectPreview(assets);
        if (Viewport.RefreshTileAssets(assets) && !Viewport.IsPlaying) RefreshComponentList();
        if (!refreshSceneReferences || prefabSource != null || Viewport.IsPlaying || busy) return;
        refreshSceneReferences = false;
        var before = Scene.CaptureObjects();
        var selection = Selected?.Id;
        Scene.RefreshPrefabs(assets);
        Scene.ResolveAssets(assets);
        if (Scene.CaptureObjects() == before) return;
        RebuildHierarchy(Scene.Objects.FirstOrDefault(o => o.Id == selection));
        MarkDirty();
        Viewport.ClearSprites();
    }

    private async Task ChooseAssets()
    {
        if(project==null && !await Save())return;
        var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title="Import assets into project",AllowMultiple=true });
        var inputs=files.Select(f=>f.TryGetLocalPath()??throw new IOException("Choose local files.")).ToArray();
        if(inputs.Length==0)return;
        var root=project!;var folder=browserFolder;
        var paths=await Task.Run(()=>{
            foreach(var png in inputs.Where(SpriteAssets.IsPng)) Engine.Desktop.SdlCanvas.ReadPngSize(png);
            return ProjectImport.Copy(root,folder,inputs);
        });
        catalogFiles=null;
        BrowseFolder(ParentPath(paths[0]),paths[0]);
        if(clipEditor is {} clip) clip.SetCatalog(ProjectAssets.Scan(root));
        RefreshComponentList();
        Status.Text=paths.Length==1?$"Imported {paths[0]}":$"Imported {paths.Length} assets.";
    }

    private async Task InspectAssets(bool issues)
    {
        if(project==null)return;
        var root=project;
        string? selected=(ProjectBrowser.SelectedItem as ProjectEntry) is { IsFolder:false } entry?entry.RelativePath:null;
        var dialog=new Window{Title="Asset References",Width=640,Height=415,MinWidth=425,MinHeight=260,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
        var layout=new Grid{Margin=new Thickness(12),RowSpacing=7,RowDefinitions=new("Auto,Auto,*,Auto")};
        var assetPicker=new ComboBox{HorizontalAlignment=HorizontalAlignment.Stretch};
        AutomationProperties.SetName(assetPicker,"Inspected asset");
        var toolbar=new Grid{ColumnDefinitions=new("*,Auto"),ColumnSpacing=6};toolbar.Children.Add(assetPicker);
        var refresh=new Button{Content="Rescan"};Grid.SetColumn(refresh,1);toolbar.Children.Add(refresh);layout.Children.Add(toolbar);
        var mode=new ComboBox{ItemsSource=new[]{"Dependencies","Used by","Project issues"},SelectedIndex=issues||selected==null?2:0};
        AutomationProperties.SetName(mode,"Reference view");
        var caption=new TextBlock{Classes={"label"},TextTrimming=TextTrimming.CharacterEllipsis};
        var bar=new Grid{ColumnDefinitions=new("Auto,*"),ColumnSpacing=10};bar.Children.Add(mode);Grid.SetColumn(caption,1);bar.Children.Add(caption);Grid.SetRow(bar,1);layout.Children.Add(bar);
        var list=new ListBox();
        AutomationProperties.SetName(list,"Reference results");Grid.SetRow(list,2);layout.Children.Add(list);
        var reveal=new Button{Content="Show in Project"};var close=new Button{Content="Close",IsCancel=true};
        var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=6,Children={reveal,close}};
        Grid.SetRow(footer,3);layout.Children.Add(footer);
        var report=await Task.Run(()=>ProjectCatalog.Inspect(root));
        list.ItemTemplate=new FuncDataTemplate<AssetLink>((link,_)=>{
            var path=mode.SelectedIndex==1?link.Source:link.Target;
            var where=mode.SelectedIndex==2?link.Source+"  ·  "+link.Field:link.Field;
            var title=new TextBlock{Text=path,TextTrimming=TextTrimming.LeadingCharacterEllipsis};
            var sub=new TextBlock{Text=where,Classes={"label"},TextTrimming=TextTrimming.CharacterEllipsis};
            var row=new StackPanel{Spacing=2,Children={title,sub}};
            if(link.Problem!=null)row.Children.Add(new TextBlock{Text=link.Problem,Foreground=Brushes.LightSalmon,TextWrapping=TextWrapping.Wrap});
            ToolTip.SetTip(row,path+"\n"+where);return row;
        });
        void Draw(){
            var p=assetPicker.SelectedItem as string??"";
            assetPicker.IsEnabled=mode.SelectedIndex!=2;
            var rows=(mode.SelectedIndex switch{0=>report.Uses(p),1=>report.UsedBy(p),_=>report.Problems}).ToArray();
            list.ItemsSource=rows;
            caption.Text=mode.SelectedIndex==2?(rows.Length==0?"No issues in saved files":$"{rows.Length} issues in saved files"):
                rows.Length==0?"No direct references":$"{rows.Length} direct references";
            reveal.IsEnabled=false;
        }
        string? Related(AssetLink link)=>mode.SelectedIndex==0?link.Target:link.Source;
        string? RevealPath(AssetLink link)=>Related(link) is {} path && File.Exists(Path.Combine(root,path))?path:
            File.Exists(Path.Combine(root,link.Source))?link.Source:null;
        void Populate(string? prefer){
            var paths=ProjectCatalog.Files(root);assetPicker.ItemsSource=paths;
            assetPicker.SelectedItem=paths.Contains(prefer)?prefer:paths.FirstOrDefault();Draw();
        }
        mode.SelectionChanged+=(_,_)=>Draw();assetPicker.SelectionChanged+=(_,_)=>Draw();
        list.SelectionChanged+=(_,_)=>reveal.IsEnabled=list.SelectedItem is AssetLink link && RevealPath(link)!=null;
        list.DoubleTapped+=(_,_)=>{
            if(list.SelectedItem is not AssetLink link || RevealPath(link) is not {} next || !assetPicker.Items.Contains(next))return;
            mode.SelectedIndex=0;assetPicker.SelectedItem=next;
        };
        reveal.Click+=(_,_)=>{if(list.SelectedItem is AssetLink link && RevealPath(link) is {} path)dialog.Close(path);};
        close.Click+=(_,_)=>dialog.Close();
        refresh.Click+=async (_,_)=>{
            refresh.IsEnabled=false;
            try{var next=await Task.Run(()=>ProjectCatalog.Inspect(root));report=next;Populate(assetPicker.SelectedItem as string);}
            catch(Exception e){caption.Text=e.Message;ToolTip.SetTip(caption,e.Message);}
            finally{refresh.IsEnabled=true;}
        };
        Populate(selected);dialog.Content=layout;
        if(await dialog.ShowDialog<string?>(this) is {} target) BrowseFolder(ParentPath(target),target);
    }
}
