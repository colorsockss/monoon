using System.Drawing;
using System.Numerics;
using Avalonia;
using Engine.Core;
using Point = Avalonia.Point;
using Color = System.Drawing.Color;

namespace Engine.Editor;

internal enum GizmoTool { Move, Rotate, Scale, Rect }

public sealed partial class SceneViewport
{
    internal GizmoTool Gizmo { get; set; }
    internal bool SnapEnabled { get; set; }
    internal IReadOnlyList<SceneObject> Selection { get; set; }=[];
    internal event Action<SceneObject?>? TogglePicked;
    private enum Grip { None, X, Y, Center, Ring }
    private const float Arm=44;
    private SelectionTransform? gesture;
    private Vector2 gestureStart;
    private Grip grip;
    private bool HasWorldSelection => Selection.Count>0 && Selection.All(IsWorld);
    internal static bool IsWorld(SceneObject obj)=>!ScreenUi.IsWidget(obj) && obj.GetComponent<UiCanvasComponent>()==null;
    private static Vector2 WorldPoint(Point point)=>new((float)point.X,(float)point.Y);

    private Grip HitGrip(Vector2 point)
    {
        if(!HasWorldSelection)return Grip.None;
        var d=(point-SelectionTransform.Center(Selection))*(float)Scale;
        if(Gizmo==GizmoTool.Rotate)return Math.Abs(d.Length()-Arm)<=7?Grip.Ring:Grip.None;
        if(Math.Abs(d.X)<=7 && Math.Abs(d.Y)<=7)return Grip.Center;
        if(d.X>=8 && d.X<=Arm+7 && Math.Abs(d.Y)<=6)return Grip.X;
        if(-d.Y>=8 && -d.Y<=Arm+7 && Math.Abs(d.X)<=6)return Grip.Y;
        return Grip.None;
    }

    // Rect: the axis handles change the selected object's Width and Height instead of its scale.
    private (float Width,float Height)? rectStart;

    private void BeginGesture(Vector2 point,Grip handle)
    {
        rectStart=Gizmo==GizmoTool.Rect && Selection.Count==1 ? (Selection[0].Width,Selection[0].Height) : null;
        gesture=new SelectionTransform(Selection);
        gestureStart=point;grip=handle;
    }

    private void UpdateGesture(Vector2 point)
    {
        if(gesture==null)return;
        var d=point-gestureStart;
        if(!movedDuringDrag && d.Length()*Scale<2)return;
        static float Snap(float n,float step)=>MathF.Round(n/step,MidpointRounding.AwayFromZero)*step;
        if(Gizmo==GizmoTool.Rect && grip is Grip.X or Grip.Y)
        {
            if(rectStart is not {} size)return;
            float change=grip==Grip.X?d.X:-d.Y;
            if(SnapEnabled)change=Snap(change,16);
            if(grip==Grip.X)Selection[0].Width=Math.Max(1,size.Width+change);else Selection[0].Height=Math.Max(1,size.Height+change);
            movedDuringDrag=true;Moved?.Invoke();InvalidateUi();return;
        }
        var pivot=gesture.Pivot;
        Matrix3x2 delta;
        if(Gizmo==GizmoTool.Rotate)
        {
            var a=gestureStart-pivot;var b=point-pivot;
            if(b.LengthSquared()<1e-8f)return;
            float radians=MathF.Atan2(a.X*b.Y-a.Y*b.X,Vector2.Dot(a,b));
            if(SnapEnabled)radians=Snap(radians,MathF.PI/12);
            delta=Matrix3x2.CreateRotation(radians,pivot);
        }
        else if(Gizmo==GizmoTool.Scale)
        {
            // 44 screen points on an axis double that dimension; the center scales uniformly.
            float amount=(grip==Grip.Y?-d.Y:grip==Grip.X?d.X:(d.X-d.Y)/2)*(float)Scale/Arm;
            var factor=Math.Clamp(1+(SnapEnabled?Snap(amount,.1f):amount),.01f,100f);
            delta=Matrix3x2.CreateScale(grip==Grip.Y?1:factor,grip==Grip.X?1:factor,pivot);
        }
        else
        {
            if(SnapEnabled)d=new(Snap(d.X,16),Snap(d.Y,16));
            if(grip==Grip.X)d.Y=0;
            if(grip==Grip.Y)d.X=0;
            delta=Matrix3x2.CreateTranslation(d);
        }
        gesture.Apply(delta);movedDuringDrag=true;Moved?.Invoke();InvalidateUi();
    }

    internal bool CancelGesture()
    {
        if(gesture==null)return false;
        gesture.Apply(Matrix3x2.Identity);gesture=null;movedDuringDrag=false;
        if(rectStart is {} size && Selection.Count==1){Selection[0].Width=size.Width;Selection[0].Height=size.Height;}
        Moved?.Invoke();InvalidateUi();return true;
    }

    private void DrawSelection(ISceneCanvas canvas,float unit)
    {
        var (frame,zoom)=UiFrame();
        foreach(var obj in Selection)
        {
            if(PreviewUi.Bounds(obj.Id) is {} rect)
            {
                Outline(canvas,new(frame.X+rect.X/zoom,frame.Y+rect.Y/zoom,rect.Width/zoom,rect.Height/zoom),unit,SelectionColor);
                continue;
            }
            var corners=new[]{ Vector2.Zero,new Vector2(obj.Width,0),new Vector2(obj.Width,obj.Height),new Vector2(0,obj.Height) };
            for(int i=0;i<4;i++)
                canvas.DrawLine(obj.LocalToWorld(corners[i]),obj.LocalToWorld(corners[(i+1)%4]),unit*1.5f,SelectionColor);
        }
        DrawPath(canvas,unit);
        if(!HasWorldSelection || EditMap!=null)return;
        var center=SelectionTransform.Center(Selection);
        var x=center+new Vector2(Arm*unit,0);
        var y=center-new Vector2(0,Arm*unit);
        var red=Color.FromArgb(255,119,108);var green=Color.FromArgb(122,222,151);
        void Square(Vector2 at,float size,Color color)=>canvas.FillRectangle(new(at.X-size*unit/2,at.Y-size*unit/2,size*unit,size*unit),color);
        if(Gizmo==GizmoTool.Rotate)
        {
            var last=x;
            for(int i=1;i<=64;i++){
                float angle=i*MathF.Tau/64;var next=center+Arm*unit*new Vector2(MathF.Cos(angle),MathF.Sin(angle));
                canvas.DrawLine(last,next,2*unit,Color.Gold);last=next;
            }
        }
        else
        {
            canvas.DrawLine(center,x,2*unit,red);canvas.DrawLine(center,y,2*unit,green);
            if(Gizmo is GizmoTool.Scale or GizmoTool.Rect){Square(x,8,red);Square(y,8,green);}
            else
            {
                foreach(int sign in new[]{-1,1}){
                    canvas.DrawLine(x,x+new Vector2(-8,sign*4)*unit,2*unit,red);
                    canvas.DrawLine(y,y+new Vector2(sign*4,8)*unit,2*unit,green);
                }
            }
        }
        Square(center,7,Color.Gold);
    }
}
