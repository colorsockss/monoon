using Brutal.SdlApi;
using Engine.Core;

namespace Engine.Desktop;

// SDL's software geometry backend truncates vertex texture coordinates to whole texels.
// Expand only cells whose half/quarter points would land between texels.
internal sealed unsafe class SoftwareTileTextures(string path) : IDisposable
{
    private readonly Dictionary<(SpriteRegion, int, int), SdlTexture> textures = new();
    private SdlSurface* sheet;

    public static (int X, int Y) Scale(SpriteRegion source, string layout)
    {
        int x = layout == TileGrid.HexFlat ? 4 : 2;
        int y = layout == TileGrid.HexPointy ? 4 : 2;
        return (Factor(source.Width, x), Factor(source.Height, y));
        static int Factor(int size, int denominator) => size % denominator == 0 ? 1 : size % 2 == 0 ? 2 : denominator;
    }

    public SdlTexture Get(SdlRenderer renderer, SpriteRegion source, int scaleX, int scaleY)
    {
        var key = (source, scaleX, scaleY);
        if (textures.TryGetValue(key, out var cached)) return cached;
        if (sheet == null)
        {
            sheet = Sdl.PInvoke.LoadPNG(path);
            if (sheet == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
            // Copy straight RGBA, including partially transparent pixels, to the new cell.
            if (Sdl.PInvoke.SetSurfaceBlendMode(sheet, 0) == 0)
            {
                var message = Sdl.PInvoke.GetError();
                Sdl.PInvoke.DestroySurface(sheet);
                sheet = null;
                throw new InvalidDataException(message);
            }
        }
        var scaled = Sdl.PInvoke.CreateSurface(checked(source.Width * scaleX), checked(source.Height * scaleY), SdlPixelFormat.Rgba32);
        if (scaled == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
        try
        {
            var region = new SdlRect { x = source.X, y = source.Y, w = source.Width, h = source.Height };
            if (Sdl.PInvoke.BlitSurfaceScaled(sheet, &region, scaled, null, SdlScaleMode.Nearest) == 0)
                throw new InvalidDataException(Sdl.PInvoke.GetError());
            var texture = renderer.CreateTextureFromSurface(scaled);
            if (!texture.SetScaleMode(SdlScaleMode.Nearest))
            {
                texture.Dispose();
                throw new InvalidDataException(Sdl.PInvoke.GetError());
            }
            textures.Add(key, texture);
            return texture;
        }
        finally { Sdl.PInvoke.DestroySurface(scaled); }
    }

    public void Dispose()
    {
        foreach (var texture in textures.Values) texture.Dispose();
        textures.Clear();
        if (sheet != null) Sdl.PInvoke.DestroySurface(sheet);
        sheet = null;
    }
}
