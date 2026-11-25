using System.Drawing;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record CameraRect(float X, float Y, float Width, float Height)
{
    internal bool Valid => float.IsFinite(X) && float.IsFinite(Y) && Width>0 && Height>0 &&
        float.IsFinite(Width) && float.IsFinite(Height) && float.IsFinite(X+Width) && float.IsFinite(Y+Height);
}

public sealed partial record Camera2DComponent
{
    public const string Primary="Primary", Extra="Extra", Minimap="Minimap";
    public string Output { get; set; } = Primary;
    // Null viewport: full output for primary/extra, top-right quarter for a minimap.
    public CameraRect? Viewport { get; set; }
    public bool ShowViews { get; set; } = true;
    public int Order { get; set; }
    public Guid? Follow { get; set; }
    public float HalfLife { get; set; }
    public CameraRect? Bounds { get; set; }
    // Roadmap 16.11. World pixels the tracked point may move from the view centre before the camera follows.
    public float DeadZoneWidth { get; set; }
    public float DeadZoneHeight { get; set; }
    // Seconds of the tracked point's velocity the camera aims ahead.
    public float LookAhead { get; set; }
    // Object group framed together with Follow: the camera centres the members' bounds and zooms out (no further than
    // Bounds allows) until they fit with FramePadding world pixels around them.
    public string? FrameGroup { get; set; }
    public float FramePadding { get; set; }
    public const float MaxLookAhead = 10;
    // Merging split screen: this Primary camera and the Extra camera SplitWith share the union of their viewports. Closer
    // targets than MergeDistance (world pixels) merge into this view at their midpoint; farther than SplitDistance splits
    // it left and right again. The transition takes BlendSeconds.
    public Guid? SplitWith { get; set; }
    public float MergeDistance { get; set; }
    public float SplitDistance { get; set; }
    public float BlendSeconds { get; set; }
    public const float MaxBlendSeconds = 10;

    // Copies of a subtree point at the copied Follow and SplitWith objects; links outside it stay.
    internal Camera2DComponent Relink(Func<Guid,Guid?> copy) => this with {
        Follow=Follow is Guid f ? copy(f) ?? f : null, SplitWith=SplitWith is Guid s ? copy(s) ?? s : null };
    internal Camera2DComponent Relink(IReadOnlyDictionary<Guid,Guid> ids) => Relink(id => ids.TryGetValue(id,out var to) ? to : null);
    internal bool Tracks => Follow!=null || FrameGroup!=null;
    internal bool Framing => DeadZoneWidth!=0 || DeadZoneHeight!=0 || LookAhead!=0 || FrameGroup!=null || FramePadding!=0;
    internal bool Splits => SplitWith!=null || MergeDistance!=0 || SplitDistance!=0 || BlendSeconds!=0;
    internal bool Extended => Output!=Primary || Viewport!=null || Order!=0 || Follow!=null || HalfLife!=0 || Bounds!=null || Framing;
    internal void Validate(string owner)
    {
        static bool Size(float v) => float.IsFinite(v) && v>=0;
        if (!Size(MergeDistance) || !Size(SplitDistance) || !Size(BlendSeconds) || BlendSeconds>MaxBlendSeconds ||
            SplitWith!=null && (Output!=Primary || MergeDistance<=0 || SplitDistance<MergeDistance))
            throw new InvalidDataException($"{owner}: a split screen needs a Primary camera, a merge distance above 0, a split distance at least as large and a blend of 0–{MaxBlendSeconds} seconds.");
        if (!(Zoom>=MinZoom && Zoom<=MaxZoom) || Output is not (Primary or Extra or Minimap) ||
            !float.IsFinite(HalfLife) || HalfLife<0 || Bounds is { Valid:false } ||
            !Size(DeadZoneWidth) || !Size(DeadZoneHeight) || !Size(FramePadding) || !Size(LookAhead) || LookAhead>MaxLookAhead ||
            FrameGroup is {} group && (group.Length is 0 or > 64 || group!=group.Trim()) ||
            Viewport is {} v && (!v.Valid || v.X<0 || v.Y<0 || v.X+v.Width>1.000001f || v.Y+v.Height>1.000001f))
            throw new InvalidDataException($"{owner}: invalid Camera 2D output, zoom, follow time, bounds, dead zone, look-ahead, frame group or normalized viewport.");
    }

    private static readonly CameraRect Full=new(0,0,1,1), Inset=new(.73f,.03f,.25f,.25f);
    public CameraRect DefaultViewport() => Output==Minimap ? Inset : Full;
    internal Rectangle Port(Size resolution) { var v=Viewport??DefaultViewport(); return Port(resolution,new(v.X,v.Y,v.Width,v.Height)); }
    internal static Rectangle Port(Size resolution,RectangleF v)
    {
        // Round shared edges, not widths: split views meet even at odd logical resolutions.
        static int Edge(float value,int extent) => Math.Clamp((int)MathF.Round(value*extent),0,extent);
        int left=Math.Min(resolution.Width-1,Edge(v.X,resolution.Width)), top=Math.Min(resolution.Height-1,Edge(v.Y,resolution.Height));
        return Rectangle.FromLTRB(left,top,Math.Max(left+1,Edge(v.X+v.Width,resolution.Width)),Math.Max(top+1,Edge(v.Y+v.Height,resolution.Height)));
    }
    public CameraView View(Size resolution,Vector2 center) => View(resolution,center,Zoom);
    // zoom: the group framing's zoom. Bounds may still raise it so the view never shows outside the map.
    public CameraView View(Size resolution,Vector2 center,float zoom) => View(Port(resolution),center,zoom);
    // port: this camera's viewport in logical pixels (a split screen moves it at run time).
    internal CameraView View(Rectangle port,Vector2 center,float zoom)
    {
        if (Bounds is {} b) zoom=Math.Max(zoom,Math.Max(port.Width/b.Width,port.Height/b.Height));
        float w=port.Width/zoom,h=port.Height/zoom;
        var frame=new RectangleF(center.X-w/2,center.Y-h/2,w,h);
        if (Bounds is {} limit)
        {
            frame.X=Math.Clamp(frame.X,limit.X,Math.Max(limit.X,limit.X+limit.Width-w));
            frame.Y=Math.Clamp(frame.Y,limit.Y,Math.Max(limit.Y,limit.Y+limit.Height-h));
        }
        return new(port,frame,zoom);
    }
}

public readonly record struct CameraView(Rectangle Viewport,RectangleF World,float Zoom)
{
    public Vector2 Center => new(World.X+World.Width*.5f,World.Y+World.Height*.5f);
    public Vector2 WorldToScreen(Vector2 world) => new(Viewport.X+(world.X-World.X)*Zoom,Viewport.Y+(world.Y-World.Y)*Zoom);
    public Vector2 ScreenToWorld(Vector2 point) => new(World.X+(point.X-Viewport.X)/Zoom,World.Y+(point.Y-Viewport.Y)/Zoom);
}
