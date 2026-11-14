using Engine.Core;
using SkiaSharp;
using System.Drawing;

namespace Engine.Desktop;

// A mask covers the original logical canvas, including borders trimmed by the atlas.
public static class SpritePixels
{
    public static SKBitmap Filter(SpriteFilter filter, Color tint)
    {
        using var image = SKBitmap.Decode(filter.Image) ?? throw new InvalidDataException($"Invalid PNG: {filter.Image}");
        using var mask = filter.Mask == null ? null : SKBitmap.Decode(filter.Mask) ?? throw new InvalidDataException($"Invalid PNG: {filter.Mask}");
        var region = filter.Source ?? new(0,0,image.Width,image.Height);
        var canvas = filter.Canvas ?? new(region.Width,region.Height);
        if (!region.Fits(image.Width,image.Height) || !canvas.Fits(region)) throw new InvalidDataException("Invalid sprite crop.");
        var w = Math.Max(region.Width,(int)Math.Ceiling((mask?.Width ?? 0) * (double)region.Width / canvas.Width));
        var h = Math.Max(region.Height,(int)Math.Ceiling((mask?.Height ?? 0) * (double)region.Height / canvas.Height));
        if ((long)w*h > 16777216) throw new InvalidDataException("Filtered sprite exceeds 16 megapixels.");
        var result = new SKBitmap(w,h,SKColorType.Rgba8888,SKAlphaType.Unpremul);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            double u = (x+.5)*region.Width/w, v = (y+.5)*region.Height/h;
            var pixel = image.GetPixel(region.X+(int)u,region.Y+(int)v);
            int alpha = mask == null ? 255 : mask.GetPixel((int)((canvas.OffsetX+u)*mask.Width/canvas.Width),
                (int)((canvas.OffsetY+v)*mask.Height/canvas.Height)).Alpha;
            result.SetPixel(x,y,new SKColor(Mul(pixel.Red,tint.R),Mul(pixel.Green,tint.G),Mul(pixel.Blue,tint.B),Mul(Mul(pixel.Alpha,alpha),tint.A)));
        }
        return result;
    }
    private static byte Mul(int a, int b) => (byte)((a*b+127)/255);
}

public sealed class SpriteVariants<T>(Func<SpriteFilter,Color,T> create) : IDisposable where T : IDisposable
{
    private readonly Dictionary<(SpriteFilter,Color),T> cache = [];
    public T Get(SpriteFilter filter,Color tint)
    {
        if (cache.TryGetValue((filter,tint),out var value)) return value;
        return cache[(filter,tint)] = create(filter,tint);
    }
    public void Invalidate(string path)
    {
        foreach (var key in cache.Keys.Where(k => k.Item1.Image == path || k.Item1.Mask == path).ToArray())
        { cache[key].Dispose(); cache.Remove(key); }
    }
    public void Dispose() { foreach (var item in cache.Values) item.Dispose(); cache.Clear(); }
}
