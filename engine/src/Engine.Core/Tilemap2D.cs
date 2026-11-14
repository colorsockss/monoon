using System.Collections.Immutable;
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record TileSize(int Width, int Height);
[YamlObject]
public sealed partial record TileDefinition(int Id, SpriteRegion Source, bool Solid)
{
    // A terrain brush uses 16 (square) or 64 (axial) sheet cells in mask order.
    public int AutoEdges { get; init; }
}
[YamlObject]
public sealed partial record Tileset(Guid Image, TileSize TileSize, int Margin, int Spacing, TileDefinition[] Tiles)
{
    // Never recycle a retired ID when the image is sliced again.
    public int NextId { get; init; } = 1;

    public static Tileset Load(string path) => ProjectYaml.Read<Tileset>(path);

    public TilePalette Resolve(string root, ProjectAssets assets, string name)
    {
        if (assets.PathFor(Image) is not { } image || !SpriteAssets.IsPng(image) || !File.Exists(Path.Combine(root, image)))
            throw new InvalidDataException($"{name}: missing tile PNG {Image}.");
        var path = Path.Combine(root, image);
        var size = SpriteRegion.ReadPngSize(path);
        if (TileSize is not { Width: > 0, Height: > 0 } || Margin < 0 || Spacing < 0 || Tiles is not { Length: > 0 })
            throw new InvalidDataException($"{name}: invalid tile slicing settings or empty palette.");
        var tiles = new Dictionary<int, TileDefinition>();
        foreach (var tile in Tiles)
            if (tile == null || tile.Id <= 0 || tile.Source?.Fits(size.Width, size.Height) != true || !tiles.TryAdd(tile.Id, tile))
                throw new InvalidDataException($"{name}: duplicate or invalid tile #{tile?.Id} in {image}.");
        var palette = new TilePalette(path, tiles);
        var regions = tiles.Values.DistinctBy(t => t.Source).ToDictionary(t => t.Source, t => t.Id);
        int dx = TileSize.Width + Spacing, dy = TileSize.Height + Spacing;
        int columns = (size.Width - 2 * Margin + Spacing) / dx;
        foreach (var tile in tiles.Values.Where(t => t.AutoEdges != 0))
        {
            if (tile.AutoEdges is not (4 or 6) || columns <= 0 || tile.Source.X < Margin || tile.Source.Y < Margin ||
                (tile.Source.X-Margin)%dx != 0 || (tile.Source.Y-Margin)%dy != 0)
                throw new InvalidDataException($"{name}: invalid auto terrain on tile #{tile.Id}.");
            var variants = new int[1 << tile.AutoEdges];
            int first = (tile.Source.Y-Margin)/dy * columns + (tile.Source.X-Margin)/dx;
            for (int mask=0; mask<variants.Length; mask++)
            {
                int index = first+mask;
                var region = new SpriteRegion(Margin+index%columns*dx, Margin+index/columns*dy, TileSize.Width, TileSize.Height);
                if (!regions.TryGetValue(region, out variants[mask]))
                    throw new InvalidDataException($"{name}: tile #{tile.Id} needs {variants.Length} consecutive sheet cells; missing mask {mask}.");
            }
            palette.Variants.Add(tile.Id, variants);
        }
        return palette;
    }
}

public sealed record TilePalette(string Image, IReadOnlyDictionary<int, TileDefinition> Tiles)
{
    internal Dictionary<int,int[]> Variants { get; } = [];
    public static TilePalette Load(string root, ProjectAssets assets, Guid id)
    {
        var relative = assets.PathFor(id);
        if (relative == null || !relative.EndsWith(ProjectAssets.Tileset) || !File.Exists(Path.Combine(root, relative)))
            throw new InvalidDataException($"Missing tileset {relative ?? id.ToString()}.");
        return Tileset.Load(Path.Combine(root, relative)).Resolve(root, assets, relative);
    }
}

[YamlObject]
public readonly partial record struct TileCell(int Index, int Tile);
[YamlObject]
public sealed partial record TileChunk(int X, int Y, TileCell[] Cells);

[DisplayName("Tilemap 2D"), YamlObject]
public sealed partial record Tilemap2DComponent : SceneComponent
{
    public Tilemap2DComponent() { }
    [YamlConstructor] public Tilemap2DComponent(string layout = TileGrid.Orthogonal, uint collisionMask = uint.MaxValue)
        => (Layout, CollisionMask) = (layout, collisionMask);
    public int CollisionLayer { get; set; }
    public uint CollisionMask { get; set; } = uint.MaxValue;
    public Guid? Tileset { get; set; }
    public string Layout { get; set; } = TileGrid.Orthogonal;
    public int? StreamRadius { get; set; }
    public Guid? StreamTarget { get; set; }
    // Only exported scenes contain file references. The authoring scene retains brush IDs.
    public TileChunkRef[]? ChunkFiles { get; set; }
    public TileSize CellSize { get; set; } = new(32, 32);
    public int SortingLayer { get; set; }
    public int Order { get; set; }
    // Copies of components, prefab instances and snapshots can share immutable chunks.
    private ImmutableDictionary<Point, ImmutableSortedDictionary<int, int>> chunks =
        ImmutableDictionary<Point, ImmutableSortedDictionary<int, int>>.Empty;
    public TileChunk[] Chunks
    {
        get => chunks.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X).Select(p => new TileChunk(p.Key.X, p.Key.Y,
            p.Value.Select(cell => new TileCell(cell.Key, cell.Value)).ToArray())).ToArray();
        set
        {
            var builder = chunks.Clear().ToBuilder();
            foreach (var chunk in value ?? throw new InvalidDataException("Missing tile chunks."))
            {
                if (chunk == null || chunk.Cells is not { Length: > 0 } || !ValidChunk(chunk.X) || !ValidChunk(chunk.Y))
                    throw new InvalidDataException($"Invalid tile chunk ({chunk?.X},{chunk?.Y}).");
                var cells = ImmutableSortedDictionary.CreateBuilder<int, int>();
                foreach (var cell in chunk.Cells)
                {
                    if (cell.Index is < 0 or >= 1024 || cell.Tile <= 0 || !cells.TryAdd(cell.Index, cell.Tile))
                        throw new InvalidDataException($"Invalid or duplicate cell {cell.Index} in tile chunk ({chunk.X},{chunk.Y}).");
                }
                if (!builder.TryAdd(new(chunk.X, chunk.Y), cells.ToImmutable()))
                    throw new InvalidDataException($"Duplicate tile chunk ({chunk.X},{chunk.Y}).");
            }
            chunks = builder.ToImmutable();
        }
    }

    private static bool ValidChunk(int n) => n >= (int.MinValue >> 5) && n <= (int.MaxValue >> 5);
    private static int Index(Point p) => ((p.Y & 31) << 5) + (p.X & 31);
    public int Get(Point p) => chunks.TryGetValue(new(p.X >> 5, p.Y >> 5), out var cells) ? cells.GetValueOrDefault(Index(p)) : 0;
    public void Set(Point p, int tile)
    {
        if (tile < 0) throw new ArgumentOutOfRangeException(nameof(tile));
        var key = new Point(p.X >> 5, p.Y >> 5);
        var cells = chunks.GetValueOrDefault(key, ImmutableSortedDictionary<int, int>.Empty);
        cells = tile == 0 ? cells.Remove(Index(p)) : cells.SetItem(Index(p), tile);
        chunks = cells.IsEmpty ? chunks.Remove(key) : chunks.SetItem(key, cells);
    }

    private ImmutableDictionary<Point, ImmutableDictionary<int,int>> visuals = ImmutableDictionary<Point, ImmutableDictionary<int,int>>.Empty;
    internal void Install(TilePage page)
    {
        var key = new Point(page.Chunk.X, page.Chunk.Y);
        chunks = chunks.SetItem(key, page.Chunk.Cells.ToImmutableSortedDictionary(c => c.Index,c => c.Tile));
        visuals = visuals.SetItem(key, page.Variants.ToImmutableDictionary(c => c.Index,c => c.Tile));
    }
    internal void Evict(Point key)
    {
        chunks = chunks.Remove(key); visuals = visuals.Remove(key);
    }
    public int DisplayTile(TilePalette palette, Point cell, int brush)
    {
        if (visuals.TryGetValue(new(cell.X >> 5,cell.Y >> 5), out var baked))
            return baked.GetValueOrDefault(Index(cell),brush);
        if (!palette.Variants.TryGetValue(brush,out var variants)) return brush;
        bool hex = TileGrid.IsHex(Layout);
        if (variants.Length != (hex ? 64 : 16)) return brush;
        int mask = 0;
        for (int i=0; i<(hex ? 6 : 4); i++)
        {
            var d = (hex ? HexEdges : SquareEdges)[i];
            long x = (long)cell.X+d.X, y = (long)cell.Y+d.Y;
            if (x is >= int.MinValue and <= int.MaxValue && y is >= int.MinValue and <= int.MaxValue && Get(new((int)x,(int)y)) == brush)
                mask |= 1 << i;
        }
        return variants[mask];
    }
    private static readonly Point[] SquareEdges = [new(0,-1),new(1,0),new(0,1),new(-1,0)];
    private static readonly Point[] HexEdges = [new(1,0),new(0,1),new(-1,1),new(-1,0),new(0,-1),new(1,-1)];

    public Point CellAt(Vector2 origin, double x, double y) =>
        TileGrid.Pick(Layout,(x-origin.X)/CellSize.Width,(y-origin.Y)/CellSize.Height);
    public RectangleF Bounds(Vector2 origin, Point cell)
    {
        var (x,y) = TileGrid.Project(Layout,cell.X,cell.Y);
        return new((float)(origin.X+x*CellSize.Width),(float)(origin.Y+y*CellSize.Height),CellSize.Width,CellSize.Height);
    }

    public (Point First, Point Last) CellRange(Vector2 origin, RectangleF view)
    {
        var a = CellAt(origin,view.Left,view.Top);
        var b = CellAt(origin,view.Right,view.Bottom);
        if (Layout == TileGrid.Orthogonal) return (a,b);
        var c = CellAt(origin,view.Left,view.Bottom);
        var d = CellAt(origin,view.Right,view.Top);
        // One neighbouring cell can intersect the view past a rounded corner.
        return (new(Low(a.X,b.X,c.X,d.X),Low(a.Y,b.Y,c.Y,d.Y)),new(High(a.X,b.X,c.X,d.X),High(a.Y,b.Y,c.Y,d.Y)));
        static int Low(int a,int b,int c,int d) => int.CreateSaturating((long)Math.Min(Math.Min(a,b),Math.Min(c,d))-2);
        static int High(int a,int b,int c,int d) => int.CreateSaturating((long)Math.Max(Math.Max(a,b),Math.Max(c,d))+2);
    }

    private static Point Position(Point chunk, int index) => new(chunk.X * 32 + (index & 31), chunk.Y * 32 + (index >> 5));

    public IEnumerable<(Point Cell, int Tile)> InView(Vector2 origin, RectangleF view)
    {
        var (first,last) = CellRange(origin,view);
        int left = first.X >> 5, top = first.Y >> 5, right = last.X >> 5, bottom = last.Y >> 5;
        IEnumerable<Point> Candidates()
        {
            if ((long)(right - left + 1) * (bottom - top + 1) > chunks.Count)
            {
                foreach (var key in chunks.Keys)
                    if (key.X >= left && key.X <= right && key.Y >= top && key.Y <= bottom) yield return key;
            }
            else
                for (int y = top; y <= bottom; y++)
                    for (int x = left; x <= right; x++) yield return new(x, y);
        }
        foreach (var key in Candidates())
        {
            if (!chunks.TryGetValue(key, out var cells)) continue;
            foreach (var (index, tile) in cells)
            {
                var cell = Position(key, index);
                if (cell.X >= first.X && cell.X <= last.X && cell.Y >= first.Y && cell.Y <= last.Y) yield return (cell, tile);
            }
        }
    }

    public IEnumerable<(Point Cell, int Tile)> OccupiedCells()
    {
        foreach (var (key, cells) in chunks)
            foreach (var (index, tile) in cells)
                yield return (Position(key, index), tile);
    }

    public void ValidateTiles(TilePalette? palette, string name)
    {
        foreach (var (cell, tile) in OccupiedCells())
            if (palette?.Tiles.ContainsKey(tile) != true)
                throw new InvalidDataException($"{name}: missing tile #{tile} at {cell}.");
    }

    public void Draw(ICanvas2D canvas, TilePalette palette, Matrix3x2 world, RectangleF view, Matrix3x2 camera)
    {
        var localView = Affine2D.Bounds(view,Affine2D.Inverse(world));
        foreach (var (cell,id) in InView(Vector2.Zero,localView))
        {
            var rect = Bounds(Vector2.Zero,cell);
            if (localView.IntersectsWith(rect) && palette.Tiles.TryGetValue(DisplayTile(palette,cell,id),out var tile))
                canvas.DrawAffine(palette.Image,rect,world*camera,Color.White,tile.Source,Layout);
        }
    }

    public void Draw(ICanvas2D canvas, TilePalette palette, Vector2 origin, RectangleF view, Vector2 offset, float zoom)
    {
        foreach (var (cell, id) in InView(origin, view))
        {
            if (!palette.Tiles.TryGetValue(DisplayTile(palette,cell,id), out var tile)) continue;
            var rect = Bounds(origin, cell);
            if (view.IntersectsWith(rect))
                canvas.DrawTile(palette.Image, new((rect.X - offset.X) * zoom, (rect.Y - offset.Y) * zoom, rect.Width * zoom, rect.Height * zoom), tile.Source, Layout);
        }
    }
}
