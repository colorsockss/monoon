using System.Drawing;
using System.Numerics;
using Engine.Core;

internal static class CollisionChecks
{
    private static void Check(bool valid,string name) { if (!valid) throw new Exception("Physics: "+name); }
    private static void Near(float actual,float expected,string name) => Check(Math.Abs(actual-expected)<.001f,$"{name}: {actual} vs {expected}");
    private static PhysicsHit Hit(PhysicsHit? h) => h??throw new Exception("Missing physics hit");
    internal static void Run(string repository)
    {
        var path=Path.Combine(repository,"samples","CollisionLab","Scenes","CollisionLab.scene");
        var scene=SceneDocument.Load(path);
        var obj=scene.Objects.Single(o=>o.Name=="Ramp"); var poly=obj.GetComponent<PolygonCollider2DComponent>()!;
        var probe=scene.Objects.Single(o=>o.Name=="Probe"); var sensor=scene.Objects.Single(o=>o.Name=="Sensor");
        Check(!obj.AddComponent(new BoxCollider2DComponent()),"one shape per owner");
        var copy=SceneDocument.Empty(new());
        copy.RestoreObjects(scene.CaptureObjects());
        var reopened=copy.Objects.Single(o=>o.Id==obj.Id).GetComponent<PolygonCollider2DComponent>();
        Check(reopened?.Mask==2 && reopened.Layer==2 && reopened.Points.SequenceEqual(poly.Points),"polygon and filters roundtrip");
        var old=SceneDocument.Load(Path.Combine(repository,"samples","KeyRoom","Scenes","KeyRoom.scene"));
        Check(old.Objects.SelectMany(o=>o.Components).OfType<BoxCollider2DComponent>().Any(b=>b.Mask==uint.MaxValue && b.Layer==0),"legacy mask");
        using var game=new GameSession(scene);
        var world=game.Current; var query=world.Physics;
        var actors=new PhysicsFilter(2);
        var ray=Hit(query.Raycast(new(0,200),new(2,0),800,actors));
        Check(ray.Target.ObjectId==probe.Id,"circle ray owner"); Near(ray.Distance,80,"circle ray");Near(ray.Normal.X,-1,"circle normal");
        Near(Hit(query.Raycast(new(0,180),Vector2.UnitX,800,actors)).Distance,100,"tangent ray");
        Check(query.Raycast(new(0,179.99f),Vector2.UnitX,800,actors)==null,"near tangent miss");
        Check(query.Raycast(new(0,200),Vector2.UnitX,79,actors)==null,"finite range");
        Check(query.Raycast(new(0,200),Vector2.UnitX,80,actors)!=null,"exact endpoint");
        Near(Hit(query.Raycast(new(100,200),Vector2.UnitX,0,actors)).Distance,0,"origin inside");
        Check(query.Overlap(Shape2D.Circle(new(60,200),20),actors).Count==0,"touch isn't overlap");
        Check(query.Overlap(Shape2D.Circle(new(100,200),2),actors with { Ignore=probe.Id }).Count==0,"ignore owner");
        Check(query.Overlap(Shape2D.Circle(new(100,200),2),new PhysicsFilter(0)).Count==0,"empty query mask");
        Near(Hit(query.Sweep(Shape2D.Circle(new(0,200),10),new(800,0),actors)).Distance,70,"circle vs circle sweep");
        Near(Hit(query.Sweep(Shape2D.Box(new(0,198,10,4)),new(800,0),actors)).Distance,70,"polygon vs circle sweep");
        var walls=new PhysicsFilter(4);
        Near(Hit(query.Sweep(Shape2D.Polygon(new(0,196),new(10,200),new(0,204)),new(800,0),walls)).Distance,490,"polygon sweep");
        var round=Hit(query.Sweep(Shape2D.Circle(new(0,137),5),new(800,0),walls));
        Near(round.Point.X,500,"vertex point X");Near(round.Point.Y,140,"vertex point Y");
        Near(round.Distance,496,"rounded polygon corner");Near(round.Normal.X,-.8f,"corner normal X");Near(round.Normal.Y,-.6f,"corner normal Y");
        Check(query.Overlap(Shape2D.Box(new(589,141,5,5)),walls).Count==0,"polygon empty AABB corner");
        Check(query.Overlap(Shape2D.Circle(new(240,200),1),new(8,true)).Single().ObjectId==sensor.Id,"include trigger");
        Check(query.Raycast(new(220,200),Vector2.UnitX,100,new(8))==null,"ray excludes trigger");
        world.SetActive(obj.Id,false);Check(query.Raycast(new(0,200),Vector2.UnitX,800,walls)==null,"inactive collider");world.SetActive(obj.Id,true);
        game.Actions.Press("Right");game.FixedUpdate(game.Actions.Take(),1/60f);game.FixedUpdate(game.Actions.Take(),1/60f);
        Check(game.State.Storage.LoadSlot(1,1)?.GetBool("pass")==true,"continuous trigger and mutually filtered blockers match queries");
        var extended=SceneDocument.Empty(new());
        var mirrored=new SceneObject { X=250,Y=200,Width=20,Height=40,ScaleX=-3,ScaleY=2,Rotation=90 };
        mirrored.AddComponent(new CircleCollider2DComponent { Radius=5,Layer=31,Mask=0 });extended.Objects.Add(mirrored);
        var center=mirrored.LocalToWorld(new(10,20));
        using var transformed=new SceneGame(extended);
        Near(Hit(transformed.Raycast(center-new Vector2(30,0),Vector2.UnitX,100,new(1u<<31))).Distance,20,"transformed explicit radius / layer 31");
        var invalid=SceneDocument.Empty(new());
        var bad=new SceneObject { Name="Malformed" };var shape=new PolygonCollider2DComponent();bad.AddComponent(shape);invalid.Objects.Add(bad);
        foreach (var pts in new ColliderVertex[][] { [new(0,0),new(1,0)], [new(0,0),new(1,1),new(0,1),new(1,0)],
                 [new(0,0),new(1,0),new(.4f,.2f),new(1,1),new(0,1)] })
        {
            shape.Points=pts;
            try { copy.RestoreObjects(invalid.CaptureObjects()); throw new Exception("Accepted malformed polygon"); }
            catch (InvalidDataException) { }
        }
        Console.WriteLine("PASS collision geometry: shape casts, tangent/endpoint, overlap, filters, affine circle, sensor crossing, YAML validation and legacy defaults");
    }
    internal static void Tiles(string root,Guid tileset)
    {
        foreach (var layout in new[]{TileGrid.Orthogonal,TileGrid.Isometric,TileGrid.HexPointy,TileGrid.HexFlat})
        {
            var scene=SceneDocument.Empty(new());scene.ProjectDirectory=root;
            var o=new SceneObject { X=100,Y=120,Rotation=27 };
            var map=new Tilemap2DComponent { Tileset=tileset,Layout=layout,CollisionLayer=31,CollisionMask=0,CellSize=new(32,32) };
            var cell=new Point(-35,2);map.Set(cell,1);map.Set(new(6,6),2);o.AddComponent(map);scene.Objects.Add(o);
            using var game=new SceneGame(scene);var area=map.Bounds(Vector2.Zero,cell);
            var center=o.LocalToWorld(new(area.X+16,area.Y+16));
            var filter=new PhysicsFilter(1u<<31);
            Check(game.Overlap(Shape2D.Circle(center,1),filter).Single().Tile==cell,"solid tile address: "+layout);
            Check(game.Overlap(Shape2D.Circle(center,1),new PhysicsFilter(1)).Count==0,"tile mask: "+layout);
            var ray=Hit(game.Raycast(center-new Vector2(80,0),Vector2.UnitX,160,filter));
            var sweep=Hit(game.Sweep(Shape2D.Circle(center-new Vector2(80,0),2),new(160,0),filter));
            Check(ray.Target.Tile==cell && sweep.Target.Tile==cell && sweep.Distance<ray.Distance,"tile polygon casts: "+layout);
        }
        Console.WriteLine("PASS physics queries against transformed orthogonal, isometric and hexagonal solid tiles");
    }
}
