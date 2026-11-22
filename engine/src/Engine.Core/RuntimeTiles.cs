using System.Drawing;

namespace Engine.Core;

/// <summary>One cell of a tile layer. Tile 0 is empty. Display is the resolved art (an auto-terrain variant or the tile itself).</summary>
public readonly record struct TileInfo(int Tile, int Display, string? Terrain, int MoveCost, bool Solid);

public sealed partial class SceneGame
{
    /// <summary>Replace a non-streamed layer with row-major cells starting at (0,0).
    /// Zero clears a cell. Rendering and collision share the same validated runtime snapshot.</summary>
    public bool ReplaceTiles(Guid layer, int width, int height, ReadOnlySpan<int> tiles)
    {
        if (!byId.TryGetValue(layer, out var body) || removed.Contains(layer) ||
            body.Map is not {} map || body.Stream != null) return false;
        if (width <= 0 || height <= 0 || (long)width * height != tiles.Length || tiles.Length > 1024 * 1024)
            throw new ArgumentException("Expected a positive rectangular grid of at most 1,048,576 tile IDs.");
        List<TileChunk> chunks = [];
        List<TileCell> cells = [];
        for (int top = 0; top < height; top += 32)
        for (int left = 0; left < width; left += 32)
        {
            cells.Clear();
            for (int y = top; y < Math.Min(top + 32, height); y++)
            for (int x = left; x < Math.Min(left + 32, width); x++)
            {
                int id = tiles[y * width + x];
                if (id == 0) continue;
                if (body.Palette?.Tiles.ContainsKey(id) != true)
                    throw new ArgumentException($"Unknown tile #{id} at ({x}, {y}) on {body.Source.Name}.", nameof(tiles));
                cells.Add(new((y - top) * 32 + x - left, id));
            }
            if (cells.Count != 0) chunks.Add(new(left / 32, top / 32, cells.ToArray()));
        }
        // Never edit the authored component. Invalid input also leaves the current run intact.
        body.Map = map with { Chunks = chunks.ToArray() };
        foreach (var (grid, origin) in body.Grids ?? [])
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++) grid.SetEntryCost(new(x, y), EntryCost(body, new(origin.X + x, origin.Y + y)));
        return true;
    }

    private bool TryTileLayer(Guid layer, out Body body)
    {
        body = null!;
        return byId.TryGetValue(layer, out body!) && !removed.Contains(layer) && body.Map != null;
    }

    private static int EntryCost(Body body, Point cell) =>
        body.Palette?.Tiles.GetValueOrDefault(body.Map!.Get(cell)) is {} tile ? tile.EntryCost() : 0;

    /// <summary>False for a missing layer or a streamed cell whose page is not resident.</summary>
    public bool TryGetTile(Guid layer, Point cell, out TileInfo info)
    {
        info = default;
        if (!TryTileLayer(layer, out var body) || body.Stream?.Contains(new(cell.X >> 5, cell.Y >> 5)) == false) return false;
        var map = body.Map!;
        int id = map.Get(cell);
        if (id != 0 && body.Palette?.Tiles.GetValueOrDefault(id) is {} tile)
            info = new(id, map.DisplayTile(body.Palette, cell, id), tile.Terrain, tile.EntryCost(), tile.Solid);
        return true;
    }

    /// <summary>Replace one cell (0 clears it). Drawing, auto-terrain neighbours, tile collision and
    /// grids from <see cref="TileNavigation"/> change together. Streamed layers accept resident cells only.</summary>
    public bool SetTile(Guid layer, Point cell, int tile)
    {
        if (!TryTileLayer(layer, out var body)) return false;
        if (tile < 0 || tile != 0 && body.Palette?.Tiles.ContainsKey(tile) != true)
            throw new ArgumentException($"Unknown tile #{tile} on {body.Source.Name}.", nameof(tile));
        if (body.Stream is {} stream)
        {
            if (!stream.Set(cell, tile, body.Palette)) return false;
        }
        else
        {
            // Never edit the authored component; chunk dictionaries are immutable and shared.
            var map = body.Map! with { };
            map.Set(cell, tile);
            body.Map = map;
        }
        foreach (var (grid, origin) in body.Grids ?? [])
        {
            Point local = new(cell.X - origin.X, cell.Y - origin.Y);
            if ((uint)local.X < grid.Width && (uint)local.Y < grid.Height) grid.SetEntryCost(local, EntryCost(body, cell));
        }
        return true;
    }

    /// <summary>A grid over <paramref name="area"/> (grid cell (0,0) = area.Location) whose entry costs come from the
    /// tileset (empty cells block). SetTile and ReplaceTiles keep it current. Null for missing or streamed layers.</summary>
    public GridNavigation? TileNavigation(Guid layer, Rectangle area)
    {
        if (!TryTileLayer(layer, out var body) || body.Stream != null) return null;
        if (area.Width <= 0 || area.Height <= 0 || (long)area.Width * area.Height > 1024 * 1024)
            throw new ArgumentException("Expected a positive area of at most 1,048,576 cells.", nameof(area));
        var costs = new int[area.Width * area.Height];
        for (int y = 0; y < area.Height; y++)
        for (int x = 0; x < area.Width; x++) costs[y * area.Width + x] = EntryCost(body, new(area.X + x, area.Y + y));
        var grid = new GridNavigation(area.Width, area.Height, costs);
        (body.Grids ??= []).Add((grid, area.Location));
        return grid;
    }
}
