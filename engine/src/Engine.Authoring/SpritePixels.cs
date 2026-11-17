using Engine.Core;
using SkiaSharp;
using System.Drawing;

namespace Engine.Desktop;

// A mask covers the original logical canvas, including borders trimmed by the atlas.
public static class SpritePixels
{
    public static SKBitmap Filter(SpriteFilter filter, Color tint)
    {
        var image = SdlSpriteMask.Filter(filter, tint);
        var result = new SKBitmap(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        System.Runtime.InteropServices.Marshal.Copy(image.Pixels, 0, result.GetPixels(), image.Pixels.Length);
        return result;
    }
}
