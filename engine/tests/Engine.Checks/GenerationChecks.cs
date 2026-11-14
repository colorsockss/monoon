using System.Drawing;
using System.Numerics;
using System.Security.Cryptography;
using Engine.Core;

internal static class GenerationChecks
{
    private static void Check(bool result, string message)
    {
        if (!result) throw new Exception("Generation: " + message);
        Console.WriteLine("PASS generation: " + message);
    }

    private static byte[] Tiles(MazeLayout m) =>
        (from y in Enumerable.Range(0, m.Height) from x in Enumerable.Range(0, m.Width)
         select (byte)(new Point(x,y) == m.Exit ? 4 : new Point(x,y) == m.Entrance ? 3 : m.IsPassage(x,y) ? 2 : 1)).ToArray();

    private static Dictionary<Point,int> Distances(MazeLayout m)
    {
        var distance = new Dictionary<Point,int> { [m.Entrance] = 0 };
        Queue<Point> queue = new(); queue.Enqueue(m.Entrance);
        while (queue.TryDequeue(out var p))
            foreach (var q in new Point[] {new(p.X+1,p.Y),new(p.X,p.Y+1),new(p.X-1,p.Y),new(p.X,p.Y-1)})
                if (m.IsPassage(q.X,q.Y) && distance.TryAdd(q, distance[p]+1)) queue.Enqueue(q);
        return distance;
    }

    private sealed class HudCapture : ICanvas2D, IDisposable
    {
        public readonly List<string> Lines=[];
        public IDisposable PushClip(RectangleF area) => this;
        public void Dispose() { }
        public void Clear(Color color) => Lines.Clear();
        public void FillRectangle(RectangleF rect,Color color) { }
        public bool DrawSprite(string path,RectangleF rect) => true;
        public void DrawText(string value,float x,float y,float size,Color color) => Lines.Add(value);
    }

    internal static void Run(string repository)
    {
        var rng = new SeededRandom(0);
        Check(rng.NextUInt64() == 0xE220A8397B1DCDAFUL && rng.NextUInt64() == 0x6E789E6AA1B965F4UL &&
              rng.NextUInt64() == 0x06C45D188009454FUL, "SplitMix64 integer stream matches the frozen v1 vector");
        ulong branch = rng.Fork(7).NextUInt64();
        for (int i=0;i<80;i++) rng.NextInt(int.MaxValue);
        Check(rng.Fork(7).NextUInt64()==branch && rng.Fork(8).NextUInt64()!=branch && rng.NextInt(1)==0,
            "named numeric streams are independent of draws from their parent");
        bool invalidBound=false; try { rng.NextInt(0); } catch(ArgumentOutOfRangeException) { invalidBound=true; }
        Check(invalidBound, "zero-sized random range rejected");
        var fixedMap=MazeLayout.Generate(12,7,42);
        Check(Convert.ToHexString(SHA256.HashData(Tiles(fixedMap))) == "BA09B9F295679FDA51BE7BF8D1D84E898170AD2E57ED5375FE22ECCC90700E1C",
            "seed 42, layout v1: all 375 tile IDs match the golden map");
        bool connected=true;
        foreach (var (w,h) in new[]{(1,12),(16,1),(2,2),(12,7),(45,33)})
        foreach (ulong seed in new ulong[]{0,1,7,104729,uint.MaxValue,ulong.MaxValue})
        {
            var m=MazeLayout.Generate(w,h,seed);
            var d=Distances(m);
            int expectedPassages=2*w*h-1;
            connected &= d.Count==expectedPassages && Tiles(m).Count(t=>t!=1)==expectedPassages &&
                d.Keys.All(p=>p.X>0 && p.X<m.Width-1 && p.Y>0 && p.Y<m.Height-1) &&
                d[m.Exit]==d.Values.Max() && m.Exit!=m.Entrance;
        }
        Check(connected, "thin and rectangular mazes keep a closed border, span every room and exit at maximum distance");

        var scene=SceneDocument.Load(Path.Combine(repository,"samples/SeedLab/Scenes/Survey.scene"));
        var board=scene.Objects.Single(o=>o.Name=="Board");
        var authored=board.GetComponent<Tilemap2DComponent>()!;
        using var session=new GameSession(scene);
        var game=session.Current;
        void Step() => session.FixedUpdate(session.Actions.Take(),1f/60);
        void Press(string key) { session.Actions.Press(key);Step();session.Actions.Release(key);Step(); }
        Guid Pawn() => game.Objects.Single(o=>o.Name=="Explorer").Id;
        Vector2 Position() => game.TryGetPosition(Pawn(),out var pos)?pos:throw new Exception("Missing explorer");
        bool Wall(int x,int y) => game.Physics.Overlap(Shape2D.Circle(new(24+24*x+12,64+24*y+12),1))
            .Any(hit=>hit.ObjectId==board.Id);
        bool Matches(MazeLayout m) => Enumerable.Range(0,m.Height).All(y=>
            Enumerable.Range(0,m.Width).All(x=>Wall(x,y)!=m.IsPassage(x,y)));
        var initial=MazeLayout.Generate(12,7,104729);
        Step();Check(Matches(initial),"all physics cells follow the generated board");
        Press("N");Check(Matches(MazeLayout.Generate(12,7,104730)),"next seed replaces old walls and passages");
        Press("P");Check(Matches(initial),"previous seed restores every collision cell");
        Guid before=Pawn();
        var ui=game.Ui;
        var field=scene.Objects.Single(o=>o.Name=="Seed field");
        void Apply(string value)
        {
            ui.SetText(field.Id,value);
            ui.Pointer(true,700,220);ui.Pointer(false,700,220);Step();Step();
        }
        Apply("4294967296");
        Check(Pawn()==before && Matches(initial),"out-of-range seed leaves the map and current player alone");
        Apply("42");
        Check(Pawn()!=before && Matches(fixedMap),"custom seed replaces the player exactly once and installs the new map");
        var pos=Position();session.Actions.Press("A");for(int i=0;i<45;i++)Step();session.Actions.Release("A");Step();
        Check(Position()==pos,"a blocked grid turn leaves the explorer centered beside the generated wall");
        Press("R");Check(Matches(fixedMap) && Vector2.Distance(pos,Position())<.01f,"rebuild reproduces the collision map and resets the pawn");

        // Follow an independently computed shortest path with ordinary player input.
        var distances=Distances(fixedMap);
        var route=new Stack<Point>();
        for (var p=fixedMap.Exit; p!=fixedMap.Entrance;)
        {
            route.Push(p);
            int depth=distances[p];
            p=new Point[]{new(p.X-1,p.Y),new(p.X+1,p.Y),new(p.X,p.Y-1),new(p.X,p.Y+1)}
                .First(q=>distances.TryGetValue(q,out int prior) && prior==depth-1);
        }
        var current=fixedMap.Entrance;
        foreach(var next in route)
        {
            string key=next.X>current.X?"D":next.X<current.X?"A":next.Y>current.Y?"S":"W";
            session.Actions.Press(key);for (int i=0;i<10;i++) Step();session.Actions.Release(key);Step();
            current=next;
        }
        var hud=new HudCapture(); ui.Draw(hud);
        Check(Vector2.Distance(Position()+new Vector2(7),
                new Vector2(24+24*fixedMap.Exit.X+12,64+24*fixedMap.Exit.Y+12))<.2f &&
            hud.Lines.Any(s=>s.StartsWith("Exit reached!")), "the explorer can walk the generated route and activate the gold exit");

        game.ReplaceTiles(board.Id,34,1,Enumerable.Repeat(1,34).ToArray());
        try { game.ReplaceTiles(board.Id,2,1,[0,999]);throw new Exception("unknown tile accepted"); }
        catch (ArgumentException) { }
        Check(Wall(33,0) && Wall(0,0) && !Wall(0,1),"multi-chunk substitution and failed update are atomic");
        game.ReplaceTiles(board.Id,1,1,[0]);
        Check(!Wall(33,0) && !Wall(0,0) && authored.Chunks.Length==0,"zeros clear the runtime map; the authored layer remains empty");
    }
}
