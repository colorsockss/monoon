using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject] public sealed partial record BoneKey(float Phase, float Degrees);
[YamlObject] public sealed partial record RigBone(string Name, string? Parent, float X, float Y, float Angle,
    float Length, float Width, string Color, BoneKey[]? Keys=null);

[YamlObject]
public sealed partial record SkeletonRig(RigBone[] Bones)
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
    }
}

public enum SkeletonClock { Time, Travel }

[DisplayName("Skeleton Animator 2D"), YamlObject, EngineFeature(EngineFeature.Skeletons)]
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

public readonly record struct BonePose2D(Vector2 Start, Vector2 End);
