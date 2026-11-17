using Brutal.SdlApi;

namespace Engine.Desktop;

public sealed class RgbaImage
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public RgbaImage(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0 || checked((long)width * height * 4) != pixels.LongLength)
            throw new ArgumentException("Expected a tightly packed RGBA image.");
        (Width, Height, Pixels) = (width, height, pixels);
    }

    internal unsafe SdlTexture CreateTexture(SdlRenderer renderer)
    {
        fixed (byte* pixels = Pixels)
        {
            var surface = Sdl.PInvoke.CreateSurfaceFrom(Width, Height, SdlPixelFormat.Rgba32, pixels, Width * 4);
            if (surface == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
            try { return renderer.CreateTextureFromSurface(surface); }
            finally { Sdl.PInvoke.DestroySurface(surface); }
        }
    }
}
