using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public sealed partial class SceneGame
{
    private Body[] cameras=[], overlays=[];
    private void IndexCameras()
    {
        cameras=bodies.Where(b=>b.Camera!=null).ToArray();
        overlays=cameras.Where(b=>b.Camera!.Output!=Camera2DComponent.Primary)
            .OrderBy(b=>b.Camera!.Output==Camera2DComponent.Minimap).ThenBy(b=>b.Camera!.Order).ToArray();
        foreach (var b in cameras)
        {
            if (b.CameraReady) continue;
            b.CameraHome=b.CameraStart=b.CameraEnd=b.Camera!.View(LogicalSize,b.Position).Center;
            b.CameraZoomStart=b.CameraZoomEnd=b.Camera.Zoom;
            b.CameraReady=true;
        }
        // Each Extra camera pairs with the first Primary camera that splits with it; pairs keep their state across reindexing.
        splits=cameras.Where(b=>b.Camera is { Output:Camera2DComponent.Primary, SplitWith:Guid id } && byId.TryGetValue(id,out var p) &&
                p.Camera?.Output==Camera2DComponent.Extra)
            .Select(b=>b.Split?.Main==b ? b.Split : new SplitPair(b,byId[b.Camera!.SplitWith!.Value])).DistinctBy(s=>s.Partner).ToArray();
        foreach (var b in cameras) b.Split=null;
        foreach (var s in splits) s.Main.Split=s.Partner.Split=s;
        // A new pair starts in the state its targets' distance asks for, without blending in.
        foreach (var s in splits) if (!s.Ready && Distance(s,out float d,out bool left))
        { s.Apart=d>s.Main.Camera!.SplitDistance; s.Start=s.End=s.Apart?1:0; s.MainLeft=left; s.Ready=true; }
    }
    private bool Distance(SplitPair s,out float distance,out bool mainLeft)
    {
        distance=0;mainLeft=true;
        if (!s.Main.Active || !s.Partner.Active || !Target(s.Main,out var a,out _) || !Target(s.Partner,out var b,out _)) return false;
        float dx=a.X-b.X,dy=a.Y-b.Y;
        distance=MathF.Sqrt(dx*dx+dy*dy);mainLeft=a.X<=b.X;
        return true;
    }

    private SplitPair[] splits=[];
    /// <summary>A Primary camera and its SplitWith partner. Amount 0 is one merged view, 1 two halves of their viewports' union.</summary>
    internal sealed class SplitPair
    {
        public readonly Body Main, Partner;
        public readonly RectangleF Union;
        public float Start, End;
        public bool Apart, Ready, MainLeft=true;
        public Vector2 Middle;
        public SplitPair(Body main,Body partner)
        {
            (Main,Partner)=(main,partner);
            CameraRect a=main.Camera!.Viewport??main.Camera.DefaultViewport(),b=partner.Camera!.Viewport??partner.Camera.DefaultViewport();
            float left=Math.Min(a.X,b.X),top=Math.Min(a.Y,b.Y);
            Union=new(left,top,Math.Max(a.X+a.Width,b.X+b.Width)-left,Math.Max(a.Y+a.Height,b.Y+b.Height)-top);
        }
        // Smoothstep of the amount; the partner opens a strip on its side and the main view keeps the rest.
        public RectangleF Rect(Body b,float amount)
        {
            float strip=Union.Width*.5f*(amount*amount*(3-2*amount)),rest=Union.Width-strip;
            bool left=(b==Main)==MainLeft;
            return b==Main ? Union with { X=left?Union.X:Union.X+strip, Width=rest } : Union with { X=left?Union.X:Union.X+rest, Width=strip };
        }
    }

    // Merge or split each pair on the targets' distance (with the gap between the two distances as hysteresis), then blend.
    private void StepSplits(float seconds)
    {
        foreach (var s in splits)
        {
            var c=s.Main.Camera!;
            s.Start=s.End;
            Vector2 a=default;
            bool main=s.Main.Active && Target(s.Main,out a,out _);
            // Without its partner the main view widens to the whole union and follows its own target.
            if (main && !s.Partner.Active) { s.Apart=false; s.Ready=true; s.Middle=a; }
            else if (main && Target(s.Partner,out var b,out _))
            {
                float dx=a.X-b.X,dy=a.Y-b.Y,d=MathF.Sqrt(dx*dx+dy*dy);
                if (!s.Ready) { s.Apart=d>c.SplitDistance; s.Start=s.End=s.Apart?1:0; s.Ready=true; }
                else if (s.Apart ? d<c.MergeDistance : d>c.SplitDistance) s.Apart=!s.Apart;
                if (s.End==0) s.MainLeft=a.X<=b.X; // Sides are chosen while merged and kept until the views merge again.
                s.Middle=new((a.X+b.X)*.5f,(a.Y+b.Y)*.5f);
            }
            float to=s.Apart?1:0;
            s.End=c.BlendSeconds<=0 ? to : Math.Clamp(s.End+(s.Apart?seconds:-seconds)/c.BlendSeconds,0,1);
        }
    }
    // A paired camera aims between the pair's midpoint, seen from where its viewport sits in the merged view, and its own goal.
    private Vector2 SplitGoal(Body b,SplitPair s,Vector2 goal)
    {
        float amount=s.End,eased=amount*amount*(3-2*amount);
        var rect=s.Rect(b,amount);
        float offset=(rect.X+rect.Width*.5f-(s.Union.X+s.Union.Width*.5f))*LogicalSize.Width/b.Camera!.Zoom;
        return Affine2D.Lerp(new(s.Middle.X+offset,s.Middle.Y),goal,eased);
    }
    private Rectangle Port(Body b,float fraction) => b.Split is {} s ?
        Camera2DComponent.Port(LogicalSize,s.Rect(b,s.Start+(s.End-s.Start)*fraction)) : b.Camera!.Port(LogicalSize);
    // A merged pair draws only the main view; so does one whose partner strip is still under half a logical pixel wide.
    private bool Merged(Body b,float fraction) => b.Split is {} s && s.Partner==b &&
        s.Rect(b,s.Start+(s.End-s.Start)*fraction).Width*LogicalSize.Width<.5f;
    private void StepCameras(float seconds)
    {
        StepSplits(seconds);
        foreach (var b in cameras)
        {
            var c=b.Camera!;
            b.CameraStart=b.CameraEnd;
            b.CameraZoomStart=b.CameraZoomEnd;
            if (!b.Active) continue;
            if (!Target(b,out var target,out float zoom)) { b.CameraTracked=false; continue; }
            var goal=target;
            if (c.Tracks)
            {
                // Lead by the tracked point's velocity over the last tick, then hold while the goal stays in the dead zone.
                if (c.LookAhead>0 && b.CameraTracked && seconds>0) goal+=(target-b.CameraTarget)*(c.LookAhead/seconds);
                b.CameraTarget=target;b.CameraTracked=true;
                if (b.Split is {} pair) goal=SplitGoal(b,pair,goal);
                goal=new(Hold(goal.X,b.CameraStart.X,c.DeadZoneWidth*.5f),Hold(goal.Y,b.CameraStart.Y,c.DeadZoneHeight*.5f));
            }
            // Apply the player's final post-collision transform, independent of scene object order.
            float alpha=!c.Tracks || c.HalfLife==0?1:(float)(1-Math.Exp(-Math.Log(2)*seconds/c.HalfLife));
            b.CameraZoomEnd=c.FrameGroup==null?c.Zoom:b.CameraZoomStart+(zoom-b.CameraZoomStart)*alpha;
            b.CameraEnd=c.View(Port(b,1),Affine2D.Lerp(b.CameraStart,goal,alpha),b.CameraZoomEnd).Center;
        }
    }
    // The point a camera tracks (its own position without Follow or Frame); false when no target is active.
    private bool Target(Body b,out Vector2 target,out float zoom)
    {
        var c=b.Camera!;
        target=b.Position;zoom=c.Zoom;
        if (c.FrameGroup is {} group) return FrameTargets(c,group,out target,out zoom);
        if (c.Follow is not Guid id) return true;
        if (!byId.TryGetValue(id,out var follow) || !follow.Active) return false;
        target=Affine2D.Transform(follow.Size*.5f,follow.MatrixAt(follow.Position));
        return true;
    }
    private static float Hold(float goal,float center,float half) =>
        goal>center+half ? goal-half : goal<center-half ? goal+half : center;

    // The world bounds of Follow and every active member of the group; false (keep the last view) when none is active.
    private bool FrameTargets(Camera2DComponent c,string group,out Vector2 center,out float zoom)
    {
        float left=float.PositiveInfinity,top=float.PositiveInfinity,right=float.NegativeInfinity,bottom=float.NegativeInfinity;
        if (c.Follow is Guid id && byId.TryGetValue(id,out var follow) && follow.Active) Include(follow,ref left,ref top,ref right,ref bottom);
        var members=FindByGroup(group);
        for (int i=0;i<members.Count;i++)
            if (byId.TryGetValue(members[i],out var m) && m.Active && m.Camera==null) Include(m,ref left,ref top,ref right,ref bottom);
        center=default;zoom=c.Zoom;
        if (left>right) return false;
        center=new((left+right)*.5f,(top+bottom)*.5f);
        var port=c.Port(LogicalSize);
        float pad=c.FramePadding*2;
        zoom=Math.Clamp(Math.Min(port.Width/(right-left+pad),port.Height/(bottom-top+pad)),Camera2DComponent.MinZoom,c.Zoom);
        return true;
    }
    private static void Include(Body b,ref float left,ref float top,ref float right,ref float bottom)
    {
        var r=Affine2D.Bounds(new RectangleF(0,0,b.Size.X,b.Size.Y),b.MatrixAt(b.Position));
        left=Math.Min(left,r.Left);top=Math.Min(top,r.Top);right=Math.Max(right,r.Right);bottom=Math.Max(bottom,r.Bottom);
    }
    private CameraView Sample(Body b,float fraction)
    {
        var c=b.Camera!;
        // Fixed/parented cameras retain the original interpolated motion path (including slides).
        var center=!c.Tracks?b.At(fraction):Affine2D.Lerp(b.CameraStart,b.CameraEnd,fraction);
        var view=c.View(Port(b,fraction),center,c.FrameGroup==null ? c.Zoom : b.CameraZoomStart+(b.CameraZoomEnd-b.CameraZoomStart)*fraction);
        if (!IntegerScale) return view;
        // Whole game pixels: sprites keep their texel grid instead of shimmering between rows while the camera moves.
        var world=view.World;
        world.X=MathF.Round(world.X*view.Zoom)/view.Zoom;
        world.Y=MathF.Round(world.Y*view.Zoom)/view.Zoom;
        return view with { World=world };
    }
    public bool IntegerScale => settings.IntegerScale == true;
    public bool TryGetView(Guid id,out CameraView view)
    {
        if (byId.TryGetValue(id,out var b) && b.Active && b.Camera!=null)
        {
            // A merged partner reports the shared view it is part of.
            view=Sample(Merged(b,1) && b.Split!.Main.Active ? b.Split.Main : b,1);
            return true;
        }
        view=default;return false;
    }
}
