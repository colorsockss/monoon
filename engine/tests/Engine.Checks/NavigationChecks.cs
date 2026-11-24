using Engine.Core;
using System.Numerics;

internal static class NavigationChecks
{
    [Check(3200, StopAfter = "nav")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    public static void Run(string repository,string scratch)
    {
        SightAndClearance();
        var scene=SceneDocument.Load(Path.Combine(repository,"samples","PlatformerLab","Scenes","Platform.scene"));
        var enemy=scene.Objects.Single(o=>o.Name=="Lookout").Id;
        var region=scene.Objects.Single(o=>o.Name=="Walk region").Id;
        var runner=scene.Objects.Single(o=>o.Name=="Runner").Id;
        var store=new PlayerStorage(Path.Combine(scratch,"nav-profile"));
        using var session=new GameSession(scene,null,null,store);
        var game=session.Current;
        var route=game.FindWalkPath(region,enemy,new(270,288));
        Check(route.Count>4 && route.Select(n=>n.Support).Distinct().Count()>=2,"planner connects the patrol deck and incline");
        Check(!game.CanSee(enemy,runner,154),"player starts outside sight");
        Check(game.FindWalkPath(region,enemy,new(750,240)).Count==0,"unconnected landing across the shuttle gap has no walk route");
        const float dt=1f/60;
        void Advance(int ticks)
        {
            for(int i=0;i<ticks;i++) session.FixedUpdate(session.Actions.Take(),dt);
        }
        Advance(140);
        game.TryGetPosition(enemy,out var e);
        Check(e.X>385 && e.X<441,"guard patrols without a target");
        session.Actions.Press("Right");Advance(94);session.Actions.Release("Right");Advance(140);
        game.TryGetPosition(enemy,out e);
        Check(e.X<335 && e.Y>221,$"chase moves the physical enemy down the slope ({e.X}, {e.Y})");
        session.Actions.Press("Left");Advance(123);session.Actions.Release("Left");Advance(540);
        var data=store.LoadSlot(4,1);
        Check(data?.GetBool("passed")==true && data.GetNumber("supportSegments")>=2 &&
            data.GetText("sequence")=="Patrol > Chase > Return > Patrol","lost player returns guard to the patrol deck and resumes walking");
    }
    private static void SightAndClearance()
    {
        var scene=SceneDocument.Empty(new());
        SceneObject Box(string name,int x,int y,int w,int h,bool solid=false)
        {
            var o=new SceneObject {Name=name,X=x,Y=y,Width=w,Height=h};
            if(solid)o.AddComponent(new BoxCollider2DComponent());
            scene.Objects.Add(o);return o;
        }
        var region=Box("Navigation region",0,0,220,118);
        region.AddComponent(new NavigationRegion2DComponent { AgentWidth=14,AgentHeight=20,Spacing=10 });
        var floor=Box("Floor",0,95,220,20,true);
        var agent=Box("Agent",20,75,14,20);
        var target=Box("Target",160,75,14,20);
        var wall=Box("Barrier",90,60,3,35,true);
        using var game=new SceneGame(scene);
        var goal=new Vector2(167,95);
        Check(!game.CanSee(agent.Id,target.Id,200) && game.CanSee(agent.Id,target.Id,200,0),"solid wall occludes sight and mask can exclude it");
        Check(game.FindWalkPath(region.Id,agent.Id,goal).Count==0,"a thin blocking wall cannot be walked through");
        game.SetActive(wall.Id,false);
        Check(game.CanSee(agent.Id,target.Id,200) && game.FindWalkPath(region.Id,agent.Id,goal).Count>5,"next plan uses current active colliders");
        game.SetActive(floor.Id,false);
        Check(game.FindWalkPath(region.Id,agent.Id,goal).Count==0,"unsupported route disappears");
        game.SetActive(target.Id,false);
        Check(!game.CanSee(agent.Id,target.Id,200),"inactive target is invisible");
    }
    private static void Check(bool value,string label)
    {
        if(!value)throw new Exception("Navigation: "+label);
        Console.WriteLine("PASS navigation: "+label);
    }
}
