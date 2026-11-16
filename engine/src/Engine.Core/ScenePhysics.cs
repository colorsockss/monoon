using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public sealed partial class SceneGame : IPhysics2D
{
    public IPhysics2D Physics => this;
    private readonly record struct Obstacle(Shape2D Shape, PhysicsTarget Target);
    private IEnumerable<Obstacle> Candidates(RectangleF bounds,uint mask,bool triggersIncluded,Guid? ignore,
        int first=-1,int last=-1,int movingLayer=-1,Body? ignoreOwner=null)
    {
        foreach (var body in triggersIncluded?colliders:solids)
        {
            if (body.Collider is not {} c || !Allows(body,c.Layer,c.Mask,mask,ignore,first,last,movingLayer,ignoreOwner)) continue;
            var shape=body.ShapeAt(body.Position);
            if (BoundsMeet(bounds,shape.Bounds)) yield return new(shape,new(body.Id,c.Layer,c.IsTrigger,null));
        }
        foreach (var obstacle in TileCandidates(bounds, mask, ignore, first, last, movingLayer, ignoreOwner))
            yield return obstacle;
    }

    private IEnumerable<Obstacle> TileCandidates(RectangleF bounds, uint mask, Guid? ignore, int first, int last,
        int movingLayer, Body? ignoreOwner, bool sweptMaps = false)
    {
        foreach (var body in tileLayers)
        {
            var map=body.Map!;
            if (body.Palette == null || !Allows(body, map.CollisionLayer, map.CollisionMask, mask, ignore,
                    first, last, movingLayer, ignoreOwner)) continue;
            var m=body.MatrixAt(body.Position);
            var search = bounds;
            if (sweptMaps && body.Moved)
                for (float t = 0; t < 1; t = body.NextBreak(t))
                {
                    var relative = bounds;
                    var shift = body.Position - body.At(t);
                    relative.Offset(shift.X,shift.Y);
                    search = RectangleF.Union(search,relative);
                }
            var local=Affine2D.Bounds(search,Affine2D.Inverse(m));
            // Include the neighbouring solid cell when a ray ends exactly on its boundary.
            local.Inflate(.001f,.001f);
            foreach (var (cell,id) in map.InView(Vector2.Zero,local))
            {
                if (!body.Palette.Tiles[id].Solid) continue;
                var shape=CollisionGeometry.Tile(map.Bounds(Vector2.Zero,cell),map.Layout,m);
                if (BoundsMeet(search,shape.Bounds)) yield return new(shape,new(body.Id,map.CollisionLayer,false,cell));
            }
        }
    }
    private static bool Allows(Body body, int layer, uint otherMask, uint mask, Guid? ignore, int first, int last,
        int movingLayer, Body? ignoreOwner) => body.Active && body.Id != ignore && (mask & (1u << layer)) != 0 &&
        (movingLayer < 0 || (otherMask & (1u << movingLayer)) != 0) && (body.Index < first || body.Index >= last) &&
        (ignoreOwner == null || body.MotionOwner != ignoreOwner);
    private static bool BoundsMeet(RectangleF a, RectangleF b) =>
        a.Left <= b.Right && a.Right >= b.Left && a.Top <= b.Bottom && a.Bottom >= b.Top;
    private static RectangleF SweptBounds(Shape2D s,Vector2 travel)
    {
        var end=s.Bounds; end.Offset(travel.X,travel.Y);
        return RectangleF.Union(s.Bounds,end);
    }
    // Stable ties, including cells enumerated from sparse map chunks.
    private static int Compare(PhysicsTarget a,PhysicsTarget b)
    {
        int order=a.ObjectId.CompareTo(b.ObjectId);
        if (order!=0) return order;
        order=Nullable.Compare(a.Tile?.Y,b.Tile?.Y);
        return order!=0?order:Nullable.Compare(a.Tile?.X,b.Tile?.X);
    }
    public IReadOnlyList<PhysicsTarget> Overlap(Shape2D shape,PhysicsFilter? filter=null)
    {
        shape.Check();
        var found=new List<PhysicsTarget>();
        foreach (var o in Candidates(shape.Bounds,filter?.Mask??uint.MaxValue,filter?.IncludeTriggers??false,filter?.Ignore))
            if (CollisionGeometry.Overlap(shape,o.Shape)) found.Add(o.Target);
        found.Sort(Compare);
        if(Diagnostics is {Queries:true} debug)debug.Record(new(Time.TickCount,QueryKind.Overlap,shape,default,found.Count,found.Count>0?found[0]:null,null));
        return found;
    }
    public PhysicsHit? Raycast(Vector2 origin,Vector2 direction,float distance,PhysicsFilter? filter=null)
    {
        if (!Shape2D.Finite(origin) || !Shape2D.Finite(direction) || direction==Vector2.Zero || !float.IsFinite(distance) || distance<0)
            throw new ArgumentException("Expected a finite ray with nonzero direction and nonnegative distance.");
        double length=Math.Sqrt((double)direction.X*direction.X+(double)direction.Y*direction.Y);
        return QueryCast(Shape2D.PointAt(origin),new((float)(direction.X/length*distance),(float)(direction.Y/length*distance)),filter,QueryKind.Raycast);
    }
    public PhysicsHit? Sweep(Shape2D shape,Vector2 displacement,PhysicsFilter? filter=null)
    {
        shape.Check();
        if (!Shape2D.Finite(displacement)) throw new ArgumentException("Expected a finite displacement.");
        return QueryCast(shape,displacement,filter,QueryKind.Sweep);
    }
    private PhysicsHit? QueryCast(Shape2D shape,Vector2 travel,PhysicsFilter? filter,QueryKind kind)
    {
        PhysicsHit? best=null;
        foreach (var o in Candidates(SweptBounds(shape,travel),filter?.Mask??uint.MaxValue,filter?.IncludeTriggers??false,filter?.Ignore))
        {
            var hit=CollisionGeometry.Cast(shape,travel,o.Shape,true,false);
            if (hit is not {} h || best is {} b && (h.Time>b.Fraction || h.Time==b.Fraction && Compare(o.Target,b.Target)>=0)) continue;
            best=new(o.Target,h.Time,h.Time*travel.Length(),
                CollisionGeometry.ContactPoint(shape.Translated(travel*h.Time),o.Shape,h.Normal),h.Normal);
        }
        if(Diagnostics is {Queries:true} debug)debug.Record(new(Time.TickCount,kind,shape,travel,best.HasValue?1:0,best?.Target,best));
        return best;
    }
    // A discrete move must fit in its entirety. A partially blocked sweep leaves the
    // whole hierarchy at its starting position, including the collider and sprite.
    public bool TryMoveStep(Guid id, Vector2 displacement)
    {
        if (!float.IsFinite(displacement.X) || !float.IsFinite(displacement.Y))
            throw new ArgumentOutOfRangeException(nameof(displacement));
        if (!byId.TryGetValue(id, out var body) || !body.Active || body.MotionOwner != null ||
            body.SubtreeEnd == 0 || removed.Contains(id)) return false;
        var group = Subtree(body);
        foreach (var member in group) if (member.Rigid != null) return false;
        var path = MoveGroup(group, displacement);
        if (path.End != displacement) return false;
        foreach (var member in group) member.Translate(path);
        return true;
    }

    private MotionPath MoveGroup(ReadOnlySpan<Body> group,Vector2 target)
    {
        var position=Vector2.Zero; var bend=Vector2.Zero; float time=0,bendTime=0;
        var normals=new List<Vector2>(4);
        int first=group[0].Index,last=group[0].SubtreeEnd;
        for (int pass=0;pass<2 && position!=target;pass++)
        {
            float earliest=1;
            normals.Clear();
            var travel=target-position;
            foreach (var body in group)
            {
                if (!body.Active || body.Collider is not { IsTrigger:false } c) continue;
                var shape=body.ShapeAt(body.Position+position);
                PhysicsHit? traceHit=null;
                var debug=Diagnostics is {Queries:true} capture?capture:null;
                foreach (var o in Candidates(SweptBounds(shape,travel),c.Mask,false,null,first,last,c.Layer))
                {
                    if (CollisionGeometry.Cast(shape,travel,o.Shape,false,true) is not {} hit) continue;
                    if(debug!=null && (traceHit==null || hit.Time<traceHit.Value.Fraction))
                        traceHit=new(o.Target,hit.Time,hit.Time*travel.Length(),CollisionGeometry.ContactPoint(shape.Translated(travel*hit.Time),o.Shape,hit.Normal),hit.Normal);
                    if(hit.Time>earliest+1e-7f)continue;
                    if (hit.Time<earliest-1e-7f) normals.Clear();
                    earliest=Math.Min(earliest,hit.Time); normals.Add(hit.Normal);
                }
                debug?.Record(new(Time.TickCount,QueryKind.Movement,shape,travel,traceHit.HasValue?1:0,traceHit?.Target,traceHit));
            }
            if (normals.Count==0) return new(Vector2.Zero,bend,bendTime,target,1);
            position+=travel*earliest;
            time+=(1-time)*earliest;
            if (pass!=0) break;
            bend=position; bendTime=time;
            var remaining=travel*(1-earliest);
            Vector2 slide=default;
            foreach (var n in normals)
            {
                var along=remaining-n*Vector2.Dot(remaining,n);
                if (along.LengthSquared()>slide.LengthSquared() && normals.All(v=>Vector2.Dot(v,along)>=-1e-5f)) slide=along;
            }
            target=position+slide;
        }
        return new(Vector2.Zero,bend,bendTime,position,time);
    }
}
