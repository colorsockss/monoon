using RectangleF = System.Drawing.RectangleF;
using Vector2 = System.Numerics.Vector2;
using Avalonia;
using Avalonia.Input;
using Engine.Core;

namespace Engine.Editor;

public sealed partial class SceneViewport
{
    // Game panel scale: 0 fits the game into the panel, otherwise panel units per game pixel.
    internal double GameZoom { get; set; }
    internal double Scale => spritePreview ? 1 : IsPlaying
        ? GameZoom > 0 ? GameZoom : Math.Max(double.Epsilon, Math.Min(Bounds.Width / LayoutSettings.Width, Bounds.Height / LayoutSettings.Height))
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
        if (IsPlaying || stroke != null || gesture!=null || pathGrip>=0 || dragging != null || panStart != null) e.Pointer.Capture(this);
    }

    internal void BeginDrag(Point point,bool toggle=false)
    {
        Focus();
        var p = ToWorld(point);
        if (game != null) { GamePointer(point,"Mouse.Left",true); return; }
        EndDrag();
        if (BeginTiles(p)) return;
        var worldPoint=WorldPoint(p);
        if(!toggle && BeginPathDrag(worldPoint))return;
        if(!toggle && HitGrip(worldPoint) is var handle && handle!=Grip.None){BeginGesture(worldPoint,handle);return;}
        var u = EditUiPoint(p);
        dragging = PreviewUi.Pick((float)u.X, (float)u.Y);
        if (dragging != null && !IsPickable(dragging)) dragging = null;
        draggingUi = dragging != null;
        dragging ??= Scene.DrawOrder().LastOrDefault(obj => obj.GetComponent<SpriteRendererComponent>() != null &&
            IsPickable(obj) && obj.ContainsWorldPoint(new((float)p.X, (float)p.Y)));
        dragging ??= Scene.Cameras.LastOrDefault(camera => IsPickable(camera) &&
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
        bool pathMoved = pathGrip >= 0 && movedDuringDrag;
        pathGrip = -1;
        if (pathMoved) PathEdited?.Invoke();
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
        if(pathGrip>=0){DragPath(WorldPoint(ToWorld(point)));return;}
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
        if(pathGrip>=0)DragPath(WorldPoint(p));
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
}
