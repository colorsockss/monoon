using System.Text;
using Brutal.SdlApi;
using SkiaSharp;

namespace Engine.Desktop;

internal sealed class SdlText : IDisposable
{
    private const int FontSize = 32;
    private readonly Dictionary<string, (SdlTexture Texture, int Width, int Height)> cache = new();
    private readonly SKFontManager fonts = SKFontManager.Default;
    private readonly Dictionary<int, SKTypeface> faces = new();

    private IEnumerable<(string Text,SKTypeface Face,float Width)> Runs(string text,SKPaint paint)
    {
        var elements=System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while(elements.MoveNext())
        {
            string glyph=elements.GetTextElement();
            int cp=Rune.GetRuneAt(glyph,0).Value;
            if(!faces.TryGetValue(cp,out var face))faces[cp]=face=fonts.MatchCharacter(cp)??SKTypeface.Default;
            paint.Typeface=face;yield return (glyph,face,paint.MeasureText(glyph));
        }
    }
    public float Measure(string text,float size)
    {
        using var paint=new SKPaint { TextSize=FontSize };
        return Runs(text,paint).Sum(r=>r.Width)*size/FontSize;
    }
    public long Submissions {get;private set;}
    public unsafe void Draw(SdlRenderer renderer, string text, float x, float y, float size, System.Drawing.Color color)
    {
        if (text.Length == 0 || size <= 0) return;
        if (!cache.TryGetValue(text, out var label))
        {
            if (cache.Count >= 128) Clear();
            using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = FontSize };
            var glyphs = new List<(string Glyph, SKTypeface Face, float X)>();
            float width = 0;
            foreach(var run in Runs(text,paint))
            {
                glyphs.Add((run.Text,run.Face,width));width+=run.Width;
                if(width>=8000)break;
            }
            using var bitmap = new SKBitmap(Math.Max(1, (int)Math.Min(8192,Math.Ceiling(width)+4)), FontSize + 12, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                foreach (var glyph in glyphs)
                {
                    paint.Typeface = glyph.Face;
                    canvas.DrawText(glyph.Glyph, glyph.X + 1, FontSize, paint);
                }
                canvas.Flush();
            }
            var pixels = (byte*)bitmap.GetPixels();
            for (int i = 0; i < bitmap.ByteCount; i += 4) pixels[i] = pixels[i + 1] = pixels[i + 2] = 255;
            var surface = Sdl.PInvoke.CreateSurfaceFrom(bitmap.Width, bitmap.Height, SdlPixelFormat.Rgba32, pixels, bitmap.RowBytes);
            if (surface == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
            try { label = (renderer.CreateTextureFromSurface(surface), bitmap.Width, bitmap.Height); }
            finally { Sdl.PInvoke.DestroySurface(surface); }
            cache.Add(text, label);
        }
        float scale = size / FontSize;
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
        foreach (var face in faces.Values.Distinct()) if (face != SKTypeface.Default) face.Dispose();
        faces.Clear();
    }
}
