using Brutal.SdlApi;
using Engine.Core;
using System.Drawing;

namespace Engine.Desktop;

internal static unsafe class SdlSpriteMask
{
    private sealed class Image(string path) : IDisposable
    {
        private readonly SdlSurface* surface = Read(path);
        internal int Width => surface->w;
        internal int Height => surface->h;
        internal byte* Pixel(int x, int y) => (byte*)surface->pixels + y * surface->pitch + x * 4;

        private static SdlSurface* Read(string path)
        {
            var source = Sdl.PInvoke.LoadPNG(path);
            if (source == null) throw new InvalidDataException($"{path}: {Sdl.PInvoke.GetError()}");
            try
            {
                var converted = Sdl.PInvoke.ConvertSurface(source, SdlPixelFormat.Rgba32);
                if (converted == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
                return converted;
            }
            finally { Sdl.PInvoke.DestroySurface(source); }
        }
        public void Dispose() => Sdl.PInvoke.DestroySurface(surface);
    }

    internal static RgbaImage Filter(SpriteFilter filter, Color tint)
    {
        using var image = new Image(filter.Image);
        using var mask = filter.Mask == null ? null : new Image(filter.Mask);
        var region = filter.Source ?? new(0, 0, image.Width, image.Height);
        var canvas = filter.Canvas ?? new(region.Width, region.Height);
        if (!region.Fits(image.Width, image.Height) || !canvas.Fits(region)) throw new InvalidDataException("Invalid sprite crop.");
        int width = Math.Max(region.Width, (int)Math.Ceiling((mask?.Width ?? 0) * (double)region.Width / canvas.Width));
        int height = Math.Max(region.Height, (int)Math.Ceiling((mask?.Height ?? 0) * (double)region.Height / canvas.Height));
        if ((long)width * height > 16777216) throw new InvalidDataException("Filtered sprite exceeds 16 megapixels.");
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double u = (x + .5) * region.Width / width, v = (y + .5) * region.Height / height;
            var source = image.Pixel(region.X + (int)u, region.Y + (int)v);
            int alpha = mask == null ? 255 : mask.Pixel((int)((canvas.OffsetX + u) * mask.Width / canvas.Width),
                (int)((canvas.OffsetY + v) * mask.Height / canvas.Height))[3];
            int i = (y * width + x) * 4;
            pixels[i] = Multiply(source[0], tint.R);
            pixels[i + 1] = Multiply(source[1], tint.G);
            pixels[i + 2] = Multiply(source[2], tint.B);
            pixels[i + 3] = Multiply(Multiply(source[3], alpha), tint.A);
        }
        return new(width, height, pixels);

        static byte Multiply(int a, int b) => (byte)((a * b + 127) / 255);
    }
}
