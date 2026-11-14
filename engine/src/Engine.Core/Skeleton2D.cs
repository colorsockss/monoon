using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject] public sealed partial record BoneKey(float Phase, float Degrees);
[YamlObject] public sealed partial record RigBone(string Name, string? Parent, float X, float Y, float Angle,
    float Length, float Width, string Color, BoneKey[]? Keys=null);
[YamlObject] public sealed partial record FootChain(string Upper, string Lower, string Foot,
    float HomeX, float HomeY, float Phase, int Bend);

[YamlObject]
public sealed partial record SkeletonRig(RigBone[] Bones, FootChain[]? Feet=null)
{
    public const string Extension=".rig";
    public static SkeletonRig Load(string file)
    {
        var rig=ProjectYaml.Read<SkeletonRig>(file);
        rig.Validate(file);
        return rig;
    }
    internal void Validate(string file)
    {
        if (Bones is not {Length:>0 and <=256}) throw new InvalidDataException($"{file}: expected 1–256 bones.");
        var names=new Dictionary<string,RigBone>(StringComparer.Ordinal);
        foreach (var b in Bones)
        {
            if (b==null || string.IsNullOrWhiteSpace(b.Name) || b.Parent!=null && !names.ContainsKey(b.Parent) ||
                !names.TryAdd(b.Name,b) || !Shape2D.Finite(new(b.X,b.Y)) || !float.IsFinite(b.Angle) ||
                b.Length is not (>0 and <=16384) || b.Width is not (>=0 and <=4096) || !SceneObject.TryColor(b.Color,out _))
                throw new InvalidDataException($"{file}: bones need unique names, parent-first order, finite transforms and a valid color.");
            float previous=-1;
            foreach (var k in b.Keys??[])
            {
                if(k==null || k.Phase is not (>=0 and <1) || k.Phase<=previous || !float.IsFinite(k.Degrees))
                    throw new InvalidDataException($"{file}: invalid animation keys on {b.Name}.");
                previous=k.Phase;
            }
        }
        var used=new HashSet<string>();
        foreach(var leg in Feet??[])
        {
            if (leg==null || leg.Upper==null || leg.Lower==null || leg.Foot==null ||
                !names.TryGetValue(leg.Upper,out var a) || !names.TryGetValue(leg.Lower,out var b) || !names.TryGetValue(leg.Foot,out var c) ||
                b.Parent!=a.Name || c.Parent!=b.Name || b.X!=a.Length || b.Y!=0 || c.X!=b.Length || c.Y!=0 ||
                !used.Add(a.Name) || !used.Add(b.Name) || !used.Add(c.Name) || leg.Bend is not (-1 or 1) ||
                leg.Phase is not (>=0 and <1) || !Shape2D.Finite(new(leg.HomeX,leg.HomeY)))
                throw new InvalidDataException($"{file}: each foot needs a distinct connected two-bone chain ending at the sole.");
        }
    }
}

public enum SkeletonClock { Time, Travel }

[DisplayName("Skeleton Animator 2D"), YamlObject]
public sealed partial record SkeletonAnimator2DComponent : SceneComponent
{
    public SkeletonAnimator2DComponent() { }
    [YamlConstructor] public SkeletonAnimator2DComponent(float period=1,float stride=28) => (Period,Stride)=(period,stride);
    public Guid? Rig {get;set;}
    public int SortingLayer {get;set;}
    public int Order {get;set;}
    public SkeletonClock Clock {get;set;}
    public float Period {get;set;}=1;
    public float Stride {get;set;}=28;
    internal void Validate(string owner)
    {
        if (!Enum.IsDefined(Clock) || Period is not (>=.001f and <=86400) || Stride is not (>=.1f and <=16384))
            throw new InvalidDataException($"{owner}: invalid skeleton clock, cycle duration or stride.");
    }
}

[DisplayName("Foot IK 2D"), YamlObject]
public sealed partial record FootIK2DComponent : SceneComponent
{
    public FootIK2DComponent() { }
    [YamlConstructor] public FootIK2DComponent(float probe=22,float lift=5,uint mask=uint.MaxValue) => (Probe,Lift,Mask)=(probe,lift,mask);
    public float Probe {get;set;}=22;
    public float Lift {get;set;}=5;
    public uint Mask {get;set;}=uint.MaxValue;
    internal void Validate(string owner)
    {
        if (Probe is not (>.01f and <=16384) || Lift is not (>=0 and <=4096))
            throw new InvalidDataException($"{owner}: invalid IK probe distance or step height.");
    }
}

public readonly record struct BonePose2D(Vector2 Start, Vector2 End, Guid? Ground, bool Planted);
