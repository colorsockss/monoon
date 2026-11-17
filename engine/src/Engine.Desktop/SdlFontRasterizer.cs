using System.Text;
using static StbTrueTypeSharp.StbTrueType;

namespace Engine.Desktop;

// Generate monochrome glyph coverage. SDL owns all textures, tinting and screen drawing.
internal sealed unsafe class SdlFontRasterizer : IGameTextRasterizer
{
    internal const int FontSize = FontGlyphs.FontSize;
    private sealed class Face : IDisposable
    {
        internal readonly stbtt_fontinfo Font;
        internal readonly string Name;
        internal readonly Dictionary<int, FontGlyph>? Baked;
        internal Face(string path, bool packaged)
        {
            Name = Path.GetFileName(path);
            var bytes = File.ReadAllBytes(path);
            Baked = packaged ? FontGlyphs.Read(path, bytes) : null;
            Font = Load(path, bytes);
        }
        internal float Scale => stbtt_ScaleForMappingEmToPixels(Font, FontSize);
        private static stbtt_fontinfo Load(string path, byte[] bytes)
        {
            if (bytes.Length >= 16)
            {
                fixed (byte* data = bytes)
                {
                    int offset = stbtt_GetFontOffsetForIndex(data, 0);
                    if (offset >= 0 && offset <= bytes.Length - 12 && CreateFont(bytes, offset) is {} face) return face;
                }
            }
            throw new InvalidDataException($"Cannot load game font: {path}");
        }
        public void Dispose() => Font.Dispose();
    }

    private IReadOnlyList<string> paths = [];
    private string[]? system;
    private readonly Dictionary<string, Face> fonts = [];
    private readonly Dictionary<int, FontGlyph> glyphs = [];
    private readonly HashSet<string> unsupported = [];
    private readonly bool trace = Environment.GetEnvironmentVariable("MONOON_FONT_TRACE") == "1";

    public bool UseFonts(IReadOnlyList<string> next)
    {
        if (ReferenceEquals(paths, next) || paths.SequenceEqual(next, StringComparer.Ordinal)) return false;
        Dispose();
        paths = next;
        return true;
    }

    private static IEnumerable<string> InstalledFonts()
    {
        if (OperatingSystem.IsMacOS())
        {
            const string root = "/System/Library/Fonts/";
            return new[] { "Helvetica.ttc", "AppleSDGothicNeo.ttc", "Hiragino Sans GB.ttc", "Apple Symbols.ttf" }
                .Select(file => root + file).Concat(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        }
        if (OperatingSystem.IsWindows())
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            return new[] { "segoeui.ttf", "malgun.ttf", "msyh.ttc", "meiryo.ttc", "seguisym.ttf" }
                .Select(file => Path.Combine(root, file)).Concat(Directory.EnumerateFiles(root));
        }
        string[] roots = ["/usr/share/fonts", "/usr/local/share/fonts", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/fonts")];
        var available = roots.Where(Directory.Exists).SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        return available.OrderBy(path => Path.GetFileName(path) switch { "DejaVuSans.ttf" => 0, "LiberationSans-Regular.ttf" => 1, "NotoSansCJK-Regular.ttc" => 2, _ => 3 });
    }

    private FontGlyph GetGlyph(int codepoint)
    {
        if (glyphs.TryGetValue(codepoint, out var cached)) return cached;
        var candidates = paths.Count > 0 ? paths : system ??= InstalledFonts().Where(path =>
            (Path.GetExtension(path).ToLowerInvariant() is ".otf" or ".ttf" or ".ttc") && File.Exists(path)).Distinct().ToArray();
        Face? first = null, selected = null;
        int index = 0;
        foreach (string path in candidates)
        {
            if (unsupported.Contains(path)) continue;
            bool loaded = fonts.TryGetValue(path, out var face);
            if (!loaded)
            {
                try { face = new Face(path, paths.Count != 0); }
                catch (Exception ex) when (paths.Count == 0 && ex is InvalidDataException or IOException or UnauthorizedAccessException)
                { unsupported.Add(path); continue; }
            }
            first ??= face!;
            index = stbtt_FindGlyphIndex(face!.Font, codepoint);
            // Keep system faces that actually supply glyphs. A missing character can probe many installed fonts.
            if (!loaded)
            {
                if (paths.Count != 0 || index != 0 || face == first) fonts.Add(path, face);
                else face.Dispose();
            }
            if (index != 0) { selected = face; break; }
        }
        selected ??= first ?? throw new InvalidDataException("No system font is available. Configure a default game font.");
        if (trace) Console.Error.WriteLine($"[game-font] U+{codepoint:X4} {(index == 0 ? "missing" : selected.Name)}");
        if (glyphs.Count >= 4096) glyphs.Clear();
        if (selected.Baked != null && selected.Baked.TryGetValue(codepoint, out var baked)) return glyphs[codepoint] = baked;
        float scale = selected.Scale;
        int advance, bearing, left, top, right, bottom;
        stbtt_GetGlyphHMetrics(selected.Font, index, &advance, &bearing);
        stbtt_GetGlyphBitmapBox(selected.Font, index, scale, scale, &left, &top, &right, &bottom);
        int width = right - left, height = bottom - top;
        byte[] alpha = new byte[checked(width * height)];
        if (alpha.Length != 0)
        {
            fixed (byte* dest = alpha)
                stbtt_MakeGlyphBitmap(selected.Font, dest, width, height, width, scale, scale, index);
        }
        return glyphs[codepoint] = new((int)MathF.Round(advance * scale), left, top, width, height, alpha);
    }

    public float Measure(string text, float size)
    {
        float width = 0;
        foreach (var rune in text.Normalize().EnumerateRunes()) width += GetGlyph(rune.Value).Advance;
        return width * size / FontSize;
    }

    public RgbaImage Rasterize(string text)
    {
        string normalized = text.Normalize();
        float advance = 0;
        foreach (var rune in normalized.EnumerateRunes())
        {
            advance += GetGlyph(rune.Value).Advance;
            if (advance >= 8000) break;
        }
        int width = (int)Math.Ceiling(advance) + 4, height = FontSize + 12;
        var rgba = new byte[width * height * 4];
        Array.Fill(rgba, (byte)255);
        for (int i = 3; i < rgba.Length; i += 4) rgba[i] = 0;
        float x = 1;
        foreach (var rune in normalized.EnumerateRunes())
        {
            var glyph = GetGlyph(rune.Value);
            int left = (int)MathF.Floor(x + .5f) + glyph.Left, top = FontSize + glyph.Top;
            for (int row = Math.Max(0, -top); row < Math.Min(glyph.Height, height - top); row++)
            for (int col = Math.Max(0, -left); col < Math.Min(glyph.Width, width - left); col++)
            {
                int i = ((top + row) * width + left + col) * 4 + 3;
                int alpha = glyph.Alpha[row * glyph.Width + col];
                rgba[i] += (byte)((alpha * (255 - rgba[i]) + 127) / 255);
            }
            x += glyph.Advance;
            if (x - 1 >= 8000) break;
        }
        return new(width, height, rgba);
    }

    public void Dispose()
    {
        foreach (var font in fonts.Values) font.Dispose();
        fonts.Clear();
        unsupported.Clear();
        glyphs.Clear();
        paths = [];
    }
}
