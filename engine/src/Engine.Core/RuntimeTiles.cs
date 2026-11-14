namespace Engine.Core;

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
        return true;
    }
}
