using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

public enum AudioBus { Effects, Music }
[YamlObject]
public sealed partial record MixerLevels(float Master=1,float Music=1,float Effects=1)
{
    internal bool Valid=>Unit(Master)&&Unit(Music)&&Unit(Effects);
    internal static bool Unit(float v)=>v is >=0 and <=1;
}

public interface IGameAudio
{
    MixerLevels Levels { get; set; }
    bool Play(Guid source,float? fadeSeconds=null);
    bool Stop(Guid source,float? fadeSeconds=null);
    bool IsPlaying(Guid source);
    // Monotonic Stopwatch timestamp, expressed in output frames since the latest Play.
    bool TryGetPlaybackFrame(Guid source, long timestamp, out long frame);
}

// The output clock advances in stereo sample frames, independently of FPS and game time scale.
public sealed class AudioMixer : IDisposable
{
    public const int SampleRate=48000;
    private const int GainRamp=240,MaxVoices=96;
    private readonly object gate=new();
    private readonly Dictionary<string,AudioFile> files=new(StringComparer.Ordinal);
    private readonly List<Voice> voices=[];
    private MixerLevels levels=new();
    private float master=1,music=1,effects=1;
    private int busRamp;
    private bool disposed;
    private readonly AudioOutputClock clock = new();
    private long renderedFrames;

    internal bool TryGetPlaybackFrame(AudioPlayback? playback, long timestamp, out long frame)
    {
        lock (gate)
        {
            frame = 0;
            if (playback == null || !clock.TryFrame(timestamp, out var output) ||
                output < playback.Start || output >= playback.End) return false;
            frame = output - playback.Start;
            return true;
        }
    }

    // Called by the single output worker, before it renders the next buffer.
    public void ObserveOutput(int queuedFrames, long timestamp)
    {
        if (queuedFrames < 0) throw new ArgumentOutOfRangeException(nameof(queuedFrames));
        lock (gate) clock.Observe(renderedFrames - queuedFrames, timestamp);
    }

    public MixerLevels Levels
    {
        get { lock(gate) return levels; }
        set
        {
            if(value?.Valid!=true) throw new ArgumentOutOfRangeException(nameof(value),"Bus gains must be between 0 and 1.");
            lock(gate)
            {
                if(levels==value)return;
                levels=value;busRamp=voices.Count==0?0:GainRamp;
                if(busRamp==0)(master,music,effects)=(value.Master,value.Music,value.Effects);
            }
        }
    }
    private sealed class Voice(object owner,AudioFile info,AudioSourceComponent config,float seconds,Vector2 position) : IDisposable
    {
        public readonly object Owner=owner;
        public readonly AudioFile Info=info;
        public readonly IAudioStream Stream=info.Open();
        public readonly AudioBus Bus=config.Bus;
        public readonly bool Loop=config.Loop,Spatial=config.Spatial;
        public readonly float Volume=config.Volume,Range=config.Range;
        public Vector2 Position=position, Gain;
        public required AudioPlayback Playback;
        public double Cursor;
        public float Envelope=seconds>0?0:1;
        public int Remaining=Duration(seconds);
        public bool Stopping;
        public void Dispose()=>Stream.Dispose();
    }
    private Vector2 listener;
    private static int Duration(float seconds)
    {
        if(!float.IsFinite(seconds)||seconds is <0 or >3600)throw new ArgumentOutOfRangeException(nameof(seconds));
        return (int)Math.Round(seconds*SampleRate);
    }
    private static Vector2 Gains(Voice v,Vector2 listener)
    {
        if(!v.Spatial)return new(v.Volume);
        var delta=v.Position-listener;
        float gain=Math.Clamp(1-delta.Length()/v.Range,0,1)*v.Volume;
        float pan=Math.Clamp(delta.X/v.Range,-1,1);
        return new(gain*MathF.Sqrt((1-pan)*.5f),gain*MathF.Sqrt((1+pan)*.5f));
    }
    internal void MoveListener(Vector2 center) { lock(gate) listener=center; }
    internal void MoveSource(object owner,Vector2 center)
    { lock(gate) foreach(var v in voices)if(ReferenceEquals(v.Owner,owner))v.Position=center; }
    public bool HasVoices { get { lock(gate)return voices.Count!=0; } }
    internal bool Playing(object owner) { lock(gate)return voices.Exists(v=>ReferenceEquals(v.Owner,owner)); }
    public void Prepare(string path) { lock(gate){ if(!files.ContainsKey(path))files.Add(path,AudioFile.Read(path)); } }
    // Split scene loading reads (and for WAV decodes) the file on a worker thread, outside the output lock.
    internal sealed class Decoded(AudioFile file) : IDisposable { public readonly AudioFile File = file; public void Dispose() { } }
    internal static IDisposable Decode(string path) => new Decoded(AudioFile.Read(path));
    internal void Prepare(string path, IDisposable decoded) { lock(gate) files.TryAdd(path, ((Decoded)decoded).File); }
    public void Release(string path) { lock(gate)files.Remove(path); }
    internal AudioPlayback Start(object owner,string path,AudioSourceComponent c,Vector2 position,float seconds)
    {
        Duration(seconds);
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(!files.TryGetValue(path,out var info))files[path]=info=AudioFile.Read(path);
            var voice=new Voice(owner,info,c,seconds,position) { Playback = new(renderedFrames) };
            voice.Gain=Gains(voice,listener);
            if(c.Bus==AudioBus.Music || c.Loop)Stop(owner,0);
            if(voices.Count>=MaxVoices)Remove(Math.Max(0,voices.FindIndex(v=>v.Bus==AudioBus.Effects && !v.Loop)));
            voices.Add(voice);
            return voice.Playback;
        }
    }
    private void Remove(int index, long? end = null)
    {
        voices[index].Playback.End = end ?? renderedFrames;
        voices[index].Dispose();
        voices.RemoveAt(index);
    }
    internal bool Stop(object owner,float seconds)
    {
        int frames=Duration(seconds);bool found=false;
        lock(gate)for(int i=voices.Count-1;i>=0;i--)
        {
            var v=voices[i];if(!ReferenceEquals(v.Owner,owner))continue;
            found=true;if(frames==0)Remove(i);else { v.Stopping=true;v.Remaining=frames; }
        }
        return found;
    }
    // Shared render path for the SDL output and offline checks. Destination is float32 L,R,L,R.
    public void Render(Span<float> stereo)
    {
        if(stereo.Length%2!=0)throw new ArgumentException("Expected stereo frames.");
        stereo.Clear();
        lock(gate)
        {
            Span<float> mb=stackalloc float[512],eb=stackalloc float[512];
            for(int off=0;off<stereo.Length;off+=1024)
            {
                int n=Math.Min(512,(stereo.Length-off)/2);
                for(int j=0;j<n;j++)
                {
                    if(busRamp>0)
                    {
                        master+=(levels.Master-master)/busRamp;music+=(levels.Music-music)/busRamp;effects+=(levels.Effects-effects)/busRamp;busRamp--;
                    }
                    mb[j]=master*music;eb[j]=master*effects;
                }
                for(int i=voices.Count-1;i>=0;i--)
                {
                    var v=voices[i];var target=Gains(v,listener);
                    var delta=(target-v.Gain)/n;
                    double step=(double)v.Info.Rate/SampleRate;
                    for(int j=0;j<n;j++)
                    {
                        long at=(long)v.Cursor;
                        long next=at+1<v.Info.Frames?at+1:v.Loop?0:at;
                        var a=v.Stream.Sample(at);float t=(float)(v.Cursor-at);
                        var b=t==0?a:v.Stream.Sample(next);
                        float l=a.L+(b.L-a.L)*t,r=a.R+(b.R-a.R)*t;
                        if(v.Spatial)l=r=.5f*(l+r);
                        if(v.Remaining>0){v.Envelope+=((v.Stopping?0:1)-v.Envelope)/v.Remaining;v.Remaining--;}
                        else if(!v.Stopping)v.Envelope=1;
                        float gain=(v.Bus==AudioBus.Music?mb[j]:eb[j])*v.Envelope;
                        v.Gain+=delta;
                        stereo[off+j*2]+=l*gain*v.Gain.X;stereo[off+j*2+1]+=r*gain*v.Gain.Y;
                        v.Cursor+=step;
                        if(v.Stopping && v.Remaining==0 || v.Cursor>=v.Info.Frames && !v.Loop){Remove(i, renderedFrames + j + 1);break;}
                        if(v.Cursor>=v.Info.Frames)v.Cursor%=v.Info.Frames;
                    }
                    v.Gain=target;
                }
                renderedFrames += n;
            }
        }
        for(int i=0;i<stereo.Length;i++)stereo[i]=Math.Clamp(stereo[i],-1,1);
    }
    public void Dispose()
    {
        lock(gate){foreach(var v in voices)v.Dispose();voices.Clear();files.Clear();disposed=true;}
    }
}
