using System.Buffers.Binary;
using System.Drawing;
using VYaml.Annotations;

namespace Engine.Core;

// Logical frame size before the transparent borders were trimmed.
[YamlObject]
public sealed partial record SpriteCanvas(int Width, int Height, int OffsetX = 0, int OffsetY = 0)
{
    public bool Fits(SpriteRegion? source) => source != null &&
        new SpriteRegion(OffsetX, OffsetY, source.Width, source.Height).Fits(Width, Height);

    public RectangleF Place(RectangleF frame, SpriteRegion source)
    {
        float sx = frame.Width / Width, sy = frame.Height / Height;
        return new(frame.X + OffsetX * sx, frame.Y + OffsetY * sy, source.Width * sx, source.Height * sy);
    }
}

[YamlObject]
public sealed partial record SheetSlice(SpriteRegion Source, SpriteCanvas? Canvas = null);

// Pixel coordinates in the source PNG, measured from the top left.
[YamlObject]
public sealed partial record SpriteRegion(int X, int Y, int Width, int Height)
{
    public bool Fits(int width, int height) =>
        X >= 0 && Y >= 0 && Width > 0 && Height > 0 && (long)X + Width <= width && (long)Y + Height <= height;

    public static Size ReadPngSize(string file)
    {
        using var input = File.OpenRead(file);
        Span<byte> header = stackalloc byte[24];
        input.ReadExactly(header);
        if (!header[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}) ||
            BinaryPrimitives.ReadUInt32BigEndian(header[8..]) != 13 || !header[12..16].SequenceEqual("IHDR"u8))
            throw new InvalidDataException($"Invalid PNG header in {file}.");
        return new Size(BinaryPrimitives.ReadInt32BigEndian(header[16..]), BinaryPrimitives.ReadInt32BigEndian(header[20..]));
    }
}
