using System.Text;
using Brutal.SdlApi;
using Brutal.SdlApi.Ttf;

namespace Engine.Desktop;

// Game text through BRUTAL's SDL_ttf (FreeType + HarfBuzz). Strings are shaped and rasterized once at FontSize;
// SdlText and the editor preview scale the cached label, so sizes need no extra font objects.
public sealed unsafe class TtfTextRasterizer : IGameTextRasterizer
{
    public const int FontSize = 32;
    // Label images keep MoNoon's text box: the baseline sits FontSize rows below the top, with room for descenders.
    internal const int LabelHeight = FontSize + 12;
    private IReadOnlyList<string> paths = [];
    private readonly List<TtfFont> fonts = [];
    // The primary font without fallbacks: TTF_FontHasGlyph on fonts[0] also answers for its fallbacks.
    private TtfFont bare;
    private readonly Dictionary<string, float> widths = new(StringComparer.Ordinal);
    private readonly List<(int Start, int End, int Pen)> clusters = [];
    private bool initialized;

    public bool UseFonts(IReadOnlyList<string> next)
    {
        if (ReferenceEquals(paths, next) || paths.SequenceEqual(next, StringComparer.Ordinal)) return false;
        Reset();
        paths = next;
        return true;
    }

    private static IEnumerable<string> InstalledFonts()
    {
        // shortcut: only these well-known faces back unconfigured games; configure project fonts for other scripts.
        if (OperatingSystem.IsMacOS())
            return new[] { "Helvetica.ttc", "AppleSDGothicNeo.ttc", "Hiragino Sans GB.ttc", "Apple Symbols.ttf" }.Select(f => "/System/Library/Fonts/" + f);
        if (OperatingSystem.IsWindows())
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            return new[] { "segoeui.ttf", "malgun.ttf", "msyh.ttc", "meiryo.ttc", "seguisym.ttf" }.Select(f => Path.Combine(root, f));
        }
        string[] roots = ["/usr/share/fonts", "/usr/local/share/fonts", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/fonts")];
        var all = roots.Where(Directory.Exists).SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".ttf" or ".otf" or ".ttc").ToArray();
        string[] preferred = ["DejaVuSans.ttf", "LiberationSans-Regular.ttf", "NotoSansCJK-Regular.ttc"];
        var chosen = all.Where(path => preferred.Contains(Path.GetFileName(path))).OrderBy(path => Array.IndexOf(preferred, Path.GetFileName(path))).ToArray();
        return chosen.Length > 0 ? chosen : all.Take(1);
    }

    private TtfFont Font
    {
        get
        {
            if (fonts.Count > 0) return fonts[0];
            if (!initialized)
            {
                if (SdlTtf.PInvoke.Init() == 0) throw new InvalidOperationException(Sdl.PInvoke.GetError());
                initialized = true;
            }
            bool packaged = paths.Count > 0;
            string? first = null;
            foreach (string path in packaged ? paths : InstalledFonts())
            {
                if (!packaged && !File.Exists(path)) continue;
                var face = SdlTtf.PInvoke.OpenFont(path, FontSize);
                if (face.IsNull())
                {
                    if (packaged) { CloseFonts(); throw new InvalidDataException($"Cannot load game font: {path} ({Sdl.PInvoke.GetError()})"); }
                    continue;
                }
                // Later fonts supply the characters the first one lacks (TTF_AddFallbackFont).
                if (fonts.Count > 0 && SdlTtf.PInvoke.AddFallbackFont(fonts[0], face) == 0)
                {
                    face.Close();
                    CloseFonts();
                    throw new InvalidDataException($"Cannot use fallback font {path}: {Sdl.PInvoke.GetError()}");
                }
                fonts.Add(face);
                first ??= path;
                if (fonts.Count == 2 && (bare = SdlTtf.PInvoke.OpenFont(first, FontSize)).IsNull())
                {
                    CloseFonts();
                    throw new InvalidDataException(Sdl.PInvoke.GetError());
                }
            }
            return fonts.Count > 0 ? fonts[0] : throw new InvalidDataException("No system font is available. Configure a default game font.");
        }
    }

    // SDL_ttf picks the fallback font per code point before shaping, so decomposed Hangul jamo would miss the
    // precomposed syllable in a fallback font. Labels are composed (NFC) first, as MoNoon always did.
    private static string Composed(string text) => text.IsNormalized() ? text : text.Normalize();

    public float Measure(string text, float size)
    {
        if (text.Length == 0) return 0;
        if (!widths.TryGetValue(text, out float width))
        {
            int w, h;
            if (SdlTtf.PInvoke.GetStringSize(Font, Composed(text), 0, &w, &h) == 0) throw new InvalidDataException(Sdl.PInvoke.GetError());
            if (widths.Count >= 1024) widths.Clear();
            widths.Add(text, width = w);
        }
        return width * size / FontSize;
    }

    public void MeasureEnds(string text, float size, Span<float> ends)
    {
        if (text.Length == 0) return;
        if (ends.Length < text.Length) throw new ArgumentException("One end per UTF-16 character is required.", nameof(ends));
        if (!text.IsNormalized())
        {
            // Composition changes the string length; measure each text element on its own instead.
            float x = 0;
            var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            while (elements.MoveNext())
            {
                string element = elements.GetTextElement();
                x += Measure(element, size);
                ends.Slice(elements.ElementIndex, element.Length).Fill(x);
            }
            return;
        }
        int width = Shape(text);
        float scale = size / FontSize;
        ends[..text.Length].Fill(-1);
        for (int i = 0; i < clusters.Count; i++)
            ends[clusters[i].Start..clusters[i].End].Fill((i + 1 < clusters.Count ? clusters[i + 1].Pen : width) * scale);
        for (int i = 0; i < text.Length; i++) if (ends[i] < 0) ends[i] = i > 0 ? ends[i - 1] : 0;
    }

    // Shapes composed text with TTF_Text into `clusters` (UTF-16 range and pen x) and returns its advance width.
    private int Shape(string text)
    {
        // UTF-8 offset of each UTF-16 char, to map SDL_ttf clusters back to the string.
        int[] offsets = new int[text.Length + 1];
        for (int i = 0, at = 0; i <= text.Length; i++)
        {
            offsets[i] = at;
            if (i == text.Length) break;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) { offsets[++i] = at; at += 4; }
            else at += text[i] < 0x80 ? 1 : text[i] < 0x800 ? 2 : 3;
        }
        var shaped = SdlTtf.PInvoke.CreateText(null, Font, text, 0);
        if (shaped == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
        clusters.Clear();
        try
        {
            // TTF_Text cluster rectangles cover the ink; less the glyph's left bearing, each is the pen position where
            // HarfBuzz placed the cluster (kerning included). A cluster ends where the next one starts.
            TtfSubString cluster, next;
            int k = 0, width, height;
            for (bool ok = SdlTtf.PInvoke.GetTextSubString(shaped, 0, &cluster) != 0; ok && cluster.length > 0;
                 ok = SdlTtf.PInvoke.GetNextTextSubString(shaped, &cluster, &next) != 0 && next.offset != cluster.offset, cluster = next)
            {
                while (k > 0 && offsets[k] > cluster.offset) k--;
                while (k < text.Length && offsets[k] < cluster.offset) k++;
                int start = k, left = 0, ignored;
                if (k < text.Length) SdlTtf.PInvoke.GetGlyphMetrics(Font, CodePoint(text, k), &left, &ignored, &ignored, &ignored, &ignored);
                while (k < text.Length && offsets[k] < cluster.offset + cluster.length) k++;
                clusters.Add((start, k, cluster.rect.x - left));
            }
            if (SdlTtf.PInvoke.GetTextSize(shaped, &width, &height) == 0) throw new InvalidDataException(Sdl.PInvoke.GetError());
            return width;
        }
        finally { SdlTtf.PInvoke.DestroyText(shaped); }
    }

    private static uint CodePoint(string text, int index) => (uint)(Rune.TryGetRuneAt(text, index, out var rune) ? rune.Value : 0xFFFD);

    // The font SDL_ttf uses for a char: the first configured font that has the glyph, otherwise the primary.
    private int FontFor(string text, int index)
    {
        uint cp = CodePoint(text, index);
        if (SdlTtf.PInvoke.FontHasGlyph(bare, cp) != 0) return 0;
        for (int i = 1; i < fonts.Count; i++) if (SdlTtf.PInvoke.FontHasGlyph(fonts[i], cp) != 0) return i;
        return 0;
    }

    public RgbaImage Rasterize(string text)
    {
        text = Composed(text);
        _ = Font;
        // SDL_ttf 3.2.2 places a fallback font's glyphs on that font's own ascent instead of the line's baseline. Each
        // font run is therefore rendered by its own font and put on the shared baseline at the pen x shaping gave it.
        var runs = new List<(int Start, int End, int Font)>();
        for (int i = 0, step; i < text.Length; i += step)
        {
            step = char.IsSurrogatePair(text, i) ? 2 : 1;
            int font = fonts.Count > 1 ? FontFor(text, i) : 0;
            if (runs.Count > 0 && runs[^1].Font == font) runs[^1] = runs[^1] with { End = i + step };
            else runs.Add((i, i + step, font));
        }
        int advance = text.Length == 0 ? 0 : runs.Count > 1 ? Shape(text) : (int)Measure(text, FontSize);
        var placed = new List<(nint Surface, int X, int Top)>();
        try
        {
            int width = Math.Max(1, advance);
            foreach (var run in runs)
            {
                int pen = 0;
                if (runs.Count > 1) foreach (var c in clusters) if (c.Start <= run.Start && run.Start < c.End) { pen = c.Pen; break; }
                var font = run.Font == 0 && fonts.Count > 1 ? bare : fonts[run.Font];
                var rendered = SdlTtf.PInvoke.RenderTextBlended(font, text[run.Start..run.End], 0, new SdlColor { r = 255, g = 255, b = 255, a = 255 });
                if (rendered == null) continue; // whitespace-only runs draw nothing
                var surface = Sdl.PInvoke.ConvertSurface(rendered, SdlPixelFormat.Rgba32);
                Sdl.PInvoke.DestroySurface(rendered);
                if (surface == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
                placed.Add(((nint)surface, pen, FontSize - SdlTtf.PInvoke.GetFontAscent(font)));
                width = Math.Max(width, pen + surface->w);
            }
            width += 2;
            var rgba = new byte[width * LabelHeight * 4];
            Array.Fill(rgba, (byte)255);
            for (int i = 3; i < rgba.Length; i += 4) rgba[i] = 0;
            foreach (var (handle, x, top) in placed)
            {
                var surface = (SdlSurface*)handle;
                byte* pixels = (byte*)surface->pixels;
                for (int row = Math.Max(0, -top); row < Math.Min(surface->h, LabelHeight - top); row++)
                for (int col = 0; col < surface->w; col++)
                {
                    int i = ((top + row) * width + x + col + 1) * 4 + 3;
                    rgba[i] = Math.Max(rgba[i], pixels[row * surface->pitch + col * 4 + 3]);
                }
            }
            return new(width, LabelHeight, rgba);
        }
        finally { foreach (var (handle, _, _) in placed) Sdl.PInvoke.DestroySurface((SdlSurface*)handle); }
    }

    private void CloseFonts()
    {
        foreach (var font in fonts) font.Close(); // the primary first, then its fallbacks
        if (!bare.IsNull()) bare.Close();
        bare = default;
        fonts.Clear();
        widths.Clear();
    }

    private void Reset()
    {
        CloseFonts();
        paths = [];
    }

    public void Dispose()
    {
        Reset();
        if (initialized) SdlTtf.PInvoke.Quit();
        initialized = false;
    }
}
