using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace Engine.Core;

internal sealed record AsepriteTag(string Name, int From, int To, int Direction, int Repeat);
internal sealed record AsepriteFrame(int Milliseconds, byte[] Rgba);
internal sealed record AsepriteFile(int Width, int Height, AsepriteFrame[] Frames, AsepriteTag[] Tags);

// Editor/build-tool only: games receive the generated PNG and clips, never the .aseprite file or this code.
// SDL_image 3.4 (Brutal.Sdl.Image) has no Aseprite decoder and its animation API has no tags, so the documented
// format (github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md) is read here with the base library.
internal static class AsepriteImporter
{
    internal static bool IsSource(string path) => Path.GetExtension(path).ToLowerInvariant() is ".aseprite" or ".ase";

    // Foo.aseprite -> Foo.png (frames in a grid, one slice each), Foo.clip (every frame) and Foo.<Tag>.clip per tag.
    internal static void Import(string root)
    {
        var sources = ProjectAssets.Enumerate(root, root, IsSource).Where(IsSource).Order(StringComparer.Ordinal).ToArray();
        var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // One broken source must not stop the others; every error is reported together afterwards.
        var errors = new List<string>();
        foreach (var relative in sources)
            try { ImportOne(root, relative, outputs); }
            catch (InvalidDataException ex) { errors.Add(ex.Message); }
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
    }

    private static void ImportOne(string root, string relative, HashSet<string> outputs)
    {
        {
            var source = Path.Combine(root, relative);
            var stem = Path.Combine(Path.GetDirectoryName(source)!, Path.GetFileNameWithoutExtension(source));
            if (!outputs.Add(stem)) throw new InvalidDataException($"{relative}: another Aseprite file already generates {Path.GetFileName(stem)}.png.");
            var bytes = File.ReadAllBytes(source);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            string png = stem + ".png", metaPath = png + ".meta";
            var meta = File.Exists(metaPath) ? ProjectYaml.Read<AssetMeta>(metaPath) : null;
            if (File.Exists(png) && meta?.ImportHash == null)
                throw new InvalidDataException($"{relative}: {Path.GetFileName(png)} exists and was not generated from it. Rename one of them.");
            if (meta?.ImportHash == hash && File.Exists(png)) return;
            AsepriteFile file;
            try { file = Read(bytes); }
            catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or ArgumentException or IndexOutOfRangeException)
            { throw new InvalidDataException($"{relative}: {ex.Message}", ex); }
            var id = meta?.Id ?? Guid.NewGuid();
            // An empty hash marks the sheet as generated, so an interrupted first import can be repeated.
            if (meta == null) ProjectYaml.Write(metaPath, meta = new AssetMeta(id, ImportHash: ""));
            int columns = (int)Math.Ceiling(Math.Sqrt(file.Frames.Length)), rows = (file.Frames.Length + columns - 1) / columns;
            var slices = Enumerable.Range(0, file.Frames.Length)
                .Select(i => new SheetSlice(new SpriteRegion(i % columns * file.Width, i / columns * file.Height, file.Width, file.Height))).ToArray();
            var clips = new List<(string Path, SpriteClip Clip)>
                { (stem + ProjectAssets.Clip, Clip(file, id, slices, Enumerable.Range(0, file.Frames.Length), true)) };
            foreach (var tag in file.Tags)
            {
                if (!ProjectAssets.ValidName(tag.Name) || clips.Any(c => c.Path.Equals($"{stem}.{tag.Name}{ProjectAssets.Clip}", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException($"{relative}: tag \"{tag.Name}\" is not a unique file name.");
                var (order, loop) = Sequence(tag);
                clips.Add(($"{stem}.{tag.Name}{ProjectAssets.Clip}", Clip(file, id, slices, order, loop)));
            }
            foreach (var (path, _) in clips.Where(c => File.Exists(c.Path)))
                if (SpriteClip.OpenForEditing(path).Frames.Any(f => f.Sprite != id))
                    throw new InvalidDataException($"{relative}: {Path.GetFileName(path)} exists and was not generated from it. Rename one of them.");
            using (var sheet = new SKBitmap(new SKImageInfo(columns * file.Width, rows * file.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
            {
                sheet.Erase(SKColors.Transparent);
                for (int i = 0; i < file.Frames.Length; i++)
                for (int y = 0; y < file.Height; y++)
                    System.Runtime.InteropServices.Marshal.Copy(file.Frames[i].Rgba, y * file.Width * 4,
                        sheet.GetPixels() + ((slices[i].Source.Y + y) * sheet.RowBytes + slices[i].Source.X * 4), file.Width * 4);
                using var encoded = sheet.Encode(SKEncodedImageFormat.Png, 100);
                WriteChanged(png, encoded.ToArray());
            }
            foreach (var (path, clip) in clips) WriteChanged(path, ProjectYaml.Encode(clip));
            ProjectYaml.Write(metaPath, meta with { Slices = slices, ImportHash = hash });
        }
    }

    private static void WriteChanged(string path, byte[] bytes)
    {
        if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) ProjectYaml.WriteBytes(path, bytes);
    }

    private static SpriteClip Clip(AsepriteFile file, Guid sheet, SheetSlice[] slices, IEnumerable<int> order, bool loop) =>
        new(order.Select(i => new SpriteFrame(sheet, file.Frames[i].Milliseconds / 1000f, slices[i].Source)).ToArray(), loop);

    // Repeat 0 loops forever. Otherwise each pass plays once; ping-pong passes alternate direction and share end frames.
    internal static (int[] Frames, bool Loop) Sequence(AsepriteTag tag)
    {
        int[] Run(int a, int b) => a <= b ? Enumerable.Range(a, b - a + 1).ToArray() : Enumerable.Range(b, a - b + 1).Reverse().ToArray();
        bool reverse = tag.Direction is 1 or 3, pingPong = tag.Direction is 2 or 3;
        int start = reverse ? tag.To : tag.From, end = reverse ? tag.From : tag.To;
        if (!pingPong)
            return (Enumerable.Repeat(Run(start, end), Math.Max(1, tag.Repeat)).SelectMany(x => x).ToArray(), tag.Repeat == 0);
        if (tag.Repeat == 0) return (Run(start, end).Concat(Run(end, start).Skip(1).SkipLast(1)).ToArray(), true);
        var frames = new List<int>(Run(start, end));
        for (int pass = 1; pass < tag.Repeat; pass++)
            frames.AddRange((pass % 2 == 1 ? Run(end, start) : Run(start, end)).Skip(1).DefaultIfEmpty(start));
        return (frames.ToArray(), false);
    }

    private sealed class Layer
    {
        public int Flags, Type, Level, Blend, Opacity;
        public string Name = "";
        public Layer? Parent;
        public bool Visible => (Flags & 1) != 0 && (Flags & 64) == 0 && (Parent?.Visible ?? true);
        public int EffectiveOpacity => Parent == null ? Opacity : Opacity * Parent.EffectiveOpacity / 255;
    }
    private sealed record Cel(int Layer, int X, int Y, int Opacity, int Z, int Width, int Height, byte[] Pixels);

    internal static AsepriteFile Read(byte[] data)
    {
        var r = new Reader(data);
        r.Skip(4);
        if (r.Word() != 0xA5E0) throw new InvalidDataException("Not an Aseprite file.");
        int frameCount = r.Word(), width = r.Word(), height = r.Word(), depth = r.Word();
        uint flags = r.Dword();
        r.Skip(2 + 8);
        int transparent = r.Byte();
        r.Skip(3 + 2 + 2 + 8 + 84);
        if (depth is not (32 or 16 or 8)) throw new InvalidDataException($"Unsupported colour depth {depth}.");
        if (frameCount == 0 || width == 0 || height == 0) throw new InvalidDataException("Empty sprite.");
        int bpp = depth / 8;
        var layers = new List<Layer>();
        var tags = Array.Empty<AsepriteTag>();
        var palette = new byte[256 * 4];
        bool newPalette = false;
        var cels = new List<Cel>[frameCount];
        var durations = new int[frameCount];
        for (int f = 0; f < frameCount; f++)
        {
            int frameStart = r.Position, frameSize = (int)r.Dword();
            if (r.Word() != 0xF1FA) throw new InvalidDataException($"Bad frame {f + 1} header.");
            int oldChunks = r.Word();
            durations[f] = r.Word();
            if (durations[f] == 0) throw new InvalidDataException($"Frame {f + 1} has no duration.");
            r.Skip(2);
            uint newChunks = r.Dword();
            long chunks = oldChunks == 0xFFFF && newChunks != 0 ? newChunks : oldChunks;
            cels[f] = [];
            for (long c = 0; c < chunks; c++)
            {
                int chunkStart = r.Position, size = (int)r.Dword(), type = r.Word();
                if (size < 6) throw new InvalidDataException("Bad chunk size.");
                switch (type)
                {
                    case 0x2004:
                    {
                        var layer = new Layer { Flags = r.Word(), Type = r.Word(), Level = r.Word() };
                        r.Skip(4);
                        layer.Blend = r.Word();
                        layer.Opacity = r.Byte();
                        r.Skip(3);
                        layer.Name = r.String();
                        bool group = layer.Type == 1;
                        if ((flags & 1) == 0 || group && (flags & 2) == 0) { layer.Opacity = 255; if (group) layer.Blend = 0; }
                        for (int i = layers.Count - 1; i >= 0; i--)
                            if (layers[i].Level == layer.Level - 1 && layers[i].Type == 1) { layer.Parent = layers[i]; break; }
                            else if (layers[i].Level < layer.Level) break;
                        layers.Add(layer);
                        break;
                    }
                    case 0x2005:
                    {
                        int layerIndex = r.Word(), x = r.Short(), y = r.Short(), opacity = r.Byte(), celType = r.Word(), z = r.Short();
                        r.Skip(5);
                        if (celType == 1)
                        {
                            int link = r.Word();
                            if (link >= f) throw new InvalidDataException($"Frame {f + 1} links to a later frame.");
                            if (cels[link].FirstOrDefault(e => e.Layer == layerIndex) is { } linked) cels[f].Add(linked);
                        }
                        else if (celType is 0 or 2)
                        {
                            int w = r.Word(), h = r.Word();
                            var pixels = new byte[w * h * bpp];
                            if (celType == 0) r.Bytes(pixels.Length).CopyTo(pixels);
                            else
                            {
                                using var z0 = new ZLibStream(new MemoryStream(data, r.Position, chunkStart + size - r.Position), CompressionMode.Decompress);
                                z0.ReadExactly(pixels);
                            }
                            cels[f].Add(new(layerIndex, x, y, opacity, z, w, h, pixels));
                        }
                        else throw new InvalidDataException("Tilemap cels are not supported.");
                        break;
                    }
                    case 0x2018:
                    {
                        int count = r.Word();
                        r.Skip(8);
                        tags = new AsepriteTag[count];
                        for (int i = 0; i < count; i++)
                        {
                            int from = r.Word(), to = r.Word(), direction = r.Byte(), repeat = r.Word();
                            r.Skip(6 + 3 + 1);
                            tags[i] = new(r.String(), from, to, direction, repeat);
                            if (from > to || to >= frameCount || direction > 3) throw new InvalidDataException($"Invalid tag \"{tags[i].Name}\".");
                        }
                        break;
                    }
                    case 0x2019:
                    {
                        newPalette = true;
                        r.Skip(4);
                        int first = (int)r.Dword(), last = (int)r.Dword();
                        r.Skip(8);
                        for (int i = first; i <= last; i++)
                        {
                            int entryFlags = r.Word();
                            var rgba = r.Bytes(4);
                            if (i < 256) rgba.CopyTo(palette.AsSpan(i * 4));
                            if ((entryFlags & 1) != 0) r.String();
                        }
                        break;
                    }
                    case 0x0004 when !newPalette:
                    {
                        int packets = r.Word(), index = 0;
                        for (int p = 0; p < packets; p++)
                        {
                            index += r.Byte();
                            int count = r.Byte();
                            if (count == 0) count = 256;
                            for (int i = 0; i < count; i++, index++)
                            {
                                var rgb = r.Bytes(3);
                                if (index < 256) { rgb.CopyTo(palette.AsSpan(index * 4)); palette[index * 4 + 3] = 255; }
                            }
                        }
                        break;
                    }
                }
                r.Position = chunkStart + size;
            }
            r.Position = frameStart + frameSize;
        }
        foreach (var layer in layers.Where(l => l.Visible))
        {
            if (layer.Type == 2) throw new InvalidDataException($"Layer \"{layer.Name}\": tilemap layers are not supported.");
            if (layer.Blend != 0) throw new InvalidDataException($"Layer \"{layer.Name}\": only the Normal blend mode is supported.");
        }
        var frames = new AsepriteFrame[frameCount];
        for (int f = 0; f < frameCount; f++)
        {
            var canvas = new byte[width * height * 4];
            // Cels draw in layer order shifted by their z-index; ties draw the lower z-index first.
            foreach (var cel in cels[f].OrderBy(c => c.Layer + c.Z).ThenBy(c => c.Z))
            {
                if (cel.Layer >= layers.Count) throw new InvalidDataException("Cel refers to a missing layer.");
                var layer = layers[cel.Layer];
                if (!layer.Visible || layer.Type != 0) continue;
                // shortcut: a group's opacity multiplies into each child instead of compositing the group first; differs only where children overlap.
                int opacity = cel.Opacity * layer.EffectiveOpacity / 255;
                bool background = (layer.Flags & 8) != 0;
                for (int y = Math.Max(0, -cel.Y); y < cel.Height && cel.Y + y < height; y++)
                for (int x = Math.Max(0, -cel.X); x < cel.Width && cel.X + x < width; x++)
                {
                    int s = (y * cel.Width + x) * bpp;
                    byte sr, sg, sb, sa;
                    if (bpp == 4) (sr, sg, sb, sa) = (cel.Pixels[s], cel.Pixels[s + 1], cel.Pixels[s + 2], cel.Pixels[s + 3]);
                    else if (bpp == 2) (sr, sg, sb, sa) = (cel.Pixels[s], cel.Pixels[s], cel.Pixels[s], cel.Pixels[s + 1]);
                    else
                    {
                        int i = cel.Pixels[s];
                        if (i == transparent && !background) continue;
                        (sr, sg, sb, sa) = (palette[i * 4], palette[i * 4 + 1], palette[i * 4 + 2], palette[i * 4 + 3]);
                    }
                    Blend(canvas.AsSpan(((cel.Y + y) * width + cel.X + x) * 4, 4), sr, sg, sb, sa * opacity / 255);
                }
            }
            frames[f] = new(durations[f], canvas);
        }
        return new(width, height, frames, tags);
    }

    // Aseprite's normal blend on straight (unpremultiplied) RGBA.
    private static void Blend(Span<byte> d, int r, int g, int b, int a)
    {
        if (a == 0) return;
        int da = d[3];
        if (da == 0) { d[0] = (byte)r; d[1] = (byte)g; d[2] = (byte)b; d[3] = (byte)a; return; }
        int ra = a + da - (a * da + 127) / 255;
        d[0] = (byte)(d[0] + (r - d[0]) * a / ra);
        d[1] = (byte)(d[1] + (g - d[1]) * a / ra);
        d[2] = (byte)(d[2] + (b - d[2]) * a / ra);
        d[3] = (byte)ra;
    }

    private sealed class Reader(byte[] data)
    {
        public int Position;
        private ReadOnlySpan<byte> Take(int n)
        {
            if (n < 0 || Position + n > data.Length) throw new EndOfStreamException("Truncated Aseprite file.");
            var span = data.AsSpan(Position, n);
            Position += n;
            return span;
        }
        public void Skip(int n) => Take(n);
        public int Byte() => Take(1)[0];
        public int Word() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
        public int Short() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));
        public uint Dword() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
        public byte[] Bytes(int n) => Take(n).ToArray();
        public string String() => Encoding.UTF8.GetString(Take(Word()));
    }
}
