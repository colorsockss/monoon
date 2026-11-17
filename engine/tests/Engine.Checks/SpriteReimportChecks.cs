using Engine.Core;
using SkiaSharp;

internal static class SpriteReimportChecks
{
    internal static void Run(string root)
    {
        var work = Directory.CreateTempSubdirectory("monoon-sheet-").FullName;
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(work, "Assets")).FullName;
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "Door*"))
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            string Asset(string name) => Path.Combine(folder, name);
            var sheet = Asset("DoorSheet.png");
            var clip = File.ReadAllBytes(Asset("Door.clip"));
            var meta = File.ReadAllBytes(sheet + ".meta");
            var before = File.ReadAllBytes(sheet);
            var source = Asset("Door.png");
            var next = File.ReadAllBytes(Asset("DoorOpen1.png"));
            var slices = ProjectAssets.ReadSheetSlices(sheet);
            var identity = SpriteSheetImporter.Scan(work).IdFor("Assets/DoorSheet.png");

            void Reject()
            {
                try { SpriteSheetImporter.Scan(work); }
                catch (InvalidDataException)
                {
                    if (File.ReadAllBytes(sheet).SequenceEqual(before) && File.ReadAllBytes(sheet + ".meta").SequenceEqual(meta)) return;
                    throw new Exception("Rejected source partially updated the sheet");
                }
                throw new Exception("Accepted an invalid sheet source");
            }
            File.WriteAllBytes(source, next[..24]);
            Reject();
            File.WriteAllBytes(source, next);
            File.Copy(Path.Combine(root, "Assets", "Door.png"), Asset("DoorOpen1.png"), true);
            Reject(); // The second cell is too short. The first cell must also remain unchanged.
            File.WriteAllBytes(Asset("DoorOpen1.png"), next);
            var modified = SpriteSheetImporter.Scan(work);
            using var actual = SKBitmap.Decode(sheet);
            using var expected = SKBitmap.Decode(next);
            using var original = SKBitmap.Decode(before);
            for (int y = 0; y < actual.Height; y++)
            for (int x = 0; x < actual.Width; x++)
            {
                var pixel = x < expected.Width && y < expected.Height ? expected.GetPixel(x,y) : original.GetPixel(x,y);
                if (pixel != actual.GetPixel(x,y)) throw new Exception("Wrong pixels after reimport");
            }
            var updated = File.ReadAllBytes(sheet);
            SpriteSheetImporter.Scan(work);
            if (modified.IdFor("Assets/DoorSheet.png") != identity || !ProjectAssets.ReadSheetSlices(sheet).SequenceEqual(slices) ||
                !File.ReadAllBytes(Asset("Door.clip")).SequenceEqual(clip) || !File.ReadAllBytes(sheet).SequenceEqual(updated))
                throw new Exception("Reimport changed the frame references, timing or already imported sheet");
            Console.WriteLine("PASS external sprite overwrite, stable sheet references, retry after partial writes and invalid crops");
        }
        finally { Directory.Delete(work, true); }
    }
}
