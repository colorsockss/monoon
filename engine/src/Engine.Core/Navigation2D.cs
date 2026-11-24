using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

// Agent sizes and sampling distance are in world pixels; gravity is world +Y.
[DisplayName("Navigation Region 2D"), YamlObject]
public sealed partial record NavigationRegion2DComponent : SceneComponent
{
    public NavigationRegion2DComponent() { }
    [YamlConstructor] public NavigationRegion2DComponent(float spacing=16, float agentWidth=16, float agentHeight=24,
        float maxSlope=48, uint terrainMask=uint.MaxValue) =>
        (Spacing,AgentWidth,AgentHeight,MaxSlope,TerrainMask)=(spacing,agentWidth,agentHeight,maxSlope,terrainMask);
    public float Spacing { get; set; } = 16;
    public float AgentWidth { get; set; } = 16;
    public float AgentHeight { get; set; } = 24;
    public float MaxSlope { get; set; } = 48;
    public uint TerrainMask { get; set; } = uint.MaxValue;

    internal void Validate(string name)
    {
        if (Spacing is not (>=4 and <=128) || AgentWidth is not (>=1 and <=4096) || AgentHeight is not (>=1 and <=4096) || MaxSlope is not (>=0 and <=80))
            throw new InvalidDataException($"{name}: invalid navigation spacing, agent size or slope.");
    }
}

public readonly record struct WalkPoint(Vector2 Feet,Guid Support);
public interface INavigation2D
{
    bool CanSee(Guid observer,Guid target,float radius,uint blockers=uint.MaxValue);
    bool TryGetFeet(Guid id,out Vector2 feet);
    IReadOnlyList<WalkPoint> FindWalkPath(Guid region,Guid actor,Vector2 goalFeet);
    /// <summary>Top-down free movement (16.15): bakes the region's open cells for agents of radius Agent W / 2 from the
    /// current static colliders and solid tiles in its mask. Call once at load (OnCreate) and keep the area.</summary>
    NavArea2D? BakeArea(Guid region);
}

public sealed partial class SceneGame : INavigation2D
{
    public INavigation2D Navigation => this;
    public bool TryGetFeet(Guid id,out Vector2 feet)
    {
        if (byId.TryGetValue(id,out var body) && body.Active)
        {
            feet=Affine2D.Transform(new(body.Size.X*.5f,body.Size.Y),body.MatrixAt(body.Position)); return true;
        }
        feet=default;return false;
    }

    public bool CanSee(Guid observer,Guid target,float radius,uint blockers=uint.MaxValue)
    {
        if (!float.IsFinite(radius) || radius<0) throw new ArgumentOutOfRangeException(nameof(radius));
        if (!byId.TryGetValue(observer,out var a) || !a.Active || !byId.TryGetValue(target,out var b) || !b.Active) return false;
        var origin=Center(a);var delta=Center(b)-origin;
        if (delta.LengthSquared()>radius*radius) return false;
        if (delta.LengthSquared()<1e-8f) return true;
        var ray=Shape2D.PointAt(origin);
        foreach(var o in Candidates(SweptBounds(ray,delta),blockers,false,observer))
        {
            var owner=byId[o.Target.ObjectId];
            if (owner==b || owner.MotionOwner==(a.MotionOwner??a) || owner.MotionOwner==(b.MotionOwner??b)) continue;
            if(CollisionGeometry.Cast(ray,delta,o.Shape,true,false) is { Time: <1 }) return false;
        }
        return true;
    }

    public IReadOnlyList<WalkPoint> FindWalkPath(Guid region,Guid actor,Vector2 goalFeet)
    {
        if (!Shape2D.Finite(goalFeet)) throw new ArgumentException("Expected finite goal feet.");
        if (!byId.TryGetValue(actor,out var agent) || !TryGetFeet(actor,out var start)) return [];
        agent.WalkPreview=[];
        if (!byId.TryGetValue(region,out var area) || !area.Active || area.Source.GetComponent<NavigationRegion2DComponent>() is not {} config) return [];
        var bounds=Affine2D.Bounds(new(0,0,area.Size.X,area.Size.Y),area.MatrixAt(area.Position));
        if (!bounds.Contains(start.X,start.Y) || !bounds.Contains(goalFeet.X,goalFeet.Y)) return [];
        int count=(int)Math.Ceiling(bounds.Width/config.Spacing);
        if (count is <1 or >1024) throw new InvalidDataException("Navigation region exceeds 1024 walk columns; split the region or increase spacing.");
        // One snapshot per plan, including resident tile collision and kinematic poses.
        // Actors are not terrain and never punch temporary holes in their own routes.
        var query=bounds;query.Inflate(config.AgentWidth,config.AgentHeight);
        var found=new List<Obstacle>();
        foreach(var o in Candidates(query,config.TerrainMask,false,actor))
            if(byId[o.Target.ObjectId].MotionOwner?.Rigid?.BodyType!=BodyType2D.Dynamic)found.Add(o);
        var surfaces=found.ToArray();
        return agent.WalkPreview=WalkGraph.Find(bounds,count,config,surfaces,start,goalFeet);
    }

    public NavArea2D? BakeArea(Guid region)
    {
        if (!byId.TryGetValue(region,out var area) || !area.Active || area.Source.GetComponent<NavigationRegion2DComponent>() is not { Enabled: true } config) return null;
        var bounds=Affine2D.Bounds(new(0,0,area.Size.X,area.Size.Y),area.MatrixAt(area.Position));
        float radius=config.AgentWidth*.5f;
        var query=bounds;query.Inflate(radius,radius);
        var shapes=new List<Shape2D>();
        foreach(var o in Candidates(query,config.TerrainMask,false,null))
            if(byId[o.Target.ObjectId].MotionOwner?.Rigid?.BodyType!=BodyType2D.Dynamic)shapes.Add(o.Shape);
        return NavArea2D.Bake(bounds,config.Spacing,radius,System.Runtime.InteropServices.CollectionsMarshal.AsSpan(shapes));
    }

    private static class WalkGraph
    {
        private sealed record Node(WalkPoint Point)
        {
            internal readonly List<int> Neighbors=[];
        }
        internal static IReadOnlyList<WalkPoint> Find(RectangleF bounds,int count,NavigationRegion2DComponent config,
            Obstacle[] geometry,Vector2 start,Vector2 goal)
        {
            float step=bounds.Width/count,half=config.AgentWidth*.5f, height=config.AgentHeight;
            float tangent=MathF.Tan(float.DegreesToRadians(config.MaxSlope)), cosine=MathF.Cos(float.DegreesToRadians(config.MaxSlope));
            var nodes=new List<Node>();
            var columns=new List<int>[count];
            // A bucket also includes the neighboring column so edge probes see identical solids.
            var buckets=new List<Obstacle>[count];
            for(int i=0;i<count;i++) { buckets[i]=[]; columns[i]=[]; }
            foreach(var o in geometry)
            {
                var r=o.Shape.Bounds;
                int left=Math.Clamp((int)Math.Floor((r.Left-half-bounds.Left)/step)-1,0,count-1);
                int right=Math.Clamp((int)Math.Floor((r.Right+half-bounds.Left)/step)+1,0,count-1);
                for(int i=left;i<=right;i++) buckets[i].Add(o);
            }
            var down=new Vector2(0,bounds.Height+2);
            void Sample(float x,int col,List<WalkPoint> into)
            {
                into.Clear();
                // Probe both feet as well as the center. A box on a rising slope is
                // supported by its uphill corner, including before its center crosses a seam.
                ReadOnlySpan<float> feetProbes=[x,x-half+.002f,x+half-.002f];
                foreach(float foot in feetProbes)
                {
                    var ray=Shape2D.PointAt(new(foot,bounds.Top-1));
                    foreach(var floor in buckets[col])
                    {
                        var box=floor.Shape.Bounds;
                        if(foot<box.Left || foot>box.Right) continue;
                        if(CollisionGeometry.Cast(ray,down,floor.Shape,true,false) is not {} h || h.Normal.Y > -cosine) continue;
                        float feet=bounds.Top-1+down.Y*h.Time;
                        if(feet<bounds.Top || feet>=bounds.Bottom || into.Any(p=>Math.Abs(p.Feet.Y-feet)<.02f)) continue;
                        var bodyBox=new RectangleF(x-half,feet-height,2*half,height-.02f);
                        var shape=Shape2D.Box(bodyBox);
                        if(buckets[col].Any(o=>bodyBox.IntersectsWith(o.Shape.Bounds) && CollisionGeometry.Overlap(shape,o.Shape))) continue;
                        into.Add(new(new(x,feet),floor.Target.ObjectId));
                    }
                }
            }
            var samples=new List<WalkPoint>();
            for(int col=0;col<count;col++)
            {
                Sample(bounds.Left+(col+.5f)*step,col,samples);
                foreach(var p in samples) { columns[col].Add(nodes.Count);nodes.Add(new(p)); }
            }
            // Intermediate samples reject disconnected floors and walls. Propagate reachable
            // elevations rather than fitting a straight line across a crest or a sloped seam.
            int pieces=Math.Max(2,(int)Math.Ceiling(step/4));
            float rise=step/pieces*tangent+.15f;
            var probes=Enumerable.Range(0,pieces-1).Select(_=>new List<WalkPoint>()).ToArray();
            for(int col=1;col<count;col++)
            {
                if(columns[col-1].Count==0 || columns[col].Count==0) continue;
                for(int k=1;k<pieces;k++) Sample(bounds.Left+(col-.5f+k/(float)pieces)*step,col,probes[k-1]);
                foreach(int left in columns[col-1])
                {
                    var reachable=new List<float>{nodes[left].Point.Feet.Y};
                    foreach(var probe in probes)
                    {
                        var prior=reachable;
                        reachable=probe.Where(p=>prior.Any(y=>Math.Abs(p.Feet.Y-y)<=rise)).Select(p=>p.Feet.Y).ToList();
                        if(reachable.Count==0) break;
                    }
                    foreach(int right in columns[col])
                        if(reachable.Any(y=>Math.Abs(y-nodes[right].Point.Feet.Y)<=rise))
                        {nodes[left].Neighbors.Add(right);nodes[right].Neighbors.Add(left);}
                }
            }
            int Attach(Vector2 p)
            {
                int nearest=-1;float best=float.PositiveInfinity;
                int column=Math.Clamp((int)((p.X-bounds.Left)/step),0,count-1);
                for(int c=Math.Max(0,column-1);c<=Math.Min(count-1,column+1);c++)
                foreach(int i in columns[c])
                {
                    var d=nodes[i].Point.Feet-p;
                    if(Math.Abs(d.X)>step || Math.Abs(d.Y)>(Math.Abs(d.X)+half)*tangent+2) continue;
                    float cost=d.LengthSquared();if(cost<best) {best=cost;nearest=i;}
                }
                return nearest;
            }
            int from=Attach(start),to=Attach(goal);
            if(from<0 || to<0) return [];
            var distance=new float[nodes.Count]; Array.Fill(distance,float.PositiveInfinity); distance[from]=0;
            var previous=new int[nodes.Count];Array.Fill(previous,-1);
            var queue=new PriorityQueue<int,(float Score,int Id)>();queue.Enqueue(from,(0,from));
            var closed=new bool[nodes.Count];
            while(queue.TryDequeue(out int i,out _))
            {
                if(closed[i])continue;
                if(i==to)
                {
                    var route=new List<WalkPoint>();
                    for(int j=i;j!=-1;j=previous[j]) route.Add(nodes[j].Point);
                    route.Reverse();return route.AsReadOnly();
                }
                closed[i]=true;
                foreach(int n in nodes[i].Neighbors)
                {
                    float next=distance[i]+Vector2.Distance(nodes[i].Point.Feet,nodes[n].Point.Feet);
                    if(next>=distance[n])continue;
                    distance[n]=next;previous[n]=i;
                    queue.Enqueue(n,(next+Vector2.Distance(nodes[n].Point.Feet,nodes[to].Point.Feet),n));
                }
            }
            return [];
        }
    }
}
