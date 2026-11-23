using System.Diagnostics;
using Engine.Core;

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
    // Same frame boundaries and capacity, so index i here is Work.At(i).
    public readonly FrameProfiler Work=new(Capacity);
    public FrameSample At(int index)=>frames[(head-Count+index+Capacity)%Capacity];
    public void Reset(){Count=head=0;Sequence=0;Break();Work.Clear();}
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
            Work.EndFrame();
        }
        else Work.Restart();
        previous=now;allocated=bytes;gen0=g0;gen1=g1;gen2=g2;update=0;
    }
}
