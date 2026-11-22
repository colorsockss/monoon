using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Engine.Core;
using SkiaSharp;

namespace Engine.Desktop;

internal static class FontGlyphBuild
{
    internal static void Write(string project, string game, GameLocalization localization, IEnumerable<SceneDocument> scenes,
        ContentBuildCache? cache = null, IEnumerable<string>? assets = null)
    {
        if (localization.Fonts.Count == 0) return;
        // Only cook frequently drawn, authored characters. Variable and user-entered text can use the original font.
        var characters = new SortedSet<int>(Enumerable.Range(32, 95));
        void Include(string text)
        {
            foreach (var rune in text.Normalize().EnumerateRunes())
                if (characters.Count < FontGlyphs.Limit && !Rune.IsControl(rune)) characters.Add(rune.Value);
        }
        foreach (var text in localization.Messages) Include(text);
        foreach (var scene in scenes)
        foreach (var label in scene.Objects.SelectMany(o => o.Components.OfType<UiTextComponent>()))
        {
            Include(label.Text);
            if (label.Translations != null) foreach (var value in label.Translations.Values) Include(value);
        }
        foreach (var options in scenes.SelectMany(scene => scene.Objects).SelectMany(obj => obj.Components.OfType<UiDropdownComponent>()))
            foreach (var option in options.Options) Include(option);
        // Data assets and text columns can provide in-game dialogue and choices.
        void IncludeValue(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String) Include(value.GetString()!);
            if (value.ValueKind == JsonValueKind.Array)
                foreach (var element in value.EnumerateArray()) IncludeValue(element);
            if (value.ValueKind == JsonValueKind.Object)
                foreach (var property in value.EnumerateObject()) IncludeValue(property.Value);
        }
        foreach (string asset in (assets ?? []).Order(StringComparer.Ordinal))
        {
            var file = Path.Combine(game, asset);
            if (asset.EndsWith(".data", StringComparison.OrdinalIgnoreCase))
                foreach (var value in GameData.Load(file).Values.Values) IncludeValue(value);
            if (asset.EndsWith(GameTable.Extension, StringComparison.OrdinalIgnoreCase))
                foreach (var row in GameTable.Load(file).Rows)
                    foreach (var cell in row.Cells) IncludeValue(cell);
        }
        foreach (var source in localization.Fonts)
        {
            string relative = Path.GetRelativePath(project, source).Replace('\\','/');
            string path = Path.Combine(game, relative), output = path + FontGlyphs.Extension;
            var font = File.ReadAllBytes(path);
            var hash = ContentBuildCache.Digest(font);
            void Write(string file) => WriteFont(path, file, hash, characters);
            if (cache == null) Write(output);
            else cache.Generate("glyphs " + relative, new() { [relative] = hash,
                ["characters"] = ContentBuildCache.Digest(ProjectYaml.Encode(characters.ToArray())) }, output, Write, optional: true);
            if (File.Exists(output))
            {
                var coverage = FontGlyphs.Read(path, font) ?? throw new InvalidDataException($"Invalid baked glyphs for {relative}.");
                characters.ExceptWith(coverage.Keys);
            }
        }
    }

    private static void WriteFont(string path, string output, string fontHash, SortedSet<int> characters)
    {
        using var face = SKTypeface.FromFile(path) ?? throw new InvalidDataException($"Cannot bake font: {path}");
        using var paint = new SKPaint { Typeface = face, TextSize = FontGlyphs.FontSize, IsAntialias = true, Color = SKColors.White };
        var available = new List<(int Codepoint, FontGlyph Glyph)>();
        foreach (int cp in characters.Where(cp => face.ContainsGlyph(cp)))
            if (BakeGlyph(paint, cp) is {} glyph) available.Add((cp, glyph));
        if (available.Count == 0) return;
        using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
        using var compressed = new ZLibStream(file, CompressionLevel.Optimal);
        using var writer = new BinaryWriter(compressed);
        writer.Write(FontGlyphs.Version);
        writer.Write(Convert.FromHexString(fontHash));
        writer.Write(available.Count);
        foreach (var (cp, glyph) in available)
        {
            writer.Write(cp);
            writer.Write(glyph.Advance);
            writer.Write((short)glyph.Left);
            writer.Write((short)glyph.Top);
            writer.Write((ushort)glyph.Width);
            writer.Write((ushort)glyph.Height);
            writer.Write(glyph.Alpha);
        }
    }

    private static FontGlyph? BakeGlyph(SKPaint paint, int cp)
    {
        const int size = FontGlyphs.FontSize;
        var text = char.ConvertFromUtf32(cp);
        var bounds = new SKRect();
        float advance = paint.MeasureText(text, ref bounds);
        int left = (int)Math.Floor(bounds.Left) - 1, top = Math.Max(-size, (int)Math.Floor(bounds.Top) - 1);
        int width = Math.Max(0, (int)Math.Ceiling(bounds.Right) + 1 - left);
        int height = Math.Max(0, Math.Min(12, (int)Math.Ceiling(bounds.Bottom) + 1) - top);
        if (!float.IsFinite(advance) || advance is < 0 or > 256 || width > 256 ||
            left is < short.MinValue or > short.MaxValue || top is < short.MinValue or > short.MaxValue) return null;
        var alpha = new byte[width * height];
        var glyph = new FontGlyph(advance, left, top, width, height, alpha);
        if (alpha.Length == 0) return glyph;
        using var pixels = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(pixels);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawText(text, -left, -top, paint);
        canvas.Flush();
        var rgba = pixels.GetPixelSpan();
        for (int i = 0; i < alpha.Length; i++) alpha[i] = rgba[i * 4 + 3];
        return glyph;
    }
}
