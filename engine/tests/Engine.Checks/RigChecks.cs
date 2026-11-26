using System.Numerics;
using Engine.Core;

internal static class RigChecks
{
    [Check(2600, StopAfter = "rig")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    internal static void Run(string repo,string scratch)
    {
        const float dt=1f/60;
        var scene=SceneDocument.Load(Path.Combine(repo,"samples/PlatformerLab/Scenes/Showcase.scene"));
        var runner=scene.Objects.Single(o=>o.Name=="Runner");
        var deck=scene.Objects.Single(o=>o.Name=="Shuttle");
        var store=new PlayerStorage(Path.Combine(scratch,"rig-profile"));
        using var session=new GameSession(scene,null,null,store);
        void Step() => session.FixedUpdate(session.Actions.Take(),dt);
        Step();session.Actions.Press("R"); Step();session.Actions.Release("R");
        for(int i=0;i<505;i++) Step();
        var data=store.LoadSlot(5,1);
        Check(data?.GetBool("passed")==true,$"occupied shuttle travels out and back ({data?.ToYaml()})");
        Check(data!.GetNumber("outbound")>133.99 && data.GetNumber("maxRideSlip")<.05 &&
            Math.Abs(data.GetNumber("returned"))<.01,"rider stays on the kinematic tween through both reversals");
        Check(deck.X==470 && runner.X==78,"preview does not edit authored transforms");
        // The Time-clocked jog must move the legs while every joint stays attached and every bone keeps its authored length.
        // The rig figure left the PlatformerLab showcase (user decision 2026-11-26); fixtures/RigRunner holds its Runner.rig.
        string fixture=Path.Combine(repo,"engine/tests/fixtures/RigRunner"), project=Path.Combine(scratch,"rig-runner");
        foreach(var file in Directory.EnumerateFiles(fixture,"*",SearchOption.AllDirectories))
        {
            var copy=Path.Combine(project,Path.GetRelativePath(fixture,file));
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(file,copy,true);
        }
        var figure=new SceneObject {Name="Rig runner",Width=22,Height=30};
        figure.AddComponent(new SkeletonAnimator2DComponent {Rig=Guid.Parse("125e32af-6a4f-40ca-acb2-c88f5fc833ce"),Clock=SkeletonClock.Time,Period=.7f});
        var stage=SceneDocument.Empty(new());stage.ProjectDirectory=project;
        stage.Objects.Add(figure);
        using var jog=new GameSession(stage,null,null,new PlayerStorage(Path.Combine(scratch,"rig-runner-profile")));
        Vector2? firstKnee=null;bool moved=false, attached=true;
        for(int sample=0;sample<6;sample++)
        {
            for(int i=0;i<7;i++) jog.FixedUpdate(jog.Actions.Take(),dt);
            attached&=jog.Current.TryGetBonePose(figure.Id,"RearThigh",out var thigh) &&
                jog.Current.TryGetBonePose(figure.Id,"RearShin",out var shin) &&
                jog.Current.TryGetBonePose(figure.Id,"RearFoot",out var foot) &&
                Vector2.Distance(thigh.End,shin.Start)<.001f && Vector2.Distance(shin.End,foot.Start)<.001f &&
                Math.Abs(Vector2.Distance(thigh.Start,thigh.End)-13)<.001f && Math.Abs(Vector2.Distance(shin.Start,shin.End)-12)<.001f &&
                Math.Abs(Vector2.Distance(foot.Start,foot.End)-7)<.001f;
            firstKnee??=thigh.End;
            moved|=Vector2.Distance(firstKnee.Value,thigh.End)>1;
        }
        Check(attached,"six jog samples keep the rear leg's joints connected and its bone lengths fixed");
        Check(moved,"the Time clock swings the rear thigh");
        var rig=SkeletonRig.Load(Path.Combine(fixture,"Assets/Runner.rig"));
        bool rejected=false;
        try {(rig with {Bones=rig.Bones.Reverse().ToArray()}).Validate("order");}catch(InvalidDataException){rejected=true;}
        Check(rejected,"cyclic or forward parent links are rejected before playback");

        var single=new PositionTween2DComponent {OffsetX=80,OffsetY=0,Duration=1,Ease=TweenEase.EaseIn,Repeat=TweenRepeat.Once};
        var tween=new PositionTween(single);
        var parent=Matrix3x2.CreateRotation(MathF.PI/2)*Matrix3x2.CreateTranslation(10,20);
        var p=parent.Translation;
        p+=tween.Advance(p,parent,.5f);
        Check(Vector2.Distance(p,new(10,40))<.001f,"half-time Ease In advances one quarter in the parent's local direction");
        p+=tween.Advance(p,parent,1.2f);
        Check(!tween.Playing && Vector2.Distance(p,new(10,100))<.001f,"one-shot tween clamps to the endpoint across a long step");
        var ping=new PositionTween(single with {Repeat=TweenRepeat.PingPong,Ease=TweenEase.Linear});
        var displacement=ping.Advance(default,Matrix3x2.Identity,5.5f);
        Check(Vector2.Distance(displacement,new(40,0))<.001f,"ping-pong preserves time across multiple reversals");
    }

    private static void Check(bool success,string label)
    {
        if(!success) throw new Exception("Rig: "+label);
        Console.WriteLine("PASS rig: "+label);
    }
}
