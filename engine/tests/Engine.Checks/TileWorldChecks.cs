using Engine.Core;
using Engine.Editor;
using System.Drawing;
using System.Numerics;

internal static class TileWorldChecks
{
    public static void Run(string repository,string scratch)
    {
        var project = Path.Combine(repository,"samples","PlatformerLab");
        var scene = SceneDocument.Load(Path.Combine(project,"Scenes","Platform.scene"));
        var ground = scene.Objects.Single(o => o.Name == "Ridge terrain");
        var source = ground.GetComponent<Tilemap2DComponent>()!;
        var palette = TilePalette.Load(project,ProjectAssets.ReadManifest(project),source.Tileset!.Value);
        var map = source with { Chunks=[] };
        Point left=new(-1,0), right=new(0,0);
        var stroke=new TileStroke(map,TileTool.Brush,1,left);
        stroke.Move(right);
        var changes=stroke.Finish(right);
        int Art(Point p) => map.DisplayTile(palette,p,map.Get(p));
        Check(changes.Length==2 && Art(left)==3 && Art(right)==9,"brush connects both sides of a negative chunk boundary");
        var erase=new TileStroke(map,TileTool.Eraser,0,right);
        erase.Move(right);
        Check(Art(left)==1 && Art(right)==0,"erase updates the untouched adjacent tile");
        erase.Cancel();
        Check(Art(left)==3 && Art(right)==9,"cancel restores both visuals from logical brush IDs");
        map.Set(new(1,0),2);
        Check(Art(right)==9,"a different brush does not connect");

        // Missing neighbors outside the resident window must not change baked edge art.
        var folder=Path.Combine(scratch,TileWorld.Folder);
        var refs=TileWorld.Bake(map,palette,folder);
        var baked=map with {Chunks=[], ChunkFiles=refs};
        using (var stream=new TileWorld(baked,palette,scratch))
        {
            stream.Update(new(1,0),palette);
            Check(!stream.Contains(new(-1,0)) && stream.Map.Get(left)==0 && stream.Map.DisplayTile(palette,right,1)==9,
                "unloaded neighbor is absent but cross-page art retains its west connection");
            var remote=refs.Single(r=>r.X==-1);
            var file=Path.Combine(folder,remote.Hash+".chunk");
            var bytes=File.ReadAllBytes(file);
            File.WriteAllText(file,"broken chunk");
            try { stream.Update(new(-1,0),palette); throw new Exception("accepted corrupt page"); }
            catch(InvalidDataException) { Check(!stream.Contains(new(-1,0)),"a corrupt page is rejected only when requested"); }
            File.WriteAllBytes(file,bytes); stream.Update(new(-1,0),palette);
            Check(stream.Map.Get(left)==1 && stream.Map.DisplayTile(palette,left,1)==3,"return reloads page data from disk");
            stream.Update(null,palette);
            Check(stream.State.Resident==0 && !stream.Map.OccupiedCells().Any(),"missing focus releases every resident cell");
        }
        // Hexes have two additional edge directions; isometric uses the two logical axes.
        var hex=new TilePalette("",palette.Tiles);
        hex.Variants.Add(1,Enumerable.Range(1,64).ToArray());
        map=source with {Chunks=[],Layout=TileGrid.HexFlat};
        map.Set(new(0,0),1); map.Set(new(1,-1),1);
        Check(map.DisplayTile(hex,new(0,0),1)==33 && map.DisplayTile(hex,new(1,-1),1)==5,"opposite axial hex edges choose complementary masks");
        map.Layout=TileGrid.Isometric;map.Set(new(1,0),1);
        Check(map.DisplayTile(palette,new(0,0),1)==3,"isometric adjacency uses shared grid edges");

        var store=new PlayerStorage(Path.Combine(scratch,"tileworld-profile"));
        using var session=new GameSession(scene,null,null,store);
        var runner=scene.Objects.Single(o=>o.Name=="Runner");
        session.Current.TryGetTileStream(ground.Id,out var initial);
        Check(initial.Resident==3 && initial.Total==9 && initial.Loads==3,"only initial focus window is resident before first tick");
        const float step=1f/60;
        session.Actions.Press("Right");
        for (int i=0;i<147;i++) session.FixedUpdate(session.Actions.Take(),step);
        session.Actions.Release("Right");session.Actions.Press("Left");
        for (int i=0;i<147;i++) session.FixedUpdate(session.Actions.Take(),step);
        session.Actions.Release("Left");
        var outbound=store.LoadSlot(2,1);var back=store.LoadSlot(3,1);
        session.Current.TryGetPosition(runner.Id,out var pos);
        Check(outbound?.GetBool("passed")==true && back?.GetBool("passed")==true,
            $"runner crosses two boundaries and returns; residency and physics swap both ways (x={pos.X:F1})");
        Check(source.Chunks.Length==9,"Play leaves authored pages intact");
    }
    private static void Check(bool ok,string detail)
    {
        if (!ok) throw new Exception("Tile world: "+detail);
        Console.WriteLine("PASS tile world: "+detail);
    }
}
