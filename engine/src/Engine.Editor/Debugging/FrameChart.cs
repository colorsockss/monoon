using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Engine.Editor;

internal sealed class FrameChart : Control
{
    internal PlayProfiler? History;
    internal long? Selected;
    internal event Action<long>? FrameSelected;
    private static readonly Pen selection=new(EditorTokens.Brush("EditorAccent"),1.4);
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if(History is not {Count:>0} history || Bounds.Width<=0)return;
        int i=(int)Math.Round(e.GetPosition(this).X*(PlayProfiler.Capacity-1)/Bounds.Width)-(PlayProfiler.Capacity-history.Count);
        FrameSelected?.Invoke(history.Sequence-history.Count+1+Math.Clamp(i,0,history.Count-1));
        e.Handled=true;
    }
    private static readonly Pen line=new(EditorTokens.Brush("EditorMuted"),1.4), budget=new(EditorTokens.Brush("EditorBorder"),1), collection=new(EditorTokens.Brush("EditorWarning"),1.4);
    public override void Render(DrawingContext context)
    {
        base.Render(context);context.FillRectangle(Brushes.Transparent,new Rect(Bounds.Size));
        if(History is not {Count:>1} history || Bounds.Width<=0 || Bounds.Height<=2)return;
        double high=33.34;
        for(int i=0;i<history.Count;i++)high=Math.Max(high,history.At(i).Interval);
        double Y(double ms)=>1+(Bounds.Height-2)*(1-ms/high);
        context.DrawLine(budget,new(0,Y(1000d/60)),new(Bounds.Width,Y(1000d/60)));
        Point previous=default;
        for(int i=0;i<history.Count;i++){
            var f=history.At(i);double x=(i+PlayProfiler.Capacity-history.Count)*Bounds.Width/(PlayProfiler.Capacity-1);
            var p=new Point(x,Y(f.Interval));if(i!=0)context.DrawLine(line,previous,p);
            if(f.Gen0+f.Gen1+f.Gen2>0)context.DrawLine(collection,new(x,Bounds.Height-5),new(x,Bounds.Height));
            if(Selected==history.Sequence-history.Count+1+i)context.DrawLine(selection,new(x,0),new(x,Bounds.Height));
            previous=p;
        }
    }
}
