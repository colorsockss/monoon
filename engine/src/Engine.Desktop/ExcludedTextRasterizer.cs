using Engine.Core;

namespace Engine.Desktop;

// Text in a Native AOT game whose build left the text feature out (roadmap 15.12): TtfTextRasterizer's code is not compiled in,
// so this stands in for it. It answers what TtfTextRasterizer answers without loading SDL_ttf (font lists, empty strings)
// and otherwise throws the exclusion message TtfTextRasterizer gives when the library is missing (15.11).
internal sealed class ExcludedTextRasterizer : IGameTextRasterizer
{
    private IReadOnlyList<string> paths = [];

    public bool UseFonts(IReadOnlyList<string> next)
    {
        if (ReferenceEquals(paths, next) || paths.SequenceEqual(next, StringComparer.Ordinal)) return false;
        paths = next;
        return true;
    }

    public float Measure(string text, float size) => text.Length == 0 ? 0 : throw Missing();

    public void MeasureEnds(string text, float size, Span<float> ends)
    {
        if (text.Length == 0) return;
        if (ends.Length < text.Length) throw new ArgumentException("One end per UTF-16 character is required.", nameof(ends));
        throw Missing();
    }

    public RgbaImage Rasterize(string text) => throw Missing();

    public void Dispose() { }

    private static InvalidOperationException Missing() => EngineFeatures.Excluded(EngineFeature.Text, new DllNotFoundException("SDL3_ttf"));
}
