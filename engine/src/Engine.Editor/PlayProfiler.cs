using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Engine.Editor;

// Intervals run from one completed Scene frame to the next on the UI/render thread.
internal readonly record struct FrameSample(double Interval,double Update,double Draw,long Allocation,int Gen0,int Gen1,int Gen2,long Calls);
internal sealed class PlayProfiler
{
    public const int Capacity=180;
    private readonly FrameSample[] frames=new FrameSample[Capacity];
    private long previous,allocated;
    private int gen0,gen1,gen2,head;
    private double update;
    public int Count {get;private set;}
    public long Sequence {get;private set;}
    public FrameSample At(int index)=>frames[(head-Count+index+Capacity)%Capacity];
    public void Reset(){Count=head=0;Sequence=0;Break();}
    public void Break(){previous=0;update=0;}
    public void AddUpdate(long started)=>update+=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    public void Complete(long start,long calls)
    {
        var now=Stopwatch.GetTimestamp();long bytes=GC.GetAllocatedBytesForCurrentThread();
        int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
        if(previous!=0){
            frames[head]=new(Stopwatch.GetElapsedTime(previous,now).TotalMilliseconds,update,
                Stopwatch.GetElapsedTime(start,now).TotalMilliseconds,Math.Max(0,bytes-allocated),g0-gen0,g1-gen1,g2-gen2,calls);
            head=(head+1)%Capacity;Count=Math.Min(Count+1,Capacity);Sequence++;
        }
        previous=now;allocated=bytes;gen0=g0;gen1=g1;gen2=g2;update=0;
    }
}

internal sealed class FrameChart : Control
{
    internal PlayProfiler? History;
    private static readonly Pen line=new(Brushes.MediumAquamarine,1.4), budget=new(Brushes.DimGray,1), collection=new(Brushes.Gold,1.4);
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
            previous=p;
        }
    }
}
