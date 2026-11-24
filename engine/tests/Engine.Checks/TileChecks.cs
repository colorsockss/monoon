using System.Drawing;
using System.Numerics;
using Engine.Core;
using Engine.Editor;

internal static class TileChecks
{
    [Check(7700)]
    static void Registered(CheckRun c) { var root = Path.Combine(c.Scratch, "TileTest"); Prepare(c.TileSample, root); Run(root); Directory.Delete(root, true); }
    internal static void Prepare(string original, string root)
    {
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new IOException($"Expected an empty fixture directory: {root}");
        Directory.CreateDirectory(root);
        foreach (var folder in new[] { "Assets", "Scenes", "Scripts" })
            foreach (var from in Directory.EnumerateFiles(Path.Combine(original, folder), "*", SearchOption.AllDirectories))
            {
                string to = Path.Combine(root, Path.GetRelativePath(original, from));
                Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Copy(from, to);
            }
        foreach (var name in new[] { ProjectAssets.Manifest, GameSettings.FileName }) File.Copy(Path.Combine(original, name), Path.Combine(root, name));
    }

    private static void CheckEditing()
    {
        var doc = new SceneDocument();
        var owner = new SceneObject();
        var map = new Tilemap2DComponent();
        owner.AddComponent(map); doc.Objects.Add(owner);
        var history = new SceneHistory();
        history.Reset(doc);
        owner.X = 16; history.Record(doc, owner.Id, owner.Id, null);
        var start = new Point(-34, -1); var end = new Point(35, -1);
        var stroke = new TileStroke(map, TileTool.Brush, 2, start);
        var delta = stroke.Finish(end);
        if (delta.Length != 70 || map.Chunks.Length != 4 || map.CellAt(owner.WorldPosition, 15.99, -.01) != new Point(-1, -1))
            throw new Exception("Missed sparse or negative cells.");
        history.RecordStroke(doc, owner.Id, delta); history.MarkSaved();
        var after = doc.CaptureObjects();
        history.Restore(doc, false); history.Restore(doc, false);
        if (doc.Objects.Single().X != 0 || doc.Objects.Single().GetComponent<Tilemap2DComponent>()!.Chunks.Length != 0)
            throw new Exception("Incorrect history order.");
        history.Restore(doc, true); history.Restore(doc, true);
        if (after != doc.CaptureObjects() || history.IsDirty) throw new Exception("Redo lost saved cells.");
        map = doc.Objects.Single().GetComponent<Tilemap2DComponent>()!;
        var erase = new TileStroke(map, TileTool.Eraser, 0, start);
        erase.Move(end); erase.Cancel();
        if (doc.CaptureObjects() != after) throw new Exception("Cancelled stroke survived.");
        var rectangle = new TileStroke(map, TileTool.Rectangle, 1, new(-1, -1));
        var area = rectangle.Finish(new(1, 1));
        if (area.Length != 9 || map.Get(new(0, 0)) != 1) throw new Exception("Rectangle crosses origin incorrectly.");
        history.RecordStroke(doc, owner.Id, area); history.Restore(doc, false);
        history.Restore(doc, false);
        history.RecordStroke(doc, owner.Id, new TileStroke(map, TileTool.Brush, 1, Point.Empty).Finish(Point.Empty));
        if (history.CanRedo) throw new Exception("New stroke must discard old redo entries.");
        Console.WriteLine("PASS tile stroke interpolation, rectangle, eraser cancellation, mixed Undo/Redo and save point");
    }

    private static void CheckSlideAndDraw(string root, Guid tileset)
    {
        var scene = SceneDocument.Empty(new());
        scene.ProjectDirectory = root;
        var wall = new SceneObject { Name = "Tile wall" };
        var map = new Tilemap2DComponent { Tileset = tileset, Order = -2 };
        for (int y = -33; y <= 33; y++) map.Set(new(2, y), 1);
        map.Set(new(1, 0), 2);
        wall.AddComponent(map); scene.Objects.Add(wall);
        var actor = new SceneObject { Name = "Mover", X = 0, Y = -20, Width = 8, Height = 8 };
        actor.AddComponent(new BoxCollider2DComponent());
        actor.AddComponent(new SpriteRendererComponent { Asset = "Assets/Key.png" });
        actor.AddComponent(new ScriptComponent { Script = "PlayerMovement" });
        scene.Objects.Add(actor);
        var front = new SceneObject();
        var frontMap = new Tilemap2DComponent { Tileset = tileset, Order = 2 };
        frontMap.Set(new(1, 2), 2); front.AddComponent(frontMap); scene.Objects.Add(front);
        using var game = new GameSession(scene);
        var direction = Vector2.Normalize(Vector2.One);
        game.FixedUpdate(direction, .5f);
        var canvas = new CaptureCanvas();
        game.Draw(canvas, 1);
        var player = canvas.Images.Single(i => i.Path.EndsWith("Key.png")).Rect;
        if (Math.Abs(player.X - 56) > .01f || Math.Abs(player.Y - (-20 + 120 * direction.Y)) > .02f ||
            canvas.Images[0].Source == null || canvas.Images[^1].Rect.X != 32 || canvas.Images[^1].Source?.X != 8)
            throw new Exception($"Unexpected tile slide or world sorting: {player}.");
        Console.WriteLine("PASS diagonal slide across chunk boundary, decorative tile traversal and foreground ordering");
    }

    internal static void Run(string root)
    {
        var path = Path.Combine(root, "Scenes", "KeyRoom.scene");
        var scene = SceneDocument.Load(path);
        var wall = scene.Objects.Single(o => o.Name == "Wall");
        var map = wall.GetComponent<Tilemap2DComponent>()!;
        CollisionChecks.Tiles(root,map.Tileset!.Value);
        ContactChecks.Tiles(root,map.Tileset.Value);
        CheckEditing();
        ProjectedTileChecks.Run(root,map.Tileset!.Value);
        CheckSlideAndDraw(root, map.Tileset!.Value);
        foreach (var invalid in new[] { new[]{new TileChunk(0,0,[new(-1,1)])}, new[]{new TileChunk(0,0,[new(1024,1)])},
            new[]{new TileChunk(0,0,[new(0,1),new(0,2)])}, new[]{new TileChunk(0,0,[new(0,1)]),new TileChunk(0,0,[new(2,1)])} })
        {
            try { _ = new Tilemap2DComponent { Chunks = invalid }; throw new Exception("Accepted invalid chunks."); }
            catch (InvalidDataException) { }
        }
        var copy = scene.DuplicateObject(wall);
        copy.GetComponent<Tilemap2DComponent>()!.Set(new(0, 0), 0);
        if (map.Get(new(0, 0)) != 1 || map.Get(new(-8, -6)) != 1) throw new Exception("Tile copy shared mutations");
        scene.RemoveObject(copy);
        scene.ExportPrefab(wall, Path.Combine(root, "Assets", "Wall.prefab"));
        var instance = scene.PlacePrefab("Assets/Wall.prefab", new(-320, 0));
        instance.GetComponent<Tilemap2DComponent>()!.Set(new(0, 0), 0);
        if (map.Get(new(0, 0)) != 1) throw new Exception("Prefab mutated original tiles");
        var palette = TilePalette.Load(root, ProjectAssets.Scan(root), map.Tileset!.Value);
        var visible = new RectangleF(470, 170, 256, 250);
        int count = map.InView(wall.WorldPosition, visible).Count();
        for (int i = 0; i < 300; i++) map.Set(new(i * 32 + 16000, -32000), 1);
        if (count != map.InView(wall.WorldPosition, visible).Count()) throw new Exception("Offscreen chunks were drawn");
        map.Set(new(-33, -1), 9999);
        try { map.ValidateTiles(palette, "Wall"); throw new Exception("Accepted missing tile"); }
        catch (InvalidDataException ex) when (ex.Message.Contains("9999") && ex.Message.Contains("-33")) { }
        using var game = new GameSession(SceneDocument.Load(path));
        game.FixedUpdate(Vector2.UnitY, 1); // A complete sweep through the two-unit ledge in one step.
        var canvas = new CaptureCanvas();
        game.Draw(canvas, 1);
        if (!canvas.Images.Any(i => Path.GetFileName(i.Path) == "Tiles.png" && i.Source?.X == 0 && Math.Abs(i.Rect.Y - 154) < .05))
            throw new Exception("Fast movement crossed the ledge.");
        Console.WriteLine("PASS tile coordinates, deep copies, prefab instances, visible chunks and swept wall collision");
    }
}
