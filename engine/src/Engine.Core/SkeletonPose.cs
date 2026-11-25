using System.Drawing;
using System.Numerics;

namespace Engine.Core;

// Angles interpolate in bone-local space. Forward kinematics reconstructs attached joints at render time.
internal sealed class SkeletonPose
{
    private readonly SkeletonRig rig;
    private readonly Dictionary<string,int> indices=new(StringComparer.Ordinal);
    private readonly int[] parents;
    private readonly Color[] colors;
    private readonly Matrix3x2[] pose, render;
    private float[] angles, previous;
    private double phase;

    public SkeletonPose(SkeletonRig rig)
    {
        this.rig=rig;
        int count=rig.Bones.Length;
        parents=new int[count]; colors=new Color[count];
        pose=new Matrix3x2[count]; render=new Matrix3x2[count];
        angles=new float[count]; previous=new float[count];
        for (int i=0;i<count;i++)
        {
            var bone=rig.Bones[i];
            parents[i]=bone.Parent==null ? -1 : indices[bone.Parent];
            indices.Add(bone.Name,i);
            SceneObject.TryColor(bone.Color,out colors[i]);
        }
        SampleAnimation();
        Array.Copy(angles,previous,count);
    }

    private static float Shortest(float radians) => MathF.IEEERemainder(radians,MathF.Tau);
    private static float Sample(BoneKey[]? keys,float phase)
    {
        if (keys==null || keys.Length==0) return 0;
        if (keys.Length==1) return keys[0].Degrees;
        int next=0;
        while (next<keys.Length && keys[next].Phase<=phase) next++;
        if(next==keys.Length) next=0;
        int prior=(next+keys.Length-1)%keys.Length;
        float start=keys[prior].Phase, end=keys[next].Phase;
        if (next==0) {end++; if(phase<start) phase++;}
        return keys[prior].Degrees+MathF.IEEERemainder(keys[next].Degrees-keys[prior].Degrees,360)*(phase-start)/(end-start);
    }

    private void SampleAnimation()
    {
        for(int i=0;i<angles.Length;i++)
        {
            var bone=rig.Bones[i];
            angles[i]=float.DegreesToRadians(bone.Angle+Sample(bone.Keys,(float)phase));
            Forward(i,angles[i],pose);
        }
    }
    private void Forward(int i,float angle,Matrix3x2[] output)
    {
        var b=rig.Bones[i];
        var local=Matrix3x2.CreateRotation(angle);
        local.Translation=new(b.X,b.Y);
        output[i]=parents[i]<0?local:local*output[parents[i]];
    }

    internal void Step(SceneGame.Body owner,float dt)
    {
        (angles,previous)=(previous,angles);
        var config=owner.SkeletonConfig!;
        var matrix=owner.MatrixAt(owner.Position);
        var inverse=Affine2D.Inverse(matrix);
        Vector2 displacement=owner.Position-owner.At(0);
        if (owner.MotionOwner?.Support is {} support)
        {
            var oldPose=support.MatrixAt(support.At(0),support.StartAngle);
            var carried=Affine2D.Transform(Affine2D.Transform(owner.At(0),Affine2D.Inverse(oldPose)),support.MatrixAt(support.Position));
            displacement-=carried-owner.At(0);
        }
        float travel=Affine2D.TransformNormal(displacement,inverse).X;
        phase+=config.Clock==SkeletonClock.Travel ? travel/config.Stride : dt/config.Period;
        phase-=Math.Floor(phase);
        SampleAnimation();
    }

    internal bool TryGet(string name,Matrix3x2 world,out BonePose2D result)
    {
        if (indices.TryGetValue(name,out int i))
        {
            var m=pose[i]*world;
            result=new(m.Translation,Affine2D.Transform(new(rig.Bones[i].Length,0),m));
            return true;
        }
        result=default;return false;
    }

    internal void Draw(ICanvas2D canvas,Matrix3x2 world,Matrix3x2 view,RectangleF visible,float fraction)
    {
        for(int i=0;i<angles.Length;i++)
        {
            Forward(i,previous[i]+Shortest(angles[i]-previous[i])*fraction,render);
            var b=rig.Bones[i];
            if (b.Width==0) continue;
            var m=render[i]*world;
            var rect=new RectangleF(0,-b.Width*.5f,b.Length,b.Width);
            if (visible.IntersectsWith(Affine2D.Bounds(rect,m))) canvas.DrawAffine(null,rect,m*view,colors[i]);
        }
    }
}

public sealed partial class SceneGame
{
    private Body[] skeletons=[];
    public bool TryGetBonePose(Guid id,string bone,out BonePose2D pose)
    {
        if(byId.TryGetValue(id,out var b) && b.Active && b.Skeleton!=null)
            return b.Skeleton.TryGet(bone,b.MatrixAt(b.Position),out pose);
        pose=default;return false;
    }
}
