using System.Diagnostics;
using System.Runtime.InteropServices;
using RectangleF = System.Drawing.RectangleF;
using Matrix3x2 = System.Numerics.Matrix3x2;
using Vector2 = System.Numerics.Vector2;
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
    private ScreenUi? previewUi;
    internal sealed record UiPreview(int Width, int Height, string Locale, float TextScale);
    internal UiPreview? Preview { get; private set; }
    private GameSettings? previewSource, previewSettings;
    internal GameSettings LayoutSettings
    {
        get
        {
            if (Preview is not {} preview) return Scene.Settings;
            if (previewSettings == null || !ReferenceEquals(previewSource, Scene.Settings))
            {
                previewSource = Scene.Settings;
                previewSettings = previewSource with { Width = preview.Width, Height = preview.Height,
                    Accessibility = previewSource.Accessibility with { Locale = preview.Locale, TextScale = preview.TextScale } };
            }
            return previewSettings;
        }
    }
    internal void SetUiPreview(UiPreview? preview)
    {
        Preview = preview;
        previewSettings = null;
        InvalidateUi();
    }

    internal (RectangleF Box, RectangleF Parent) UiPlacement(SceneObject obj)
    {
        var ui = PreviewUi;
        var box = ui.Bounds(obj.Id) ?? RectangleF.Empty;
        for (var parent = obj.Parent; parent != null; parent = parent.Parent)
            if (ui.Bounds(parent.Id) is {} bounds) return (box, bounds);
        return (box, new RectangleF(0, 0, LayoutSettings.Width, LayoutSettings.Height));
    }
    private ScreenUi PreviewUi
    {
        get
        {
            if (previewUi == null)
            {
                var document = Preview == null ? Scene : Scene.WithSettings(LayoutSettings);
                try { previewUi = new(document, Scene.ProjectDirectory is { } root ? ProjectAssets.ReadManifest(root) : null); }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
                {
                    previewUi = new(document, null);
                    if (spriteErrors.Add(ex.Message)) PlayFailed?.Invoke(ex.Message);
                }
            }
            previewUi.PreviewModeFor(Selected?.Id);
            return previewUi;
        }
    }
    internal void InvalidateUi() { previewUi = null; RefreshScene(); }
    private (RectangleF Frame, float Zoom) UiFrame()
    {
        if (Scene.Camera is {} camera)
        {
            var shot=camera.GetComponent<Camera2DComponent>()!.View(LayoutSettings.Resolution,camera.WorldPosition);
            var pos=shot.ScreenToWorld(Vector2.Zero);
            return (new(pos.X,pos.Y,LayoutSettings.Width/shot.Zoom,LayoutSettings.Height/shot.Zoom),shot.Zoom);
        }
        const float zoom=1;
        var center = Scene.Camera?.WorldPosition ?? new Vector2(LayoutSettings.Width / 2f, LayoutSettings.Height / 2f);
        return (LayoutSettings.CameraFrame(center, zoom), zoom);
    }
    private Point EditUiPoint(Point world)
    {
        var (f, z) = UiFrame();
        return new((world.X - f.Left) * z, (world.Y - f.Top) * z);
    }
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
        DetachedFromVisualTree += (_, _) => { Stop(); bitmaps.Clear(); variants.Dispose(); shaderSheets.Dispose(); gameText.Dispose(); };
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

    internal void ResetInput()
    {
        input?.Reset();SyncIme();
        gamepads?.SuppressHeld();
        EndDrag();
    }

    public void Play() => Play(null);

    private SceneDebugServer? playDebugger;
    internal void Play(SceneDebugServer? debugger)
    {
        playDebugger = System.Diagnostics.Debugger.IsAttached ? null : debugger;
        var scripts = Scene.ProjectDirectory is {} root
            ? ProjectScripts.ForScenePlay(root, playDebugger != null ? playDebugger.Instrument : null) : null;
        game = new GameSession(Scene, scripts, null, null, Preview == null ? null : LayoutSettings);
        Profile.Reset();
        game.Current.Diagnostics=PhysicsDebug;
        game.State.Logged+=EmitLog;
        sounds.Attach(game.Mixer);
        game.LoadFailed += message => PlayFailed?.Invoke(message);
        try
        {
            FrameCapture = Engine.Desktop.SdlFrameCapture.Open();
            game.UseResources(new SceneResources(path =>
            {
                if (native != null) native.Prepare(path);
                else if (!bitmaps.TryGet(path, out _)) throw new InvalidDataException($"Could not load sprite {path}.");
            }, path =>
            {
                bitmaps.Invalidate(path); variants.Invalidate(path);
                native?.InvalidateSprite(path);
            }, sounds.PreparePlayback, sounds.Release, shader =>
            {
                if (native!=null) native.PrepareShader(shader);
                else shaderSheets.Get(shader,GameColor.White,shader.Additive);
            }, shader => { shaderSheets.Release(shader); native?.ReleaseShader(shader); }));
        }
        catch { FrameCapture?.Dispose(); FrameCapture = null; sounds.Dispose(); game.Dispose(); game = null; throw; }
        input = new(game.Actions, () => game?.InputUi, game.LogicalSize, () => game?.InputUi == null ? null : game.Current);
        gamepads = new SdlGamepads();
        loop = new GameLoop(game, game.Actions);
        playDebugger?.TakePausedTicks();
        lastTick = Stopwatch.GetTimestamp();
        Focus();
        timer.Start();
        RefreshScene();
    }

    public void Stop()
    {
        timer.Stop();
        gamepads?.Dispose(); gamepads=null; input=null;
        var current = game;
        game = null;
        ProbeArmed=consumingProbe=false;
        PhysicsDebug.Clear();
        loop = null;
        try { current?.Dispose(); }
        catch (InvalidOperationException ex) { PlayFailed?.Invoke(ex.Message); }
        finally { playDebugger?.EndPlay(); playDebugger = null; FrameCapture?.Dispose(); FrameCapture = null; if(current!=null)current.State.Logged-=EmitLog; sounds.Dispose(); ResetInput(); RefreshScene(); }
    }

    private void Tick()
    {
        if(sounds.TakeError() is {} audioError) PlayFailed?.Invoke(audioError);
        if (input != null && IsKeyboardFocusWithin && TopLevel.GetTopLevel(this) is Window { IsActive: true }) gamepads?.Poll(input.Pad);
        var now = Stopwatch.GetTimestamp();
        // Waiting at a script breakpoint consumes wall time but must not advance game or UI clocks.
        long paused = playDebugger?.TakePausedTicks() ?? 0;
        var elapsed = Math.Max(0, now - lastTick - paused) / (double)Stopwatch.Frequency;
        lastTick = now;
        if (TopLevel.GetTopLevel(this) is not Window { IsActive: true } && KeepSimulationActive?.Invoke()!=true)
        {
            Profile.Break();loop?.ResetElapsed();
            return;
        }
        var updating=Stopwatch.GetTimestamp();
        var prior=PlayWorld;
        try { FrameCapture?.BeginUpdate(); loop?.Advance(elapsed); FrameCapture?.EndUpdate(); }
        catch (InvalidOperationException ex)
        {
            Stop();
            PlayFailed?.Invoke(ex.Message);
            return;
        }
        Profile.AddUpdate(updating);
        if(game!=null && prior!=game.Current){
            ProbeArmed=false;
            EmitLog(new(GameLogLevel.Info,$"Scene loaded: {game.Current.SceneLabel}",null,0));
        }
        SyncIme();RuntimeTick?.Invoke();
        RefreshScene();
    }

    internal static string BindingKey(KeyEventArgs e) =>
        InputControls.KeyName(e.PhysicalKey == PhysicalKey.None ? e.Key.ToString() : e.PhysicalKey.ToString());

    internal bool RouteUiKey(KeyEventArgs e,bool down)
    {
        bool editingBefore=game?.InputUi?.Editing!=null;
        long? timestamp = down && input != null && OperatingSystem.IsMacOS() && Child is MacSceneHost
            ? MacSceneHost.KeyTimestamp() : null;
        bool used=input?.Key(BindingKey(e),down,e.KeyModifiers.HasFlag(KeyModifiers.Shift),timestamp)==true;
        SyncIme();
        // Let the platform turn printable key-downs into marked/committed text.
        if(editingBefore && e.Key is not (Key.Tab or Key.Enter or Key.Escape or Key.Back or Key.Delete or Key.Left or Key.Right or Key.Home or Key.End))
            return false;
        return used;
    }

    private bool consumingProbe;
    internal void GamePointer(Point point,string? control=null,bool down=false)
    {
        if (down) Focus();
        var p=ToWorld(point);
        if(control=="Mouse.Left" && !down && consumingProbe){consumingProbe=false;return;}
        if(control=="Mouse.Left" && down && ProbeArmed && PlayWorld is {} world){
            ProbeArmed=false;consumingProbe=true;
            if(Selected!=null && world.TryDebugRay(Selected.Id,new((float)p.X,(float)p.Y),out var hit))
                ProbeFinished?.Invoke(hit is {} h?$"Raycast hit {world.ObjectLabel(h.Target.ObjectId)} · {h.Distance:0.#} units":"Raycast missed.");
            else ProbeFinished?.Invoke("Choose an active object and a point inside a camera view.");
            RefreshScene();return;
        }
        input?.Pointer(new((float)p.X,(float)p.Y),control,down); SyncIme();
    }
    internal void PointerLeft() => input?.Pointer(new(-1,-1));
    internal void GameWheel(Point point,double steps)
    {
        var p=ToWorld(point); input?.Wheel(new((float)p.X,(float)p.Y),(float)steps);
    }
    internal double Scale => spritePreview ? 1 : IsPlaying
        ? Math.Max(double.Epsilon, Math.Min(Bounds.Width / LayoutSettings.Width, Bounds.Height / LayoutSettings.Height))
        : editZoom;
    internal Point Origin => spritePreview ? default : IsPlaying
        ? new((Bounds.Width - LayoutSettings.Width * Scale) / 2, (Bounds.Height - LayoutSettings.Height * Scale) / 2)
        : new(Bounds.Width / 2 - editCenter.X * Scale, Bounds.Height / 2 - editCenter.Y * Scale);
    internal Point ToWorld(Point point) => new((point.X - Origin.X) / Scale, (point.Y - Origin.Y) / Scale);
    internal Rect CanvasArea => IsPlaying ? new Rect(0, 0, LayoutSettings.Width, LayoutSettings.Height)
        : new Rect(ToWorld(default), ToWorld(new Point(Bounds.Width, Bounds.Height)));

    internal bool PrefabFraming { get; set; }

    public void FrameCamera()
    {
        var camera = Selected?.GetComponent<Camera2DComponent>() != null ? Selected : Scene.Camera;
        var center = camera?.WorldPosition ?? Vector2.Zero;
        editCenter = new Point(center.X, center.Y);
        pendingFrame = Bounds.Width <= 0 || Bounds.Height <= 0;
        if (pendingFrame) return;
        var frame = camera?.GetComponent<Camera2DComponent>()?.View(LayoutSettings.Resolution,center).World
            ?? (Scene.IsUiOnly ? UiFrame().Frame : LayoutSettings.CameraFrame(center,1));
        if (PrefabFraming && Scene.Objects.Count > 0)
        {
            var bounds = Scene.Objects.Select(o => o.Bounds).Aggregate(RectangleF.Union);
            frame = RectangleF.Inflate(bounds, Math.Max(16, bounds.Width * .12f), Math.Max(16, bounds.Height * .12f));
        }
        editCenter = new Point(frame.X+frame.Width/2,frame.Y+frame.Height/2);
        editZoom = Math.Clamp(.85 * Math.Min(Bounds.Width / frame.Width, Bounds.Height / frame.Height), .01, 32);
        RefreshScene();
    }

    internal void ZoomAt(Point point, double steps)
    {
        if (IsPlaying) return;
        var anchor = ToWorld(point);
        editZoom = Math.Clamp(editZoom * Math.Pow(1.2, steps), .01, 32);
        editCenter += anchor - ToWorld(point);
        RefreshScene();
    }

    internal void BeginPan(Point point)
    {
        if (IsPlaying) return;
        Focus();
        EndDrag();
        panStart = point;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (native != null) return;
        if(IsPlaying) { GameWheel(e.GetPosition(this),e.Delta.Y); e.Handled=true; return; }
        ZoomAt(e.GetPosition(this), e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var buttons = e.GetCurrentPoint(this).Properties;
        if(IsPlaying)
        {
            var button=buttons.PointerUpdateKind switch { PointerUpdateKind.LeftButtonPressed=>"Mouse.Left",
                PointerUpdateKind.RightButtonPressed=>"Mouse.Right",PointerUpdateKind.MiddleButtonPressed=>"Mouse.Middle",_=>null };
            if(button!=null) { GamePointer(e.GetPosition(this),button,true); e.Pointer.Capture(this); e.Handled=true; }
            return;
        }
        if (buttons.IsRightButtonPressed || buttons.IsMiddleButtonPressed) BeginPan(e.GetPosition(this));
        else if (buttons.IsLeftButtonPressed) BeginDrag(e.GetPosition(this), (e.KeyModifiers & (KeyModifiers.Shift|KeyModifiers.Meta|KeyModifiers.Control))!=0);
        if (IsPlaying || stroke != null || gesture!=null || dragging != null || panStart != null) e.Pointer.Capture(this);
    }

    internal void BeginDrag(Point point,bool toggle=false)
    {
        Focus();
        var p = ToWorld(point);
        if (game != null) { GamePointer(point,"Mouse.Left",true); return; }
        EndDrag();
        if (BeginTiles(p)) return;
        var worldPoint=WorldPoint(p);
        if(!toggle && HitGrip(worldPoint) is var handle && handle!=Grip.None){BeginGesture(worldPoint,handle);return;}
        var u = EditUiPoint(p);
        dragging = PreviewUi.Pick((float)u.X, (float)u.Y);
        draggingUi = dragging != null;
        dragging ??= Scene.DrawOrder().LastOrDefault(obj => obj.GetComponent<SpriteRendererComponent>() != null &&
            obj.ContainsWorldPoint(new((float)p.X, (float)p.Y)));
        dragging ??= Scene.Cameras.LastOrDefault(camera =>
            Math.Abs(p.X - camera.WorldPosition.X) * Scale <= 8 && Math.Abs(p.Y - camera.WorldPosition.Y) * Scale <= 8);
        if(toggle)
        {
            TogglePicked?.Invoke(dragging);dragging=null;draggingUi=false;return;
        }
        if(dragging==null || !Selection.Contains(dragging))Picked?.Invoke(dragging);
        if(dragging!=null && !draggingUi)
        {
            if(HasWorldSelection && Gizmo==GizmoTool.Move)BeginGesture(worldPoint,Grip.Center);
            dragging=null;return;
        }
        if(Selection.Count>1){dragging=null;return;}
        if (dragging != null)
        {
            dragUiOrigin = u;
            dragTransform = new(dragging.X, dragging.Y);
            dragPins = dragging.GetComponent<UiRectComponent>() is {} rect ? rect with { } : null;
        }
        else BeginPan(point);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        DragTo(e.GetPosition(this));
    }

    internal void EndDrag()
    {
        CancelStroke();
        panStart = null;
        gesture=null;
        draggingUi = false;
        dragging = null;
        if (!movedDuringDrag) return;
        movedDuringDrag = false;
        MoveFinished?.Invoke();
    }

    internal void DragTo(Point point)
    {
        if(IsPlaying) { GamePointer(point); return; }
        if (panStart is { } last)
        {
            editCenter -= (point - last) / Scale;
            panStart = point;
            RefreshScene();
            return;
        }
        if (!IsPlaying && EditMap != null)
        {
            var cell=TileAt(ToWorld(point));
            if (hover!=cell) { hover=cell; stroke?.Move(cell); RefreshScene(); }
            return;
        }
        if(gesture!=null){UpdateGesture(WorldPoint(ToWorld(point)));return;}
        if (dragging is null || IsPlaying) return;
        if (draggingUi)
        {
            var delta = EditUiPoint(ToWorld(point)) - dragUiOrigin;
            float dx = (float)Math.Round(delta.X), dy = (float)Math.Round(delta.Y);
            var rect = dragging.GetComponent<UiRectComponent>();
            if (rect != null && dragPins != null)
            {
                rect.Left = dragPins.Left + dx;
                rect.Right = dragPins.Right - dx;
                rect.Top = dragPins.Top + dy;
                rect.Bottom = dragPins.Bottom - dy;
            }
            if (rect?.Left == null && rect?.Right == null)
                dragging.X = (float)dragTransform.X + dx / (rect?.ScaleHorizontalWithText == true ? PreviewUi.Preferences.TextScale : 1);
            if (rect?.Top == null && rect?.Bottom == null) dragging.Y = (float)dragTransform.Y + dy;
            movedDuringDrag |= dx != 0 || dy != 0;
            InvalidateUi(); Moved?.Invoke(); return;
        }

    }

    internal void ReleaseAt(Point point)
    {
        var p = ToWorld(point);
        if (stroke is { } active && EditMap != null)
        {
            stroke=null;
            try
            {
                var changes=active.Finish(TileAt(p));
                if (changes.Length>0) TilesChanged?.Invoke(Selected!.Id,changes);
            }
            catch (InvalidOperationException ex) { active.Cancel(); PlayFailed?.Invoke(ex.Message); }
            RefreshScene();
        }
        if(IsPlaying) GamePointer(point,"Mouse.Left",false);
        if(gesture!=null)UpdateGesture(WorldPoint(p));
        EndDrag();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if(IsPlaying)
        {
            var button=e.GetCurrentPoint(this).Properties.PointerUpdateKind switch { PointerUpdateKind.LeftButtonReleased=>"Mouse.Left",
                PointerUpdateKind.RightButtonReleased=>"Mouse.Right",PointerUpdateKind.MiddleButtonReleased=>"Mouse.Middle",_=>null };
            if(button!=null) GamePointer(e.GetPosition(this),button,false);
            var props=e.GetCurrentPoint(this).Properties;
            if(!props.IsLeftButtonPressed && !props.IsRightButtonPressed && !props.IsMiddleButtonPressed && e.Pointer.Captured==this)
                e.Pointer.Capture(null);
            e.Handled=true; return;
        }
        if (e.InitialPressMouseButton == MouseButton.Left) ReleaseAt(e.GetPosition(this)); else EndDrag();
        if (e.Pointer.Captured == this) e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        input?.CancelPointer();
        EndDrag();
    }

    public override void Render(DrawingContext context)
    {
        if (native != null) return;
        context.FillRectangle(Surround, new Rect(Bounds.Size));
        using var transform = context.PushTransform(Matrix.CreateScale(Scale, Scale) * Matrix.CreateTranslation(Origin.X, Origin.Y));
        var area = CanvasArea;
        using var clip = context.PushClip(area);
        var started=Stopwatch.GetTimestamp();
        var canvas=new PreviewCanvas(context, bitmaps, variants, shaderSheets, gameText, area);
        DrawWorld(canvas);
        if(IsPlaying)Profile.Complete(started,canvas.Submissions);
    }

    internal bool IsSpritePreview => spritePreview;

    internal void DrawWorld(ISceneCanvas canvas)
    {
        if (spritePreview)
        {
            canvas.Clear(SceneGame.Background);
            if (previewFrame is not { Image: {} path } frame) return;
            var size = frame.Canvas is {} trim ? new System.Drawing.Size(trim.Width,trim.Height) :
                frame.Source is {} cell ? new System.Drawing.Size(cell.Width,cell.Height) :
                previewSizes.TryGetValue(path,out var cached) ? cached : previewSizes[path] = SpriteRegion.ReadPngSize(path);
            float scale = (float)Math.Min(Bounds.Width/size.Width,Bounds.Height/size.Height);
            float width=size.Width*scale, height=size.Height*scale;
            var pivot = new Vector2((float)Bounds.Width/2,(float)Bounds.Height/2);
            var transform = Matrix3x2.CreateScale(previewPose.ScaleX ?? 1,previewPose.ScaleY ?? 1,pivot)
                * Matrix3x2.CreateRotation(float.DegreesToRadians(previewPose.Rotation ?? 0),pivot);
            (frame with { Color = previewPose.Paint(frame.Color) }).Draw(canvas,
                new((float)(Bounds.Width-width)/2,(float)(Bounds.Height-height)/2,width,height),transform);
            return;
        }
        if (game != null)
        {
            PhysicsDebug.Selection=Selected?.Id;
            game.Draw(canvas, loop?.Interpolation ?? 1f);
            return;
        }
        canvas.Clear(SceneGame.Background);
        var area = CanvasArea;
        float unit = (float)(1 / Scale);
        if (EditMap == null)
        {
            double step = 40 * Math.Pow(2, Math.Ceiling(Math.Log2(0.6 / Scale)));
            double left = Math.Floor(area.Left / step) * step, top = Math.Floor(area.Top / step) * step;
            for (int i = 0; i <= Math.Ceiling(area.Width / step); i++)
                canvas.FillRectangle(new RectangleF((float)(left + i * step), (float)area.Top, unit, (float)area.Height), GridColor);
            for (int i = 0; i <= Math.Ceiling(area.Height / step); i++)
                canvas.FillRectangle(new RectangleF((float)area.Left, (float)(top + i * step), (float)area.Width, unit), GridColor);
        }
        var visible = new RectangleF((float)area.X, (float)area.Y, (float)area.Width, (float)area.Height);
        foreach (var (obj,map,_,_) in Scene.WorldItems())
        {
            if (map != null)
            {
                if (Palette(map.Tileset) is { } palette) map.Draw(canvas,palette,obj.WorldMatrix,visible,System.Numerics.Matrix3x2.Identity);
                continue;
            }
            DrawSceneSprite(canvas,obj,visible);
        }
        foreach (var obj in Scene.Objects)
            if (obj.GetComponent<PointLight2DComponent>() is {} light)
                light.Draw(canvas,ShaderClip.LightFalloff,Vector2.Transform(new(obj.Width*.5f,obj.Height*.5f),obj.WorldMatrix),visible,System.Numerics.Matrix3x2.Identity,0);
        foreach (var camera in Scene.Cameras)
        {
            var center = camera.WorldPosition;
            var lens=camera.GetComponent<Camera2DComponent>()!;
            var frame=lens.View(LayoutSettings.Resolution,center).World;
            if (camera==Selected && lens.Bounds is {} box)
                Outline(canvas,new RectangleF(box.X,box.Y,box.Width,box.Height),unit,FrameColor);
            var color = camera == Selected ? SelectionColor : camera == Scene.Camera ? GameColor.FromArgb(135, 150, 158) : FrameColor;
            canvas.DrawText( (camera == Scene.Camera ? "Start / " : "") + camera.Name, frame.Left + 3 * unit, frame.Top + 3 * unit, 11 * unit, color);
            Outline(canvas, frame, 1.3f * unit, color);
            canvas.FillRectangle(new RectangleF(center.X - 6 * unit, center.Y - unit, 12 * unit, 2 * unit), color);
            canvas.FillRectangle(new RectangleF(center.X - unit, center.Y - 6 * unit, 2 * unit, 12 * unit), color);
        }
        DrawTileGuide(canvas,visible,unit);
        var (uiFrame, zoom) = UiFrame();
        using (Preview == null ? null : canvas.PushClip(uiFrame))
            PreviewUi.Draw(canvas, uiFrame.X, uiFrame.Y, 1 / zoom);
        if (Preview != null) Outline(canvas, uiFrame, unit, FrameColor);
        DrawSelection(canvas,unit);
    }

    private static void Outline(ICanvas2D canvas, RectangleF box, float line, GameColor color)
    {
        canvas.FillRectangle(new RectangleF(box.Left, box.Top, box.Width, line), color);
        canvas.FillRectangle(new RectangleF(box.Left, box.Bottom - line, box.Width, line), color);
        canvas.FillRectangle(new RectangleF(box.Left, box.Top, line, box.Height), color);
        canvas.FillRectangle(new RectangleF(box.Right - line, box.Top, line, box.Height), color);
    }

    private sealed class PreviewText : IDisposable
    {
        public readonly Engine.Desktop.GameTextRasterizer Renderer = new();
        private readonly Dictionary<string,Bitmap> cache = [];
        public bool Configured { get; private set; }
        public void UseFonts(IReadOnlyList<string> paths)
        {
            if (Renderer.UseFonts(paths)) Clear();
            Configured = paths.Count > 0;
        }
        public Bitmap Get(string text)
        {
            if (cache.TryGetValue(text, out var existing)) return existing;
            if (cache.Count >= 256) Clear();
            using var pixels = Renderer.Rasterize(text);
            using var png = pixels.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var stream = png.AsStream();
            return cache[text] = new Bitmap(stream);
        }
        private void Clear()
        {
            foreach (var bitmap in cache.Values) bitmap.Dispose();
            cache.Clear();
        }
        public void Dispose() { Clear(); Renderer.Dispose(); Configured = false; }
    }

    private sealed class PreviewCanvas(DrawingContext context, SpriteCache<Bitmap> images, SpriteVariants<Bitmap> variants, ShaderSheets<Bitmap> shaders, PreviewText fonts, Rect area) : ISceneCanvas
    {
        public long Submissions;
        public void DrawShader(ShaderClip shader,int frame,RectangleF r,System.Numerics.Matrix3x2 m,GameColor tint,bool additive)
        {
            var bitmap=shaders.Get(shader,GameColor.FromArgb(255,tint),additive);
            var cell=shader.Region(frame);
            using var transform=context.PushTransform(new Matrix(m.M11,m.M12,m.M21,m.M22,m.M31,m.M32));
            using var opacity=context.PushOpacity(tint.A/255.0);
            using var blend=context.PushRenderOptions(new RenderOptions {BitmapBlendingMode=additive?BitmapBlendingMode.Plus:BitmapBlendingMode.SourceOver,
                BitmapInterpolationMode=BitmapInterpolationMode.None});
            context.DrawImage(bitmap,new Rect(cell.X,cell.Y,cell.Width,cell.Height),new Rect(r.X,r.Y,r.Width,r.Height));
            Submissions++;
        }
        public IDisposable PushClip(RectangleF r) => context.PushClip(new Rect(r.X,r.Y,r.Width,r.Height));
        public bool DrawFiltered(SpriteFilter filter, RectangleF r,System.Numerics.Matrix3x2 m,GameColor tint)
        {
            using var transform = context.PushTransform(new Matrix(m.M11,m.M12,m.M21,m.M22,m.M31,m.M32));
            var image = variants.Get(filter,GameColor.FromArgb(255,tint));
            using var opacity = context.PushOpacity(tint.A/255.0);
            Submissions++;SpriteCellImage.DrawPixels(context,image,new Rect(image.PixelSize.ToSize(1)),new Rect(r.X,r.Y,r.Width,r.Height));
            return true;
        }
        public bool DrawAffine(string? path, RectangleF r, System.Numerics.Matrix3x2 m, GameColor tint, SpriteRegion? source = null, string layout = TileGrid.Orthogonal)
        {
            if (path != null && path != SpriteAssets.SquarePath && tint.ToArgb() != GameColor.White.ToArgb())
                return DrawFiltered(new(path,source,null,null),r,m,tint);
            using var transform = context.PushTransform(new Matrix(m.M11,m.M12,m.M21,m.M22,m.M31,m.M32));
            if (path == null) FillRectangle(r,tint);
            else if (path == SpriteAssets.SquarePath) DrawSquareSprite(r,tint);
            else if (source != null && layout != TileGrid.Orthogonal) return DrawTile(path,r,source,layout);
            else return DrawSprite(path,r,source);
            return true;
        }
        private static SolidColorBrush BrushOf(GameColor c) => new(Color.FromArgb(c.A, c.R, c.G, c.B));
        public void Clear(GameColor color) => context.FillRectangle(BrushOf(color), area);
        public void FillRectangle(System.Drawing.RectangleF r, GameColor color) {
            Submissions++;context.FillRectangle(BrushOf(color),new Rect(r.X,r.Y,r.Width,r.Height));
        }
        public void DrawSquareSprite(RectangleF r, GameColor color)
        {
            if (!images.TryGet(SpriteAssets.SquarePath, out var square)) { FillRectangle(r, GameColor.Magenta); return; }
            var bounds = new Rect(r.X, r.Y, r.Width, r.Height);
            using var mask = context.PushOpacityMask(new ImageBrush(square) { Stretch = Stretch.Fill }, bounds);
            Submissions++;context.FillRectangle(BrushOf(color), bounds);
        }

        public void UseFonts(IReadOnlyList<string> paths) => fonts.UseFonts(paths);
        public float MeasureText(string text,float size)=> fonts.Configured ? fonts.Renderer.Measure(text,size) : (float)new FormattedText(text,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,Typeface.Default,size,Brushes.White).WidthIncludingTrailingWhitespace;
        public void DrawText(string text, float left, float top, float size, GameColor color)
        {
            if (text.Length == 0) return;
            if (fonts.Configured)
            {
                var glyphs = fonts.Get(text);
                var scale = size / Engine.Desktop.GameTextRasterizer.FontSize;
                var bounds = new Rect(left, top, glyphs.PixelSize.Width * scale, glyphs.PixelSize.Height * scale);
                using var mask = context.PushOpacityMask(new ImageBrush(glyphs) { Stretch = Stretch.Fill }, bounds);
                Submissions++;context.FillRectangle(BrushOf(color), bounds);
                return;
            }
            var label = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Typeface.Default, size, BrushOf(color));
            Submissions++;context.DrawText(label, new Point(left, top));
        }
        public void DrawLine(Vector2 start, Vector2 end, float width, GameColor color) {
            Submissions++;context.DrawLine(new Pen(BrushOf(color),width),new Point(start.X,start.Y),new Point(end.X,end.Y));
        }
        public bool DrawTile(string path, RectangleF r, SpriteRegion source, string layout)
        {
            if (layout == TileGrid.Orthogonal) return DrawSprite(path,r,source);
            var geometry = new StreamGeometry();
            using (var shape = geometry.Open())
            {
                var outline = TileGrid.Outline(layout);
                Point VertexAt(int i)
                {
                    var v = TileGrid.Vertex(r,TileGrid.Outline(layout)[i]);
                    return new(v.X,v.Y);
                }
                shape.BeginFigure(VertexAt(0),true);
                for (int i = 1; i < outline.Length; i++) shape.LineTo(VertexAt(i));
                shape.EndFigure(true);
            }
            using var clip = context.PushGeometryClip(geometry);
            return DrawSprite(path,r,source);
        }
        public bool DrawSprite(string path, RectangleF r) => DrawSprite(path, r, null);
        public bool DrawSprite(string path, RectangleF r, SpriteRegion? source)
        {
            if (!images.TryGet(path, out var image)) return false;
            var destination = new Rect(r.X, r.Y, r.Width, r.Height);
            if (source == null) context.DrawImage(image, destination);
            else if (source.Fits(image.PixelSize.Width, image.PixelSize.Height))
                SpriteCellImage.DrawPixels(context, image, new Rect(source.X, source.Y, source.Width, source.Height), destination);
            else return false;
            Submissions++;return true;
        }
    }
}
