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

    // 16.2: Draw (Simple, Tiled, Sliced) and the Left/Top/Right/Bottom borders in PNG pixels (Sliced always, Tiled optionally:
    // it then repeats the edges and middle instead of stretching them). Simple stores neither, so untouched scenes keep their
    // bytes. A mode change or the Tiled borders check is one Undo step; a border is applied while typing and recorded once.
    private Control DrawRows(SceneObject owner, SpriteRendererComponent sprite)
    {
        var rows = new StackPanel { Spacing = 2 };
        bool Current() => Inspected == owner && ReferenceEquals(sprite, owner.GetComponent<SpriteRendererComponent>());
        var mode = new ComboBox { ItemsSource = Enum.GetValues<SpriteDrawMode>(), SelectedItem = sprite.DrawMode ?? SpriteDrawMode.Simple,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        Avalonia.Automation.AutomationProperties.SetName(mode, "Draw mode");
        ToolTip.SetTip(mode, "Simple stretches the sprite, Tiled repeats it, Sliced keeps the borders and stretches the middle.");
        mode.SelectionChanged += (_, _) =>
        {
            if (!Current() || mode.SelectedItem is not SpriteDrawMode picked || picked == (sprite.DrawMode ?? SpriteDrawMode.Simple)) return;
            sprite.DrawMode = picked == SpriteDrawMode.Simple ? null : picked;
            if (picked == SpriteDrawMode.Simple) sprite.Slice = null;
            else if (picked == SpriteDrawMode.Sliced) sprite.Slice ??= DefaultSlice();
            MarkDirty();
            RefreshComponentList();
        };
        rows.Children.Add(FieldRows.Row("Draw", mode));
        UiBorders DefaultSlice()
        {
            var size = sprite.Asset != null && project != null && File.Exists(Path.Combine(project, sprite.Asset)) ? SpriteRegion.ReadPngSize(Path.Combine(project, sprite.Asset)) : new System.Drawing.Size(17, 17);
            int x = Math.Clamp((size.Width - 1) / 2, 0, 8), y = Math.Clamp((size.Height - 1) / 2, 0, 8);
            return new UiBorders(x, y, x, y);
        }
        if (sprite.DrawMode == SpriteDrawMode.Tiled)
        {
            var borders = new CheckBox { Content = "Slice borders", IsChecked = sprite.Slice != null };
            ToolTip.SetTip(borders, "Keep the corners and repeat the edges and middle.");
            borders.IsCheckedChanged += (_, _) =>
            {
                if (!Current()) return;
                sprite.Slice = borders.IsChecked == true ? sprite.Slice ?? DefaultSlice() : null;
                MarkDirty();
                RefreshComponentList();
            };
            rows.Children.Add(FieldRows.Row("", borders));
        }
        if (sprite is { DrawMode: not null, Slice: { } edges })
        {
            NumericUpDown Border(string name, int value, Func<UiBorders, int, UiBorders> with) => FieldRows.Number(name + " slice border", value, 0, 4096, 1, (v, commit) =>
            {
                if (!Current() || sprite.Slice is not { } now) return;
                sprite.Slice = with(now, (int)v);
                if (commit) MarkDirty(); else Viewport.RefreshScene();
            });
            rows.Children.Add(FieldRows.Row("Slice L·R", FieldRows.Split(Border("Left", edges.Left, (b, v) => b with { Left = v }), Border("Right", edges.Right, (b, v) => b with { Right = v }))));
            rows.Children.Add(FieldRows.Row("Slice T·B", FieldRows.Split(Border("Top", edges.Top, (b, v) => b with { Top = v }), Border("Bottom", edges.Bottom, (b, v) => b with { Bottom = v }))));
        }
        return rows;
    }

    private void SetSprite(SceneObject owner, SpriteRendererComponent sprite, string? asset)
    {
        if (Inspected != owner || !ReferenceEquals(owner.GetComponent<SpriteRendererComponent>(), sprite) ||
            sprite.Asset == asset) return;
        sprite.Asset = asset;
        sprite.AssetId = project != null && asset != null ? ProjectAssets.Scan(project).IdFor(asset) : null;
        Select(owner);
        MarkDirty();
    }

    private ComponentSection? spriteDropTarget;

    private ComponentSection? SpriteSectionAt(Point point,bool tiles=false)
    {
        if (busy || Viewport.IsPlaying || InspectorPanel.InputHitTest(point) is not Visual visual) return null;
        var section = visual as ComponentSection ?? visual.FindAncestorOfType<ComponentSection>();
        return section?.DataContext is AttachedComponent row && row.Owner == Inspected && (tiles ? row.Component is Tilemap2DComponent : row.Component is SpriteRendererComponent)
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
        // Finder files: an object field under the pointer takes the files it accepts (13.17); elsewhere on a Sprite Renderer
        // section a PNG replaces the sprite as before.
        DragDrop.SetAllowDrop(InspectorPanel, true);
        InspectorPanel.AddHandler(DragDrop.DragOverEvent, ReferenceFileDrag);
        InspectorPanel.AddHandler(DragDrop.DragLeaveEvent, (_, _) => { HighlightSpriteTarget(null); HighlightReference(null); });
        InspectorPanel.AddHandler(DragDrop.DropEvent, ReferenceFileDrag);
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

    // The Sprite Renderer's code-built rows inside the XAML template (13.9 tint).
    private SpriteFields SpriteFieldsFor(SpriteRendererComponent sprite, Action changed)
    {
        var fields = new SpriteFields(sprite, project, changed);
        var tint = new ColorField("Sprite tint", fields.ColorText, alpha: false);
        tint.Changed += (hex, commit) =>
        {
            if (!ReferenceEquals(Inspected?.GetComponent<SpriteRendererComponent>(), sprite)) return;
            sprite.Tint = hex;
            if (commit) MarkDirty(); else Viewport.RefreshScene();
        };
        var owner = Inspected!;
        Guid? current = sprite.AssetId ?? (sprite.Asset != null && project != null ? InspectorAssets(project).IdFor(sprite.Asset) : null);
        var image = AssetField("Sprite", SpriteAssets.IsPng, current, id =>
            SetSprite(owner, sprite, id is Guid picked && project != null ? InspectorAssets(project).PathFor(picked) : null), none: "Square");
        var mask = AssetField("Mask", SpriteAssets.IsPng, sprite.Mask, id =>
        {
            if (Inspected != owner || !ReferenceEquals(sprite, owner.GetComponent<SpriteRendererComponent>()) || sprite.Mask == id) return;
            sprite.Mask = id; Select(owner); MarkDirty();
        });
        return fields with { ColorEditor = tint, SpriteEditor = image, MaskEditor = mask, DrawEditor = DrawRows(owner, sprite) };
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
        // The PNG's import pivot (13.16, asset Inspector).
        if (File.Exists(Path.Combine(project!, png.Asset + ".meta")) && ProjectYaml.Read<AssetMeta>(Path.Combine(project!, png.Asset + ".meta")) is { } meta)
            (obj.PivotX, obj.PivotY) = (meta.PivotX ?? 0, meta.PivotY ?? 0);
        Scene.Objects.Add(obj);
        FinishObjectChange(obj);
    }

}
