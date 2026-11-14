using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private static readonly FilePickerFileType PngType = new("PNG image")
    {
        Patterns = ["*.png", "*.PNG"], AppleUniformTypeIdentifiers = ["public.png"], MimeTypes = ["image/png"]
    };

    private async Task<ImportedPng?> ChoosePng()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a PNG", FileTypeFilter = [PngType]
        });
        return files.FirstOrDefault()?.TryGetLocalPath() is { } path ? await ImportPng(path) : null;
    }

    private async Task<ImportedPng?> ImportPng(string path)
    {
        if (project == null && !await Save()) return null;
        string root = project!;
        var folder = CreationFolder(SpriteAssets.Folder);
        var png = await Task.Run(() => PngImporter.Import(root, path, folder));
        BrowseFolder(ParentPath(png.Asset), png.Asset);
        Viewport.InvalidateSprite(Path.Combine(root, png.Asset));
        if (clipEditor is {} clip) clip.SetCatalog(ProjectAssets.Scan(root));
        Status.Text = $"Imported {png.Asset}";
        return png;
    }

    private void SpriteMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SpriteFields fields } button || Selected is not { } owner) return;
        var menu = new MenuFlyout();
        var square = new MenuItem { Header = "Square", IsEnabled = !fields.IsSquare };
        var png = new MenuItem { Header = "Choose PNG…" };
        square.Click += (_, _) => SetSprite(owner, fields.Sprite, null);
        png.Click += (_, _) => RunAction(async () =>
        {
            if (await ChoosePng() is { } imported) SetSprite(owner, fields.Sprite, imported.Asset);
        });
        menu.Items.Add(square);
        menu.Items.Add(png);
        menu.ShowAt(button);

    }

    private void SetSprite(SceneObject owner, SpriteRendererComponent sprite, string? asset)
    {
        if (Selected != owner || !ReferenceEquals(owner.GetComponent<SpriteRendererComponent>(), sprite) ||
            sprite.Asset == asset) return;
        sprite.Asset = asset;
        sprite.AssetId = project != null && asset != null ? ProjectAssets.Scan(project).IdFor(asset) : null;
        Select(owner);
        MarkDirty();
    }

    private void SpriteMaskClick(object? sender,RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SpriteFields fields } button || Selected is not {} owner) return;
        var menu=new MenuFlyout();
        void Set(Guid? id)
        {
            if (Selected!=owner || !ReferenceEquals(fields.Sprite,owner.GetComponent<SpriteRendererComponent>()) || fields.Sprite.Mask==id) return;
            fields.Sprite.Mask=id; Select(owner); MarkDirty();
        }
        var clear=new MenuItem { Header="None",IsEnabled=fields.Sprite.Mask.HasValue };
        clear.Click+=(_,_)=>Set(null); menu.Items.Add(clear);
        var choose=new MenuItem { Header="Choose mask PNG…" };
        choose.Click+=(_,_)=>RunAction(async()=>{ if (await ChoosePng() is {} png) Set(ProjectAssets.Scan(project!).IdFor(png.Asset)); });
        menu.Items.Add(choose); menu.ShowAt(button);
    }

    private ComponentSection? spriteDropTarget;

    private ComponentSection? SpriteSectionAt(Point point,bool tiles=false)
    {
        if (busy || Viewport.IsPlaying || InspectorPanel.InputHitTest(point) is not Visual visual) return null;
        var section = visual as ComponentSection ?? visual.FindAncestorOfType<ComponentSection>();
        return section?.DataContext is AttachedComponent row && row.Owner == Selected && (tiles ? row.Component is Tilemap2DComponent : row.Component is SpriteRendererComponent)
            ? section : null;
    }

    private void HighlightSpriteTarget(ComponentSection? section)
    {
        if (spriteDropTarget == section) return;
        spriteDropTarget?.Classes.Remove("spriteDrop");
        spriteDropTarget = section;
        section?.Classes.Add("spriteDrop");
    }

    private void DropOnSprite(ComponentSection section, string path)
    {
        if (section.DataContext is not AttachedComponent { Component: SpriteRendererComponent sprite } row) return;
        RunAction(async () =>
        {
            if (await ImportPng(path) is { } imported) SetSprite(row.Owner, sprite, imported.Asset);
        });
    }

    private void InitializeSpriteDrops()
    {
        DragDrop.SetAllowDrop(AttachedComponents, true);
        AttachedComponents.AddHandler(DragDrop.DragOverEvent, SpriteFileDrag);
        AttachedComponents.AddHandler(DragDrop.DragLeaveEvent, (_, _) => HighlightSpriteTarget(null));
        AttachedComponents.AddHandler(DragDrop.DropEvent, SpriteFileDrag);
    }

    private void SpriteFileDrag(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        var path = files is [var file] ? file.TryGetLocalPath() : null;
        var section = path != null && SpriteAssets.IsPng(path) ? SpriteSectionAt(e.GetPosition(InspectorPanel)) : null;
        bool dropped = e.RoutedEvent == DragDrop.DropEvent;
        HighlightSpriteTarget(dropped ? null : section);
        e.DragEffects = section != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        if (dropped && section != null) DropOnSprite(section, path!);
    }

    private void SquareColorChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: SpriteFields fields } box ||
            !ReferenceEquals(Selected?.GetComponent<SpriteRendererComponent>(), fields.Sprite)) return;
        var color = box.Text?.Trim().ToUpperInvariant();
        if (!SceneObject.TryColor(color, out _) || fields.ColorText == color) return;
        fields.Sprite.Tint = color!;
        fields.RefreshColor();
        Viewport.RefreshScene();
        MarkDirty(box);
    }

    private void SquareColorCommitted(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: SpriteFields fields } box) return;
        if (!SceneObject.TryColor(box.Text?.Trim(), out _)) Status.Text = "Enter a color in #RRGGBB format.";
        box.SetCurrentValue(TextBox.TextProperty, fields.ColorText);
    }

    private void SquareColorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Viewport.Focus();
    }

    private async Task PlaceSprite(string path, Point world)
    {
        if (await ImportPng(path) is not { } png) return;
        var width = png.Size.Width;
        var height = png.Size.Height;
        string stem = Path.GetFileNameWithoutExtension(path), name = stem;
        for (int i = 2; Scene.Objects.Any(o => o.Name == name); i++) name = $"{stem} {i}";
        var obj = new SceneObject
        {
            Name = name, Width = width, Height = height,
            X = (float)Math.Round(world.X - width / 2.0),
            Y = (float)Math.Round(world.Y - height / 2.0)
        };
        obj.AddComponent(new SpriteRendererComponent { Asset = png.Asset, AssetId=ProjectAssets.Scan(project!).IdFor(png.Asset) });
        Scene.Objects.Add(obj);
        FinishObjectChange(obj);
    }

}
