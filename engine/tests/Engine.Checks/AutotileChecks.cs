using System.Drawing;
using Engine.Core;
using Engine.Editor;

// Roadmap 16.9: 8-neighbour (47 blob) terrain, seeded random tiles, editor strokes across chunk seams, and the runtime
// copy that resolves art once at load. SeedLab.Autotile replays the same scene in Scene Play, Release and Native AOT.
internal static class AutotileChecks
{
    [Check(3500, "autotile")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private static readonly Point[] Around = [new(0,-1),new(1,0),new(0,1),new(-1,0),new(1,-1),new(1,1),new(-1,1),new(-1,-1)];

    public static void Run(string repository, string scratch)
    {
        // Independent table: sort the masks whose corners count only beside two connected edges.
        static int Reduce(int m)
        {
            int r = m & 15;
            for (int c = 0; c < 4; c++)
                if ((m >> c & 1) != 0 && (m >> (c + 1) % 4 & 1) != 0) r |= m & 16 << c;
            return r;
        }
        var order = Enumerable.Range(0,256).Select(Reduce).Distinct().Order().ToList();
        Check(order.Count == 47 && order.Take(16).SequenceEqual(Enumerable.Range(0,16)) &&
              Enumerable.Range(0,256).All(m => Tileset.Blob[m] == order.IndexOf(Reduce(m))), "47 blob masks in ascending order; the first 16 equal the 4-edge sheet");

        var project = Path.Combine(repository,"samples","SeedLab");
        var assets = ProjectAssets.ReadManifest(project);
        // The SeedLab route showcase: its Hedges layer paints every hedge cell with the blob brush (1), its Decor layer flower tufts
        // with a random brush (51) and two animated torches.
        var scene = SceneDocument.Load(Path.Combine(project,"Scenes","ShowcaseRoute.scene"));
        var hedge = scene.Objects.Single(o => o.Name == "Hedges").GetComponent<Tilemap2DComponent>()!;
        var decor = scene.Objects.Single(o => o.Name == "Decor").GetComponent<Tilemap2DComponent>()!;
        var palette = TilePalette.Load(project,assets,hedge.Tileset!.Value);
        Check(palette.Variants[1].Length == 256 && palette.Randoms[1].SequenceEqual([47,48,49,50]) &&
              palette.Randoms[51].SequenceEqual([51,52,53,54]) && palette.Seeds[51] == 2026, "Hedges.tileset resolves a blob brush and two random brushes");

        int Expected(Tilemap2DComponent map, Point p, int seed = 7)
        {
            int brush = map.Get(p);
            if (brush == 51) return 51 + Tilemap2DComponent.Pick(p,2026,4);
            int mask = 0;
            for (int i = 0; i < 8; i++) if (map.Get(new(p.X + Around[i].X, p.Y + Around[i].Y)) == 1) mask |= 1 << i;
            int index = order.IndexOf(Reduce(mask));
            return index != 46 ? 1 + index : new[] { 47, 48, 49, 50 }[Tilemap2DComponent.Pick(p,seed,4)];
        }
        foreach (var (cell,_) in hedge.OccupiedCells())
        {
            int got = hedge.DisplayTile(palette,cell,1);
            Check(got == Expected(hedge,cell), $"hedge {cell} draws #{got}", quiet: true);
        }
        Check(true, "every showcase hedge cell draws the blob shape its eight neighbours call for");
        // The showcase ring holds only some shapes: a specimen of every mask (centre plus its neighbours, 4 cells apart) and a
        // 12 x 12 block for the random full cells must draw all 47 blob cells and the three random ones.
        var specimens = hedge with { Chunks = [] };
        for (int k = 0; k < 47; k++)
        {
            var centre = new Point(-16 + 4 * (k % 8), -12 + 4 * (k / 8));
            specimens.Set(centre,1);
            for (int i = 0; i < 8; i++) if ((order[k] >> i & 1) != 0) specimens.Set(new(centre.X + Around[i].X, centre.Y + Around[i].Y),1);
        }
        for (int y = 14; y < 26; y++) for (int x = -6; x < 6; x++) specimens.Set(new(x,y),1);
        var art = new HashSet<int>();
        foreach (var (cell,_) in specimens.OccupiedCells())
        {
            int got = specimens.DisplayTile(palette,cell,1);
            Check(got == Expected(specimens,cell), $"specimen {cell} draws #{got}", quiet: true);
            art.Add(got);
        }
        Check(art.Count == 50, "the hedge brush draws all 47 blob cells and the three random full cells");
        var tufts = decor with { Chunks = [] };
        for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++) tufts.Set(new(x,y),51);
        Check(decor.OccupiedCells().Where(c => decor.Get(c.Cell) == 51).All(c => decor.DisplayTile(palette,c.Cell,51) == Expected(decor,c.Cell)) &&
              tufts.OccupiedCells().Select(c => tufts.DisplayTile(palette,c.Cell,51)).Distinct().Count() == 4, "flower tufts pick from their four tiles by cell and seed");

        // Editor strokes on an empty layer: a 3x3 block across both chunk seams, then erase, cancel and repaint.
        var map = hedge with { Chunks = [] };
        var stroke = new TileStroke(map,TileTool.Rectangle,1,new(-1,-1));
        Check(stroke.Finish(new(1,1)).Length == 9, "a rectangle stroke paints 3x3 cells in four chunks");
        int[] Row() => Enumerable.Range(-2,5).SelectMany(y => Enumerable.Range(-2,5).Select(x => map.Get(new(x,y)) == 0 ? 0 : map.DisplayTile(palette,new(x,y),1))).ToArray();
        bool Matches() => Enumerable.Range(-2,5).All(y => Enumerable.Range(-2,5).All(x => map.Get(new(x,y)) == 0 || map.DisplayTile(palette,new(x,y),1) == Expected(map,new(x,y))));
        var painted = Row();
        Check(Matches() && map.DisplayTile(palette,new(-1,-1),1) == 1 + order.IndexOf(2 | 4 | 32), "seam corners connect across chunks (NW cell has E, S and SE)");
        var erase = new TileStroke(map,TileTool.Eraser,0,new(0,0));
        erase.Move(new(0,0));
        Check(Matches() && map.DisplayTile(palette,new(0,-1),1) == 1 + order.IndexOf(2 | 8), "erasing the centre reshapes all eight neighbours in four chunks");
        erase.Cancel();
        Check(Row().SequenceEqual(painted), "cancelling the erase restores every neighbour and the centre's random choice");
        erase = new TileStroke(map,TileTool.Eraser,0,new(0,0)); erase.Finish(new(0,0));
        new TileStroke(map,TileTool.Brush,1,new(0,0)).Finish(new(0,0));
        Check(Row().SequenceEqual(painted), "erase then repaint draws the same random full cell (position and seed only)");

        // Seed reproducibility: a reloaded palette repeats the pattern; another seed changes it.
        var field = hedge with { Chunks = [] };
        for (int y = 0; y < 12; y++) for (int x = 0; x < 12; x++) field.Set(new(x,y),1);
        var again = TilePalette.Load(project,assets,hedge.Tileset!.Value);
        var inner = Enumerable.Range(1,10).SelectMany(y => Enumerable.Range(1,10).Select(x => new Point(x,y))).ToArray();
        var first = inner.Select(p => field.DisplayTile(palette,p,1)).ToArray();
        Check(first.SequenceEqual(inner.Select(p => field.DisplayTile(again,p,1))) && first.Distinct().Count() == 4, "the same seed repeats the same 100 interior choices after reload");
        again.Seeds[1] = 8;
        Check(inner.Count(p => field.DisplayTile(again,p,1) != first[Array.IndexOf(inner,p)]) > 50, "seed 8 changes most interior choices");

        // Runtime: art is resolved at load, so drawing reads a table even when the palette has no rules left.
        var resolved = hedge.Resolved(palette);
        var bare = new TilePalette(palette.Image,palette.Tiles);
        Check(hedge.OccupiedCells().All(c => resolved.DisplayTile(bare,c.Cell,1) == hedge.DisplayTile(palette,c.Cell,1)), "the runtime copy draws from resolved art without neighbour lookups");
        var store = new PlayerStorage(Path.Combine(scratch,"autotile-profile"));
        using (var session = new GameSession(scene,null,null,store))
        {
            var id = scene.Objects.Single(o => o.Name == "Hedges").Id;
            Check(hedge.OccupiedCells().All(c => session.Current.TryGetTile(id,c.Cell,out var info) && info.Display == hedge.DisplayTile(palette,c.Cell,1)), "Play reads the authored art at every hedge cell");
            var edited = hedge with { };
            // Remove an inner bush, grow another beside it, cut the corner and bridge the river gap.
            foreach (var (cell,tile) in new[] { (new Point(5,3),0), (new Point(6,4),1), (new Point(0,0),0), (new Point(8,8),1) })
            {
                Check(session.Current.SetTile(id,cell,tile), $"SetTile {cell}", quiet: true);
                edited.Set(cell,tile);
            }
            Check(edited.OccupiedCells().All(c => session.Current.TryGetTile(id,c.Cell,out var info) && info.Display == edited.DisplayTile(palette,c.Cell,1)) &&
                  session.Current.TryGetTile(id,new(5,3),out var gone) && gone.Tile == 0, "runtime edits re-resolve the cell and its eight neighbours");
        }
        Check(hedge.Chunks.Length == 1 && hedge.Get(new(5,3)) == 1 && hedge.Get(new(6,4)) == 0, "Play leaves the authored layer intact");

        // Streamed pages bake the same blob and random art.
        var folder = Path.Combine(scratch,"autotile-world",TileWorld.Folder);
        var refs = TileWorld.Bake(hedge,palette,folder);
        using (var stream = new TileWorld(hedge with { Chunks = [], ChunkFiles = refs, StreamRadius = 1 },palette,Path.GetDirectoryName(folder)))
        {
            stream.Update(new(0,0),palette);
            Check(hedge.OccupiedCells().All(c => stream.Map.DisplayTile(palette,c.Cell,1) == hedge.DisplayTile(palette,c.Cell,1)), "streamed pages hold the same art");
        }

        // Validation names the gap; existing tilesets keep their bytes.
        var sheet = Tileset.Load(Path.Combine(project,"Assets","Showcase","Hedges.tileset"));
        Expect(() => (sheet with { Tiles = sheet.Tiles.Where(t => t.Id != 47).ToArray() }).Resolve(project,assets,"cut"), "missing mask 46");
        Expect(() => (sheet with { Tiles = sheet.Tiles.Select(t => t.Id == 51 ? t with { RandomTiles = 1 } : t).ToArray() }).Resolve(project,assets,"one"), "invalid auto terrain or random tiles");
        Expect(() => (sheet with { Tiles = sheet.Tiles.Select(t => t.Id == 51 ? t with { RandomTiles = 15 } : t).ToArray() }).Resolve(project,assets,"short"), "14 random sheet cells");
        // Sample tilesets are hand-written (layout differs from the writer); the writer must add no keys to them.
        int tilesets = 0;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(repository,"samples"),"*.tileset",SearchOption.AllDirectories))
        {
            var text = File.ReadAllBytes(file);
            var encoded = ProjectYaml.Encode(Tileset.Load(file));
            var written = System.Text.Encoding.UTF8.GetString(encoded);
            Check(ProjectYaml.Encode(ProjectYaml.Decode<Tileset>(encoded)).AsSpan().SequenceEqual(encoded) &&
                  (written.Contains("random") == System.Text.Encoding.UTF8.GetString(text).Contains("random")), $"{Path.GetFileName(file)} round-trips", quiet: true);
            tilesets++;
        }
        Check(tilesets >= 7, $"{tilesets} sample tilesets load, round-trip, and gain no random keys when saved");
    }

    private static void Expect(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException ex) when (ex.Message.Contains(message)) { Console.WriteLine($"PASS autotile: rejects \"{message}\""); return; }
        throw new Exception($"Autotile: expected an error containing '{message}'");
    }

    private static void Check(bool ok, string detail, bool quiet = false)
    {
        if (!ok) throw new Exception("Autotile: " + detail);
        if (!quiet) Console.WriteLine("PASS autotile: " + detail);
    }
}
