using Engine.Core;
using Engine.Editor;
using System.Drawing;
using System.Numerics;

internal static class TileWorldChecks
{
    [Check(3300)] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    // The Ridge autotile fixture (an edge brush #1, a different brush #2) copied to scratch, so the asset scan writes no manifest
    // into the repository.
    internal static (Tilemap2DComponent Map, TilePalette Palette) Ridge(string repository,string scratch)
    {
        var root=Path.Combine(scratch,"ridge-fixture");
        var source=Path.Combine(repository,"engine","tests","fixtures","PlatformerRidge","Assets");
        Directory.CreateDirectory(Path.Combine(root,"Assets"));
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file,Path.Combine(root,"Assets",Path.GetFileName(file)),true);
        var assets=ProjectAssets.Scan(root);
        var id=assets.IdFor("Assets/Ridge.tileset")!.Value;
        return (new Tilemap2DComponent { Tileset=id, CellSize=new(8,12), StreamRadius=1 },TilePalette.Load(root,assets,id));
    }

    public static void Run(string repository,string scratch)
    {
        var (source,palette) = Ridge(repository,scratch);
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

        Streaming(repository);
    }

    // The ShowcaseStream ridge: seven 32-cell pages that stream around the scripted runner (radius 1). Pages load ahead of him
    // and unload behind, and the tile collider exists only for resident pages.
    private static void Streaming(string repository)
    {
        var scene=SceneDocument.Load(Path.Combine(repository,"samples","PlatformerLab","Scenes","ShowcaseStream.scene"));
        var ground=scene.Objects.Single(o=>o.Name=="Ground");
        var runner=scene.Objects.Single(o=>o.Name=="Runner");
        var authored=ground.GetComponent<Tilemap2DComponent>()!;
        int pages=authored.Chunks.Length;
        const float page=32*16, step=1f/60;
        using var session=new GameSession(scene);
        var game=session.Current;
        game.TryGetTileStream(ground.Id,out var initial);
        Check(initial.Resident==InitialResident && initial.Total==pages && initial.Loads==initial.Resident && game.IsTileChunkLoaded(ground.Id,0,0) &&
            !game.IsTileChunkLoaded(ground.Id,pages-1,0),$"only the focus window is resident before the first tick ({initial})");
        // A probe inside the far page's ground surface (row 22 is the floor of every page).
        bool Solid(int chunk) => game.Physics.Overlap(Shape2D.Circle(new(chunk*page+page/2,22*16+8),2)).Any(h=>h.ObjectId==ground.Id);
        Check(!Solid(pages-1),"an unloaded page has no tile collider");
        int maxResident=0, lastCenter=0;bool window=true, follows=true;float lowest=0;
        for (int i=0;i<1500;i++)
        {
            session.FixedUpdate(session.Actions.Take(),step);
            game.TryGetPosition(runner.Id,out var at);
            game.TryGetTileStream(ground.Id,out var state);
            int here=(int)((at.X+11)/page);
            maxResident=Math.Max(maxResident,state.Resident);
            lowest=Math.Max(lowest,at.Y);
            // Resident pages are exactly the radius-1 window around the stream centre, which stays on the runner's page or the one
            // he just left.
            for (int c=0;c<pages;c++) window&=game.IsTileChunkLoaded(ground.Id,c,0)==(Math.Abs(c-state.Center.X)<=1);
            follows&=state.Center.X==here || state.Center.X==here-1;
            lastCenter=state.Center.X;
        }
        game.TryGetPosition(runner.Id,out var end);
        game.TryGetTileStream(ground.Id,out var final);
        Check(end.X>=3392 && lowest<352,$"the runner crosses every page on streamed ground without falling (x={end.X:F1}, lowest y={lowest:F1})");
        Check(window && follows && lastCenter==pages-1 && maxResident==3,
            $"the resident window follows the runner to the last page: one page ahead loaded, older pages unloaded ({maxResident} at most)");
        Check(final.Loads>=pages && final.Unloads>=pages-3 && Solid(pages-1) && !Solid(0),
            $"streaming swapped residency and collision along the ridge ({final})");
        Check(authored.Chunks.Length==pages && ground.GetComponent<Tilemap2DComponent>()==authored,"Play leaves authored pages intact");
    }
    const int InitialResident=2; // pages 0 and 1: the runner starts in page 0 and there is no page -1
    private static void Check(bool ok,string detail)
    {
        if (!ok) throw new Exception("Tile world: "+detail);
        Console.WriteLine("PASS tile world: "+detail);
    }
}
