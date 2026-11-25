using System.Drawing;
using System.Numerics;
using Engine.Core;

// Roadmap 16.9: frame-animated tiles. One clock per tileset (no per-cell state), frames picked from game time, no allocation
// per frame, one texture for the whole layer. Runs on the SeedLab route showcase's Decor layer (river ripples and two torches);
// SeedLab.Autotile replays it in all three paths.
internal static class TileAnimationChecks
{
    [Check(3600, "tile-animation", StopAfter = "world")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    public static void Run(string repository, string scratch)
    {
        var project = Path.Combine(repository, "samples", "SeedLab");
        var assets = ProjectAssets.ReadManifest(project);
        var scene = SceneDocument.Load(Path.Combine(project, "Scenes", "ShowcaseRoute.scene"));
        var waterObject = scene.Objects.Single(o => o.Name == "Decor");
        var water = waterObject.GetComponent<Tilemap2DComponent>()!;
        var palette = TilePalette.Load(project, assets, water.Tileset!.Value);
        Check(palette.Frames[61].Tiles.SequenceEqual([61, 62, 63]) && palette.Frames[61].Fps == 4 &&
              palette.Frames[57].Tiles.SequenceEqual([57, 58, 59, 60]) && palette.Frames[57].Fps == 8 && palette.Frames.Count == 2,
            "Hedges.tileset resolves ripples (3 frames, 4 FPS) and torch (4 frames, 8 FPS) from the following sheet cells");

        // Independent frame rule: floor(game seconds x FPS) mod frames, on the float tick step summed in double.
        double seconds = 0;
        for (int tick = 0; tick < 600; tick++, seconds += 1f / 60)
        {
            int w = 61 + (int)(Math.Floor(seconds * 4) % 3), t = 57 + (int)(Math.Floor(seconds * 8) % 4);
            Check(palette.Animated(61, seconds) == w && palette.Animated(57, seconds) == t && palette.Animated(51, seconds) == 51, $"tick {tick}", quiet: true);
        }
        Check(true, "600 ticks pick the independent frame for both tiles; unanimated tiles keep their art");

        // Drawing: every ripple cell shows the same frame from one texture; the clock and lookups allocate nothing.
        var resolved = water.Resolved(palette);
        var canvas = new Capture();
        foreach (var (time, frame) in new[] { (0.0, 61), (0.3, 62), (0.6, 63), (0.75, 61) })
        {
            canvas.Sources.Clear(); canvas.Paths.Clear();
            resolved.Draw(canvas, palette, Matrix3x2.Identity, new(0, 0, 800, 450), Matrix3x2.Identity, time);
            var drawn = canvas.Sources.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
            int Tuft(Point p) => 51 + Tilemap2DComponent.Pick(p, 2026, 4);
            var tufts = water.OccupiedCells().Where(c => c.Tile == 51).Select(c => palette.Tiles[Tuft(c.Cell)].Source).ToHashSet();
            Check(canvas.Paths.Distinct().Count() == 1 && drawn[palette.Tiles[frame].Source] == 14 &&
                  drawn[palette.Tiles[57 + (int)(Math.Floor(time * 8) % 4)].Source] == 2 && drawn.Keys.Except(tufts).Count() == 2,
                $"at {time:0.00} s all 14 ripple cells draw #{frame} and both torches share one frame, from one sheet", quiet: time != 0.3);
        }
        Spin(palette, 100);
        long allocated = Spin(palette, 20000);
        Check(allocated == 0, $"20,000 clock updates and lookups allocate {allocated} bytes");

        // Play: TryGetTile reports the frame at the current game time; a repainted cell joins the shared clock in step.
        var store = new PlayerStorage(Path.Combine(scratch, "tile-animation-profile"));
        using (var session = new GameSession(scene, null, null, store))
        {
            var game = session.Current;
            for (int i = 0; i < 20; i++) game.FixedUpdate(Vector2.Zero, 1f / 60);
            Check(game.SetTile(waterObject.Id, new(8, 0), 0) && game.SetTile(waterObject.Id, new(8, 0), 61), "SetTile clears and repaints a ripple cell");
            int want = palette.Animated(61, game.Time.GameSeconds);
            Check(want == 62 && water.OccupiedCells().Where(c => c.Tile == 61).All(c => game.TryGetTile(waterObject.Id, c.Cell, out var info) && info.Display == want),
                "after 20 ticks every ripple cell, including the repainted one, reports frame #62");
        }

        // Streamed pages keep brush art; animation applies when drawing, so pages need no new data.
        var folder = Path.Combine(scratch, "tile-animation-world", TileWorld.Folder);
        var refs = TileWorld.Bake(water, palette, folder);
        using (var stream = new TileWorld(water with { Chunks = [], ChunkFiles = refs, StreamRadius = 1 }, palette, Path.GetDirectoryName(folder)))
        {
            stream.Update(new(0, 0), palette);
            Check(water.OccupiedCells().All(c => stream.Map.DisplayTile(palette, c.Cell, c.Tile) == water.DisplayTile(palette, c.Cell, c.Tile)) &&
                  water.OccupiedCells().Where(c => c.Tile != 51).All(c => palette.Animated(stream.Map.DisplayTile(palette, c.Cell, c.Tile), 0.3) == palette.Animated(c.Tile, 0.3)),
                "streamed pages animate the same way");
        }

        // Validation and file layout.
        var sheet = Tileset.Load(Path.Combine(project, "Assets", "Showcase", "Hedges.tileset"));
        TileDefinition[] With(Func<TileDefinition, TileDefinition> change) => sheet.Tiles.Select(t => t.Id == 61 ? change(t) : t).ToArray();
        Expect(() => (sheet with { Tiles = With(t => t with { Frames = 1 }) }).Resolve(project, assets, "one"), "needs 2 to 64 frames");
        Expect(() => (sheet with { Tiles = With(t => t with { Fps = 0 }) }).Resolve(project, assets, "still"), "needs 2 to 64 frames");
        Expect(() => (sheet with { Tiles = With(t => t with { Fps = null }) }).Resolve(project, assets, "no fps"), "needs 2 to 64 frames");
        Expect(() => (sheet with { Tiles = With(t => t with { RandomTiles = 2 }) }).Resolve(project, assets, "random"), "no terrain or random brush");
        Expect(() => (sheet with { Tiles = sheet.Tiles.Select(t => t.Id == 63 ? t with { Frames = 3, Fps = 2 } : t).ToArray() }).Resolve(project, assets, "short"), "2 frame sheet cells");
        int tilesets = 0;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(repository, "samples"), "*.tileset", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var written = System.Text.Encoding.UTF8.GetString(ProjectYaml.Encode(Tileset.Load(file)));
            Check(written.Contains("frames:") == text.Contains("frames:") && written.Contains("fps:") == text.Contains("fps:"), $"{Path.GetFileName(file)} keys", quiet: true);
            tilesets++;
        }
        Check(tilesets >= 7, $"{tilesets} sample tilesets gain no frames or fps keys when saved");
    }

    private static long Spin(TilePalette palette, int count)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        int sum = 0;
        for (int i = 0; i < count; i++)
        {
            palette.Clock(i / 60.0);
            sum += palette.Animated(61) + palette.Animated(57) + palette.Animated(1);
        }
        return sum > 0 ? GC.GetAllocatedBytesForCurrentThread() - before : -1;
    }

    private sealed class Capture : ICanvas2D
    {
        public readonly List<SpriteRegion> Sources = [];
        public readonly List<string?> Paths = [];
        public bool DrawAffine(string? path, RectangleF r, Matrix3x2 matrix, Color tint, SpriteRegion? source = null, string layout = TileGrid.Orthogonal)
        {
            Paths.Add(path); Sources.Add(source!); return true;
        }
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF rectangle, Color color) { }
        public bool DrawSprite(string path, RectangleF destination) => true;
    }

    private static void Expect(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException ex) when (ex.Message.Contains(message)) { Console.WriteLine($"PASS tile animation: rejects \"{message}\""); return; }
        throw new Exception($"Tile animation: expected an error containing '{message}'");
    }

    private static void Check(bool ok, string detail, bool quiet = false)
    {
        if (!ok) throw new Exception("Tile animation: " + detail);
        if (!quiet) Console.WriteLine("PASS tile animation: " + detail);
    }
}
