using System.Security.Cryptography;
using SkiaSharp;

namespace Engine.Core;

// Sheet cells are referenced by pixel rectangle. Updating their contents must never repack them.
internal static class SpriteSheetImporter
{
    private static readonly object gate = new();

    // asepriteErrors: report Aseprite import errors instead of throwing (the editor keeps applying the other sheets).
    internal static ProjectAssets Scan(string root, Action<string>? asepriteErrors = null)
    {
        lock (gate)
        {
            // Aseprite errors are raised after the PNG sheets below are updated, so one bad file does not block the rest.
            InvalidDataException? asepriteError = null;
            if (!File.Exists(Path.Combine(root, ".monoon-player")))
                try { AsepriteImporter.Import(root); } catch (InvalidDataException ex) { asepriteError = ex; }
            var assets = ProjectAssets.Scan(root);
            var images = assets.Entries.Where(e => SpriteAssets.IsPng(e.Path) && File.Exists(Path.Combine(root,e.Path)))
                .ToDictionary(e => e.Id);
            var sheets = new Dictionary<Guid,AssetMeta>();
            foreach (var image in images.Values)
            {
                var meta = ProjectYaml.Read<AssetMeta>(Path.Combine(root, image.Path + ".meta"));
                if (meta.Inputs is { Length: > 0 }) sheets.Add(image.Id, meta);
            }
            bool changed = false;
            foreach (var (id, meta) in sheets)
            {
                var entries = meta.Inputs!;
                if (meta.Slices == null || entries.Any(e => e == null || e.Cell < 0 || e.Cell >= meta.Slices.Length ||
                    sheets.ContainsKey(e.Sprite)) || entries.DistinctBy(e => e.Cell).Count() != entries.Length)
                    throw new InvalidDataException($"Invalid source mapping in {images[id].Path}.meta");
                var updates = entries.Where(e => images.TryGetValue(e.Sprite, out var image) && image.Hash != e.Hash).ToArray();
                if (updates.Length == 0) continue;
                var path = Path.Combine(root, images[id].Path);
                using var sheet = Read(path, images[id].Hash);
                foreach (var input in updates)
                {
                    var image = images[input.Sprite];
                    using var source = Read(Path.Combine(root, image.Path), image.Hash);
                    var cell = meta.Slices[input.Cell];
                    var region = cell.Source;
                    var canvas = cell.Canvas ?? new SpriteCanvas(region.Width, region.Height);
                    if (!region.Fits(sheet.Width, sheet.Height) || !canvas.Fits(region) ||
                        source.Width != canvas.Width || source.Height != canvas.Height)
                        throw new InvalidDataException($"{image.Path}: expected a {canvas.Width} × {canvas.Height} source for cell {input.Cell + 1}.");
                    for (int y = 0; y < source.Height; y++)
                    for (int x = 0; x < source.Width; x++)
                    {
                        var pixel = source.GetPixel(x, y);
                        int dx = x - canvas.OffsetX, dy = y - canvas.OffsetY;
                        if ((uint)dx < region.Width && (uint)dy < region.Height)
                            sheet.SetPixel(region.X + dx, region.Y + dy, pixel);
                        else if (pixel.Alpha != 0)
                            throw new InvalidDataException($"{image.Path}: artwork extends outside packed cell {input.Cell + 1}.");
                    }
                }
                var updated = entries.Select(e => images.TryGetValue(e.Sprite, out var image) ? e with { Hash = image.Hash } : e).ToArray();
                using var png = sheet.Encode(SKEncodedImageFormat.Png, 100);
                // An interrupted write between these two atomic replaces is safe to repeat.
                if (Hash(File.ReadAllBytes(path)) != images[id].Hash ||
                    !ProjectYaml.Encode(ProjectYaml.Read<AssetMeta>(path + ".meta")).AsSpan().SequenceEqual(ProjectYaml.Encode(meta)))
                    throw new IOException($"Sheet changed during import: {path}");
                ProjectYaml.WriteBytes(path, png.ToArray());
                ProjectYaml.Write(path + ".meta", meta with { Inputs = updated });
                changed = true;
            }
            var result = changed ? ProjectAssets.Scan(root) : assets;
            if (asepriteError != null) { if (asepriteErrors == null) throw asepriteError; asepriteErrors(asepriteError.Message); }
            return result;
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static SKBitmap Read(string path, string expected)
    {
        var bytes = File.ReadAllBytes(path);
        if (Hash(bytes) != expected) throw new IOException($"PNG changed during import: {path}");
        using var stream = new MemoryStream(bytes);
        using var codec = SKCodec.Create(stream);
        if (codec == null || codec.EncodedFormat != SKEncodedImageFormat.Png)
            throw new InvalidDataException($"Invalid PNG: {path}");
        var bitmap = new SKBitmap(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success) return bitmap;
        bitmap.Dispose();
        throw new InvalidDataException($"Incomplete PNG: {path}");
    }
}
