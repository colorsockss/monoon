using System.Drawing;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;
using Point = Avalonia.Point;

namespace Engine.Editor;

internal sealed class MacSceneHost : NativeControlHost
{
    private const string Bridge = "BrutalScenePanel";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PointerCallback(int phase, double x, double y, double scroll);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GeometryCallback();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FileDragCallback([MarshalAs(UnmanagedType.LPUTF8Str)] string path, double x, double y, int drop);
    [DllImport(Bridge)]
    private static extern nint BrutalCreateScenePanel(PointerCallback pointer, GeometryCallback changed, FileDragCallback fileDrag);
    [DllImport(Bridge)]
    private static extern void BrutalDestroyScenePanel(nint view);
    [DllImport(Bridge)]
    private static extern int BrutalScenePanelAttached(nint view);
    [DllImport(Bridge)]
    private static extern void BrutalSetScenePanelCursor(nint view, byte[]? rgba, int width, int height, int hotX, int hotY);
    internal void SetGameCursor(RgbaImage? image, int hotX, int hotY) =>
        BrutalSetScenePanelCursor(view, image?.Pixels, image?.Width ?? 0, image?.Height ?? 0, hotX, hotY);
    [DllImport(Bridge)]
    private static extern int BrutalSizeSceneDrawable(nint view, nint layer, double panelWidth, double panelHeight, out double width, out double height);

    [DllImport(Bridge)]
    private static extern double BrutalKeyEventAge();

    internal static long KeyTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp() -
        (long)(BrutalKeyEventAge() * System.Diagnostics.Stopwatch.Frequency);

    private readonly SceneViewport viewport;
    private readonly PointerCallback pointerCallback;
    private readonly GeometryCallback geometryCallback;
    private readonly FileDragCallback fileDragCallback;
    private nint view;
    private nint layer;
    private SdlWindow window;
    private SdlRenderer renderer;
    private SdlCanvas? canvas;
    private bool videoInitialized;
    private bool framePending;

    public MacSceneHost(SceneViewport viewport)
    {
        this.viewport = viewport;
        Focusable = false;
        pointerCallback = OnNativePointer;
        geometryCallback = RequestFrame;
        fileDragCallback = OnFileDrag;
        SizeChanged += (_, _) => RequestFrame();
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        view = BrutalCreateScenePanel(pointerCallback, geometryCallback, fileDragCallback);
        RequestFrame();
        return new PlatformHandle(view, "NSView");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        view = 0;
        ReleaseRenderer();
        BrutalDestroyScenePanel(control.Handle);
    }

    internal void RequestFrame()
    {
        if (framePending || view == 0) return;
        framePending = true;
        Dispatcher.UIThread.Post(DrawFrame, DispatcherPriority.Loaded);
    }

    private unsafe void DrawFrame()
    {
        framePending = false;
        if (viewport.Child != this || view == 0 || BrutalScenePanelAttached(view) == 0 || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        if (canvas == null)
        {
            try { InitializeRenderer(); }
            catch (Exception ex) when (ex is SdlException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
            {
                viewport.ShowRendererError(ex.Message);
                return;
            }
        }
        if (BrutalSizeSceneDrawable(view, layer, Bounds.Width, Bounds.Height, out var width, out var height) == 0) return;

        // SDL normally derives its default viewport from NSWindow notifications.
        // A child can resize after that notification, or independently of the window.
        var pixels = new SdlRect { w = (int)width, h = (int)height };
        renderer.SetRenderViewport(&pixels);
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        var calls=canvas!.DrawSubmissions;
        long bytes = viewport.FrameCapture != null ? GC.GetAllocatedBytesForCurrentThread() : 0;
        canvas.Clear(SceneViewport.SurroundColor);
        var origin = viewport.Origin;
        double px = width / Bounds.Width, py = height / Bounds.Height;
        var area = viewport.CanvasArea;
        if (viewport.IsPlaying)
        {
            var clip = new SdlRect { x = (int)Math.Round(origin.X * px), y = (int)Math.Round(origin.Y * py),
                w = (int)Math.Round(area.Width * viewport.Scale * px), h = (int)Math.Round(area.Height * viewport.Scale * py) };
            renderer.SetRenderClipRect(&clip);
        }
        using (FrameProfiler.Sample(FrameProfiler.Render))
        {
            viewport.DrawWorld(new WorldCanvas(canvas, origin.X * px, origin.Y * py, viewport.Scale * px, viewport.Scale * py, area));
            canvas.FlushSprites();
        }
        if (viewport.IsPlaying) renderer.SetRenderClipRect(null);
        using (FrameProfiler.Sample(FrameProfiler.Present)) renderer.RenderPresent();
        viewport.FrameCapture?.Frame(viewport.PlayWorld, canvas.DrawSubmissions - calls, bytes);
        if(viewport.IsPlaying)viewport.Profile.Complete(started,canvas.DrawSubmissions-calls);
        // AppKit delivers the editor's input; never poll SDL's application-wide event pump.
        Sdl.Events.FlushEvents(0, 0xffff);
    }

    private unsafe void InitializeRenderer()
    {
        if (Sdl.PInvoke.GetRevision()?.Contains("BrutalEditor-PR16658", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("The Scene panel needs the bundled SDL 3.4.12 Cocoa patch.");
        Sdl.Init.InitSubSystem(SdlInitFlags.Video);
        videoInitialized = true;
        try
        {
            uint properties = Sdl.PInvoke.CreateProperties();
            try
            {
                if (properties == 0 ||
                    Sdl.PInvoke.SetPointerProperty(properties, Sdl.Constants.PropWindowCreateCocoaViewPointer, (void*)view) == 0 ||
                    Sdl.PInvoke.SetBooleanProperty(properties, Sdl.Constants.PropWindowCreateHighPixelDensityBoolean, 1) == 0)
                    throw new InvalidOperationException("Could not configure the SDL Scene view.");
                window = Sdl.Video.CreateWindowWithProperties(properties);
            }
            finally { Sdl.PInvoke.DestroyProperties(properties); }
            renderer = Sdl.Render.CreateRenderer(window, "metal");
            layer = (nint)Sdl.PInvoke.GetRenderMetalLayer(renderer);
            if (BrutalSizeSceneDrawable(view, layer, Bounds.Width, Bounds.Height, out _, out _) == 0)
                throw new InvalidOperationException("SDL's Metal surface is outside the Scene panel.");
            canvas = new SdlCanvas(renderer, batchSprites: true, previewShader: ShaderPixels.Preview);
        }
        catch
        {
            ReleaseRenderer();
            throw;
        }
    }

    private void ReleaseRenderer()
    {
        if (!videoInitialized) return;
        canvas?.Dispose();
        canvas = null;
        layer = 0;
        renderer.Dispose();
        window.Dispose();
        renderer = default;
        window = default;
        Sdl.Init.QuitSubSystem(SdlInitFlags.Video);
        videoInitialized = false;
    }

    internal void PrepareShader(ShaderClip shader)
    {
        if (canvas!=null) canvas.PrepareShader(shader);
        else ShaderPixels.Validate(shader);
    }
    internal void ReleaseShader(ShaderClip shader) => canvas?.ReleaseShader(shader);
    internal void Prepare(string path)
    {
        if (canvas != null) canvas.Prepare(path);
        else SdlCanvas.ReadPngSize(path);
    }
    internal void Upload(string path, IDisposable decoded)
    {
        if (canvas != null) canvas.Upload(path, decoded);
        else SdlCanvas.ReadPngSize(path);
    }
    internal void ClearSprites() => canvas?.ClearSprites();
    internal void InvalidateSprite(string path) => canvas?.InvalidateSprite(path);

    private void OnNativePointer(int phase, double x, double y, double scroll)
    {
        if (viewport.IsSpritePreview) return;
        var point=new Point(x,y);
        if(viewport.IsPlaying)
        {
            switch(phase)
            {
                case 0: case 10: case 2: viewport.GamePointer(point,"Mouse.Left",phase!=2); break;
                case 3: case 5: viewport.GamePointer(point,"Mouse.Right",phase==3); break;
                case 8: case 9: viewport.GamePointer(point,"Mouse.Middle",phase==8); break;
                case 4: viewport.GameWheel(point,scroll); break;
                case 7: viewport.PointerLeft(); break;
                default: viewport.GamePointer(point); break;
            }
            return;
        }
        if (phase is 3 or 8) viewport.BeginPan(new Point(x, y));
        else if (phase == 4) viewport.ZoomAt(new Point(x, y), scroll);
        else if (phase is 0 or 10) viewport.BeginDrag(new Point(x, y),phase==10);
        else if (phase == 7) viewport.LeaveTiles();
        else if (phase is 1 or 6) viewport.DragTo(new Point(x, y));
        else if (phase == 2) viewport.ReleaseAt(new Point(x, y));
        else viewport.EndDrag();
    }

    private int OnFileDrag(string path, double x, double y, int drop)
    {
        var point = new Point(x, y);
        if (!viewport.CanDropPng(point)) return 0;
        if (drop != 0) Dispatcher.UIThread.Post(() => viewport.DropPng(path, point));
        return 1;
    }

    private sealed class WorldCanvas(SdlCanvas target, double x, double y, double sx, double sy, Avalonia.Rect area) : ISceneCanvas
    {
        public void DrawSpriteInstances(SceneAppearance sprite, ReadOnlySpan<SpriteInstance2D> instances,
            System.Numerics.Matrix3x2 view, bool batched) =>
            target.DrawSpriteInstances(sprite, instances, view * new System.Numerics.Matrix3x2((float)sx,0,0,(float)sy,(float)x,(float)y), batched);
        public void DrawShader(ShaderClip shader,int frame,RectangleF local,System.Numerics.Matrix3x2 matrix,Color tint,bool additive) =>
            target.DrawShader(shader,frame,local,matrix * new System.Numerics.Matrix3x2((float)sx,0,0,(float)sy,(float)x,(float)y),tint,additive);
        public void FillStrip(ReadOnlySpan<System.Numerics.Vector2> edges, ReadOnlySpan<Color> colors, System.Numerics.Matrix3x2 matrix) =>
            target.FillStrip(edges,colors,matrix * new System.Numerics.Matrix3x2((float)sx,0,0,(float)sy,(float)x,(float)y));
        public void Clear(Color color) => FillRectangle(new RectangleF((float)area.X, (float)area.Y, (float)area.Width, (float)area.Height), color);
        public bool DrawFiltered(SpriteFilter filter, RectangleF local, System.Numerics.Matrix3x2 matrix, Color tint) =>
            target.DrawFiltered(filter,local,matrix * new System.Numerics.Matrix3x2((float)sx,0,0,(float)sy,(float)x,(float)y),tint);
        public bool DrawAffine(string? path, RectangleF local, System.Numerics.Matrix3x2 matrix, Color tint, SpriteRegion? source = null, string layout = TileGrid.Orthogonal) =>
            target.DrawAffine(path,local,matrix * new System.Numerics.Matrix3x2((float)sx,0,0,(float)sy,(float)x,(float)y),tint,source,layout);
        public IDisposable PushClip(RectangleF r) => target.PushClip(ToPixels(r));
        public void BeginColorAdjust() => target.BeginColorAdjust();
        public void EndColorAdjust(Color multiply, byte lift) => target.EndColorAdjust(multiply, lift);
        private RectangleF ToPixels(RectangleF r) => new((float)(x + r.X * sx), (float)(y + r.Y * sy),
            (float)(r.Width * sx), (float)(r.Height * sy));
        public void FillRectangle(RectangleF r, Color color) => target.FillRectangle(ToPixels(r), color);
        public void DrawSquareSprite(RectangleF r, Color color) => target.DrawSquareSprite(ToPixels(r), color);
        public void UseFonts(IReadOnlyList<string> paths) => target.UseFonts(paths);
        public float MeasureText(string text,float size)=>target.MeasureText(text,size);
        public void MeasureTextEnds(string text,float size,Span<float> ends)=>target.MeasureTextEnds(text,size,ends);
        public void DrawText(string text, float left, float top, float size, Color color) => target.DrawText(text, (float)(x + left * sx), (float)(y + top * sy), (float)(size * sy), color);
        public bool DrawTile(string path, RectangleF r, SpriteRegion source, string layout) => target.DrawTile(path,ToPixels(r),source,layout);
        public void DrawLine(System.Numerics.Vector2 start, System.Numerics.Vector2 end, float width, Color color) =>
            target.DrawLine(new((float)(x+start.X*sx),(float)(y+start.Y*sy)),new((float)(x+end.X*sx),(float)(y+end.Y*sy)),(float)(width*sy),color);
        public bool DrawSprite(string path, RectangleF r) => DrawSprite(path, r, null);
        public bool DrawSprite(string path, RectangleF r, SpriteRegion? source) => target.DrawSprite(path, ToPixels(r), source);
    }
}
