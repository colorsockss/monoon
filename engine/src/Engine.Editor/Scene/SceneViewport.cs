using System.Runtime.InteropServices;
using RectangleF = System.Drawing.RectangleF;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Engine.Core;
using Engine.Desktop;
using GameColor = System.Drawing.Color;

namespace Engine.Editor;

internal interface ISceneCanvas : ICanvas2D, IDiagnosticLines { }

public sealed partial class SceneViewport : Decorator
{
    private static readonly IBrush Surround = Brush.Parse("#14171C");
    internal static readonly GameColor SurroundColor = GameColor.FromArgb(20, 23, 28);
    private static readonly GameColor GridColor = GameColor.FromArgb(34, 43, 53);
    private static readonly GameColor FrameColor = GameColor.FromArgb(84, 99, 116);
    private static readonly GameColor SelectionColor = GameColor.FromArgb(167, 238, 225);
    private readonly MacSceneHost? native;
    private readonly PreviewText gameText = new();
    private readonly SpriteCache<Bitmap> bitmaps = new(path => new Bitmap(path));
    private ProjectAssets? sceneAssets;
    private readonly Dictionary<Guid,SkeletonPose> rigPreviews=[];
    private readonly HashSet<string> spriteErrors = [];
    // Editor-only Scene view state (Hierarchy eye and lock): never saved in the scene and ignored by Play.
    internal readonly HashSet<Guid> HiddenIds = [], LockedIds = [];
    internal bool IsHidden(SceneObject obj) => Marked(obj, HiddenIds);
    internal bool IsPickable(SceneObject obj) => !Marked(obj, HiddenIds) && !Marked(obj, LockedIds);
    private static bool Marked(SceneObject obj, HashSet<Guid> ids)
    {
        for (var node = obj; node != null && ids.Count > 0; node = node.Parent)
            if (ids.Contains(node.Id)) return true;
        return false;
    }
    // What the edit view draws, in draw order. The order is kept between redraws (pan, zoom, hover, tile strokes) and rebuilt
    // after scene edits (InvalidateUi), Hierarchy rebuilds or a different object list; Sample re-sorts Sort-by-Y rows as they move.
    private WorldDrawOrder? editOrder;
    private List<SceneObject>? editOrderObjects;
    private int editOrderCount;
    internal void InvalidateDrawOrder() => editOrder = null;
    internal IEnumerable<(SceneObject Owner, Tilemap2DComponent? Map, int Layer, int Order)> EditItems()
    {
        var objects = Scene.Objects;
        if (editOrder == null || editOrderObjects != objects || editOrderCount != objects.Count)
        {
            editOrder = new(objects, (index, _) => objects[index].WorldMatrix);
            editOrderObjects = objects;
            editOrderCount = objects.Count;
        }
        var order = editOrder;
        order.Sample(1);
        foreach (var item in order.Items)
            if (!IsHidden(objects[item.Index])) yield return (objects[item.Index], item.Map, item.Layer, item.Order);
    }
    private void DrawSceneSprite(ISceneCanvas canvas,SceneObject obj,RectangleF visible)
    {
        var shaderId=obj.GetComponent<SpriteShader2DComponent>()?.Shader;
        var rigId=obj.GetComponent<SkeletonAnimator2DComponent>()?.Rig;
        bool showSprite=visible.IntersectsWith(obj.Bounds);
        if (rigId==null && (!showSprite || shaderId==null && obj.GetComponent<SpriteRendererComponent>()==null)) return;
        var local=new RectangleF(0,0,obj.Width,obj.Height);
        try
        {
            if (showSprite) SceneAppearance.From(obj,Scene.ProjectDirectory,Scene.ProjectDirectory is {} root ?
                sceneAssets ??= ProjectAssets.ReadManifest(root) : null).Draw(canvas,local,obj.WorldMatrix);
            if (showSprite && shaderId is Guid id && Scene.ProjectDirectory is {} project)
            {
                var shader=(sceneAssets ??= ProjectAssets.ReadManifest(project)).LoadShader(project,id);
                canvas.DrawShader(shader,0,local,obj.WorldMatrix,GameColor.White,false);
            }
            if (rigId is Guid rig && Scene.ProjectDirectory is {} directory)
            {
                if (!rigPreviews.TryGetValue(rig,out var preview))
                    rigPreviews[rig]=preview=new((sceneAssets??=ProjectAssets.ReadManifest(directory)).LoadRig(directory,rig));
                preview.Draw(canvas,obj.WorldMatrix,System.Numerics.Matrix3x2.Identity,visible,1);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            if (spriteErrors.Add(ex.Message)) PlayFailed?.Invoke(ex.Message);
            canvas.DrawAffine(null,local,obj.WorldMatrix,GameColor.Magenta);
        }
    }
    private readonly SpriteVariants<Bitmap> variants = new((filter,tint) =>
    {
        using var pixels = SpritePixels.Filter(filter,tint);
        using var png = pixels.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);
        using var stream = png.AsStream();
        return new Bitmap(stream);
    }) { TintCacheCapacity = 32 };
    private readonly ShaderSheets<Bitmap> shaderSheets=new((clip,tint,_) =>
    {
        using var pixels=ShaderPixels.Bake(clip,tint);
        using var png=pixels.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);
        using var stream=png.AsStream();
        return new Bitmap(stream);
    });
    private Point editCenter;
    private double editZoom = 1;
    private Point? panStart;
    private bool pendingFrame;
    internal Point EditCenter => editCenter;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) };
    private GameInputRouter? input;
    private SdlGamepads? gamepads;
    internal Engine.Desktop.SdlFrameCapture? FrameCapture { get; private set; }
    private readonly Engine.Desktop.SdlSounds sounds = new();
    private GameSession? game;
    private bool draggingUi;
    private SceneObject? dragging;
    private bool movedDuringDrag;
    private Point dragUiOrigin;
    private Point dragTransform;
    private UiRectComponent? dragPins;
    private long lastTick;
    private GameLoop? loop;
    internal GameTime? PlayTime => game?.Time;

    public SceneDocument Scene { get; set; } = new();
    public SceneObject? Selected { get; set; }
    internal readonly PlayProfiler Profile=new();
    internal readonly PhysicsDiagnostics PhysicsDebug=new();
    internal bool ProbeArmed;
    internal Func<bool>? KeepSimulationActive;
    internal event Action<string>? ProbeFinished;
    internal event Action<GameLogEntry>? Logged;
    private void EmitLog(GameLogEntry entry)=>Logged?.Invoke(entry);
    internal bool NativeDraws=>native!=null;
    internal SceneGame? PlayWorld => game?.Current;
    internal event Action? RuntimeTick;
    public bool IsPlaying => game != null;
    public event Action<SceneObject?>? Picked;
    public event Action? Moved;
    public event Action? MoveFinished;
    public event Action<string>? PlayFailed;
    public event Action<string, Point>? PngDropped;

    private readonly bool spritePreview;
    private SceneAppearance? previewFrame;
    private readonly Dictionary<string,System.Drawing.Size> previewSizes = [];

    private ComponentPose previewPose;
    internal void ShowFrame(SceneAppearance? frame, ComponentPose pose = default)
    {
        if (previewFrame == frame && previewPose == pose) return;
        previewPose = pose;
        previewFrame = frame;
        RefreshScene();
    }

    public SceneViewport() : this(false) { }
    internal SceneViewport(bool spritePreview)
    {
        this.spritePreview = spritePreview;
        Focusable = true;
        ClipToBounds = true;
        if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            native = new MacSceneHost(this);
            Child = native;
        }
        if (spritePreview)
        {
            Focusable = false;
            AcceptFileDrops = false;
            SizeChanged += (_,_) => RefreshScene();
            DetachedFromVisualTree += (_,_) => { bitmaps.Clear(); variants.Dispose(); shaderSheets.Dispose(); gameText.Dispose(); };
            return;
        }
        SizeChanged += (_, _) => { if (pendingFrame) FrameCamera(); RefreshScene(); };
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnFileDrag);
        AddHandler(DragDrop.DropEvent, OnFileDrag);
        ConfigureIme();
        timer.Tick += (_, _) => Tick();
        PointerExited += (_,_) => { LeaveTiles(); PointerLeft(); };
        LostFocus += (_, _) => ResetInput();
        // A Scene panel moved to another window attaches again before Background work runs; keep its Play session.
        DetachedFromVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(this) != null) return;
            Stop(); bitmaps.Clear(); variants.Dispose(); shaderSheets.Dispose(); gameText.Dispose();
        }, DispatcherPriority.Background);
    }

    internal void ClearSprites()
    {
        previewUi = null;
        palettes.Clear();
        previewSizes.Clear();
        bitmaps.Clear(); variants.Dispose(); shaderSheets.Dispose(); gameText.Dispose(); sceneAssets = null; rigPreviews.Clear(); spriteErrors.Clear();
        native?.ClearSprites();
        RefreshScene();
    }

    internal void InvalidateSprite(string path)
    {
        previewUi = null;
        palettes.Clear();
        bitmaps.Invalidate(path); variants.Invalidate(path);
        native?.InvalidateSprite(path);
        RefreshScene();
    }

    internal bool AcceptFileDrops { get; set; } = true;
    internal bool CanDropPng(Point point) => AcceptFileDrops && !IsPlaying && new Rect(Bounds.Size).Contains(point);

    internal void DropPng(string path, Point point)
    {
        if (SpriteAssets.IsPng(path) && CanDropPng(point)) PngDropped?.Invoke(path, ToWorld(point));
    }

    private void OnFileDrag(object? sender, DragEventArgs e)
    {
        var point = e.GetPosition(this);
        var files = e.DataTransfer.TryGetFiles();
        var path = files is { Length: 1 } ? files[0].TryGetLocalPath() : null;
        bool accept = path != null && SpriteAssets.IsPng(path) && CanDropPng(point);
        e.DragEffects = accept ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        if (accept && e.RoutedEvent == DragDrop.DropEvent) DropPng(path!, point);
    }

    public void RefreshScene()
    {
        if (native == null) InvalidateVisual();
        else native.RequestFrame();
    }

    internal void ShowRendererError(string message)
    {
        Child = new TextBlock
        {
            Text = $"SDL Scene unavailable. {message}",
            Margin = new Thickness(24),
            TextWrapping = TextWrapping.Wrap
        };
    }
}
