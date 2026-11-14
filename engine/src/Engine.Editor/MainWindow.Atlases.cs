using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Engine.Core;
using Engine.Desktop;

namespace Engine.Editor;

public partial class MainWindow
{
    private async Task EditAtlas(string? relative)
    {
        if (project == null && !await Save()) return;
        var root = project!;
        var folder = relative == null ? CreationFolder("Assets") : ParentPath(relative);
        var original = relative == null ? null : File.ReadAllBytes(Path.Combine(root,relative));
        var chosen = relative == null ? [] : SpriteAtlas.Load(Path.Combine(root,relative)).Entries.Select(c => c.Sprite).ToHashSet();
        var dialog = new Window { Title="Sprite Atlas",Width=470,Height=400,MinWidth=340,MinHeight=250,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false };
        var name = new TextBox { Text=relative == null ? "Sprites" : Path.GetFileNameWithoutExtension(relative),IsReadOnly=relative!=null };
        var list = new StackPanel { Spacing=2 };
        var error = new TextBlock { TextWrapping=Avalonia.Media.TextWrapping.Wrap,IsVisible=false };
        var run = new Button { Content=relative == null ? "Pack" : "Repack" };
        var import = new Button { Content="Import PNGs…" };
        void ShowError(Exception ex) { error.Text=ex.Message; error.IsVisible=true; }
        void Refresh()
        {
            list.Children.Clear();
            var assets = ProjectAssets.Scan(root);
            foreach (var image in assets.Entries.Where(e=>SpriteAssets.IsPng(e.Path) && File.Exists(Path.Combine(root,e.Path)) || chosen.Contains(e.Id)))
            {
                var used = assets.Sprites(root).Find(image.Id);
                var box = new CheckBox { Content=new TextBlock { Text=image.Path,TextTrimming=Avalonia.Media.TextTrimming.LeadingCharacterEllipsis },
                    IsChecked=chosen.Contains(image.Id),IsEnabled=used==null || used.Atlas==relative,HorizontalContentAlignment=HorizontalAlignment.Stretch };
                ToolTip.SetTip(box,used!=null ? $"{image.Path} · {used.Atlas}" : image.Path);
                box.IsCheckedChanged += (_,_) => { if (box.IsChecked==true) chosen.Add(image.Id); else chosen.Remove(image.Id); run.IsEnabled=chosen.Count>0; };
                list.Children.Add(box);
            }
            run.IsEnabled=chosen.Count>0;
        }
        import.Click += async (_,_) =>
        {
            try
            {
                var files=await dialog.StorageProvider.OpenFilePickerAsync(new() { Title="Import sprites",AllowMultiple=true,FileTypeFilter=[PngType] });
                var paths=new List<string>();
                foreach(var file in files) if (file.TryGetLocalPath() is {} source) paths.Add(PngImporter.Import(root,source,folder).Asset);
                var catalog=ProjectAssets.Scan(root);
                foreach (var path in paths)
                    if (catalog.IdFor(path) is Guid id && (catalog.Sprites(root).Find(id) is not {} cell || cell.Atlas==relative)) chosen.Add(id);
                Refresh();
            } catch(Exception ex) { ShowError(ex); }
        };
        run.Click += async (_,_) =>
        {
            try
            {
                var stem=name.Text?.Trim();
                if (string.IsNullOrEmpty(stem) || stem.StartsWith('.') || stem.EndsWith('.') || !SpriteAssets.IsReference(stem+".png") ||
                    stem.Contains('/') || System.Text.RegularExpressions.Regex.IsMatch(stem,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    throw new InvalidDataException("Enter a valid atlas name.");
                var target=relative ?? folder+"/"+stem+SpriteAtlas.Extension;
                var full=Path.Combine(root,target);
                if (original!=null && (!File.Exists(full) || !original.SequenceEqual(File.ReadAllBytes(full))))
                    throw new IOException("Atlas changed on disk. Reopen it before repacking.");
                if (original==null && File.Exists(full)) throw new IOException("An atlas with that name already exists.");
                dialog.IsEnabled=false;
                var ids=chosen.ToArray();
                await Task.Run(()=> AtlasPacker.Pack(root,full,ids,original!=null));
                dialog.Close(target);
            } catch(Exception ex) { dialog.IsEnabled=true; ShowError(ex); }
        };
        var grid=new Grid { Margin=new(12),RowDefinitions=new("Auto,*,Auto,Auto"),ColumnDefinitions=new("56,*"),ColumnSpacing=5,RowSpacing=5 };
        void Add(Control control,int row,int column) { Grid.SetRow(control,row);Grid.SetColumn(control,column);grid.Children.Add(control); }
        Add(new TextBlock { Text="Name",VerticalAlignment=VerticalAlignment.Center },0,0); Add(name,0,1);
        Add(new TextBlock { Text="Sources" },1,0); Add(new ScrollViewer { Content=list },1,1); Add(error,2,1);
        var cancel=new Button { Content="Cancel" }; cancel.Click+=(_,_)=>dialog.Close();
        Add(new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=5,Children={import,cancel,run} },3,1);
        dialog.Content=grid; Refresh();
        if (await dialog.ShowDialog<string?>(this) is not {} saved) return;
        var fresh=ProjectAssets.Scan(root); Viewport.ClearSprites(); clipEditor?.SetCatalog(fresh);
        BrowseFolder(ParentPath(saved),saved); Status.Text=$"Packed {chosen.Count} sprites into {saved}.";
    }
}
