using System.Drawing;

namespace Engine.Core;

internal static class TileChunkBuild
{
    internal static TileChunkRef[] Write(ContentBuildCache cache, string owner, Tilemap2DComponent map,
        TilePalette? palette, string paletteHash, string directory)
    {
        Directory.CreateDirectory(directory);
        var result = new List<TileChunkRef>();
        foreach (var chunk in map.Chunks)
        {
            // Autotile edges can read one cell across a chunk boundary, including axial diagonals.
            int left = chunk.X * 32, top = chunk.Y * 32;
            var border = new List<int>(132);
            int Neighbour(long x, long y) => x is >= int.MinValue and <= int.MaxValue && y is >= int.MinValue and <= int.MaxValue
                ? map.Get(new Point((int)x, (int)y)) : 0;
            for (int i = -1; i <= 32; i++)
            {
                border.Add(Neighbour((long)left + i, (long)top - 1));
                border.Add(Neighbour((long)left + i, (long)top + 32));
            }
            for (int i = 0; i < 32; i++)
            {
                border.Add(Neighbour((long)left - 1, (long)top + i));
                border.Add(Neighbour((long)left + 32, (long)top + i));
            }
            var inputs = new Dictionary<string,string> { ["cells"] = ContentBuildCache.Digest(ProjectYaml.Encode(chunk)),
                ["neighbour borders"] = ContentBuildCache.Digest(ProjectYaml.Encode(border.ToArray())),
                ["tileset"] = paletteHash, ["layout"] = map.Layout };
            var temp = Path.Combine(directory, "page.tmp");
            string hash = cache.Generate($"chunk {owner} ({chunk.X},{chunk.Y})", inputs, temp,
                file => File.WriteAllBytes(file, TileWorld.BakePage(map, palette, chunk)))!;
            string path = Path.Combine(directory, hash + ".chunk");
            if (File.Exists(path)) File.Delete(temp); else File.Move(temp, path);
            result.Add(new(chunk.X, chunk.Y, hash));
        }
        return result.ToArray();
    }
}
