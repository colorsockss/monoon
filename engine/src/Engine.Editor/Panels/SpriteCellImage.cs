using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Engine.Core;

namespace Engine.Editor;

// The frame uses PNG pixels, while DrawingContext uses the bitmap's device-independent size.
internal sealed class SpriteCellImage(Bitmap sheet, SpriteRegion region, SpriteCanvas? canvas = null) : IImage
{
    public Size Size { get; } = new(canvas?.Width ?? region.Width, canvas?.Height ?? region.Height);

    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        var content = new Rect(canvas?.OffsetX ?? 0, canvas?.OffsetY ?? 0, region.Width, region.Height);
        var visible = sourceRect.Intersect(content);
        if (visible.Width <= 0 || visible.Height <= 0) return;
        double sx = destRect.Width / sourceRect.Width, sy = destRect.Height / sourceRect.Height;
        var pixels = new Rect(region.X + visible.X - content.X, region.Y + visible.Y - content.Y, visible.Width, visible.Height);
        var destination = new Rect(destRect.X + (visible.X - sourceRect.X) * sx, destRect.Y + (visible.Y - sourceRect.Y) * sy,
            visible.Width * sx, visible.Height * sy);
        DrawPixels(context, sheet, pixels, destination);
    }

    public static void DrawPixels(DrawingContext context, Bitmap sheet, Rect pixels, Rect destination)
    {
        var sx = sheet.Size.Width / sheet.PixelSize.Width;
        var sy = sheet.Size.Height / sheet.PixelSize.Height;
        using var sampling = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None });
        context.DrawImage(sheet, new Rect(pixels.X * sx, pixels.Y * sy, pixels.Width * sx, pixels.Height * sy), destination);
    }
}
