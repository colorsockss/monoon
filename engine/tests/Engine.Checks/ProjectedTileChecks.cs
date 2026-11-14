using System.Drawing;
using System.Numerics;
using Engine.Core;
using Engine.Editor;

internal static class ProjectedTileChecks
{
    internal static void Run(string root, Guid tileset)
    {
        var path = Path.Combine(root,"Scenes","GridLayouts.scene");
        var demo = SceneDocument.Load(path);
        string[] layouts = [TileGrid.Isometric,TileGrid.HexPointy,TileGrid.HexFlat];
        foreach (string layout in layouts)
        {
            var layer = demo.Objects.Select(o => o.GetComponent<Tilemap2DComponent>()).Single(m => m?.Layout == layout)!;
            var origin = new Vector2(-53.5f,71.25f);
            Point[] cells = [new(-35,63),new(-1,0),new(17,-32),new(0,0)];
            foreach (var cell in cells)
            {
                var bounds = layer.Bounds(origin,cell);
                foreach (var v in TileGrid.Outline(layout))
                {
                    var inner = TileGrid.Vertex(bounds,Vector2.Lerp(v,new(.5f),.01f));
                    if (layer.CellAt(origin,inner.X,inner.Y) != cell) throw new Exception($"{layout} picked wrong cell at {inner}.");
                    var view = new RectangleF(inner.X-.1f,inner.Y-.1f,.2f,.2f);
                    layer.Set(cell,1);
                    if (!layer.InView(origin,view).Any(c => c.Cell == cell) || !TileGrid.VisibleCells(layer,origin,view).Contains(cell))
                        throw new Exception($"{layout} culled a visible corner of {cell}.");
                }
            }
            var stroke = new TileStroke(layer,TileTool.Brush,2,new(-37,50));
            var changes = stroke.Finish(new(41,-19));
            if (TileGrid.IsHex(layout))
            {
                var line = TileGrid.HexLine(new(-37,50),new(41,-19)).ToArray();
                if (line.Length != 79 || line.Distinct().Count() != 79 || line.Any(p => layer.Get(p) != 2))
                    throw new Exception($"{layout} left a gap in a brush stroke.");
                foreach (var (a,b) in line.Zip(line.Skip(1)))
                    if (Math.Abs(a.X-b.X+a.Y-b.Y) > 1 || Math.Abs(a.X-b.X)>1 || Math.Abs(a.Y-b.Y)>1)
                        throw new Exception($"{layout} jumped across non-adjacent hexagons.");
            }
            if (changes.Length == 0) throw new Exception("Missing stroke history");
            CheckCollision(root,tileset,layout);
        }
        demo.Save(path);
        var reopened = SceneDocument.Load(path);
        foreach (var layout in layouts)
        {
            var map = reopened.Objects.Select(o => o.GetComponent<Tilemap2DComponent>()).Single(m => m?.Layout == layout)!;
            if (map.Get(new(41,-19)) != 2 || map.Get(new(-35,63)) != 1) throw new Exception($"{layout} lost saved cells.");
        }
        Console.WriteLine("PASS all projected tile layouts: pick/cull at corners, brush across negative chunks, save/reopen, angled sliding and fast solid collision");
    }

    private static void CheckCollision(string root, Guid tileset, string layout)
    {
        var scene = SceneDocument.Empty(new());
        scene.ProjectDirectory = root;
        var wall = new SceneObject { Name = "Tiles", X = 300, Y = 180 };
        var map = new Tilemap2DComponent { Layout=layout, CellSize=new(64,64), Tileset=tileset };
        map.Set(Point.Empty,1); wall.AddComponent(map); scene.Objects.Add(wall);
        var actor = new SceneObject { Name="Mover", Width=2, Height=2 };
        actor.AddComponent(new SpriteRendererComponent { Asset="Assets/Key.png" });
        actor.AddComponent(new BoxCollider2DComponent());
        actor.AddComponent(new ScriptComponent { Script="PlayerMovement" });
        scene.Objects.Add(actor);
        void CheckMove(Vector2 start, Vector2 travel, Vector2 expected)
        {
            actor.X=wall.X+start.X; actor.Y=wall.Y+start.Y;
            using var game = new GameSession(scene);
            game.FixedUpdate(Vector2.Normalize(travel),travel.Length()/240);
            var canvas = new CaptureCanvas(); game.Draw(canvas,1);
            var end = canvas.Images.Single(i => i.Path.EndsWith("Key.png")).Rect.Location;
            if (Vector2.Distance(new(end.X-wall.X,end.Y-wall.Y),expected) > .025f)
                throw new Exception($"{layout} from {start} by {travel}, expected {expected}, got {end}.");
        }
        CheckMove(new(-10,0),new(17,0),new(7,0));
        CheckMove(new(-200,31),new(500,0),new(-2,31));
        var polygon = TileGrid.Outline(layout);
        int i = layout == TileGrid.HexFlat ? 1 : 0;
        var edge = polygon[i+1]-polygon[i];
        var tangent = Vector2.Normalize(edge);
        var normal = new Vector2(tangent.Y,-tangent.X);
        var touch = (polygon[i]+polygon[i+1])*32 + new Vector2(Math.Sign(normal.X),Math.Sign(normal.Y))-Vector2.One;
        CheckMove(touch+normal*20-tangent*4,-normal*40+tangent*8,touch+tangent*4);
    }
}
