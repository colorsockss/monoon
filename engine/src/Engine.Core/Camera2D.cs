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

    internal bool Extended => Output!=Primary || Viewport!=null || Order!=0 || Follow!=null || HalfLife!=0 || Bounds!=null;
    internal void Validate(string owner)
    {
        if (!(Zoom>=MinZoom && Zoom<=MaxZoom) || Output is not (Primary or Extra or Minimap) ||
            !float.IsFinite(HalfLife) || HalfLife<0 || Bounds is { Valid:false } ||
            Viewport is {} v && (!v.Valid || v.X<0 || v.Y<0 || v.X+v.Width>1.000001f || v.Y+v.Height>1.000001f))
            throw new InvalidDataException($"{owner}: invalid Camera 2D output, zoom, follow time, bounds or normalized viewport.");
    }

    private static readonly CameraRect Full=new(0,0,1,1), Inset=new(.73f,.03f,.25f,.25f);
    public CameraRect DefaultViewport() => Output==Minimap ? Inset : Full;
    public CameraView View(Size resolution,Vector2 center)
    {
        var v=Viewport??DefaultViewport();
        // Round shared edges, not widths: split views meet even at odd logical resolutions.
        static int Edge(float value,int extent) => Math.Clamp((int)MathF.Round(value*extent),0,extent);
        int left=Math.Min(resolution.Width-1,Edge(v.X,resolution.Width)), top=Math.Min(resolution.Height-1,Edge(v.Y,resolution.Height));
        var port=Rectangle.FromLTRB(left,top,Math.Max(left+1,Edge(v.X+v.Width,resolution.Width)),Math.Max(top+1,Edge(v.Y+v.Height,resolution.Height)));
        float zoom=Zoom;
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
