using System.Drawing;
using System.Numerics;
using System.Text.Json.Serialization;
using VYaml.Annotations;

namespace Engine.Core;

// Local coordinates are pixels, Y points down; positive angles turn clockwise.
public sealed partial class SceneObject
{
    internal bool UsesAffine => Rotation != 0 || ScaleX != 1 || ScaleY != 1 || PivotX != 0 || PivotY != 0 || Shear != 0;
    public float Rotation { get; set; }
    public float ScaleX { get; set; } = 1;
    public float ScaleY { get; set; } = 1;
    public float PivotX { get; set; }
    public float PivotY { get; set; }
    // Two differently oriented, nonuniform scales can shear a basis. Keep this term
    // when reparenting so all four corners (and descendants) stay put.
    public float Shear { get; set; }

    [JsonIgnore, YamlIgnore] public Matrix3x2 LocalMatrix
    {
        get
        {
            var linear = new Matrix3x2(ScaleX, 0, Shear, ScaleY, 0, 0) *
                Matrix3x2.CreateRotation(float.DegreesToRadians(Rotation % 360));
            var pivot = new Vector2(PivotX * Width, PivotY * Height);
            linear.Translation = new Vector2(X,Y) + (pivot - Vector2.TransformNormal(pivot, linear));
            return linear;
        }
    }

    [JsonIgnore, YamlIgnore] public Matrix3x2 WorldMatrix
    {
        get
        {
            var m = LocalMatrix;
            for (var p = Parent; p != null; p = p.Parent) m *= p.LocalMatrix;
            return m;
        }
    }
    [JsonIgnore, YamlIgnore] public Vector2 WorldPosition => WorldMatrix.Translation;
    [JsonIgnore, YamlIgnore] public float WorldRotation => Affine2D.Angle(WorldMatrix);
    public Vector2 LocalToWorld(Vector2 point) => Vector2.Transform(point, WorldMatrix);
    public Vector2 WorldToLocal(Vector2 point) => Vector2.Transform(point, Affine2D.Inverse(WorldMatrix));
    public bool ContainsWorldPoint(Vector2 point)
    {
        var p = WorldToLocal(point);
        return p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;
    }
    internal void SetOrigin(Vector2 origin)
    {
        var p = new Vector2(PivotX * Width, PivotY * Height);
        var xy = origin + (Vector2.TransformNormal(p, LocalMatrix) - p);
        X = xy.X; Y = xy.Y;
    }
    internal void SetLocalMatrix(Matrix3x2 m)
    {
        var axis = new Vector2(m.M11, m.M12);
        var sx = MathF.CopySign(axis.Length(), ScaleX);
        var u = axis / sx;
        var v = new Vector2(m.M21, m.M22);
        ScaleX = sx;
        ScaleY = Vector2.Dot(v, new(-u.Y, u.X));
        Shear = Vector2.Dot(v, u);
        Rotation = float.RadiansToDegrees(MathF.Atan2(u.Y, u.X));
        SetOrigin(m.Translation);
    }
    internal SceneObject RuntimeCopy() => (SceneObject)MemberwiseClone();
    internal SceneObject CopyTransform(Guid id) => new() { Id = id, Name = Name, X = X, Y = Y,
        Width = Width, Height = Height, Rotation = Rotation, ScaleX = ScaleX, ScaleY = ScaleY,
        PivotX = PivotX, PivotY = PivotY, Shear = Shear };
}

public static class Affine2D
{
    public static float Angle(Matrix3x2 m) => float.RadiansToDegrees(MathF.Atan2(m.M12, m.M11));
    public static Matrix3x2 Inverse(Matrix3x2 m) => Matrix3x2.Invert(m, out var inv) && Finite(inv) ? inv :
        throw new InvalidDataException("Transform 2D needs a finite, invertible scale.");
    internal static bool Finite(Matrix3x2 m) => float.IsFinite(m.M11) && float.IsFinite(m.M12) &&
        float.IsFinite(m.M21) && float.IsFinite(m.M22) && float.IsFinite(m.M31) && float.IsFinite(m.M32);
    public static RectangleF Bounds(RectangleF r, Matrix3x2 m)
    {
        var a = Vector2.Transform(new(r.Left,r.Top),m);
        var b = Vector2.Transform(new(r.Right,r.Top),m);
        var c = Vector2.Transform(new(r.Right,r.Bottom),m);
        var d = Vector2.Transform(new(r.Left,r.Bottom),m);
        var min = Vector2.Min(Vector2.Min(a,b),Vector2.Min(c,d));
        var max = Vector2.Max(Vector2.Max(a,b),Vector2.Max(c,d));
        return RectangleF.FromLTRB(min.X,min.Y,max.X,max.Y);
    }
}
