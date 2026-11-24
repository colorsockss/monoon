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
    }
    private void StepCameras(float seconds)
    {
        foreach (var b in cameras)
        {
            var c=b.Camera!;
            b.CameraStart=b.CameraEnd;
            b.CameraZoomStart=b.CameraZoomEnd;
            if (!b.Active) continue;
            var target=b.Position;
            float zoom=c.Zoom;
            if (c.FrameGroup is {} group)
            {
                if (!FrameTargets(c,group,out target,out zoom)) { b.CameraTracked=false; continue; }
            }
            else if (c.Follow is Guid id)
            {
                if (!byId.TryGetValue(id,out var follow) || !follow.Active) { b.CameraTracked=false; continue; }
                target=Affine2D.Transform(follow.Size*.5f,follow.MatrixAt(follow.Position));
            }
            var goal=target;
            if (c.Tracks)
            {
                // Lead by the tracked point's velocity over the last tick, then hold while the goal stays in the dead zone.
                if (c.LookAhead>0 && b.CameraTracked && seconds>0) goal+=(target-b.CameraTarget)*(c.LookAhead/seconds);
                b.CameraTarget=target;b.CameraTracked=true;
                goal=new(Hold(goal.X,b.CameraStart.X,c.DeadZoneWidth*.5f),Hold(goal.Y,b.CameraStart.Y,c.DeadZoneHeight*.5f));
            }
            // Apply the player's final post-collision transform, independent of scene object order.
            float alpha=!c.Tracks || c.HalfLife==0?1:(float)(1-Math.Exp(-Math.Log(2)*seconds/c.HalfLife));
            b.CameraZoomEnd=c.FrameGroup==null?c.Zoom:b.CameraZoomStart+(zoom-b.CameraZoomStart)*alpha;
            b.CameraEnd=c.View(LogicalSize,Affine2D.Lerp(b.CameraStart,goal,alpha),b.CameraZoomEnd).Center;
        }
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
        var view=c.FrameGroup==null ? c.View(LogicalSize,center) :
            c.View(LogicalSize,center,b.CameraZoomStart+(b.CameraZoomEnd-b.CameraZoomStart)*fraction);
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
        { view=Sample(b,1);return true; }
        view=default;return false;
    }
}
