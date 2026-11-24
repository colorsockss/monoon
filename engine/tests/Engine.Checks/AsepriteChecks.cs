using Engine.Core;
using SkiaSharp;

// Fixtures come from engine/tests/SpriteLab.Aseprite/aseprite_fixture.py (Aseprite itself is not installed).
internal static class AsepriteChecks
{
    [Check(6100)] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    internal static void Run(string repository, string scratch)
    {
        var fixtures = Path.Combine(repository, "engine", "tests", "fixtures", "Aseprite");
        foreach (var name in new[] { "Rgba", "Indexed" })
        {
            var file = AsepriteImporter.Read(File.ReadAllBytes(Path.Combine(fixtures, name + ".aseprite")));
            byte[] Pixel(int f, int x, int y) => file.Frames[f].Rgba.AsSpan((y * file.Width + x) * 4, 4).ToArray();
            bool Is(byte[] p, int r, int g, int b, int a) => Math.Abs(p[0] - r) <= 1 && Math.Abs(p[1] - g) <= 1 && Math.Abs(p[2] - b) <= 1 && Math.Abs(p[3] - a) <= 1;
            Check(file.Width == 8 && file.Height == 6 && file.Frames.Select(f => f.Milliseconds).SequenceEqual([100, 150, 200, 250]), $"{name}: size and frame durations");
            Check(Is(Pixel(0, 0, 1), 255, 0, 0, 255) && Pixel(0, 7, 0)[3] == 0 && Pixel(0, 3, 3)[3] == 0, $"{name}: visible layer drawn, hidden layer skipped");
            Check(Is(Pixel(0, 6, 4), 0, 0, 255, 128), $"{name}: layer opacity");
            Check(Is(Pixel(1, 1, 1), 127, 0, 128, 255) && Is(Pixel(1, 2, 2), 127, 0, 128, 255) && Pixel(1, 0, 1)[3] == 0, $"{name}: normal blend over lower layer");
            Check(Is(Pixel(2, 4, 0), 255, 255, 255, 64) && Is(Pixel(2, 3, 2), 255, 0, 0, 255), $"{name}: cel opacity times layer opacity");
            Check(Is(Pixel(3, 0, 1), 255, 0, 0, 255) && Pixel(3, 6, 4)[3] == 0, $"{name}: linked cel");
            Check(string.Join(",", file.Tags.Select(t => $"{t.Name}:{t.From}-{t.To}:{t.Direction}:{t.Repeat}")) ==
                "Walk:0-2:0:0,Back:0-2:1:0,Bounce:0-3:2:0,Twice:1-2:0:2,Swing:0-2:2:3,Mirror:0-2:3:0", $"{name}: tags");
        }
        string Order(string tag) { var (frames, loop) = AsepriteImporter.Sequence(AsepriteImporter.Read(File.ReadAllBytes(Path.Combine(fixtures, "Rgba.aseprite"))).Tags.Single(t => t.Name == tag)); return string.Join("", frames) + (loop ? "~" : "."); }
        Check(Order("Walk") == "012~" && Order("Back") == "210~" && Order("Bounce") == "012321~" && Order("Twice") == "1212." &&
              Order("Swing") == "0121012." && Order("Mirror") == "2101~", "tag directions and repeat counts");
        try { AsepriteImporter.Read(File.ReadAllBytes(Path.Combine(fixtures, "Multiply.aseprite"))); throw new Exception("Expected unsupported blend mode."); }
        catch (InvalidDataException ex) when (ex.Message.Contains("Normal blend")) { }

        var root = Path.Combine(scratch, "AsepriteImport");
        var assetsDir = Path.Combine(root, "Assets");
        Directory.CreateDirectory(assetsDir);
        var source = Path.Combine(assetsDir, "Hero.aseprite");
        File.Copy(Path.Combine(fixtures, "Rgba.aseprite"), source);
        var assets = SpriteSheetImporter.Scan(root);
        var sheet = assets.IdFor("Assets/Hero.png")!.Value;
        Check(SpriteRegion.ReadPngSize(Path.Combine(assetsDir, "Hero.png")) == new System.Drawing.Size(16, 12) &&
              ProjectAssets.ReadSheetSlices(Path.Combine(assetsDir, "Hero.png")).Select(s => s.Source).SequenceEqual(
                  [new SpriteRegion(0, 0, 8, 6), new(8, 0, 8, 6), new(0, 6, 8, 6), new(8, 6, 8, 6)]), "sheet grid and slices");
        SpriteClip Clip(string name) => SpriteClip.Load(Path.Combine(assetsDir, name + ".clip"));
        var all = Clip("Hero");
        Check(all.Loop && all.Frames.All(f => f.Sprite == sheet) && all.Frames.Select(f => f.Seconds).SequenceEqual([.1f, .15f, .2f, .25f]) &&
              all.Frames[3].Source == new SpriteRegion(8, 6, 8, 6), "whole-file clip");
        var swing = Clip("Hero.Swing");
        Check(!swing.Loop && swing.Frames.Select(f => f.Source!.X / 8 + f.Source.Y / 6 * 2).SequenceEqual([0, 1, 2, 1, 0, 1, 2]) &&
              swing.Frames[1].Seconds == .15f && Clip("Hero.Bounce").Frames.Length == 6 && assets.IdFor("Assets/Hero.Mirror.clip") != null, "tag clips");
        using (var bitmap = SKBitmap.Decode(Path.Combine(assetsDir, "Hero.png")))
            Check(bitmap.GetPixel(8 + 1, 1) == new SKColor(127, 0, 128, 255) && bitmap.GetPixel(8 + 7, 6 + 4).Alpha == 0, "sheet pixels");
        var stamps = Directory.GetFiles(assetsDir).ToDictionary(f => f, File.GetLastWriteTimeUtc);
        SpriteSheetImporter.Scan(root);
        Check(Directory.GetFiles(assetsDir).All(f => stamps.TryGetValue(f, out var t) && t == File.GetLastWriteTimeUtc(f)), "unchanged source does not rewrite outputs");
        var edited = File.ReadAllBytes(source);
        edited[128 + 8] = 0xF4; edited[128 + 9] = 0x01; // first frame: 500 ms
        File.WriteAllBytes(source, edited);
        var again = SpriteSheetImporter.Scan(root);
        Check(again.IdFor("Assets/Hero.png") == sheet && Clip("Hero").Frames[0].Seconds == .5f && Clip("Hero.Walk").Frames[0].Seconds == .5f &&
              Clip("Hero.Swing").Frames[1].Seconds == .15f, "edited source reimports and keeps the sheet ID");
        File.Copy(Path.Combine(fixtures, "Indexed.aseprite"), source, true);
        SpriteSheetImporter.Scan(root);
        Check(Clip("Hero").Frames[0].Seconds == .1f, "indexed source replaces RGBA source");
        File.Copy(Path.Combine(fixtures, "Rgba.aseprite"), Path.Combine(assetsDir, "Alpha.aseprite"));
        File.Copy(Path.Combine(assetsDir, "Hero.png"), Path.Combine(assetsDir, "Alpha.png"));
        File.WriteAllText(Path.Combine(assetsDir, "Alpha.png.meta"), $"id: {Guid.NewGuid()}\n");
        try { SpriteSheetImporter.Scan(root); throw new Exception("Expected a name collision."); }
        catch (InvalidDataException ex) when (ex.Message.Contains("not generated")) { }
        // Alpha sorts before Hero: its error must not stop Hero from reimporting.
        File.WriteAllBytes(source, edited);
        string? reported = null;
        SpriteSheetImporter.Scan(root, message => reported = message);
        Check(reported?.Contains("Alpha.aseprite") == true && Clip("Hero").Frames[0].Seconds == .5f, "a broken source is reported while the others still import");
        Directory.Delete(root, true);

        // SpriteLab: the committed sheet and clip are what the importer produces, and frames equal the original PNGs.
        var sample = Path.Combine(repository, "samples", "SpriteLab");
        var copy = Path.Combine(scratch, "SpriteLabAseprite");
        foreach (var file in Directory.GetFiles(sample, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(copy, Path.GetRelativePath(sample, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        File.WriteAllText(Path.Combine(copy, "Assets", "ArrowCycle.png.meta"), File.ReadAllText(Path.Combine(copy, "Assets", "ArrowCycle.png.meta")).Replace("importHash:", "importHash: stale #"));
        SpriteSheetImporter.Scan(copy);
        foreach (var name in new[] { "ArrowCycle.png", "ArrowCycle.png.meta", "ArrowCycle.clip" })
            Check(File.ReadAllBytes(Path.Combine(sample, "Assets", name)).SequenceEqual(File.ReadAllBytes(Path.Combine(copy, "Assets", name))), $"SpriteLab {name} is current");
        var art = AsepriteImporter.Read(File.ReadAllBytes(Path.Combine(sample, "Assets", "ArrowCycle.aseprite")));
        foreach (var (frame, png) in new[] { (0, "Arrow.png"), (1, "Dot.png") })
        {
            using var original = new SKBitmap(new SKImageInfo(16, 12, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            using (var codec = SKCodec.Create(Path.Combine(sample, "Assets", png))) codec.GetPixels(original.Info, original.GetPixels());
            Check(art.Frames[frame].Rgba.AsSpan().SequenceEqual(original.GetPixelSpan()), $"SpriteLab frame {frame + 1} equals {png}");
        }
        Directory.Delete(copy, true);
        Console.WriteLine("PASS Aseprite RGBA/indexed layers, opacity, linked cels, tags, sheet/clip import, reimport, SpriteLab sample");
    }

    private static void Check(bool ok, string message) { if (!ok) throw new Exception("Aseprite: " + message); }
}
