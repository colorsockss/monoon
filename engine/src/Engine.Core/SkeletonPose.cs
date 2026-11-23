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
    private readonly bool[] soles;
    private readonly Matrix3x2[] pose, render;
    private float[] angles, previous;
    private readonly (int upper,int lower,int foot,FootChain config)[] legs;
    private readonly Guid?[] ground;
    private readonly bool[] planted;
    private double phase;
    private float swingWeight;

    public SkeletonPose(SkeletonRig rig)
    {
        this.rig=rig;
        int count=rig.Bones.Length;
        parents=new int[count]; colors=new Color[count]; soles=new bool[count];
        pose=new Matrix3x2[count]; render=new Matrix3x2[count];
        ground=new Guid?[count]; planted=new bool[count]; angles=new float[count]; previous=new float[count];
        for (int i=0;i<count;i++)
        {
            var bone=rig.Bones[i];
            parents[i]=bone.Parent==null ? -1 : indices[bone.Parent];
            indices.Add(bone.Name,i);
            SceneObject.TryColor(bone.Color,out colors[i]);
        }
        legs=(rig.Feet??[]).Select(f=>(indices[f.Upper],indices[f.Lower],indices[f.Foot],f)).ToArray();
        foreach (var leg in legs) soles[leg.foot]=true;
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
    private float ParentAngle(int bone) => parents[bone]<0?0:MathF.Atan2(pose[parents[bone]].M12,pose[parents[bone]].M11);

    internal void Step(SceneGame game,SceneGame.Body owner,float dt)
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
        float want=config.Clock==SkeletonClock.Time || Math.Abs(travel)>.0001f ? 1 : 0;
        swingWeight+=Math.Clamp(want-swingWeight,-dt*10,dt*10);
        SampleAnimation();
        Array.Clear(ground); Array.Clear(planted);
        if (owner.FootIK is not {} ik || owner.MotionOwner is {Support:null}) return;
        foreach (var (upper,lower,foot,leg) in legs)
        {
            float p=(float)((phase+leg.Phase)%1);
            float s=Math.Clamp((p-.5f)*2,0,1);
            // Stance cancels the body's travel. The swing catches up by exactly one stride.
            float offset=config.Stride*(.25f-p+s*s*(3-2*s));
            float lift=ik.Lift*MathF.Sin(MathF.PI*s)*swingWeight;
            var home=Affine2D.Transform(new(leg.HomeX+offset,leg.HomeY),matrix);
            var hit=game.Raycast(home-new Vector2(0,ik.Probe),Vector2.UnitY,2*ik.Probe,owner.FootFilter);
            if (hit is not {} surface || surface.Normal.Y>=-.05f) continue;
            var contact=home+new Vector2(0,surface.Distance-ik.Probe);
            var target=Affine2D.Transform(contact+surface.Normal*lift,inverse);
            var hip=pose[upper].Translation;
            var delta=target-hip;
            float a=rig.Bones[upper].Length,b=rig.Bones[lower].Length;
            float distance=Math.Clamp(delta.Length(),Math.Max(.0001f,Math.Abs(a-b)),a+b);
            float along=(a*a-b*b+distance*distance)/(2*distance);
            var axis=delta.LengthSquared()>1e-10f ? Vector2.Normalize(delta) : Vector2.UnitY;
            var knee=axis*along+new Vector2(axis.Y,-axis.X)*(leg.Bend*MathF.Sqrt(Math.Max(0,a*a-along*along)));
            var shin=axis*distance-knee;
            float thighAngle=MathF.Atan2(knee.Y,knee.X);
            float shinAngle=MathF.Atan2(shin.Y,shin.X);
            angles[upper]=thighAngle-ParentAngle(upper);
            angles[lower]=shinAngle-thighAngle;
            var tangent=Affine2D.TransformNormal(new(-surface.Normal.Y,surface.Normal.X),inverse);
            angles[foot]=MathF.Atan2(tangent.Y,tangent.X)-shinAngle;
            // Rebuild descendants including attachments and the bases of any later chains.
            for (int i=upper;i<angles.Length;i++) Forward(i,angles[i],pose);
            ground[foot]=surface.Target.ObjectId;
            planted[foot]=lift<.01f && Vector2.DistanceSquared(pose[foot].Translation,target)<.0025f;
        }
    }

    internal bool TryGet(string name,Matrix3x2 world,out BonePose2D result)
    {
        if (indices.TryGetValue(name,out int i))
        {
            var m=pose[i]*world;
            result=new(m.Translation,Affine2D.Transform(new(rig.Bones[i].Length,0),m),ground[i],planted[i]);
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
            var rect=soles[i]?new RectangleF(-b.Length*.4f,-b.Width,b.Length,b.Width):
                new RectangleF(0,-b.Width*.5f,b.Length,b.Width);
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
