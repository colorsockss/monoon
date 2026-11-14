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
            b.CameraStart=b.CameraEnd=b.Camera!.View(LogicalSize,b.Position).Center;
            b.CameraReady=true;
        }
    }
    private void StepCameras(float seconds)
    {
        foreach (var b in cameras)
        {
            var c=b.Camera!;
            b.CameraStart=b.CameraEnd;
            if (!b.Active) continue;
            var target=b.Position;
            if (c.Follow is Guid id)
            {
                if (!byId.TryGetValue(id,out var follow) || !follow.Active) continue;
                target=Vector2.Transform(follow.Size*.5f,follow.MatrixAt(follow.Position));
            }
            // Apply the player's final post-collision transform, independent of scene object order.
            float alpha=c.Follow==null || c.HalfLife==0?1:(float)(1-Math.Exp(-Math.Log(2)*seconds/c.HalfLife));
            b.CameraEnd=c.View(LogicalSize,Vector2.Lerp(b.CameraStart,target,alpha)).Center;
        }
    }
    private CameraView Sample(Body b,float fraction)
    {
        var c=b.Camera!;
        // Fixed/parented cameras retain the original interpolated motion path (including slides).
        var center=c.Follow==null?b.At(fraction):Vector2.Lerp(b.CameraStart,b.CameraEnd,fraction);
        return c.View(LogicalSize,center);
    }
    public bool TryGetView(Guid id,out CameraView view)
    {
        if (byId.TryGetValue(id,out var b) && b.Active && b.Camera!=null)
        { view=Sample(b,1);return true; }
        view=default;return false;
    }
}
