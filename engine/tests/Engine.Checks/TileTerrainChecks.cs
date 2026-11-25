using System.Drawing;
using System.Numerics;
using Engine.Core;

// Roadmap 10.6: tileset terrain data and runtime cell edits shared by drawing, collision and grid navigation.
internal static class TileTerrainChecks
{
    [Check(3400, "terrain")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private static void Check(bool ok, string detail)
    {
        if (!ok) throw new Exception("Tile terrain: " + detail);
        Console.WriteLine("PASS tile terrain: " + detail);
    }

    public static void Run(string repository, string scratch)
    {
        var project = Path.Combine(repository, "samples", "SeedLab");
        var assets = ProjectAssets.ReadManifest(project);
        var tilesetPath = Path.Combine(project, "Assets", "Showcase", "Field.tileset");
        var tileset = Tileset.Load(tilesetPath);
        var copy = Path.Combine(scratch, "terrain.tileset");
        ProjectYaml.Write(copy, tileset);
        var reread = Tileset.Load(copy);
        Check(reread.Tiles.Zip(tileset.Tiles).All(p => p.First == p.Second) &&
              reread.Tiles.Single(t => t.Id == 3) is { Terrain: "mud", MoveCost: 8 } &&
              File.ReadAllText(copy).Contains("terrain: bridge"), "terrain names and move costs survive a save and reload");
        Check(new TileDefinition(1, new(0, 0, 8, 8), true).EntryCost() == 0 && new TileDefinition(1, new(0, 0, 8, 8), false).EntryCost() == 1 &&
              tileset.Tiles.Single(t => t.Id == 4).EntryCost() == 0, "unset cost blocks solid tiles and defaults to 1 elsewhere");
        try
        {
            (tileset with { Tiles = [.. tileset.Tiles.Select(t => t.Id == 3 ? t with { MoveCost = -1 } : t)] })
                .Resolve(project, assets, "Field.tileset");
            throw new Exception("negative move cost accepted");
        }
        catch (InvalidDataException) { Check(true, "a negative move cost is rejected when the tileset resolves"); }

        Live(project);
        Streamed(repository, scratch);
    }

    // The SeedLab route showcase with its scripts removed: the API edits cells, and each edit changes the drawn tile, the tile
    // collision and the routes of a bound grid (road 1, grass 3, mud 8, bridge 1; water and hedges block).
    private static void Live(string project)
    {
        var scene = SceneDocument.Load(Path.Combine(project, "Scenes", "ShowcaseRoute.scene"));
        foreach (var o in scene.Objects) o.Components = o.Components.RemoveAll(c => c is ScriptComponent);
        var board = scene.Objects.Single(o => o.Name == "Field").Id;
        var courier = scene.Objects.Single(o => o.Name == "Courier").Id;
        string Authored() => string.Join(",", scene.Objects.Single(o => o.Id == board).GetComponent<Tilemap2DComponent>()!.Chunks
            .SelectMany(c => c.Cells.Select(cell => $"{c.X}/{c.Y}/{cell.Index}:{cell.Tile}")));
        string authored = Authored();
        using var session = new GameSession(scene);
        var game = session.Current;
        session.FixedUpdate(session.Actions.Take(), 1f / 60);
        TileInfo Cell(int x, int y) => game.TryGetTile(board, new(x, y), out var info) ? info : throw new Exception("missing layer");
        bool Blocked(int x, int y) => game.Physics.Overlap(Shape2D.Circle(new(32 * x + 16, 32 * y + 16), 1)).Any(h => h.ObjectId == board);
        void Lay(int tile) { game.SetTile(board, new(8, 5), tile); game.SetTile(board, new(9, 5), tile); }
        var grid = game.TileNavigation(board, new(0, 0, 18, 9))!;
        Point start = new(1, 5), goal = new(15, 5);

        bool field = Cell(8, 5) is { Tile: 4, Terrain: "water", MoveCost: 0, Solid: true } && Blocked(8, 5) &&
              Cell(8, 1) is { Tile: 5, Terrain: "bridge", MoveCost: 1, Solid: false } && !Blocked(8, 1) &&
              Cell(3, 1) is { Terrain: "road", MoveCost: 1 } && Cell(3, 2) is { Terrain: "grass", MoveCost: 3 };
        Check(field, "field cells report tileset terrain; water collides, the north bridge does not" +
            (field ? "" : $" ({Cell(8, 5)} {Blocked(8, 5)} / {Cell(8, 1)} {Blocked(8, 1)} / {Cell(3, 1)} / {Cell(3, 2)})"));
        var north = grid.FindPath(start, goal);
        Check(north.Cost == 220 && north.Cells.Count == 23 && north.Cells.Contains(new(8, 1)), "dry ford: a 22-step route over the north bridge, cost 220");

        long revision = grid.Revision;
        Lay(5);
        Check(Cell(8, 5) is { Tile: 5, MoveCost: 1, Solid: false } && !Blocked(8, 5) && grid.Revision > revision,
            "two SetTile calls lay the south bridge: its collider goes and the bound grid changes revision");
        var south = grid.FindPath(start, goal);
        Check(south.Cost == 140 && south.Cells.Count == 15 && south.Cells.Contains(new(8, 5)), "bridge laid: a 14-step road straight across, cost 140");
        Lay(4);
        Check(Blocked(8, 5) && grid.FindPath(start, goal).Cost == 220, "lifting it restores the water collider and the northern route");

        foreach (int x in new[] { 3, 4, 5, 6, 7 }) game.SetTile(board, new(x, 1), 3);
        Check(Cell(4, 1) is { Tile: 3, Terrain: "mud", MoveCost: 8, Solid: false } && !Blocked(4, 1), "rain writes mud: the tileset cost 8 and no collider");
        Check(grid.FindPath(start, goal).Cost == 350 && grid.FindPath(start, goal, GridDirections.Eight).Cost == 344,
            "rain on the north road: the cheapest route costs 350, 344 with diagonals");

        // A grid bound to part of the layer follows SetTile and ReplaceTiles: three grass cells of row 6.
        var part = game.TileNavigation(board, new(1, 6, 3, 1))!;
        revision = part.Revision;
        Check(part.FindPath(new(0, 0), new(2, 0)).Cost == 60 && game.SetTile(board, new(2, 6), 6) && Blocked(2, 6) &&
              part.FindPath(new(0, 0), new(2, 0)).Cells.Count == 0 && part.Revision > revision, "a hedge written with SetTile collides and blocks a bound grid");
        game.SetTile(board, new(2, 6), 3);
        Check(part.FindPath(new(0, 0), new(2, 0)).Cost == 110 && !Blocked(2, 6), "mud written with SetTile costs 8 in a bound grid");
        game.ReplaceTiles(board, 1, 1, [2]);
        Check(game.TryGetTile(board, new(2, 6), out var empty) && empty.Tile == 0 && part.FindPath(new(0, 0), new(1, 0)).Cells.Count == 0,
            "ReplaceTiles refreshes bound grids; empty cells block");
        Check(!game.SetTile(Guid.NewGuid(), default, 1) && !game.TryGetTile(courier, default, out _) && game.TileNavigation(courier, new(0, 0, 1, 1)) == null,
            "missing tile layers are refused");
        try { game.SetTile(board, default, 999); throw new Exception("unknown tile accepted"); }
        catch (ArgumentException) { }
        Check(authored.Length > 0 && Authored() == authored, "Play leaves the authored layer untouched");
    }

    // Streamed pages of the Ridge autotile fixture: edits resolve auto-terrain borders and survive eviction.
    private static void Streamed(string repository, string scratch)
    {
        var (source, palette) = TileWorldChecks.Ridge(repository, scratch);
        Point left = new(-1, 0), right = new(0, 0), east = new(1, 0);
        var full = source with { Chunks = [] };
        full.Set(left, 1); full.Set(right, 1);
        var folder = Path.Combine(scratch, "terrain-world", TileWorld.Folder);
        var baked = full with { Chunks = [], ChunkFiles = TileWorld.Bake(full, palette, folder) };
        using var stream = new TileWorld(baked, palette, Path.GetDirectoryName(folder));
        bool Same(params Point[] cells) => cells.All(p => stream.Map.Get(p) == full.Get(p) &&
            (full.Get(p) == 0 || stream.Map.DisplayTile(palette, p, stream.Map.Get(p)) == full.DisplayTile(palette, p, full.Get(p))));
        stream.Update(new(0, 0), palette);
        Check(Same(left, right, east) && stream.Map.DisplayTile(palette, right, 1) == 9, "baked pages match the authored map");
        full.Set(east, 1);
        Check(stream.Set(east, 1, palette) && Same(left, right, east) && stream.Map.DisplayTile(palette, right, 1) != 9,
            "a streamed edit re-resolves the neighbour's baked border art");
        stream.Set(left, 0, palette); full.Set(left, 0);
        Check(Same(left, right, east), "clearing a cell across a page boundary disconnects the neighbour");
        stream.Update(new(3, 0), palette); stream.Update(new(0, 0), palette);
        Check(stream.State.Unloads >= 2 && Same(left, right, east), "edits are reapplied when evicted pages reload");
        stream.Update(new(1, 0), palette);
        Check(!stream.Set(left, 1, palette) && !stream.Contains(new(-1, 0)), "cells of unloaded pages cannot be edited");
    }
}
