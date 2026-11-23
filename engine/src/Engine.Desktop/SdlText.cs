using Brutal.SdlApi;

namespace Engine.Desktop;

public interface IGameTextRasterizer : IDisposable
{
    bool UseFonts(IReadOnlyList<string> paths);
    float Measure(string text, float size);
    /// <summary>Right edge of the glyph cluster holding each UTF-16 char when <paramref name="text"/> is drawn in one piece.</summary>
    void MeasureEnds(string text, float size, Span<float> ends);
    /// <summary>White coverage at <see cref="TtfTextRasterizer.FontSize"/>; the baseline is that many rows below the top.</summary>
    RgbaImage Rasterize(string text);
}

internal sealed class SdlText(IGameTextRasterizer textRenderer) : IDisposable
{
    private readonly Dictionary<string, (SdlTexture Texture, int Width, int Height)> cache = new();
    public void UseFonts(IReadOnlyList<string> paths)
    {
        if (textRenderer.UseFonts(paths)) Clear();
    }
    public float Measure(string text,float size) => textRenderer.Measure(text,size);
    public void MeasureEnds(string text,float size,Span<float> ends) => textRenderer.MeasureEnds(text,size,ends);
    public long Submissions {get;private set;}
    public unsafe void Draw(SdlRenderer renderer, string text, float x, float y, float size, System.Drawing.Color color)
    {
        if (text.Length == 0 || size <= 0) return;
        if (!cache.TryGetValue(text, out var label))
        {
            if (cache.Count >= 128) Clear();
            var bitmap = textRenderer.Rasterize(text);
            label = (bitmap.CreateTexture(renderer), bitmap.Width, bitmap.Height);
            cache.Add(text, label);
        }
        float scale = size / TtfTextRasterizer.FontSize;
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
