using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private async Task EditTileset(string? relative)
    {
        if (project == null && !await Save()) return;
        var window = new TilesetWindow(project!, CreationFolder("Assets"), relative, Scene);
        if (await window.ShowDialog<string?>(this) is not { } saved) return;
        Viewport.ClearSprites();
        BrowseFolder(ParentPath(saved), saved);
        RefreshComponentList();
        Status.Text = $"Saved {saved}.";
    }

    private void CreateTilemap(SceneObject? parent)
    {
        string name = "Tilemap";
        for (int i = 2; Scene.Objects.Any(o => o.Parent == parent && o.Name == name); i++) name = $"Tilemap {i}";
        var obj = new SceneObject { Name = name };
        obj.AddComponent(new Tilemap2DComponent()); Scene.Objects.Add(obj);
        if (parent != null) Scene.Reparent(obj, parent, keepWorld: false);
        FinishObjectChange(obj); FocusHierarchySelection();
    }

    private void AssignTileset(Tilemap2DComponent map, Guid? id)
    {
        if (map.Tileset == id) return;
        map.Tileset = id; Viewport.EditTiles(null); MarkDirty(); RefreshComponentList();
    }

    private void TileTools(Tilemap2DComponent map, Action<string, Control> row)
    {
        var palette = Viewport.Palette(map.Tileset);
        var editing = Selected != null && Viewport.TileEditOwner == Selected.Id;
        var toggle = new Button
        {
            Content = editing ? "Finish Editing" : "Edit Tiles",
            IsEnabled = palette != null || editing,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        toggle.Click += (_, _) => { Viewport.EditTiles(editing ? null : Selected?.Id); RefreshComponentList(); Viewport.Focus(); };
        row("", toggle);
        if (!editing || palette == null) return;
        var tools = new ComboBox { ItemsSource = Enum.GetValues<TileTool>(), SelectedItem = Viewport.Tool, HorizontalAlignment = HorizontalAlignment.Stretch };
        tools.SelectionChanged += (_, _) => { if (tools.SelectedItem is TileTool tool) { Viewport.Tool = tool; Viewport.RefreshScene(); } };
        row("Tool", tools);
        var bitmap = Viewport.TileImage(palette.Image);
        var tiles = palette.Tiles.Values.OrderByDescending(t => t.AutoEdges != 0).ThenBy(t => t.Id).ToArray();
        var current = tiles.FirstOrDefault(t => t.Id == Viewport.BrushTile) ?? tiles.First();
        Viewport.BrushTile = current.Id;
        var picker = new ComboBox
        {
            ItemsSource = tiles,
            SelectedItem = current,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<TileDefinition>((tile, _) =>
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
                if (bitmap != null && tile != null) line.Children.Add(new Image { Width = 20, Height = 20, Source = new SpriteCellImage(bitmap, tile.Source) });
                line.Children.Add(new TextBlock { Text = $"#{tile?.Id}{(tile?.AutoEdges > 0 ? " · Terrain" : "")}{(tile?.Collision is {} shape ? shape.OneWay ? " · One-way" : " · Polygon" : tile?.Solid == true ? " · Solid" : "")}", VerticalAlignment = VerticalAlignment.Center });
                return line;
            })
        };
        picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is TileDefinition tile) { Viewport.BrushTile = tile.Id; Viewport.RefreshScene(); } };
        row("Tile", picker);
    }
}
