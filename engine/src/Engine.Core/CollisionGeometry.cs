using System.Drawing;
using System.Numerics;

namespace Engine.Core;

// World shapes are convex polygons, true circles, or a zero-radius point for raycasts.
// Linear casts use continuous SAT for polygons and analytic face / vertex times for round features.
internal static partial class CollisionGeometry
{
    private const double Eps = 1e-5;
    private static double Dot(Vector2 a, Vector2 b) => (double)a.X*b.X+(double)a.Y*b.Y;
    private static double Cross(Vector2 a, Vector2 b) => (double)a.X*b.Y-(double)a.Y*b.X;
    private static Vector2 Left(Vector2 e) => Vector2.Normalize(new(-e.Y,e.X));
    internal static bool Convex(ReadOnlySpan<Vector2> p)
    {
        if (p.Length is <3 or >32) return false;
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
            throw new InvalidDataException($"{name}: expected 3–32 vertices in convex boundary order.");
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
            return Shape2D.Circle(Vector2.Transform(size*.5f,m),scale*(c.Radius>0?c.Radius:Math.Min(size.X,size.Y)*.5f));
        }
        if (collider is PolygonCollider2DComponent p)
            return Shape2D.Hull(p.Points.Select(v=>Vector2.Transform(new Vector2(v.X,v.Y)*size,m)).ToArray());
        return Tile(new(0,0,size.X,size.Y),TileGrid.Orthogonal,m);
    }
    internal static Shape2D Tile(RectangleF rect,string layout,Matrix3x2 matrix)
    {
        var outline=TileGrid.Outline(layout);
        var p=new Vector2[outline.Length];
        for (int i=0;i<p.Length;i++) p[i]=Vector2.Transform(TileGrid.Vertex(rect,outline[i]),matrix);
        return Shape2D.Hull(p);
    }
    // Intersect the two support features along their common tangent. Parallel faces
    // report the midpoint of contact; a point or curved feature gives a single point.
    internal static Vector2 ContactPoint(Shape2D moving,Shape2D target,Vector2 normal)
    {
        if (target.Vertices==null) return target.Offset+normal*target.Radius;
        var tangent=new Vector2(-normal.Y,normal.X);
        (Vector2 point,double low,double high) Feature(Shape2D s,Vector2 axis)
        {
            if (s.Vertices==null) return (s.Offset+axis*s.Radius,0,0);
            var p=s.Vertices;
            int best=0;
            for (int i=1;i<p.Length;i++) if (Dot(p[i]-p[best],axis)>0) best=i;
            double lo=0,hi=0;
            foreach (var v in p)
            {
                var delta=v-p[best];
                if (Dot(delta,axis)<-Eps) continue;
                double t=Dot(delta,tangent);lo=Math.Min(lo,t);hi=Math.Max(hi,t);
            }
            return (s.Offset+p[best],lo,hi);
        }
        var a=Feature(moving,-normal);var b=Feature(target,normal);
        double offset=Dot(a.point-b.point,tangent);
        double start=Math.Clamp(a.low+offset,b.low,b.high),end=Math.Clamp(a.high+offset,b.low,b.high);
        return b.point+tangent*(float)((start+end)*.5);
    }

    internal readonly record struct Contact(float Time,Vector2 Normal);

    // Signed distance to the nearest polygon boundary (negative inside), and an outward normal.
    private static double ToPolygon(Vector2 point,Vector2[] p,out Vector2 normal)
    {
        bool ccw=Cross(p[1]-p[0],p[2]-p[0])>0;
        double face=double.NegativeInfinity, nearest=double.PositiveInfinity;
        Vector2 faceNormal=default, radial=default;
        for (int i=0;i<p.Length;i++)
        {
            var a=p[i]; var e=p[(i+1)%p.Length]-a;
            var n=Left(e)*(ccw?-1:1);
            var rel=point-a;
            var d=Dot(rel,n); if (d>face) { face=d; faceNormal=n; }
            var away=rel-e*(float)Math.Clamp(Dot(rel,e)/Dot(e,e),0,1);
            double ds=Dot(away,away);
            if (ds<nearest) { nearest=ds; radial=away; }
        }
        normal=face<=0 || nearest<Eps*Eps ? faceNormal : Vector2.Normalize(radial);
        return face<=0 ? face : Math.Sqrt(nearest);
    }
    internal static bool Overlap(Shape2D a,Shape2D b) => b.FaceNormal == default
        ? Cast(a,Vector2.Zero,b,false,false) != null
        : Cast(Shape2D.PointAt(b.Offset + b.Vertices![0]), b.Vertices[1] - b.Vertices[0], a, true, false) != null;
    // query=true includes an initial touch and contact at the endpoint; a trigger needs interior overlap.
    internal static Contact? Cast(Shape2D a,Vector2 travel,Shape2D b,bool query,bool movement)
    {
        if (b.FaceNormal != default) return CastFace(a, travel, b, query);
        if (a.Vertices!=null && b.Vertices!=null) return Polygons(a,travel,b,query,movement);
        if (a.Vertices!=null)
        {
            var hit=Round(b,-travel,a,query,movement);
            return hit is {} h ? new(h.Time,-h.Normal) : null;
        }
        return Round(a,travel,b,query,movement);
    }
    private static Contact? Round(Shape2D a,Vector2 travel,Shape2D b,bool query,bool movement)
    {
        var p=a.Offset-b.Offset;
        var verts=b.Vertices;
        var radius=a.Radius+b.Radius;
        double speed=Math.Sqrt(Dot(travel,travel));
        double velocityEps=1e-6*speed;
        Vector2 n;
        double gap;
        if (verts!=null) gap=ToPolygon(p,verts,out n)-radius;
        else
        {
            gap=Math.Sqrt(Dot(p,p))-radius;
            n=p!=Vector2.Zero?Vector2.Normalize(p):travel!=Vector2.Zero?Vector2.Normalize(travel):Vector2.UnitX;
        }
        if (gap<=Eps)
        {
            if (query || gap < -Eps && !movement || Dot(travel,n)<-velocityEps) return new(0,n);
            return null;
        }
        if (speed==0) return null;
        double best=double.PositiveInfinity; Vector2 normal=default;
        void Accept(double t,Vector2 axis)
        {
            if (t<0 || t>1 || (!query && !movement && t>=1) || t>=best) return;
            double approach=Dot(travel,axis);
            if (query ? approach>velocityEps : approach>=-velocityEps) return;
            best=t; normal=axis;
        }
        void Vertex(Vector2 vertex)
        {
            var d=p-vertex;
            double proj=Dot(d,travel), v2=Dot(travel,travel), c=Dot(d,d)-(double)radius*radius;
            double disc=proj*proj-v2*c;
            if (disc<0 || proj>=0) return;
            double t=c/(-proj+Math.Sqrt(disc));
            if (t<0 || t>1 || t>=best) return;
            var delta=d+travel*(float)t;
            if (delta!=Vector2.Zero) Accept(t,Vector2.Normalize(delta));
        }
        if (verts==null) Vertex(Vector2.Zero);
        else
        {
            bool ccw=Cross(verts[1]-verts[0],verts[2]-verts[0])>0;
            for (int i=0;i<verts.Length;i++)
            {
                var v=verts[i]; var edge=verts[(i+1)%verts.Length]-v;
                var axis=Left(edge)*(ccw?-1:1);
                double speedN=Dot(travel,axis);
                if (speedN < -velocityEps)
                {
                    double t=(radius-Dot(p-v,axis))/speedN;
                    if (t>=0 && t<=1 && t<best)
                    {
                        double along=Dot((p-v)+travel*(float)t,edge);
                        if (along>=0 && along<=Dot(edge,edge)) Accept(t,axis);
                    }
                }
                if (radius>0) Vertex(v);
            }
        }
        return double.IsFinite(best)?new((float)best,normal):null;
    }
    private static Contact? Polygons(Shape2D a,Vector2 travel,Shape2D b,bool query,bool movement)
    {
        double enter=double.NegativeInfinity,exit=double.PositiveInfinity,depth=double.PositiveInfinity;
        Vector2 atHit=default,nearest=default;
        var offset=a.Offset-b.Offset;
        double speed = Math.Sqrt(Dot(travel, travel));
        double velocityEps = 1e-6 * speed;
        // World transforms and motor steps arrive as floats. At a tile seam a few ulps
        // below a shared tangent must not turn the next tile's internal side into a wall.
        var bounds = a.Bounds;
        float magnitude = Math.Max(Math.Max(Math.Abs(bounds.Left), Math.Abs(bounds.Right)),
            Math.Max(Math.Abs(bounds.Top), Math.Abs(bounds.Bottom)));
        double tangentSlop = movement ? Math.Max(Eps, 4d * (MathF.BitIncrement(magnitude) - magnitude)) : Eps;
        double simultaneous = movement ? tangentSlop / Math.Max(1e-8, speed) : 0;
        bool Test(Vector2[] edges)
        {
            for (int i=0;i<edges.Length;i++)
            {
                var axis=Left(edges[(i+1)%edges.Length]-edges[i]);
                (double lo,double hi) Span(Vector2[] points)
                {
                    double l=Dot(points[0],axis),h=l;
                    foreach (var v in points.AsSpan(1)) { var d=Dot(v,axis); l=Math.Min(l,d); h=Math.Max(h,d); }
                    return (l,h);
                }
                var (amin,amax)=Span(a.Vertices!);var (bmin,bmax)=Span(b.Vertices!);
                double delta=Dot(offset,axis),left=amax+delta-bmin,right=bmax-amin-delta;
                double pen=Math.Min(left,right); var outward=left<right?-axis:axis;
                if (pen<depth || pen==depth && Dot(travel,outward)<Dot(travel,nearest)) { depth=pen; nearest=outward; }
                double vAxis=Dot(travel,axis);
                if (Math.Abs(vAxis)<=velocityEps)
                {
                    if (query ? pen < -Eps : pen <= tangentSlop) return false;
                    continue;
                }
                double t1=-left/vAxis,t2=right/vAxis,from=Math.Min(t1,t2);
                var face = vAxis > 0 ? -axis : axis;
                // At a shared vertex, slide on the least obstructive face. Keep the latest
                // time of entry for CCD even when two projected times differ by rounding.
                if (from > enter + simultaneous || movement && Math.Abs(from - enter) <= simultaneous &&
                    Dot(travel, face) > Dot(travel, atHit)) atHit = face;
                enter = Math.Max(enter, from);
                exit=Math.Min(exit,Math.Max(t1,t2));
                if (query ? enter>exit : enter>=exit) return false;
            }
            return true;
        }
        if (!Test(a.Vertices!) || !Test(b.Vertices!)) return null;
        if (depth>=-Eps && query) return new(0,nearest);
        if (depth>Eps) return !movement || Dot(travel,nearest)<-velocityEps ? new(0,nearest) : null;
        if (exit<=0 || enter>1 || (!query && !movement && enter>=1) || travel==Vector2.Zero) return null;
        // A few ulps of penetration at a resting corner are not a future impact.
        // Reject a separating motion instead of reusing the face from a past entry.
        if (movement && enter<0 && depth>=-Eps && Dot(travel,nearest)>=-velocityEps) return null;
        return new((float)Math.Max(0,enter),atHit);
    }
}
