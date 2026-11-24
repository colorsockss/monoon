using System.Drawing;
using System.Numerics;
using System.Security.Cryptography;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record TileChunkRef(int X, int Y, string Hash);
[YamlObject]
internal sealed partial record TilePage(TileChunk Chunk, TileCell[] Variants);

public readonly record struct TileStreamState(Point Center, int Resident, int Total, long Loads, long Unloads);

// A page contains logical IDs for collision and resolved IDs for display. Borders are baked
// from the complete authored map, so residency of adjacent pages cannot change a tile's art.
internal sealed class TileWorld : IDisposable
{
    internal const string Folder = ".monoon-world";
    private readonly Dictionary<Point,string> index = [];
    private readonly HashSet<Point> resident = [];
    // Runtime cell edits outlive page eviction and are reapplied on reload. Zero clears a cell.
    private readonly Dictionary<Point,int> edits = [];
    private readonly string directory;
    private readonly bool temporary;
    internal readonly Tilemap2DComponent Map;
    private Point? center;
    private long loads, unloads;
    internal TileStreamState State => new(center ?? default,resident.Count,index.Count,loads,unloads);
    internal bool Contains(Point key) => resident.Contains(key);
    internal int Edits { get; private set; } // Counts accepted Set calls so physics can rebuild tile shapes.

    internal static TileChunkRef[] Bake(Tilemap2DComponent source, TilePalette? palette, string directory)
    {
        Directory.CreateDirectory(directory);
        var refs = new List<TileChunkRef>();
        foreach (var chunk in source.Chunks)
        {
            var bytes = BakePage(source, palette, chunk);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var path = Path.Combine(directory,hash+".chunk");
            if (!File.Exists(path)) ProjectYaml.WriteBytes(path,bytes);
            refs.Add(new(chunk.X,chunk.Y,hash));
        }
        return refs.ToArray();
    }

    internal static byte[] BakePage(Tilemap2DComponent source, TilePalette? palette, TileChunk chunk)
    {
        var variants = new List<TileCell>();
        foreach (var cell in chunk.Cells)
        {
            if (palette == null || !palette.Tiles.ContainsKey(cell.Tile))
                throw new InvalidDataException($"Unknown tile #{cell.Tile} in ({chunk.X},{chunk.Y}).");
            var point = new Point(chunk.X*32+(cell.Index&31), chunk.Y*32+(cell.Index>>5));
            int id = source.DisplayTile(palette,point,cell.Tile);
            if (id != cell.Tile) variants.Add(new(cell.Index,id));
        }
        return ProjectYaml.Encode(new TilePage(chunk,variants.ToArray()));
    }

    internal TileWorld(Tilemap2DComponent source, TilePalette? palette, string? project)
    {
        Map = source with { Chunks = [], ChunkFiles = null };
        temporary = source.ChunkFiles == null;
        directory = temporary ? Directory.CreateTempSubdirectory("monoon-world-").FullName :
            Path.Combine(project ?? throw new InvalidDataException("Chunk files need a game directory."),Folder);
        try
        {
            foreach (var r in source.ChunkFiles ?? (ProjectAssets.ExportedGameOnly
                ? throw new InvalidDataException("Exported streamed tilemaps need baked chunk files.") : Bake(source,palette,directory)))
            {
                if (r == null || r.Hash?.Length != 64 || !r.Hash.All(char.IsAsciiHexDigit) ||
                    !index.TryAdd(new(r.X,r.Y),r.Hash))
                    throw new InvalidDataException("Invalid or duplicate world chunk reference.");
            }
        }
        catch { Dispose(); throw; }
    }

    internal void Update(Point? next, TilePalette? palette)
    {
        if (center == next) return;
        int radius = Map.StreamRadius!.Value;
        // Enumerate the small target window, not the world index.
        if (next is Point n)
        for (int y=n.Y-radius; y<=n.Y+radius; y++)
        for (int x=n.X-radius; x<=n.X+radius; x++)
        {
            var key = new Point(x,y);
            if (resident.Contains(key) || !index.TryGetValue(key, out var hash)) continue;
            var bytes = File.ReadAllBytes(Path.Combine(directory,hash+".chunk"));
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"World chunk ({x},{y}) failed its content check.");
            var page = ProjectYaml.Decode<TilePage>(bytes);
            if (page.Chunk is not {} c || c.X != x || c.Y != y || c.Cells is not {Length: >0 and <=1024} ||
                page.Variants == null || c.Cells.Any(v => (uint)v.Index>=1024 || palette?.Tiles.ContainsKey(v.Tile)!=true) ||
                page.Variants.Any(v => palette?.Tiles.ContainsKey(v.Tile)!=true))
                throw new InvalidDataException($"Invalid world chunk ({x},{y}).");
            Map.Install(page); resident.Add(key); loads++;
            foreach (var (cell,tile) in edits)
                if (Key(cell) == key) Map.Set(cell,tile);
            foreach (var cell in edits.Keys)
                if (Math.Abs(Key(cell).X-x) <= 1 && Math.Abs(Key(cell).Y-y) <= 1) Refresh(cell,palette);
        }
        foreach (var key in resident.ToArray())
            if (next is not Point p || Math.Abs(key.X-p.X)>radius || Math.Abs(key.Y-p.Y)>radius)
            { Map.Evict(key); resident.Remove(key); unloads++; }
        center = next;
    }

    private static Point Key(Point cell) => new(cell.X >> 5,cell.Y >> 5);

    internal bool Set(Point cell, int tile, TilePalette? palette)
    {
        if (!resident.Contains(Key(cell))) return false;
        edits[cell] = tile;
        Edits++;
        Map.Set(cell,tile);
        Refresh(cell,palette);
        return true;
    }

    // Re-resolve border art of an edited cell and its resident neighbours.
    // shortcut: a neighbour's edge toward a third, unloaded page keeps its baked bit; exact unless that page was also edited while unloaded neighbours were absent.
    private void Refresh(Point cell, TilePalette? palette)
    {
        if (palette != null) Map.Refresh(palette,cell,n => resident.Contains(Key(n)));
    }

    public void Dispose()
    {
        if (temporary && Directory.Exists(directory)) Directory.Delete(directory,true);
    }
}

public sealed partial class SceneGame
{
    private readonly HashSet<TileWorld> ownedWorlds = [];
    public bool TryGetTileStream(Guid layer, out TileStreamState state)
    {
        if (byId.TryGetValue(layer,out var b) && !removed.Contains(layer) && b.Stream is {} stream)
        { state=stream.State; return true; }
        state=default; return false;
    }
    public bool IsTileChunkLoaded(Guid layer,int x,int y) =>
        byId.TryGetValue(layer,out var b) && !removed.Contains(layer) && b.Stream?.Contains(new(x,y)) == true;

    private void UpdateTileWorlds()
    {
        if (EngineFeatures.Tilemaps) UpdateTileStreams();
    }

    private void UpdateTileStreams()
    {
        foreach (var layer in tileLayers)
        {
            if (layer.Stream is not {} stream) continue;
            var target = stream.Map.StreamTarget is Guid id ? byId.GetValueOrDefault(id) : ViewCamera;
            Point? key = null;
            if (layer.Active && target?.Active == true)
            {
                var local = Affine2D.Transform(target.Position, Affine2D.Inverse(layer.MatrixAt(layer.Position)));
                var cell = stream.Map.CellAt(Vector2.Zero,local.X,local.Y);
                key = new(cell.X>>5,cell.Y>>5);
            }
            stream.Update(key,layer.Palette);
        }
    }
}
