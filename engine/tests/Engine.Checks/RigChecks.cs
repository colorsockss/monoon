using System.Numerics;
using Engine.Core;

internal static class RigChecks
{
    internal static void Run(string repo,string scratch)
    {
        const float dt=1f/60;
        var scene=SceneDocument.Load(Path.Combine(repo,"samples/PlatformerLab/Scenes/Platform.scene"));
        var runner=scene.Objects.Single(o=>o.Name=="Runner");
        var deck=scene.Objects.Single(o=>o.Name=="Shuttle");
        var store=new PlayerStorage(Path.Combine(scratch,"rig-profile"));
        using var session=new GameSession(scene,null,null,store);
        void Step() => session.FixedUpdate(session.Actions.Take(),dt);
        Step();session.Actions.Press("R"); Step();session.Actions.Release("R");
        for(int i=0;i<505;i++) Step();
        var data=store.LoadSlot(5,1);
        Check(data?.GetBool("passed")==true,$"both stance soles stay on the incline; swing clears it; occupied shuttle travels out and back ({data?.ToYaml()})");
        Check(data!.GetNumber("outbound")>133.99 && data.GetNumber("maxRideSlip")<.05 &&
            Math.Abs(data.GetNumber("returned"))<.01,"rider stays on the kinematic tween through both reversals");
        session.Actions.Press("X");Step();session.Actions.Release("X");
        session.Actions.Press("Space");Step();session.Actions.Release("Space");
        Check(session.Current.TryGetBonePose(runner.Id,"RearFoot",out var airborne) && airborne.Ground==null,"jump releases IK contact");
        Check(session.Current.TryGetBonePose(runner.Id,"RearThigh",out var thigh) &&
            session.Current.TryGetBonePose(runner.Id,"RearShin",out var shin) &&
            Vector2.Distance(thigh.End,shin.Start)<.001f && Vector2.Distance(shin.End,airborne.Start)<.001f &&
            Math.Abs(Vector2.Distance(shin.Start,shin.End)-12)<.001f,"airborne animation keeps joints connected and bone lengths fixed");
        Check(deck.X==470 && runner.X==78,"preview does not edit authored transforms");
        var rig=SkeletonRig.Load(Path.Combine(repo,"samples/PlatformerLab/Assets/Runner.rig"));
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
