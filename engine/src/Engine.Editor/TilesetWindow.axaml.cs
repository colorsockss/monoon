using System.Globalization;
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
    // Gameplay terrain name and navigation cost, separate from the auto-terrain brush above.
    private readonly Dictionary<SpriteRegion, (string? Name, int? Cost)> ground = [];
    private sealed record CollisionDraft(int Mode, string Points);
    private readonly Dictionary<SpriteRegion, CollisionDraft> collisions = [];
    private static readonly CollisionDraft Empty = new(0, "");
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
        foreach (var tile in original?.Tiles.DistinctBy(t => t.Source) ?? [])
        {
            collisions[tile.Source] = new(tile.Collision is {} shape ? shape.OneWay ? 3 : 2 : tile.Solid ? 1 : 0,
                string.Join("  ", (tile.Collision?.Vertices ?? []).Select(p => FormattableString.Invariant($"{p.X:R},{p.Y:R}"))));
            terrain[tile.Source] = tile.AutoEdges;
            ground[tile.Source] = (tile.Terrain, tile.MoveCost);
        }
        foreach (var field in new[] { CellW, CellH, MarginBox, Gap }) field.ValueChanged += (_, _) => Slice();
        Images.SelectionChanged += (_, _) => Slice();
        Cells.SelectionChanged += Describe;
        ShapeMode.SelectionChanged += (_, _) =>
        {
            if (updating || Selected is not {} tile) return;
            int mode = ShapeMode.SelectedIndex;
            collisions[tile.Source] = new(mode, mode == 3 ? "0,0  1,0" : mode == 2 ? "0,1  1,0  1,1" : "");
            Describe();
        };
        PointsBox.PropertyChanged += (_, change) =>
        {
            if (updating || change.Property != TextBox.TextProperty || Selected is not {} tile) return;
            collisions[tile.Source] = new(ShapeMode.SelectedIndex, PointsBox.Text ?? "");
            PreviewCollision(tile);
        };
        TerrainMode.SelectionChanged += (_, _) => {
            if (!updating && Selected is {} tile) terrain[tile.Source] = TerrainMode.SelectedIndex switch { 1 => 4, 2 => 6, _ => 0 };
        };
        TerrainName.PropertyChanged += (_, change) =>
        {
            if (!updating && change.Property == TextBox.TextProperty && Selected is {} tile)
                ground[tile.Source] = (string.IsNullOrWhiteSpace(TerrainName.Text) ? null : TerrainName.Text.Trim(), ground.GetValueOrDefault(tile.Source).Cost);
        };
        MoveCostBox.ValueChanged += (_, _) =>
        {
            if (!updating && Selected is {} tile)
                ground[tile.Source] = (ground.GetValueOrDefault(tile.Source).Name, MoveCostBox.Value is decimal v ? (int)v : null);
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
        var draft = tile == null ? Empty : collisions.GetValueOrDefault(tile.Source, Empty);
        ShapeMode.IsEnabled = tile != null;
        ShapeMode.SelectedIndex = draft.Mode;
        VerticesRow.IsVisible = draft.Mode >= 2;
        PointsBox.Text = draft.Points;
        TerrainMode.IsEnabled = tile != null;
        TerrainMode.SelectedIndex = (tile == null ? 0 : terrain.GetValueOrDefault(tile.Source)) switch { 4 => 1, 6 => 2, _ => 0 };
        var (name, cost) = tile == null ? default : ground.GetValueOrDefault(tile.Source);
        TerrainName.IsEnabled = MoveCostBox.IsEnabled = tile != null;
        TerrainName.Text = name;
        MoveCostBox.Value = cost;
        updating = false;
        if (missingId == 0) Info.Text = tile == null ? $"{sliced.Count} tiles · Select a cell" : $"Tile #{tile.Id} · ({tile.Source.X}, {tile.Source.Y})";
        Replace.IsEnabled = tile != null;
        PreviewCollision(tile);
    }
    private TileDefinition WithCollision(TileDefinition tile)
    {
        var draft = collisions.GetValueOrDefault(tile.Source, Empty);
        TileCollision? collision = null;
        if (draft.Mode >= 2)
        {
            var vertices = new List<ColliderVertex>();
            foreach (var pair in draft.Points.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                var xy = pair.Split(',');
                if (xy.Length != 2 || !float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                    !float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    throw new InvalidDataException($"Tile #{tile.Id}: enter x,y pairs separated by spaces.");
                vertices.Add(new(x,y));
            }
            collision = new(vertices.ToArray(), draft.Mode == 3);
            collision.Validate("Tileset",tile.Id);
        }
        return tile with { Solid = draft.Mode != 0, Collision = collision };
    }

    private void PreviewCollision(TileDefinition? tile)
    {
        try
        {
            Cells.SetCollision(tile == null ? null : WithCollision(tile).Collision);
            if (tile != null && missingId == 0) Info.Text = $"Tile #{tile.Id} · ({tile.Source.X}, {tile.Source.Y})";
        }
        catch (InvalidDataException ex) { Cells.SetCollision(null); Info.Text = ex.Message; }
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
            var tiles = sliced.Values.Select(t => WithCollision(t) with { AutoEdges = terrain.GetValueOrDefault(t.Source),
                Terrain = ground.GetValueOrDefault(t.Source).Name, MoveCost = ground.GetValueOrDefault(t.Source).Cost }).ToList();
            var retained = tiles.Select(t => t.Id).ToHashSet();
            foreach (var old in original?.Tiles ?? [])
            {
                if (retained.Contains(old.Id)) continue;
                // Keep explicit replacements as stable IDs. No scene/prefab rewrites are needed.
                var dest = replacements.GetValueOrDefault(old.Id, old.Source);
                if (sliced.ContainsKey(dest)) tiles.Add(WithCollision(old with { Source = dest }));
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
