using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Engine.Core;

namespace Engine.Editor;

internal partial class TilesetWindow : Window
{
    private readonly string root, folder;
    private readonly string? relative;
    private readonly ProjectAssets assets;
    private readonly Tileset? original;
    private readonly SceneDocument scene;
    private readonly SpriteCache<Bitmap> images = new(p => new Bitmap(p));
    private readonly Dictionary<SpriteRegion, int> terrain = [];
    private readonly Dictionary<SpriteRegion, bool> solids = [];
    private readonly Dictionary<int, SpriteRegion> replacements = [];
    private readonly Dictionary<SpriteRegion, TileDefinition> sliced = [];
    private int nextId, missingId;
    private bool updating;

    public TilesetWindow(string root, string folder, string? relative, SceneDocument scene)
    {
        InitializeComponent();
        (this.root, this.folder, this.relative, this.scene) = (root, folder, relative, scene);
        assets = ProjectAssets.Scan(root);
        original = relative == null ? null : Tileset.Load(Path.Combine(root, relative));
        AssetName.Text = relative == null ? "Tiles" : Path.GetFileNameWithoutExtension(relative);
        NameRow.IsVisible = original == null;
        Title = original == null ? "New Tileset" : $"{AssetName.Text} · Tileset";
        ShowGrid.IsChecked = original == null;
        SheetWindowLayout.ArrangeFooter(Footer, TileStatus, Actions);
        var choices = assets.Entries.Where(p => SpriteAssets.IsPng(p.Path) && File.Exists(Path.Combine(root, p.Path)))
            .Select(p => new ClipPng(p.Id, p.Path, p.Hash)).ToArray();
        Images.ItemsSource = choices;
        Images.SelectedItem = original == null ? choices.FirstOrDefault() : choices.FirstOrDefault(p => p.Id == original.Image);
        CellW.Value = original?.TileSize.Width ?? 16; CellH.Value = original?.TileSize.Height ?? 16;
        MarginBox.Value = original?.Margin ?? 0; Gap.Value = original?.Spacing ?? 0;
        Cells.SingleSelection = true;
        foreach (var tile in original?.Tiles ?? []) { solids[tile.Source] = tile.Solid; terrain[tile.Source] = tile.AutoEdges; }
        foreach (var field in new[] { CellW, CellH, MarginBox, Gap }) field.ValueChanged += (_, _) => Slice();
        Images.SelectionChanged += (_, _) => Slice();
        Cells.SelectionChanged += Describe;
        Solid.IsCheckedChanged += (_, _) =>
        {
            if (!updating && Selected is { } tile) solids[tile.Source] = Solid.IsChecked == true;
        };
        TerrainMode.SelectionChanged += (_, _) => {
            if (!updating && Selected is {} tile) terrain[tile.Source] = TerrainMode.SelectedIndex switch { 1 => 4, 2 => 6, _ => 0 };
        };
        SaveAsset.Click += (_, _) => Save();
        Cancel.Click += (_, _) => Close();
        Replace.Click += (_, _) => { if (missingId > 0 && Selected is { } tile) { replacements[missingId] = tile.Source; Save(); } };
        Closed += (_, _) => images.Dispose();
        Slice();
    }

    private TileDefinition? Selected => Cells.Regions().FirstOrDefault() is { } r ? sliced.GetValueOrDefault(r) : null;
    private void Describe()
    {
        var tile = Selected;
        updating = true;
        Solid.IsEnabled = tile != null;
        Solid.IsChecked = tile != null && solids.GetValueOrDefault(tile.Source);
        TerrainMode.IsEnabled = tile != null;
        TerrainMode.SelectedIndex = (tile == null ? 0 : terrain.GetValueOrDefault(tile.Source)) switch { 4 => 1, 6 => 2, _ => 0 };
        updating = false;
        if (missingId == 0) Info.Text = tile == null ? $"{sliced.Count} tiles · Select a cell" : $"Tile #{tile.Id} · ({tile.Source.X}, {tile.Source.Y})";
        Replace.IsEnabled = tile != null;
    }
    private void Slice()
    {
        SheetWindowLayout.UpdateGridButton(ShowGrid, (int)(CellW.Value ?? 0), (int)(CellH.Value ?? 0));
        sliced.Clear(); missingId = 0; replacements.Clear(); Replace.IsVisible = false;
        nextId = Math.Max(original?.NextId ?? 1, checked((original?.Tiles.Max(t => (int?)t.Id) ?? 0) + 1));
        var existing = (original?.Tiles ?? []).DistinctBy(t => t.Source).ToDictionary(t => t.Source);
        if (Images.SelectedItem is not ClipPng png || !images.TryGet(Path.Combine(root, png.Path), out var bitmap) ||
            CellW.Value is not decimal width || CellH.Value is not decimal height || MarginBox.Value is not decimal margin || Gap.Value is not decimal gap)
        {
            Cells.SetSheet(null, null); SaveAsset.IsEnabled = false; Info.Text = "Choose a PNG and enter the cell dimensions."; return;
        }
        var grid = new SheetGrid((int)width, (int)height, (int)margin, (int)margin, (int)gap, (int)gap);
        int rows = grid.Rows(bitmap.PixelSize.Height), columns = grid.Columns(bitmap.PixelSize.Width);
        Cells.SetSheet(bitmap, grid);
        if ((long)rows * columns > 65536) { SaveAsset.IsEnabled = false; Info.Text = "Choose a larger cell size (maximum 65,536 tiles)."; return; }
        for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            {
                var r = grid.Region(x, y);
                sliced[r] = existing.TryGetValue(r, out var t) ? t : new(nextId++, r, false);
            }
        SaveAsset.IsEnabled = sliced.Count > 0; Describe();
    }

    private void Save()
    {
        try
        {
            var name = relative == null ? AssetName.Text?.Trim() : Path.GetFileNameWithoutExtension(relative);
            if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.Contains('\\') || name.Contains('/')) throw new IOException("Enter a valid tileset name.");
            var target = relative ?? $"{folder}/{name}{ProjectAssets.Tileset}";
            var file = Path.Combine(root, target);
            if (relative == null && (name.StartsWith('.') || name.IndexOfAny("<>:\"|?*".ToCharArray()) >= 0))
                throw new IOException("Choose a visible filename that works on all platforms.");
            if (relative == null && File.Exists(file)) throw new IOException("A tileset with this name already exists.");
            var tiles = sliced.Values.Select(t => t with { Solid = solids.GetValueOrDefault(t.Source), AutoEdges = terrain.GetValueOrDefault(t.Source) }).ToList();
            var retained = tiles.Select(t => t.Id).ToHashSet();
            foreach (var old in original?.Tiles ?? [])
            {
                if (retained.Contains(old.Id)) continue;
                // Keep explicit replacements as stable IDs. No scene/prefab rewrites are needed.
                var dest = replacements.GetValueOrDefault(old.Id, old.Source);
                if (sliced.ContainsKey(dest)) tiles.Add(old with { Source=dest, Solid=solids.GetValueOrDefault(dest) });
            }
            var ids = tiles.Select(t => t.Id).ToHashSet();
            if (original != null && original.Tiles.Any(t => !ids.Contains(t.Id)) && assets.IdFor(relative!) is Guid id)
            {
                var missing = FirstMissing(id, ids);
                if (missing != null)
                {
                    missingId = missing.Value.Tile;
                    Replace.IsVisible = true;
                    Replace.IsEnabled = Selected != null;
                    Info.Text = $"{missing.Value.Location} uses #{missingId}. Select its replacement.";
                    return;
                }
            }
            var asset = new Tileset(((ClipPng)Images.SelectedItem!).Id, new((int)CellW.Value!.Value, (int)CellH.Value!.Value),
                (int)MarginBox.Value!.Value, (int)Gap.Value!.Value, tiles.ToArray())
            { NextId = nextId };
            asset.Resolve(root, assets, target);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            ProjectAssets.Write(file, asset); ProjectAssets.Scan(root); Close(target);
        }
        catch (Exception ex) { Info.Text = ex.Message; }
    }
    private (int Tile, string Location)? FirstMissing(Guid tileset, HashSet<int> valid)
    {
        foreach (var (location, doc) in Documents())
        {
            foreach (var obj in doc.Objects)
            {
                var map = obj.GetComponent<Tilemap2DComponent>();
                if (map == null || map.Tileset != tileset) continue;
                foreach (var (cell, tile) in map.OccupiedCells())
                    if (!valid.Contains(tile)) return (tile, $"{location} / {obj.Name} ({cell.X}, {cell.Y})");
            }
        }
        return null;
    }

    private IEnumerable<(string, SceneDocument)> Documents()
    {
        yield return ("Open scene", scene);
        foreach (var entry in assets.Entries.Where(e => Path.GetExtension(e.Path) is ProjectAssets.Scene or ProjectAssets.Prefab))
        {
            var file = Path.Combine(root, entry.Path);
            if (File.Exists(file)) yield return (entry.Path, SceneDocument.ReadAsset(file));
        }
    }
}
