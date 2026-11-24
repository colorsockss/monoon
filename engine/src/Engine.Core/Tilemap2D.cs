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
    // A terrain brush uses 16 (4 edges), 64 (6 axial edges) or 47 (8 neighbours, blob) sheet cells in mask order.
    public TileCollision? Collision { get; init; }
    public int AutoEdges { get; init; }
    // 2+: the cell is picked per position from this many alternatives (a single tile: it and the following sheet cells;
    // a terrain brush: its fully connected cell and the cells after the terrain block). The seed fixes the pattern.
    // Nullable so files without random tiles are written exactly as before.
    public int? RandomTiles { get; init; }
    public int? RandomSeed { get; init; }
    // 2+: the tile cycles through itself and the following sheet cells at Fps frames per second of game time. Every cell
    // of the tile shows the same frame (one clock per tileset), so cells hold no animation state.
    public int? Frames { get; init; }
    public int? Fps { get; init; }
    // Gameplay terrain kind and grid navigation weight. Unset cost: 1, or 0 (blocked) for solid tiles.
    public string? Terrain { get; init; }
    public int? MoveCost { get; init; }
    public int EntryCost() => MoveCost ?? (Solid ? 0 : 1);
}
[YamlObject]
// NextId: never recycle a retired ID when the image is sliced again.
public sealed partial record Tileset(Guid Image, TileSize TileSize, int Margin, int Spacing, TileDefinition[] Tiles, int NextId = 1)
{
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
        {
            if (tile == null || tile.Id <= 0 || tile.Source?.Fits(size.Width, size.Height) != true || !tiles.TryAdd(tile.Id, tile))
                throw new InvalidDataException($"{name}: duplicate or invalid tile #{tile?.Id} in {image}.");
            tile.Collision?.Validate(name, tile.Id);
            if (tile.MoveCost < 0 || tile.Terrain is { Length: > 64 })
                throw new InvalidDataException($"{name}: tile #{tile.Id} needs a nonnegative move cost and a terrain name of at most 64 characters.");
        }
        var palette = new TilePalette(path, tiles);
        var regions = tiles.Values.DistinctBy(t => t.Source).ToDictionary(t => t.Source, t => t.Id);
        int dx = TileSize.Width + Spacing, dy = TileSize.Height + Spacing;
        int columns = (size.Width - 2 * Margin + Spacing) / dx;
        int Cell(SpriteRegion source) => (source.Y-Margin)/dy * columns + (source.X-Margin)/dx;
        int At(TileDefinition tile, int index, string what)
        {
            var region = new SpriteRegion(Margin+index%columns*dx, Margin+index/columns*dy, TileSize.Width, TileSize.Height);
            return regions.TryGetValue(region, out var id) ? id :
                throw new InvalidDataException($"{name}: tile #{tile.Id} needs {what}.");
        }
        foreach (var tile in tiles.Values.Where(t => t.AutoEdges != 0 || t.RandomTiles != null || t.Frames != null || t.Fps != null))
        {
            if (tile.AutoEdges is not (0 or 4 or 6 or 8) || tile.RandomTiles is < 2 or > 64 || columns <= 0 || tile.Source.X < Margin ||
                tile.Source.Y < Margin || (tile.Source.X-Margin)%dx != 0 || (tile.Source.Y-Margin)%dy != 0)
                throw new InvalidDataException($"{name}: invalid auto terrain or random tiles on tile #{tile.Id}.");
            int first = Cell(tile.Source), cells = tile.AutoEdges switch { 8 => 47, 0 => 0, var e => 1 << e };
            if (tile.Frames != null || tile.Fps != null)
            {
                if (tile.Frames is not (>= 2 and <= 64) || tile.Fps is not (>= 1 and <= 60) || tile.AutoEdges != 0 || tile.RandomTiles != null)
                    throw new InvalidDataException($"{name}: tile #{tile.Id} needs 2 to 64 frames at 1 to 60 FPS and no terrain or random brush.");
                var frames = new int[tile.Frames.Value];
                frames[0] = tile.Id;
                for (int i = 1; i < frames.Length; i++) frames[i] = At(tile, first + i, $"{frames.Length - 1} frame sheet cells after it");
                palette.Frames.Add(tile.Id, new(frames, tile.Fps.Value));
                continue;
            }
            int full = tile.Id;
            if (cells != 0)
            {
                var art = new int[cells];
                for (int mask=0; mask<cells; mask++) art[mask] = At(tile, first+mask, $"{cells} consecutive sheet cells; missing mask {mask}");
                // Eight neighbours: index the art by the raw 8-bit mask so drawing needs no reduction step.
                var variants = art;
                if (cells == 47)
                {
                    variants = new int[256];
                    for (int mask = 0; mask < 256; mask++) variants[mask] = art[Blob[mask]];
                }
                palette.Variants.Add(tile.Id, variants);
                full = variants[^1];
            }
            if (tile.RandomTiles is not { } count) continue;
            var choices = new int[count];
            choices[0] = full;
            for (int i=1; i<choices.Length; i++)
                choices[i] = At(tile, first + Math.Max(cells, 1) + i - 1, $"{choices.Length - 1} random sheet cells after it");
            palette.Randoms.Add(tile.Id, choices);
            palette.Seeds.Add(tile.Id, tile.RandomSeed ?? 0);
        }
        return palette;
    }

    // 8-neighbour blob: bits N 1, E 2, S 4, W 8, NE 16, SE 32, SW 64, NW 128. A corner counts only when both of its edges
    // connect, leaving 47 masks; the sheet holds them in ascending order (the first 16 equal the 4-edge sheet).
    internal static readonly byte[] Blob = BlobOrder();
    private static byte[] BlobOrder()
    {
        static int Reduce(int m) => m & 15 | ((m & 3) == 3 ? m & 16 : 0) | ((m & 6) == 6 ? m & 32 : 0) |
            ((m & 12) == 12 ? m & 64 : 0) | ((m & 9) == 9 ? m & 128 : 0);
        var rank = new byte[256];
        for (int m = 0; m < 256; m++) rank[Reduce(m)] = 1;
        for (int r = 0, n = 0; r < 256; r++) if (rank[r] != 0) rank[r] = (byte)n++;
        var blob = new byte[256];
        for (int m = 0; m < 256; m++) blob[m] = rank[Reduce(m)];
        return blob;
    }
}

public sealed record TilePalette(string Image, IReadOnlyDictionary<int, TileDefinition> Tiles)
{
    internal Dictionary<int,int[]> Variants { get; } = [];
    internal Dictionary<int,int[]> Randoms { get; } = [];
    internal Dictionary<int,int> Seeds { get; } = [];
    internal Dictionary<int,TileFrames> Frames { get; } = [];
    // The shared clock: one frame pick per animated tile per draw; cells only look their tile up.
    internal void Clock(double seconds)
    {
        foreach (var frames in Frames.Values) frames.Current = frames.At(seconds);
    }
    internal int Animated(int display) => Frames.Count != 0 && Frames.TryGetValue(display, out var frames) ? frames.Current : display;
    internal int Animated(int display, double seconds) => Frames.Count != 0 && Frames.TryGetValue(display, out var frames) ? frames.At(seconds) : display;
    public static TilePalette Load(string root, ProjectAssets assets, Guid id)
    {
        var relative = assets.PathFor(id);
        if (relative == null || !relative.EndsWith(ProjectAssets.Tileset) || !File.Exists(Path.Combine(root, relative)))
            throw new InvalidDataException($"Missing tileset {relative ?? id.ToString()}.");
        return Tileset.Load(Path.Combine(root, relative)).Resolve(root, assets, relative);
    }
}

internal sealed class TileFrames(int[] tiles, int fps)
{
    public readonly int[] Tiles = tiles;
    public readonly int Fps = fps;
    public int Current = tiles[0];
    public int At(double seconds) => Tiles[(int)((long)Math.Floor(Math.Max(0, seconds) * Fps) % Tiles.Length)];
}

[YamlObject]
public readonly partial record struct TileCell(int Index, int Tile);
[YamlObject]
public sealed partial record TileChunk(int X, int Y, TileCell[] Cells);

[DisplayName("Tilemap 2D"), YamlObject, EngineFeature(EngineFeature.Tilemaps)]
public sealed partial record Tilemap2DComponent : SceneComponent
{
    internal override bool CanDisable => false;
    public Tilemap2DComponent() { }
    [YamlConstructor] public Tilemap2DComponent(string layout = TileGrid.Orthogonal, uint collisionMask = uint.MaxValue, TileSize? cellSize = null)
        => (Layout, CollisionMask, CellSize) = (layout, collisionMask, cellSize ?? CellSize);
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
    // Particle collision keeps dense copies of chunks; a new reference here means the cells changed.
    internal object CellVersion() => chunks;
    internal int[]? DenseChunk(Point key)
    {
        if (!chunks.TryGetValue(key, out var cells)) return null;
        var dense = new int[1024];
        foreach (var (index, tile) in cells) dense[index] = tile;
        return dense;
    }
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
        return resolved ? brush : Variant(palette,cell,brush,_ => true,brush);
    }
    // Runtime copies resolve every cell's art once, so drawing a frame never inspects neighbours or hashes positions.
    private bool resolved;
    internal Tilemap2DComponent Resolved(TilePalette palette)
    {
        var copy = this with { visuals = ImmutableDictionary<Point, ImmutableDictionary<int,int>>.Empty, resolved = false };
        foreach (var (key, cells) in chunks)
            foreach (var (index, tile) in cells)
            {
                var cell = Position(key,index);
                copy.SetVisual(cell,copy.Variant(palette,cell,tile,_ => true,tile),tile);
            }
        copy.resolved = true;
        return copy;
    }
    // Re-resolve an edited cell and its neighbours whose pages are known (all of them unless streamed).
    internal void Refresh(TilePalette palette, Point cell, Func<Point,bool> known)
    {
        var around = TileGrid.IsHex(Layout) ? HexEdges : SquareNeighbours;
        for (int i = -1; i < around.Length; i++)
        {
            Point p = i < 0 ? cell : new(cell.X + around[i].X, cell.Y + around[i].Y);
            if (!known(p)) continue;
            int brush = Get(p);
            SetVisual(p, brush == 0 ? 0 : Variant(palette,p,brush,known,DisplayTile(palette,p,brush)), brush);
        }
    }
    // Edges toward unknown cells keep their bit from the previous art, when it is a variant of the same brush.
    internal int Variant(TilePalette palette, Point cell, int brush, Func<Point,bool> known, int previous)
    {
        int display = brush;
        palette.Randoms.TryGetValue(brush, out var random);
        if (palette.Variants.TryGetValue(brush,out var variants))
        {
            bool hex = TileGrid.IsHex(Layout);
            if (hex != (variants.Length == 64)) return brush;
            var around = hex ? HexEdges : variants.Length == 256 ? SquareNeighbours : SquareEdges;
            int old = random != null && Array.IndexOf(random,previous) >= 0 ? variants.Length - 1 : Array.IndexOf(variants,previous), mask = 0;
            for (int i=0; i<around.Length; i++)
            {
                var d = around[i];
                long x = (long)cell.X+d.X, y = (long)cell.Y+d.Y;
                if (x is < int.MinValue or > int.MaxValue || y is < int.MinValue or > int.MaxValue) continue;
                Point n = new((int)x,(int)y);
                if (known(n) ? Get(n) == brush : old >= 0 && (old >> i & 1) != 0) mask |= 1 << i;
            }
            display = variants[mask];
            if (display != variants[^1]) return display;
        }
        return random != null ? random[Pick(cell, palette.Seeds[brush], random.Length)] : display;
    }
    // Integer hash of the cell and seed: the same on every platform, and repainting a cell restores its choice.
    internal static int Pick(Point cell, int seed, int count)
    {
        uint h = unchecked((uint)cell.X * 0x8DA6B343u ^ (uint)cell.Y * 0xD8163841u ^ (uint)seed * 0xCB1AB31Fu);
        h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
        return (int)(h % (uint)count);
    }
    internal void SetVisual(Point cell, int display, int brush)
    {
        var key = new Point(cell.X >> 5,cell.Y >> 5);
        var baked = visuals.GetValueOrDefault(key,ImmutableDictionary<int,int>.Empty);
        visuals = visuals.SetItem(key,display == brush ? baked.Remove(Index(cell)) : baked.SetItem(Index(cell),display));
    }
    internal static readonly Point[] SquareEdges = [new(0,-1),new(1,0),new(0,1),new(-1,0)];
    // The four edges, then the corners NE, SE, SW, NW (blob mask bits 16..128).
    internal static readonly Point[] SquareNeighbours = [..SquareEdges,new(1,-1),new(1,1),new(-1,1),new(-1,-1)];
    internal static readonly Point[] HexEdges = [new(1,0),new(0,1),new(-1,1),new(-1,0),new(0,-1),new(1,-1)];

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
        var cells = new List<(Point, int)>();
        InView(origin, view, cells);
        return cells;
    }

    // Appends the cells in the same order as the public overload, without allocating (physics queries every tick, drawing).
    internal void InView(Vector2 origin, RectangleF view, List<(Point Cell, int Tile)> into)
    {
        var (first,last) = CellRange(origin,view);
        int left = first.X >> 5, top = first.Y >> 5, right = last.X >> 5, bottom = last.Y >> 5;
        if ((long)(right - left + 1) * (bottom - top + 1) > chunks.Count)
        {
            foreach (var (key, _) in chunks) // The struct enumerator: Keys allocates an iterator. Same order.
                if (key.X >= left && key.X <= right && key.Y >= top && key.Y <= bottom) Chunk(key);
        }
        else
            for (int y = top; y <= bottom; y++)
                for (int x = left; x <= right; x++) Chunk(new(x, y));
        void Chunk(Point key)
        {
            if (!chunks.TryGetValue(key, out var cells)) return;
            // A small view probes its cells in index order instead of walking the whole chunk; same cells, same order.
            int left0 = Math.Max(first.X, key.X * 32), right0 = Math.Min(last.X, key.X * 32 + 31);
            int top0 = Math.Max(first.Y, key.Y * 32), bottom0 = Math.Min(last.Y, key.Y * 32 + 31);
            if ((long)(right0 - left0 + 1) * (bottom0 - top0 + 1) < cells.Count)
            {
                for (long y = top0; y <= bottom0; y++) // long: the last chunk ends at int.MaxValue
                    for (long x = left0; x <= right0; x++)
                        if (cells.TryGetValue(Index(new((int)x, (int)y)), out var tile)) into.Add((new((int)x, (int)y), tile));
                return;
            }
            foreach (var (index, tile) in cells)
            {
                var cell = Position(key, index);
                if (cell.X >= first.X && cell.X <= last.X && cell.Y >= first.Y && cell.Y <= last.Y) into.Add((cell, tile));
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

    [ThreadStatic] private static List<(Point Cell, int Tile)>? drawCells;
    private static List<(Point Cell, int Tile)> DrawCells => drawCells ??= [];

    public void Draw(ICanvas2D canvas, TilePalette palette, Matrix3x2 world, RectangleF view, Matrix3x2 camera, double seconds = 0)
    {
        palette.Clock(seconds);
        var localView = Affine2D.Bounds(view,Affine2D.Inverse(world));
        var cells = DrawCells; cells.Clear(); InView(Vector2.Zero,localView,cells);
        foreach (var (cell,id) in cells)
        {
            var rect = Bounds(Vector2.Zero,cell);
            if (localView.IntersectsWith(rect) && palette.Tiles.TryGetValue(palette.Animated(DisplayTile(palette,cell,id)),out var tile))
                canvas.DrawAffine(palette.Image,rect,world*camera,Color.White,tile.Source,Layout);
        }
    }

    public void Draw(ICanvas2D canvas, TilePalette palette, Vector2 origin, RectangleF view, Vector2 offset, float zoom)
    {
        var cells = DrawCells; cells.Clear(); InView(origin, view, cells);
        foreach (var (cell, id) in cells)
        {
            if (!palette.Tiles.TryGetValue(palette.Animated(DisplayTile(palette,cell,id)), out var tile)) continue;
            var rect = Bounds(origin, cell);
            if (view.IntersectsWith(rect))
                canvas.DrawTile(palette.Image, new((rect.X - offset.X) * zoom, (rect.Y - offset.Y) * zoom, rect.Width * zoom, rect.Height * zoom), tile.Source, Layout);
        }
    }
}
