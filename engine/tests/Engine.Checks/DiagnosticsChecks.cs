using System.Drawing;
using System.Numerics;
using Engine.Core;

internal static class DiagnosticsChecks
{
    [Check(5400, "diagnostics")] static void Registered(CheckRun c) => Run(c.Project);
    private sealed class Canvas : ICanvas2D, IDiagnosticLines
    {
        public readonly List<(Vector2 A,Vector2 B,Color Color)> Lines=[];
        public void Clear(Color _) {}
        public void FillRectangle(RectangleF _,Color c) {}
        public bool DrawSprite(string path,RectangleF r)=>true;
        public void DrawLine(Vector2 a,Vector2 b,float width,Color color)=>Lines.Add((a,b,color));
        public IDisposable PushClip(RectangleF r)=>new Scope();
        private sealed class Scope:IDisposable {public void Dispose(){}}
    }
    internal static void Run(string project)
    {
        void Check(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS diagnostics: "+what);}
        var scene=SceneDocument.Empty(new(400,200));
        var c=scene.Camera!.GetComponent<Camera2DComponent>()!;
        c.Viewport=new(.5f,0,.5f,1);
        var thing=new SceneObject{Name="Probe",X=150,Y=100,Width=10,Height=10};
        thing.AddComponent(new BoxCollider2DComponent());
        var wall=new SceneObject{Name="Wall",X=200,Y=99,Width=10,Height=18};
        wall.AddComponent(new CircleCollider2DComponent{IsTrigger=true});
        scene.Objects.AddRange([thing,wall]);
        using var world=new SceneGame(scene);
        var canvas=new Canvas();
        world.Draw(canvas,1);Check(canvas.Lines.Count==0,"no debug drawing by default");
        world.Diagnostics=new(){Queries=true,Colliders=true,Selection=thing.Id};
        world.Draw(canvas,1);
        world.TryGetView(scene.Camera.Id,out var view);
        var mapped=view.WorldToScreen(new(thing.X,thing.Y));
        Check(canvas.Lines.Count(l=>l.Color==Color.SpringGreen)==4 && canvas.Lines.Count(l=>l.Color==Color.HotPink)==32 &&
            canvas.Lines.Any(l=>l.A==mapped),"physics outlines use the camera viewport and distinguish solid and trigger");
        Check(!world.TryDebugRay(thing.Id,new(10,10),out _) &&
             world.TryDebugRay(thing.Id,view.WorldToScreen(new(250,105)),out var hit) && hit?.Target.ObjectId==wall.Id &&
             world.Diagnostics.Latest is {Kind:QueryKind.Raycast,Hit:{} h} && h==hit.Value,
             "selection ray respects viewport, ignores its owner and records the actual trigger hit");
        var hits=world.Overlap(Shape2D.Box(new(149,99,12,12)));
        Check(world.Diagnostics.Latest is {Kind:QueryKind.Overlap,Count:1,Target:{} t} && t==hits.Single(),"overlap trace matches query results");
        for(int i=0;i<100;i++) world.Sweep(Shape2D.Circle(Vector2.Zero,1),Vector2.UnitX);
        Check(world.Diagnostics.Latest is {Kind:QueryKind.Sweep,Count:0} && world.Diagnostics.Recent(0).Count()==PhysicsDiagnostics.Capacity &&
            !world.Diagnostics.Recent(PhysicsDiagnostics.VisibleTicks+1).Any(),"bounded query history records misses and expires by simulation tick");
        world.Diagnostics.Queries=false;
        world.Raycast(Vector2.Zero,Vector2.UnitX,20);
        Check(world.Diagnostics.Latest==null && world.Inspect(wall.Id)?.Active==true && world.SetActive(wall.Id,false) && world.Inspect(wall.Id)?.ActiveSelf==false,
            "turning off queries clears traces and live inspection reads runtime activation");

        using var keyroom=new GameSession(SceneDocument.Load(Path.Combine(project,"Scenes","Showcase.scene")));
        var live=keyroom.Current;live.Diagnostics=new(){Queries=true};
        var player=live.Objects.Single(o=>o.Name=="Player");
        var initial=live.Inspect(player.Id)!.Value.Position;
        keyroom.FixedUpdate(Vector2.UnitX,1f/60);
        Check(live.Diagnostics.Latest is {Kind:QueryKind.Movement} && live.Inspect(player.Id)!.Value.Position.X>initial.X &&
            player.WorldPosition==initial,"movement sweep and live world position do not mutate the scene");
        var table=ProjectAssets.ReadManifest(project);
        var id=live.Instantiate(new(table.IdFor("Assets/Showcase/Coin.prefab")!.Value),new(50,50));
        Check(live.Inspect(id)?.SpawnedFrom=="Assets/Showcase/Coin.prefab" && live.Destroy(id) && live.Inspect(id)==null,"spawned prefab origin and destruction are visible immediately");
        GameLogEntry? logged=null;keyroom.State.Logged+=e=>logged=e;
        keyroom.State.Log("test warning",GameLogLevel.Warning);
        Check(logged is {Level:GameLogLevel.Warning,Message:"test warning",Line:>0},"runtime log keeps severity and caller location");
        RunGameLog(Check);
    }

    // Player logs go through Brutal.Logging but must keep the console fallback's exact lines and streams.
    private static void RunGameLog(Action<bool,string> check)
    {
        var state=new SessionState();
        Engine.Desktop.GameLog.Attach(state);
        var (stdout,stderr)=(Console.Out,Console.Error);
        var (output,errors)=(new StringWriter(),new StringWriter());
        Console.SetOut(output);Console.SetError(errors);
        try
        {
            state.Log("SPATIAL count=1 {braces} ünï  x");
            state.Log("careful",GameLogLevel.Warning);
            state.Log("broken",GameLogLevel.Error);
            Engine.Desktop.GameLog.Error("Scene failed");
        }
        finally {Console.SetOut(stdout);Console.SetError(stderr);}
        string n=Environment.NewLine;
        check(output.ToString()==$"SPATIAL count=1 {{braces}} ünï  x{n}careful{n}" && errors.ToString()==$"broken{n}Scene failed{n}",
            "GameLog writes script logs unchanged, errors to stderr");
        check(!Brutal.Logging.LogCategory.Get("Sdl").IsCategoryEnabled,"GameLog keeps BRUTAL's per-call SDL trace off");
    }
}
