using Brutal.SdlApi;

namespace Engine.Desktop;

internal sealed class SdlText : IDisposable
{
    private readonly GameTextRasterizer textRenderer = new();
    private readonly Dictionary<string, (SdlTexture Texture, int Width, int Height)> cache = new();
    public void UseFonts(IReadOnlyList<string> paths)
    {
        if (textRenderer.UseFonts(paths)) Clear();
    }
    public float Measure(string text,float size) => textRenderer.Measure(text,size);
    public long Submissions {get;private set;}
    public unsafe void Draw(SdlRenderer renderer, string text, float x, float y, float size, System.Drawing.Color color)
    {
        if (text.Length == 0 || size <= 0) return;
        if (!cache.TryGetValue(text, out var label))
        {
            if (cache.Count >= 128) Clear();
            using var bitmap = textRenderer.Rasterize(text);
            var pixels = (byte*)bitmap.GetPixels();
            for (int i = 0; i < bitmap.ByteCount; i += 4) pixels[i] = pixels[i + 1] = pixels[i + 2] = 255;
            var surface = Sdl.PInvoke.CreateSurfaceFrom(bitmap.Width, bitmap.Height, SdlPixelFormat.Rgba32, pixels, bitmap.RowBytes);
            if (surface == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
            try { label = (renderer.CreateTextureFromSurface(surface), bitmap.Width, bitmap.Height); }
            finally { Sdl.PInvoke.DestroySurface(surface); }
            cache.Add(text, label);
        }
        float scale = size / GameTextRasterizer.FontSize;
        var target = new SdlFRect { x = x, y = y, w = label.Width * scale, h = label.Height * scale };
        Sdl.Render.SetTextureColorMod(label.Texture, color.R, color.G, color.B);
        Submissions++;
        renderer.RenderTexture(label.Texture, null, &target);
    }

    private void Clear()
    {
        foreach (var label in cache.Values) label.Texture.Dispose();
        cache.Clear();
    }

    public void Dispose()
    {
        Clear();
        textRenderer.Dispose();
    }
}
