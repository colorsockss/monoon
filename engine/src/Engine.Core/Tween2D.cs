using System.ComponentModel;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

public enum TweenEase { Linear, EaseIn, EaseOut, EaseInOut }
public enum TweenRepeat { Once, PingPong }

[DisplayName("Position Tween 2D"), YamlObject]
public sealed partial record PositionTween2DComponent : SceneComponent
{
    public PositionTween2DComponent() { }
    [YamlConstructor] public PositionTween2DComponent(float offsetX=100, float duration=1, bool playOnStart=true,
        TweenEase ease=TweenEase.EaseInOut, TweenRepeat repeat=TweenRepeat.PingPong) =>
        (OffsetX,Duration,PlayOnStart,Ease,Repeat)=(offsetX,duration,playOnStart,ease,repeat);
    public float OffsetX { get; set; }=100;
    public float OffsetY { get; set; }
    public float Duration { get; set; }=1;
    public bool PlayOnStart { get; set; }=true;
    public TweenEase Ease { get; set; }=TweenEase.EaseInOut;
    public TweenRepeat Repeat { get; set; }=TweenRepeat.PingPong;
    internal void Validate(SceneObject owner)
    {
        if (!float.IsFinite(OffsetX) || !float.IsFinite(OffsetY) || Duration is not (>=.001f and <=86400) ||
            !Enum.IsDefined(Ease) || !Enum.IsDefined(Repeat) ||
            owner.GetComponent<Rigidbody2DComponent>() is {BodyType:not BodyType2D.Kinematic})
            throw new InvalidDataException($"{owner.Name}: position tween requires a transform or kinematic body and a positive duration.");
    }
}

internal sealed class PositionTween(PositionTween2DComponent config)
{
    private Vector2? origin;
    private double elapsed;
    public bool Playing { get; set; }=config.PlayOnStart;
    public Vector2 Advance(Vector2 position, Matrix3x2 parent, float dt)
    {
        if (!Playing) return default;
        origin ??= Vector2.Transform(position,Affine2D.Inverse(parent));
        elapsed+=dt;
        double t;
        if (config.Repeat==TweenRepeat.PingPong)
        {
            elapsed%=2d*config.Duration;
            t=1-Math.Abs(1-elapsed/config.Duration);
        }
        else
        {
            t=Math.Min(1,elapsed/config.Duration);
            if (t==1) Playing=false;
        }
        double eased=config.Ease switch {
            TweenEase.EaseIn => t*t, TweenEase.EaseOut => t*(2-t),
            TweenEase.EaseInOut => .5-.5*Math.Cos(t*Math.PI), _=>t
        };
        var target=origin.Value+new Vector2(config.OffsetX,config.OffsetY)*(float)eased;
        return Vector2.Transform(target,parent)-position;
    }
}

public sealed partial class SceneGame
{
    public bool PlayTween(Guid id)
    {
        if (!byId.TryGetValue(id,out var b) || !b.Active || b.Tween==null) return false;
        b.Tween.Playing=true;
        return true;
    }
}
