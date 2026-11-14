using System.Drawing;
using System.Numerics;

namespace Engine.Core;

/// <summary>World-space shape for physics queries. A polygon must be ordered and strictly convex.</summary>
public readonly struct Shape2D
{
    internal readonly Vector2[]? Vertices;
    internal readonly Vector2 Offset;
    internal readonly float Radius;
    internal readonly RectangleF Bounds;
    private Shape2D(Vector2[]? vertices, Vector2 offset, float radius, RectangleF bounds)
        => (Vertices,Offset,Radius,Bounds)=(vertices,offset,radius,bounds);
    internal static Shape2D PointAt(Vector2 p) => new(null,p,0,new(p.X,p.Y,0,0));
    public static Shape2D Circle(Vector2 center, float radius)
    {
        if (!Finite(center) || !(radius > 0) || !float.IsFinite(radius)) throw new ArgumentException("Expected a finite center and positive radius.");
        return new(null,center,radius,new(center.X-radius,center.Y-radius,2*radius,2*radius));
    }
    public static Shape2D Box(RectangleF bounds)
    {
        if (!(bounds.Width>0 && bounds.Height>0)) throw new ArgumentException("Expected positive box dimensions.");
        return Polygon([new(bounds.Left,bounds.Top),new(bounds.Right,bounds.Top),new(bounds.Right,bounds.Bottom),new(bounds.Left,bounds.Bottom)]);
    }
    public static Shape2D Polygon(params ReadOnlySpan<Vector2> points)
    {
        if (!CollisionGeometry.Convex(points)) throw new ArgumentException("Expected 3–32 finite vertices outlining a strictly convex polygon.");
        return Hull(points.ToArray());
    }
    internal static Shape2D Hull(Vector2[] p)
    {
        Vector2 min=p[0],max=min;
        foreach (var v in p) { min=Vector2.Min(v,min); max=Vector2.Max(v,max); }
        return new(p,Vector2.Zero,0,RectangleF.FromLTRB(min.X,min.Y,max.X,max.Y));
    }
    internal Shape2D Translated(Vector2 t) { var b=Bounds; b.Offset(t.X,t.Y); return new(Vertices,Offset+t,Radius,b); }
    internal static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);
    internal void Check() { if (Vertices==null && Radius==0) throw new ArgumentException("Use a Shape2D factory."); }
}

/// <summary>Mask selects target layers. Triggers are excluded by default; Ignore omits one owner.</summary>
public sealed record PhysicsFilter(uint Mask = uint.MaxValue, bool IncludeTriggers = false, Guid? Ignore = null);
public readonly record struct PhysicsTarget(Guid ObjectId, int Layer, bool IsTrigger, Point? Tile);
/// <summary>Fraction is in [0,1]. Normal points out of the target. Initial overlap returns zero distance.</summary>
public readonly record struct PhysicsHit(PhysicsTarget Target, float Fraction, float Distance, Vector2 Point, Vector2 Normal);
public interface IPhysics2D
{
    IReadOnlyList<PhysicsTarget> Overlap(Shape2D shape, PhysicsFilter? filter = null);
    PhysicsHit? Raycast(Vector2 origin, Vector2 direction, float distance, PhysicsFilter? filter = null);
    PhysicsHit? Sweep(Shape2D shape, Vector2 displacement, PhysicsFilter? filter = null);
}
