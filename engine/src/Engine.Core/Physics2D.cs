using System.Drawing;
using System.Numerics;

namespace Engine.Core;

/// <summary>World-space shape for physics queries. A polygon must be ordered and strictly convex, with 3–8 vertices
/// (the Box2D polygon limit). A capsule is a rounded segment; an edge collider is a chain of segments.</summary>
[EngineFeature(EngineFeature.Physics)]
public readonly struct Shape2D
{
    internal readonly Vector2[]? Vertices;
    internal readonly Vector2 Offset;
    internal readonly float Radius;
    internal readonly RectangleF Bounds;
    internal readonly Vector2 FaceNormal;
    // Box2D polygon in the coordinates of Vertices; the shape sits at Offset.
    internal readonly CollisionGeometry.Hull? Poly;
    // An edge chain: its segments relative to Offset (Vertices holds the polyline). Parts.Length == Vertices.Length for a loop.
    internal readonly Shape2D[]? Parts;
    private Shape2D(Vector2[]? vertices, Vector2 offset, float radius, RectangleF bounds, Vector2 faceNormal = default,
        CollisionGeometry.Hull? poly = null, Shape2D[]? parts = null)
        => (Vertices,Offset,Radius,Bounds,FaceNormal,Poly,Parts)=(vertices,offset,radius,bounds,faceNormal,poly,parts);
    internal static Shape2D Face(Vector2 a, Vector2 b, Vector2 normal) => new([a,b],default,0,
        RectangleF.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)),normal);
    internal static Shape2D PointAt(Vector2 p) => new(null,p,0,new(p.X,p.Y,0,0));
    public static Shape2D Circle(Vector2 center, float radius)
    {
        if (!Finite(center) || !(radius > 0) || !float.IsFinite(radius)) throw new ArgumentException("Expected a finite center and positive radius.");
        return new(null,center,radius,new(center.X-radius,center.Y-radius,2*radius,2*radius));
    }
    /// <summary>A capsule: the segment from a to b, rounded by radius. Coincident ends make a circle.</summary>
    public static Shape2D Capsule(Vector2 a, Vector2 b, float radius)
    {
        if (!Finite(a) || !Finite(b) || !(radius > 0) || !float.IsFinite(radius)) throw new ArgumentException("Expected finite ends and a positive radius.");
        return CapsuleOf(a, b, radius);
    }
    internal static Shape2D CapsuleOf(Vector2 a, Vector2 b, float radius)
    {
        // Box2D needs a capsule axis longer than its linear slop (0.005 px).
        if (Vector2.DistanceSquared(a, b) <= 1e-4f) return Circle((a + b) * .5f, radius);
        var box = RectangleF.FromLTRB(Math.Min(a.X,b.X)-radius,Math.Min(a.Y,b.Y)-radius,Math.Max(a.X,b.X)+radius,Math.Max(a.Y,b.Y)+radius);
        return new([a,b],Vector2.Zero,radius,box,default,new(a,b,radius));
    }
    // Segments shorter than 0.01 px are dropped: Box2D cannot give them a normal.
    internal static Shape2D Chain(Vector2[] points, bool loop)
    {
        var kept = new List<Vector2>(points.Length);
        foreach (var p in points) if (kept.Count == 0 || Vector2.DistanceSquared(kept[^1], p) > 1e-4f) kept.Add(p);
        if (loop && kept.Count > 1 && Vector2.DistanceSquared(kept[^1], kept[0]) <= 1e-4f) kept.RemoveAt(kept.Count - 1);
        loop &= kept.Count >= 3;
        var p2 = kept.ToArray();
        var parts = new Shape2D[loop ? p2.Length : Math.Max(0, p2.Length - 1)];
        Vector2 min = p2.Length > 0 ? p2[0] : default, max = min;
        for (int i = 0; i < parts.Length; i++)
        {
            Vector2 a = p2[i], b = p2[(i + 1) % p2.Length];
            parts[i] = new([a,b],Vector2.Zero,0,RectangleF.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)),default,new(a,b,0));
        }
        foreach (var v in p2) { min=Vector2.Min(v,min); max=Vector2.Max(v,max); }
        return new(p2,Vector2.Zero,0,RectangleF.FromLTRB(min.X,min.Y,max.X,max.Y),default,null,parts);
    }
    public static Shape2D Box(RectangleF bounds)
    {
        if (!(bounds.Width>0 && bounds.Height>0)) throw new ArgumentException("Expected positive box dimensions.");
        return Polygon([new(bounds.Left,bounds.Top),new(bounds.Right,bounds.Top),new(bounds.Right,bounds.Bottom),new(bounds.Left,bounds.Bottom)]);
    }
    public static Shape2D Polygon(params ReadOnlySpan<Vector2> points)
    {
        if (!CollisionGeometry.Convex(points)) throw new ArgumentException("Expected 3–8 finite vertices outlining a strictly convex polygon.");
        return Hull(points.ToArray());
    }
    internal static Shape2D Hull(Vector2[] p)
    {
        Vector2 min=p[0],max=min;
        foreach (var v in p) { min=Vector2.Min(v,min); max=Vector2.Max(v,max); }
        return new(p,Vector2.Zero,0,RectangleF.FromLTRB(min.X,min.Y,max.X,max.Y),default,new(p));
    }
    internal Shape2D Translated(Vector2 t) { var b=Bounds; b.Offset(t.X,t.Y); return new(Vertices,Offset+t,Radius,b,FaceNormal,Poly,Parts); }
    internal static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);
    internal void Check() { if (Vertices==null && Radius==0) throw new ArgumentException("Use a Shape2D factory."); }
}

/// <summary>Mask selects target layers. Triggers are excluded by default; Ignore omits one owner.</summary>
public sealed record PhysicsFilter(uint Mask = uint.MaxValue, bool IncludeTriggers = false, Guid? Ignore = null);
public readonly record struct PhysicsTarget(Guid ObjectId, int Layer, bool IsTrigger, Point? Tile);
/// <summary>Fraction is in [0,1]. Normal points out of the target. Initial overlap returns zero distance.</summary>
public readonly record struct PhysicsHit(PhysicsTarget Target, float Fraction, float Distance, Vector2 Point, Vector2 Normal);
/// <summary>Broad-phase diagnostics for one world. Tile cells are queried separately.</summary>
public sealed class PhysicsQueryDiagnostics
{
    public bool UseSpatialIndex { get; set; } = true;
    public int LastColliderCandidates { get; internal set; }
}

[EngineFeature(EngineFeature.Physics)]
public interface IPhysics2D
{
    PhysicsQueryDiagnostics QueryDiagnostics { get; }
    IReadOnlyList<PhysicsTarget> Overlap(Shape2D shape, PhysicsFilter? filter = null);
    PhysicsHit? Raycast(Vector2 origin, Vector2 direction, float distance, PhysicsFilter? filter = null);
    PhysicsHit? Sweep(Shape2D shape, Vector2 displacement, PhysicsFilter? filter = null);
}
