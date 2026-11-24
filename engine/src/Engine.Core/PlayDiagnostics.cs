using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public enum GameLogLevel { Info, Warning, Error }
public readonly record struct GameLogEntry(GameLogLevel Level,string Message,string? Source,int Line);
public interface IDiagnosticLines
{
    void DrawLine(Vector2 start,Vector2 end,float width,Color color);
}

public enum QueryKind { Overlap, Raycast, Sweep, Movement }
public readonly record struct QueryTrace(long Tick,QueryKind Kind,Shape2D Shape,Vector2 Travel,int Count, PhysicsTarget? Target,PhysicsHit? Hit);
public sealed class PhysicsDiagnostics
{
    public bool Colliders { get; set; }
    private bool queries;
    public bool Queries { get=>queries;set { queries=value;if(!value) {used=0;next=0;} } }
    public Guid? Selection { get; set; }
    public const int Capacity=64, VisibleTicks=60;
    private readonly QueryTrace[] history=new QueryTrace[Capacity];
    private int used,next;
    internal void Record(QueryTrace trace)
    {
        history[next]=trace;next=(next+1)%Capacity;used=Math.Min(used+1,Capacity);
    }
    internal void Clear(){used=next=0;}
    public QueryTrace? Latest => used==0?null:history[(next+Capacity-1)%Capacity];
    public IEnumerable<QueryTrace> Recent(long tick)
    {
        for(int i=used;i>0;i--){var q=history[(next+Capacity-i)%Capacity];if(tick-q.Tick<=VisibleTicks)yield return q;}
    }
}

public readonly record struct RuntimeObjectState(bool ActiveSelf,bool Active,string? SpawnedFrom,Vector2 Position,
    Guid? SceneId,int Contacts,string? Animation,int Frame,bool AudioPlaying);

public sealed partial class SceneGame
{
    // Installed only by an observing editor; shipped games keep the ordinary physics/draw paths.
    public PhysicsDiagnostics? Diagnostics { get; set; }
    internal IEnumerable<GameBehaviour> LiveScripts(Guid id) =>
        byId.TryGetValue(id,out var b) && !removed.Contains(id)?b.Behaviours:[];
    public string ObjectLabel(Guid id)=>byId.TryGetValue(id,out var b)?b.Source.Name:id.ToString();
    public string SceneLabel=>SceneLabelFor(State.SceneId);
    public string SceneLabelFor(Guid? scene)=>scene is Guid id?assets?.PathFor(id)??id.ToString():"Unsaved scene";
    public RuntimeObjectState? Inspect(Guid id)
    {
        if (!byId.TryGetValue(id,out var b) || removed.Contains(id))return null;
        return new(b.ActiveSelf,b.Active,b.SpawnedFrom,b.Position,b.SceneOwner??State.SceneId,
            contacts.Count(pair => pair.Valid && (pair.A == b || pair.B == b)),b.Animation?.State,b.Animation?.Frame??-1,IsPlaying(id));
    }

    public bool TryDebugRay(Guid origin,Vector2 screen,out PhysicsHit? hit)
    {
        hit=null;
        if (Diagnostics?.Queries!=true || !byId.TryGetValue(origin,out var b) || !b.Active) return false;
        var lens = CameraAt(screen, 1);
        if(lens==null)return false;
        var from=Affine2D.Transform(b.Size*.5f,b.MatrixAt(b.Position));
        var delta=Sample(lens,1).ScreenToWorld(screen)-from;
        if(delta.LengthSquared()<1e-8f)return false;
        hit=Raycast(from,delta,delta.Length(),new(IncludeTriggers:true,Ignore:origin));return true;
    }
    private void DrawDiagnostics(IDiagnosticLines canvas,PhysicsDiagnostics debug,CameraView shot,float fraction)
    {
        void Line(Vector2 a,Vector2 b,Color color,float width=1.5f)=>canvas.DrawLine(shot.WorldToScreen(a),shot.WorldToScreen(b),width,color);
        void Shape(Shape2D shape,Color color,float width=1.5f)
        {
            void Ring(Vector2 c,float r){var previous=c+new Vector2(r,0);
                for(int i=1;i<=32;i++){float angle=i*(MathF.Tau/32);var point=c+r*new Vector2(MathF.Cos(angle),MathF.Sin(angle));Line(previous,point,color,width);previous=point;}}
            if(shape.Parts is {} parts){
                foreach(var part in parts) Line(part.Vertices![0]+shape.Offset,part.Vertices[1]+shape.Offset,color,width);
            }else if(shape.Vertices is [var a,var b] && shape.Radius>0){
                var side=Vector2.Normalize(new Vector2(b.Y-a.Y,a.X-b.X))*shape.Radius;
                Line(a+side+shape.Offset,b+side+shape.Offset,color,width);Line(a-side+shape.Offset,b-side+shape.Offset,color,width);
                Ring(a+shape.Offset,shape.Radius);Ring(b+shape.Offset,shape.Radius);
            }else if(shape.Vertices is {} p){
                for(int i=0;i<p.Length;i++) Line(p[i]+shape.Offset,p[(i+1)%p.Length]+shape.Offset,color,width);
            }else if(shape.Radius>0){
                var previous=shape.Offset+new Vector2(shape.Radius,0);
                for(int i=1;i<=32;i++){float angle=i*(MathF.Tau/32);var point=shape.Offset+shape.Radius*new Vector2(MathF.Cos(angle),MathF.Sin(angle));Line(previous,point,color,width);previous=point;}
            }
        }
        if(debug.Colliders){
            foreach(var b in colliders){
                if(!b.Active || b.Collider is not {} collider)continue;
                var shape=b.ShapeAt(b.At(fraction), b.AngleAt(fraction));if(!shot.World.IntersectsWith(shape.Bounds))continue;
                Shape(shape,collider.IsTrigger?Color.HotPink:Color.SpringGreen,debug.Selection==b.Id?3:1.5f);
            }
            foreach(var b in bodies)
                if (b.Active && b.WalkPreview.Count>1)
                    for(int i=1;i<b.WalkPreview.Count;i++) Line(b.WalkPreview[i-1].Feet-Vector2.UnitY*2,b.WalkPreview[i].Feet-Vector2.UnitY*2,Color.CornflowerBlue,2);
            foreach (var b in joints)
                foreach (var joint in b.Joints)
                if (b.Active && joint.Connected is Guid id && byId.TryGetValue(id, out var other) && other.Active)
                    Line(Affine2D.Transform(b.Size * .5f, b.MatrixAt(b.At(fraction), b.AngleAt(fraction))),
                        Affine2D.Transform(other.Size * .5f, other.MatrixAt(other.At(fraction), other.AngleAt(fraction))), Color.DeepSkyBlue);
            foreach(var b in tileLayers){
                if(!b.Active || b.Palette==null)continue;
                var m=b.MatrixAt(b.At(fraction));var map=b.Map!;
                foreach(var (cell,id) in map.InView(Vector2.Zero,Affine2D.Bounds(shot.World,Affine2D.Inverse(m))))
                    if (TileCollision.WorldShape(b.Palette.Tiles[id], map.Bounds(Vector2.Zero,cell), map.Layout, m) is {} tile)
                    {
                        Shape(tile, Color.SpringGreen);
                        if (tile.FaceNormal != default)
                        {
                            var middle = (tile.Vertices![0] + tile.Vertices[1]) * .5f;
                            Line(middle, middle + tile.FaceNormal * (8 / shot.Zoom), Color.Gold);
                        }
                    }
            }
        }
        if(!debug.Queries)return;
        foreach(var q in debug.Recent(Time.TickCount)){
            var color=q.Count==0?Color.DeepSkyBlue:Color.Gold;
            var rect=q.Shape.Bounds;
            var start=new Vector2(rect.X+rect.Width/2,rect.Y+rect.Height/2);
            Shape(q.Shape,color);
            if(q.Travel!=Vector2.Zero){Line(start,start+q.Travel,color);Shape(q.Shape.Translated(q.Travel*(q.Hit?.Fraction??1)),color);}
            if(q.Hit is {} h){
                var mark=new Vector2(3/shot.Zoom,0);var up=new Vector2(0,mark.X);
                Line(h.Point-mark-up,h.Point+mark+up,Color.White);Line(h.Point-mark+up,h.Point+mark-up,Color.White);
                Line(h.Point,h.Point+h.Normal*(12/shot.Zoom),Color.Gold,2);
            }
        }
    }
}
