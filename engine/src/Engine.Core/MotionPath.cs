using System.Numerics;

namespace Engine.Core;

// A body may hit one wall, slide along it, then stop at a second wall within one tick.
internal readonly record struct MotionPath(Vector2 Start, Vector2 Bend, float BendTime, Vector2 End, float EndTime)
{
    public Vector2 At(float time)
    {
        if (time >= EndTime) return End;
        if (time < BendTime) return Affine2D.Lerp(Start, Bend, time / BendTime);
        return Affine2D.Lerp(Bend, End, (time - BendTime) / (EndTime - BendTime));
    }

    public float NextBreak(float time) => time < BendTime ? BendTime : time < EndTime ? EndTime : 1;
}
