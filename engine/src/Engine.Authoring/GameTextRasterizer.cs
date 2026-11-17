using System.Globalization;
using System.Text;
using SkiaSharp;

namespace Engine.Desktop;

// Both desktop canvases measure and rasterize the same runs at the same reference size.
public sealed class GameTextRasterizer : IGameTextRasterizer
{
    public const int FontSize = FontGlyphs.FontSize;
    private IReadOnlyList<string> paths = Array.Empty<string>();
    private SKTypeface[] packaged = [];
    private readonly Dictionary<string, SKTypeface> selected = new(StringComparer.Ordinal);
    private readonly Dictionary<int, SKTypeface> system = [];
    private readonly bool trace = Environment.GetEnvironmentVariable("MONOON_FONT_TRACE") == "1";

    public bool UseFonts(IReadOnlyList<string> next)
    {
        if (ReferenceEquals(paths, next)) return false;
        if (paths.SequenceEqual(next, StringComparer.Ordinal))
        {
            paths = next;
            return false;
        }
        var loaded = new List<SKTypeface>();
        try
        {
            foreach (var path in next)
                loaded.Add(SKTypeface.FromFile(path) ?? throw new InvalidDataException($"Cannot load game font: {path}"));
        }
        catch { foreach (var face in loaded) face.Dispose(); throw; }
        Dispose();
        packaged = loaded.ToArray();
        paths = next;
        return true;
    }

    private SKTypeface Select(string element)
    {
        if (selected.TryGetValue(element, out var face)) return face;
        int cp = Rune.GetRuneAt(element, 0).Value;
        if (packaged.Length == 0)
        {
            if (!system.TryGetValue(cp, out face)) system.Add(cp, face = SKFontManager.Default.MatchCharacter(cp) ?? SKTypeface.Default);
        }
        else
        {
            int index = Array.FindIndex(packaged, f => f.ContainsGlyphs(element));
            face = packaged[Math.Max(0, index)];
            if (trace) Console.Error.WriteLine($"[game-font] U+{cp:X4} {(index < 0 ? "missing" : Path.GetFileName(paths[index]))}");
        }
        return selected[element] = face;
    }

    private IEnumerable<(string Text, SKTypeface Face, float Width)> Runs(string text, SKPaint paint)
    {
        var elements = StringInfo.GetTextElementEnumerator(text.Normalize());
        while (elements.MoveNext())
        {
            var glyph = elements.GetTextElement();
            var face = Select(glyph);
            paint.Typeface = face;
            yield return (glyph, face, paint.MeasureText(glyph));
        }
    }

    public float Measure(string text, float size)
    {
        using var paint = new SKPaint { TextSize = FontSize };
        return Runs(text, paint).Sum(run => run.Width) * size / FontSize;
    }

    public SKBitmap Rasterize(string text)
    {
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = FontSize };
        var glyphs = new List<(string Text, SKTypeface Face, float X)>();
        float width = 0;
        foreach (var run in Runs(text, paint))
        {
            glyphs.Add((run.Text, run.Face, width));
            width += run.Width;
            if (width >= 8000) break;
        }
        var bitmap = new SKBitmap(Math.Max(1, (int)Math.Ceiling(width) + 4), FontSize + 12, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        foreach (var glyph in glyphs)
        {
            paint.Typeface = glyph.Face;
            canvas.DrawText(glyph.Text, glyph.X + 1, FontSize, paint);
        }
        canvas.Flush();
        return bitmap;
    }

    RgbaImage IGameTextRasterizer.Rasterize(string text)
    {
        using var bitmap = Rasterize(text);
        var rgba = bitmap.Bytes;
        for (int i = 0; i < rgba.Length; i += 4) rgba[i] = rgba[i + 1] = rgba[i + 2] = 255;
        return new(bitmap.Width, bitmap.Height, rgba);
    }

    public void Dispose()
    {
        selected.Clear();
        foreach (var face in system.Values.Distinct()) if (face != SKTypeface.Default) face.Dispose();
        system.Clear();
        foreach (var face in packaged) face.Dispose();
        packaged = [];
        paths = Array.Empty<string>();
    }
}
