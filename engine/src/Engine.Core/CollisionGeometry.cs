using System.Drawing;
using System.Numerics;
using Brutal.Box2DApi;
using Brutal.Numerics;

namespace Engine.Core;

// World shapes are convex polygons, true circles, a zero-radius point for raycasts, or a one-way face.
// The narrow phase is Box2D's: GJK distance, manifolds for resting and overlapping contact, shape casts and ray casts.
// Box2D works in MoNoon pixels (one length unit per pixel), so its linear slop is 0.005 px.
internal static partial class CollisionGeometry
{
    private const double Eps = 1e-5;
    // Resting contact tolerance in pixels. Float transforms at a few thousand pixels carry ~1e-4 px of rounding.
    private const float Touch = 1e-4f;
    // Movement probes this far along the travel to tell sliding from pushing into a touched surface.
    private const float Probe = .01f;

    /// <summary>Box2D polygon for a convex outline, computed once per shape outline.</summary>
    internal sealed class Hull
    {
        public readonly B2Polygon Polygon;
        public Hull(Vector2[] points)
        {
            Span<float2> p = stackalloc float2[points.Length];
            for (int i = 0; i < p.Length; i++) p[i] = F(points[i]);
            var hull = points.Length <= 8 ? Box2D.ComputeHull(p, p.Length) : default;
            if (hull.Count < 3) throw new InvalidDataException("Expected a convex outline of 3–8 vertices that Box2D can represent.");
            Polygon = Box2D.MakePolygon(in hull, 0);
        }
    }

    internal static float2 F(Vector2 v) => new(v.X, v.Y);
    internal static Vector2 V(float2 v) => new(v.X, v.Y);
    private static double Dot(Vector2 a, Vector2 b) => (double)a.X*b.X+(double)a.Y*b.Y;
    private static double Cross(Vector2 a, Vector2 b) => (double)a.X*b.Y-(double)a.Y*b.X;

    internal static bool Convex(ReadOnlySpan<Vector2> p)
    {
        if (p.Length is <3 or >8) return false;
        int sign=0;
        for (int i=0;i<p.Length;i++)
        {
            if (!Shape2D.Finite(p[i])) return false;
            int j=(i+1)%p.Length;
            for (int k=0;k<p.Length;k++)
            {
                if (k==i || k==j) continue;
                double side=Cross(p[j]-p[i],p[k]-p[i]);
                if (!double.IsFinite(side) || side==0) return false;
                if (sign==0) sign=Math.Sign(side);
                if (sign!=Math.Sign(side)) return false;
            }
        }
        return true;
    }
    internal static void Validate(Collider2DComponent c,string name)
    {
        if (c.Layer is <0 or >31 || c is CircleCollider2DComponent r && !(r.Radius>=0 && float.IsFinite(r.Radius)))
            throw new InvalidDataException($"{name}: invalid collider layer (0–31) or radius.");
        if (c is PolygonCollider2DComponent p && !Convex(p.Points.Select(v=>new Vector2(v.X,v.Y)).ToArray()))
            throw new InvalidDataException($"{name}: expected 3–8 vertices in convex boundary order.");
    }
    internal static bool Pair(Collider2DComponent a,Collider2DComponent b) =>
        (a.Mask & 1u<<b.Layer)!=0 && (b.Mask & 1u<<a.Layer)!=0;

    internal static Shape2D ForObject(SceneObject o) => ForObject(o, o.WorldMatrix);
    internal static Shape2D ForObject(SceneObject o, Matrix3x2 m)
    {
        var collider=o.GetComponent<Collider2DComponent>();
        if (collider==null) return default;
        m.Translation=Vector2.Zero;
        var size=new Vector2(o.Width,o.Height);
        if (collider is CircleCollider2DComponent c)
        {
            // Keep a circle under nonuniform scale / shear: use the smaller singular value.
            double a=Dot(new(m.M11,m.M12),new(m.M11,m.M12)), b=Dot(new(m.M21,m.M22),new(m.M21,m.M22));
            double h=Dot(new(m.M11,m.M12),new(m.M21,m.M22));
            double major=Math.Sqrt(.5*(a+b+Math.Sqrt((a-b)*(a-b)+4*h*h)));
            float scale=(float)(Math.Abs((double)m.M11*m.M22-(double)m.M12*m.M21)/major);
            return Shape2D.Circle(Affine2D.Transform(size*.5f,m),scale*(c.Radius>0?c.Radius:Math.Min(size.X,size.Y)*.5f));
        }
        if (collider is PolygonCollider2DComponent p)
            return Shape2D.Hull(p.Points.Select(v=>Affine2D.Transform(new Vector2(v.X,v.Y)*size,m)).ToArray());
        return Tile(new(0,0,size.X,size.Y),TileGrid.Orthogonal,m);
    }
    internal static Shape2D Tile(RectangleF rect,string layout,Matrix3x2 matrix)
    {
        var outline=TileGrid.Outline(layout);
        var p=new Vector2[outline.Length];
        for (int i=0;i<p.Length;i++) p[i]=Affine2D.Transform(TileGrid.Vertex(rect,outline[i]),matrix);
        return Shape2D.Hull(p);
    }

    internal static B2ShapeProxy Proxy(in Shape2D s)
    {
        var proxy = new B2ShapeProxy();
        if (s.Poly is {} hull)
        {
            ref readonly var polygon = ref hull.Polygon;
            for (int i = 0; i < polygon.Count; i++) proxy.Points[i] = polygon.Vertices[i] + F(s.Offset);
            proxy.Count = polygon.Count;
        }
        else if (s.Vertices != null)
        {
            proxy.Points[0] = F(s.Vertices[0] + s.Offset);
            proxy.Points[1] = F(s.Vertices[1] + s.Offset);
            proxy.Count = 2;
        }
        else
        {
            proxy.Points[0] = F(s.Offset);
            proxy.Count = 1;
            proxy.Radius = s.Radius;
        }
        return proxy;
    }

    // Box2D manifold of shape b (reference) against shape a. The normal points from b to a and the
    // separation is negative while they overlap. A face is collided as its segment.
    private static B2Manifold Manifold(in Shape2D a, in Shape2D b)
    {
        var xa = new B2Transform(F(a.Offset), B2Rot.Identity);
        var xb = new B2Transform(F(b.Offset), B2Rot.Identity);
        var circleA = new B2Circle { Radius = a.Radius };
        var circleB = new B2Circle { Radius = b.Radius };
        if (b.Poly is {} pb)
            return a.Poly is {} pa ? Box2D.CollidePolygons(in pb.Polygon, xb, in pa.Polygon, xa)
                : Box2D.CollidePolygonAndCircle(in pb.Polygon, xb, in circleA, xa);
        if (b.Vertices != null)
        {
            var segment = new B2Segment { Point1 = F(b.Vertices[0]), Point2 = F(b.Vertices[1]) };
            return a.Poly is {} pa ? Box2D.CollideSegmentAndPolygon(in segment, xb, in pa.Polygon, xa)
                : Box2D.CollideSegmentAndCircle(in segment, xb, in circleA, xa);
        }
        if (a.Poly is {} polygon)
        {
            var flipped = Box2D.CollidePolygonAndCircle(in polygon.Polygon, xa, in circleB, xb);
            flipped.Normal = -flipped.Normal;
            return flipped;
        }
        return Box2D.CollideCircles(in circleB, xb, in circleA, xa);
    }

    /// <summary>Signed distance between the shapes (negative while overlapping) and the normal out of b toward a.</summary>
    internal static float Separation(in Shape2D a, in Shape2D b, out Vector2 normal)
    {
        var input = new B2DistanceInput { ProxyA = Proxy(b), ProxyB = Proxy(a), TransformA = B2Transform.Identity,
            TransformB = B2Transform.Identity, UseRadii = true };
        var cache = new B2SimplexCache();
        B2Simplex simplex = default;
        var output = Box2D.ShapeDistance(in input, ref cache, ref simplex, 0);
        if (output.Distance > Touch && output.Normal != default)
        {
            normal = V(output.Normal);
            return output.Distance;
        }
        var manifold = Manifold(a, b);
        if (manifold.PointCount == 0)
        {
            normal = output.Normal != default ? V(output.Normal) : FallbackNormal(a, b);
            return output.Distance;
        }
        float separation = manifold.Points[0].Separation;
        if (manifold.PointCount > 1) separation = Math.Min(separation, manifold.Points[1].Separation);
        normal = V(manifold.Normal);
        return separation;
    }

    private static Vector2 FallbackNormal(in Shape2D a, in Shape2D b)
    {
        var d = Center(a) - Center(b);
        return d == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(d);
        static Vector2 Center(in Shape2D s) => new(s.Bounds.X + s.Bounds.Width * .5f, s.Bounds.Y + s.Bounds.Height * .5f);
    }

    // The middle of the touching features: Box2D manifold points, or the closest point on the target.
    internal static Vector2 ContactPoint(Shape2D moving,Shape2D target,Vector2 normal)
    {
        var manifold = Manifold(moving, target);
        if (manifold.PointCount == 0)
        {
            var input = new B2DistanceInput { ProxyA = Proxy(target), ProxyB = Proxy(moving), TransformA = B2Transform.Identity,
                TransformB = B2Transform.Identity, UseRadii = true };
            var cache = new B2SimplexCache();
            B2Simplex simplex = default;
            return V(Box2D.ShapeDistance(in input, ref cache, ref simplex, 0).PointA);
        }
        var point = V(manifold.Points[0].Point);
        if (manifold.PointCount > 1) point = (point + V(manifold.Points[1].Point)) * .5f;
        // Manifold points sit midway between the shapes; move onto the target surface.
        float gap = manifold.Points[0].Separation;
        if (manifold.PointCount > 1) gap = (gap + manifold.Points[1].Separation) * .5f;
        return point - V(manifold.Normal) * (gap * .5f);
    }

    internal readonly record struct Contact(float Time,Vector2 Normal);

    internal static bool Overlap(Shape2D a,Shape2D b) => b.FaceNormal == default
        ? Cast(a,Vector2.Zero,b,false,false) != null
        : Cast(Shape2D.PointAt(b.Offset + b.Vertices![0]), b.Vertices[1] - b.Vertices[0], a, true, false) != null;

    // query=true includes an initial touch and contact at the endpoint; a trigger needs interior overlap.
    // movement=true ignores touched surfaces that the travel slides along or leaves.
    internal static Contact? Cast(Shape2D a,Vector2 travel,Shape2D b,bool query,bool movement)
    {
        if (b.FaceNormal != default) return CastFace(a, travel, b, query);
        double speed = Math.Sqrt(Dot(travel, travel));
        double velocityEps = 1e-6 * speed;
        float near = Separation(a, b, out var n);
        if (near <= Touch)
        {
            if (query) return new(0, n);
            if (!movement) return near < -Touch ? new(0, n) : null;
            if (travel == Vector2.Zero) return null;
            // Probe a short step: a surface the travel slides along stays within the touch tolerance.
            var ahead = a.Translated(travel * (float)Math.Min(1, Probe / speed));
            float after = Separation(ahead, b, out var pushed);
            if (after >= Math.Min(near, -Touch) || Dot(travel, pushed) >= -velocityEps) return null;
            return new(0, pushed);
        }
        if (speed == 0) return null;
        float t;
        if (a.Vertices == null && a.Radius == 0)
        {
            // Rays are exact in Box2D. Polygons are relative to their outline origin.
            var ray = new B2RayCastInput { Origin = F(a.Offset - b.Offset), Translation = F(travel), MaxFraction = 1 };
            B2CastOutput output;
            if (b.Poly is {} hull) output = Box2D.RayCastPolygon(in ray, in hull.Polygon);
            else { var circle = new B2Circle { Radius = b.Radius }; output = Box2D.RayCastCircle(in ray, in circle); }
            if (!output.Hit) return null;
            t = output.Fraction;
            n = V(output.Normal);
        }
        else
        {
            // Box2D stops a cast within its linear slop of contact; reach a little past the end and refine below.
            var input = new B2ShapeCastPairInput { ProxyA = Proxy(b), ProxyB = Proxy(a), TransformA = B2Transform.Identity,
                TransformB = B2Transform.Identity, TranslationB = F(travel), MaxFraction = 1 + (float)(.02 / speed) };
            var output = Box2D.ShapeCast(in input);
            if (!output.Hit) return null;
            t = output.Fraction;
            n = V(output.Normal);
            // Advance or retreat to the exact contact along the hit normal.
            float gap = Separation(a.Translated(travel * t), b, out var refined);
            double approach = -Dot(travel, refined);
            if (approach > velocityEps)
            {
                t = (float)Math.Max(0, t + gap / approach);
                n = refined;
            }
        }
        if (t > 1 || (!query && !movement && t >= 1)) return null;
        double along = Dot(travel, n);
        if (query ? along > velocityEps : along >= -velocityEps) return null;
        // A convex target entered only at a grazing touch is not an obstacle: at a tile seam the next tile's
        // corner lies exactly on the path. Probe just past the touch, as for resting contact above.
        if (movement && Separation(a.Translated(travel * (float)(t + Probe / speed)), b, out _) >= -Touch) return null;
        return new(t, n);
    }
}
