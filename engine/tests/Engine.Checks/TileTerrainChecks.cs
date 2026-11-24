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
        var tilesetPath = Path.Combine(project, "Assets", "Route.tileset");
        var tileset = Tileset.Load(tilesetPath);
        var copy = Path.Combine(scratch, "terrain.tileset");
        ProjectYaml.Write(copy, tileset);
        var reread = Tileset.Load(copy);
        Check(reread.Tiles.Zip(tileset.Tiles).All(p => p.First == p.Second) &&
              reread.Tiles.Single(t => t.Id == 3) is { Terrain: "mud", MoveCost: 12 } &&
              File.ReadAllText(copy).Contains("terrain: dry mud"), "terrain names and move costs survive a save and reload");
        Check(new TileDefinition(1, new(0, 0, 8, 8), true).EntryCost() == 0 && new TileDefinition(1, new(0, 0, 8, 8), false).EntryCost() == 1 &&
              tileset.Tiles.Single(t => t.Id == 4).EntryCost() == 0, "unset cost blocks solid tiles and defaults to 1 elsewhere");
        try
        {
            (tileset with { Tiles = [.. tileset.Tiles.Select(t => t.Id == 3 ? t with { MoveCost = -1 } : t)] })
                .Resolve(project, assets, "Route.tileset");
            throw new Exception("negative move cost accepted");
        }
        catch (InvalidDataException) { Check(true, "a negative move cost is rejected when the tileset resolves"); }

        Live(project);
        Streamed(repository, scratch);
    }

    // The SeedLab courier: tile edits drive the drawn tile, the solid door and the planned route.
    private static void Live(string project)
    {
        var scene = SceneDocument.Load(Path.Combine(project, "Scenes", "Route.scene"));
        var board = scene.Objects.Single(o => o.Name == "Terrain").Id;
        var courier = scene.Objects.Single(o => o.Name == "Courier").Id;
        using var session = new GameSession(scene);
        var game = session.Current;
        void Step() => session.FixedUpdate(session.Actions.Take(), 1f / 60);
        void Press(string key) { session.Actions.Press(key); Step(); session.Actions.Release(key); Step(); }
        TileInfo Cell(int x, int y) => game.TryGetTile(board, new(x, y), out var info) ? info : throw new Exception("missing layer");
        bool Blocked(int x, int y) => game.Physics.Overlap(Shape2D.Circle(new(24 + 40 * x + 20, 92 + 40 * y + 20), 1)).Any(h => h.ObjectId == board);
        Point Courier() => game.TryGetLocalTransform(courier, out var t) ? new(((int)t.Position.X - 12) / 40, ((int)t.Position.Y - 12) / 40) : throw new Exception("missing courier");
        List<Point> Walk(Action? midway = null)
        {
            var cells = new List<Point> { Courier() };
            Press("Space");
            for (int i = 0; i < 400 && Courier() != new Point(11, 4); i++)
            {
                if (i == 30) midway?.Invoke();
                Step();
                if (Courier() != cells[^1]) cells.Add(Courier());
            }
            return cells;
        }
        Step();

        Check(Cell(6, 4) is { Tile: 4, Terrain: "door", MoveCost: 0, Solid: true } && Blocked(6, 4) &&
              Cell(3, 5) is { Terrain: "mud", MoveCost: 12 } && !Blocked(3, 5), "door and mud cells report tileset terrain; the closed door collides");
        Check(Cell(2, 2).Tile == 8 && Cell(1, 2).Tile == 2 && Cell(6, 3).Tile == 1, "route dots are drawn as cells on the planned northern corridor");
        var north = Walk();
        Check(north.Contains(new(6, 1)) && north[^1] == new Point(11, 4) && north.Count == 17, "closed door: 16-step northern route");

        Press("R"); Press("O");
        Check(Cell(6, 4) is { Tile: 5, MoveCost: 1, Solid: false } && !Blocked(6, 4) && Cell(2, 2).Tile == 2,
            "one door cell swap removes its collider, opens the grid and moves the route dots");
        var straight = Walk();
        Check(straight.Count == 11 && straight.Contains(new(6, 4)), "open door: 10-step route through the swapped cell");
        Press("R"); Press("O");
        Check(Cell(6, 4).Tile == 4 && Blocked(6, 4), "closing restores the solid door");

        Press("B");
        Check(Cell(4, 1) is { Terrain: "mud", MoveCost: 12 } && Cell(5, 5).Tile == 9, "painted mud uses the tileset cost; the route moves south");
        var south = Walk();
        Check(!south.Any(p => p.Y == 1) && south.Contains(new(6, 5)) && south.Count == 13, "painted north: 12-step route across three southern mud cells");

        Press("R"); Press("B");
        Check(Cell(4, 1) is { Tile: 8, Terrain: "floor" } && Cell(2, 2).Tile == 8, "clearing the paint restores floor and the northern plan");
        Point pivot = default;
        var replanned = Walk(() => { pivot = Courier(); session.Actions.Press("B"); Step(); session.Actions.Release("B"); });
        Check(pivot.Y < 4 && replanned[^1] == new Point(11, 4) && !replanned.Any(p => p.Y == 1 && p.X >= 4) &&
              replanned.Contains(new(6, 5)), $"painting mid-walk re-plans from {pivot} and avoids the new mud");

        Press("R"); Press("M");
        Check(Cell(3, 5) is { Tile: 10, Terrain: "dry mud", MoveCost: 1 } && Cell(4, 1).Tile is 10 or 11, "M swaps every mud cell to dry mud");
        Press("M"); Press("B");

        // A grid created by the API follows SetTile and ReplaceTiles.
        var grid = game.TileNavigation(board, new(1, 1, 3, 1))!;
        long revision = grid.Revision;
        Check(grid.FindPath(new(0, 0), new(2, 0)).Cost == 20 && game.SetTile(board, new(2, 1), 1) && Blocked(2, 1) &&
              grid.FindPath(new(0, 0), new(2, 0)).Cells.Count == 0 && grid.Revision > revision, "a wall written with SetTile collides and blocks a bound grid");
        game.SetTile(board, new(2, 1), 3);
        Check(grid.FindPath(new(0, 0), new(2, 0)).Cost == 130 && !Blocked(2, 1), "mud written with SetTile costs 12 in a bound grid");
        game.ReplaceTiles(board, 1, 1, [2]);
        Check(game.TryGetTile(board, new(2, 1), out var empty) && empty.Tile == 0 && grid.FindPath(new(0, 0), new(1, 0)).Cells.Count == 0,
            "ReplaceTiles refreshes bound grids; empty cells block");
        Check(!game.SetTile(Guid.NewGuid(), default, 1) && !game.TryGetTile(courier, default, out _) && game.TileNavigation(courier, new(0, 0, 1, 1)) == null,
            "missing tile layers are refused");
        try { game.SetTile(board, default, 999); throw new Exception("unknown tile accepted"); }
        catch (ArgumentException) { }
        Check(scene.Objects.Single(o => o.Id == board).GetComponent<Tilemap2DComponent>()!.Chunks.Length == 0, "Play leaves the authored layer untouched");
    }

    // Streamed pages: edits resolve auto-terrain borders and survive eviction.
    private static void Streamed(string repository, string scratch)
    {
        var project = Path.Combine(repository, "samples", "PlatformerLab");
        var scene = SceneDocument.Load(Path.Combine(project, "Scenes", "Platform.scene"));
        var source = scene.Objects.Single(o => o.Name == "Ridge terrain").GetComponent<Tilemap2DComponent>()!;
        var palette = TilePalette.Load(project, ProjectAssets.ReadManifest(project), source.Tileset!.Value);
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
