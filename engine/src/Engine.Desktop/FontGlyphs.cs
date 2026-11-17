using System.IO.Compression;
using System.Security.Cryptography;

namespace Engine.Desktop;

internal sealed record FontGlyph(float Advance, int Left, int Top, int Width, int Height, byte[] Alpha);

// This optional build product is tied to the font bytes, including after a mod or content update.
internal static class FontGlyphs
{
    internal const int FontSize = 32;
    internal const int Limit = 4096;
    internal const string Extension = ".glyphs";
    internal const uint Version = 0x31474E4D; // MNG1
    internal static Dictionary<int, FontGlyph>? Read(string path, byte[] font)
    {
        if (!File.Exists(path + Extension)) return null;
        try { return ReadCoverage(path + Extension, font); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { return null; }
    }

    private static Dictionary<int, FontGlyph>? ReadCoverage(string path, byte[] font)
    {
        using var file = File.OpenRead(path);
        using var compressed = new ZLibStream(file, CompressionMode.Decompress);
        using var reader = new BinaryReader(compressed);
        if (reader.ReadUInt32() != Version || !reader.ReadBytes(32).AsSpan().SequenceEqual(SHA256.HashData(font))) return null;
        int count = reader.ReadInt32();
        if ((uint)count > Limit) throw new InvalidDataException("Invalid font glyph count.");
        var result = new Dictionary<int, FontGlyph>(count);
        for (int i = 0; i < count; i++)
        {
            int codepoint = reader.ReadInt32();
            float advance = reader.ReadSingle();
            int left = reader.ReadInt16(), top = reader.ReadInt16(), width = reader.ReadUInt16(), height = reader.ReadUInt16();
            if (!float.IsFinite(advance) || advance is < 0 or > 256 || width > 256 || height > FontSize + 12)
                throw new InvalidDataException("Invalid font glyph dimensions.");
            var alpha = reader.ReadBytes(width * height);
            if (alpha.Length != width * height) throw new EndOfStreamException();
            if (!result.TryAdd(codepoint, new(advance, left, top, width, height, alpha))) return null;
        }
        return result;
    }
}
