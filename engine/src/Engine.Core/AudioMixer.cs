using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Brutal.SdlApi;
using Brutal.SdlApi.Mixer;
using VYaml.Annotations;

namespace Engine.Core;

public enum AudioBus { Effects, Music, Voice, Ui, Ambience }
// Voice, Ui and Ambience are null at full level, so settings files written before those buses stay byte-identical.
[YamlObject]
public sealed partial record MixerLevels(float Master=1,float Music=1,float Effects=1,float? Voice=null,float? Ui=null,float? Ambience=null)
{
    internal bool Valid=>Unit(Master)&&Unit(Music)&&Unit(Effects)&&Unit(Voice??1)&&Unit(Ui??1)&&Unit(Ambience??1);
    internal static bool Unit(float v)=>v is >=0 and <=1;
    public float Level(AudioBus bus)=>bus switch
    {
        AudioBus.Music=>Music, AudioBus.Voice=>Voice??1, AudioBus.Ui=>Ui??1, AudioBus.Ambience=>Ambience??1, _=>Effects
    };
    // A bus left at null and one set to 1 are the same mix.
    public bool Equals(MixerLevels? other)=>other is not null && Master==other.Master && Music==other.Music && Effects==other.Effects &&
        Level(AudioBus.Voice)==other.Level(AudioBus.Voice) && Level(AudioBus.Ui)==other.Level(AudioBus.Ui) && Level(AudioBus.Ambience)==other.Level(AudioBus.Ambience);
    public override int GetHashCode()=>Master.GetHashCode()^Music.GetHashCode()*31^Effects.GetHashCode()*961; // Small AOT code; Equals decides.
}

// A named set of bus multipliers (on top of Levels) that TransitionTo blends to over time.
[YamlObject]
public sealed partial record AudioSnapshot(string Name="",MixerLevels? Levels=null);

// While any voice plays on Trigger, Target is lowered by Decibels (sidechain on bus activity, not on signal level).
[YamlObject]
public sealed partial record AudioDucking(AudioBus Trigger=AudioBus.Voice,AudioBus Target=AudioBus.Music,float Decibels=-8,float Attack=.05f,float Release=.4f)
{
    internal bool Valid=>Trigger!=Target && Bus(Trigger) && Bus(Target) && Decibels is >=-80 and <=0 && Attack is >=0 and <=10 && Release is >=0 and <=10;
    internal static bool Bus(AudioBus bus)=>(uint)bus<=(uint)AudioBus.Ambience;
}

[EngineFeature(EngineFeature.Audio)]
public interface IGameAudio
{
    MixerLevels Levels { get; set; }
    bool Play(Guid source,float? fadeSeconds=null);
    bool Stop(Guid source,float? fadeSeconds=null);
    bool IsPlaying(Guid source);
    // Monotonic Stopwatch timestamp, expressed in output frames since the latest Play.
    bool TryGetPlaybackFrame(Guid source, long timestamp, out long frame);
    // Blends the bus multipliers to a GameSettings snapshot (null: back to 100%) over output time. False for an unknown name.
    bool TransitionTo(string? snapshot,float seconds=0);
}

// SDL_mixer (through BRUTAL) decodes, resamples, loops, pans and sums the voices. MoNoon keeps what
// SDL_mixer does differently: sample-exact fades and the mono fold of spatial sources (track cooked
// callback, at the 48 kHz output rate), ramped bus gains (group post-mix callbacks), the voice limit
// and the output clock. The SDL output and offline checks share this render path. Each bus is one SDL group whose post-mix
// callback applies master x level x snapshot x ducking per output frame; at 1 those factors leave the samples unchanged.
[EngineFeature(EngineFeature.Audio)]
public sealed unsafe class AudioMixer : IDisposable
{
    public const int SampleRate=48000;
    private const int GainRamp=240,MaxVoices=96,Block=512;
    private readonly object gate=new();
    private readonly Dictionary<string,AudioFile> files=new(StringComparer.Ordinal);
    private readonly List<Voice> voices=[];
    private const int Buses=5;
    private MixerLevels levels=new();
    private float master=1;
    private readonly float[] level=[1,1,1,1,1];
    private int busRamp;
    private AudioSnapshot[] snapshots=[];
    private Duck[] ducking=[];
    private float snapMaster=1,snapMasterTarget=1;
    private readonly float[] snap=[1,1,1,1,1],snapTarget=[1,1,1,1,1],duck=[1,1,1,1,1],duckGoal=[1,1,1,1,1];
    private readonly int[] duckRamp=new int[Buses],duckRelease=new int[Buses];
    private int snapRamp;
    private readonly record struct Duck(int Trigger,int Target,float Gain,int Attack,int Release);
    private sealed class BusMix { public MixGroup Group; public GCHandle Handle; public readonly float[] Gains=new float[Block]; public int Offset; }
    private readonly BusMix[] buses=[new(),new(),new(),new(),new()];
    private bool disposed;
    private readonly AudioOutputClock clock = new();
    private long renderedFrames;
    private MixMixer mix;
    // MIX_PlayTrack resets the loop count from its options, so looping must be requested here.
    private static readonly Lazy<uint> LoopForever=new(()=>
    {
        uint props=Sdl.PInvoke.CreateProperties();
        Sdl.PInvoke.SetNumberProperty(props,SdlMixer.Constants.PropPlayLoopsNumber,-1);
        return props;
    });

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
                if(busRamp==0){master=value.Master;for(int b=0;b<Buses;b++)level[b]=value.Level((AudioBus)b);}
            }
        }
    }
    private sealed class Voice(object owner,AudioSourceComponent config,float seconds,Vector2 position)
    {
        public readonly object Owner=owner;
        public readonly AudioBus Bus=config.Bus;
        public readonly bool Loop=config.Loop,Spatial=config.Spatial;
        public readonly float Volume=config.Volume,Range=config.Range;
        public Vector2 Position=position, Gain;
        public required AudioPlayback Playback;
        public MixTrack Track;
        public GCHandle Handle;
        public float Envelope=seconds>0?0:1;
        public int Remaining=Duration(seconds);
        public bool Stopping;
        public int Written, Silent=-1; // Frames delivered in this block; block frame after a completed fade-out.
        public bool Fresh; // Resampled and not mixed yet.
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
    // Split scene loading probes the file header on a worker thread, outside the output lock.
    internal sealed class Decoded(AudioFile file) : IDisposable { public readonly AudioFile File = file; public void Dispose() { } }
    internal static IDisposable Decode(string path) => new Decoded(AudioFile.Read(path));
    internal void Prepare(string path, IDisposable decoded) { lock(gate) files.TryAdd(path, ((Decoded)decoded).File); }
    public void Release(string path) { lock(gate)files.Remove(path); }
    private void Open()
    {
        if(!mix.IsNull())return;
        AudioFile.Init();
        // SDL_AUDIO_F32LE stereo at the clock rate; the SDL device stream converts it if the hardware differs.
        var spec=new SdlAudioSpec { format=(SdlAudioFormat)0x8120, channels=2, freq=SampleRate };
        mix=SdlMixer.CreateMixer(&spec);
        // Music and Effects first, in the order the two-bus mixer created them. The other buses get their group on first use:
        // an SDL group costs mixing time every block even when empty.
        Group(AudioBus.Music);Group(AudioBus.Effects);
    }
    private MixGroup Group(AudioBus bus)
    {
        var b=buses[(int)bus];
        if(b.Group.IsNull())
        {
            b.Group=SdlMixer.CreateGroup(mix);b.Handle=GCHandle.Alloc(b);
            SdlMixer.PInvoke.SetGroupPostMixCallback(b.Group,&Mixed,(void*)GCHandle.ToIntPtr(b.Handle));
        }
        return b.Group;
    }
    internal AudioPlayback Start(object owner,string path,AudioSourceComponent c,Vector2 position,float seconds)
    {
        Duration(seconds);
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(!files.TryGetValue(path,out var info))files[path]=info=AudioFile.Read(path);
            Open();
            var voice=new Voice(owner,c,seconds,position) { Playback = new(renderedFrames), Fresh=info.Rate!=SampleRate };
            voice.Gain=Gains(voice,listener);
            if(c.Bus is AudioBus.Music or AudioBus.Voice || c.Loop)Stop(owner,0);
            if(voices.Count>=MaxVoices)Remove(Math.Max(0,voices.FindIndex(v=>v.Bus==AudioBus.Effects && !v.Loop)));
            voice.Track=SdlMixer.CreateTrack(mix);
            voice.Handle=GCHandle.Alloc(voice);
            try
            {
                SdlMixer.SetTrackGroup(voice.Track,Group(c.Bus));
                SdlMixer.PInvoke.SetTrackCookedCallback(voice.Track,&Shape,(void*)GCHandle.ToIntPtr(voice.Handle));
                // Each voice streams its own decoder from the file, so long music is never held decoded in memory.
                nint io=Sdl.PInvoke.IOFromFile(path,"rb");
                if(io==0 || SdlMixer.PInvoke.SetTrackIOStream(voice.Track,io,1)==0)throw new IOException($"{path}: {Sdl.PInvoke.GetError()}");
                var gains=new MixStereoGains { left=voice.Gain.X, right=voice.Gain.Y };
                SdlMixer.SetTrackStereo(voice.Track,&gains);
                SdlMixer.PlayTrack(voice.Track,voice.Loop?LoopForever.Value:0);
            }
            catch { SdlMixer.DestroyTrack(voice.Track);voice.Handle.Free();throw; }
            voices.Add(voice);
            return voice.Playback;
        }
    }
    private void Remove(int index, long? end = null)
    {
        var v=voices[index];
        v.Playback.End = end ?? renderedFrames;
        SdlMixer.DestroyTrack(v.Track);
        v.Handle.Free();
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
    // Always mixes in 512-frame blocks: SDL_mixer's resampler output depends on the request sizes.
    public void Render(Span<float> stereo)
    {
        if(stereo.Length%2!=0)throw new ArgumentException("Expected stereo frames.");
        lock(gate)
        {
            if(disposed){stereo.Clear();return;} // The output worker can outlive the session by one buffer.
            Open();
            for(int off=0;off<stereo.Length;off+=2*Block)
            {
                int n=Math.Min(Block,(stereo.Length-off)/2);
                UpdateDucking();
                // Steady gains (the usual block): one product per bus, the same value the ramp loop below would write.
                if(busRamp==0 && snapRamp==0 && !Ducking())
                    for(int b=0;b<Buses;b++)buses[b].Gains.AsSpan(0,n).Fill(master*level[b]*snapMaster*snap[b]*duck[b]);
                else for(int j=0;j<n;j++)
                {
                    if(busRamp>0)
                    {
                        master+=(levels.Master-master)/busRamp;
                        for(int b=0;b<Buses;b++)level[b]+=(levels.Level((AudioBus)b)-level[b])/busRamp;
                        busRamp--;
                    }
                    if(snapRamp>0 && --snapRamp==0){snapMaster=snapMasterTarget;snapTarget.CopyTo(snap,0);}
                    else if(snapRamp>0)
                    {
                        snapMaster+=(snapMasterTarget-snapMaster)/(snapRamp+1);
                        for(int b=0;b<Buses;b++)snap[b]+=(snapTarget[b]-snap[b])/(snapRamp+1);
                    }
                    for(int b=0;b<Buses;b++)
                    {
                        if(duckRamp[b]>0)duck[b]=--duckRamp[b]==0?duckGoal[b]:duck[b]+(duckGoal[b]-duck[b])/(duckRamp[b]+1);
                        buses[b].Gains[j]=master*level[b]*snapMaster*snap[b]*duck[b];
                    }
                }
                foreach(var b in buses)b.Offset=0;
                foreach(var v in voices)
                {
                    var target=Gains(v,listener);
                    if(target==v.Gain)continue;
                    var gains=new MixStereoGains { left=target.X, right=target.Y };
                    SdlMixer.SetTrackStereo(v.Track,&gains);
                    v.Gain=target;
                }
                // A resampled track's first request comes back short by the resampler latency (14 frames at 24 kHz),
                // leaving silence at its end. Asking for one frame first turns that into a one-frame start delay.
                // Running tracks give the same samples however the block is split.
                int first=voices.Exists(v=>v.Fresh)?1:n;
                Generate(stereo.Slice(off,2*first));
                foreach(var v in voices)
                {
                    if(!v.Fresh)continue;
                    if(v.Written==0 && v.Silent<0 && Step(v))v.Silent=1;
                    v.Written=Math.Max(v.Written,first);v.Fresh=false;
                }
                if(n>first)Generate(stereo.Slice(off+2*first,2*(n-first)));
                for(int i=voices.Count-1;i>=0;i--)
                {
                    var v=voices[i];
                    // Keep the envelope on output time if a track still delivers less than the block.
                    if(v.Silent<0 && v.Written<n && SdlMixer.TrackPlaying(v.Track))
                        for(int j=v.Written;j<n && v.Silent<0;j++)if(Step(v))v.Silent=j+1;
                    if(v.Silent>=0)Remove(i,renderedFrames+v.Silent);
                    else if(!SdlMixer.TrackPlaying(v.Track))Remove(i,renderedFrames+v.Written);
                    else v.Written=0;
                }
                renderedFrames += n;
            }
        }
        for(int i=0;i<stereo.Length;i++)stereo[i]=Math.Clamp(stereo[i],-1,1);
    }
    private void Generate(Span<float> stereo)
    {
        fixed(float* block=stereo)
            if(SdlMixer.Generate(mix,block,stereo.Length*sizeof(float))<0)throw new IOException(Sdl.PInvoke.GetError());
    }
    // Runs inside Generate on the rendering thread, per 48 kHz frame and before the stereo gains.
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static void Shape(void* user,MixTrack track,SdlAudioSpec* spec,float* pcm,int samples)
    {
        var v=(Voice)GCHandle.FromIntPtr((nint)user).Target!;
        int channels=spec->channels,frames=samples/channels;
        for(int j=0;j<frames;j++,pcm+=channels)
        {
            if(v.Silent>=0){ new Span<float>(pcm,channels).Clear(); continue; }
            bool done=Step(v);
            if(v.Spatial && channels==2)pcm[0]=pcm[1]=.5f*(pcm[0]+pcm[1]);
            for(int c=0;c<channels;c++)pcm[c]*=v.Envelope;
            if(done)v.Silent=v.Written+j+1;
        }
        v.Written+=frames;
    }
    // Linear fade toward the target over the remaining frames; true on the last frame of a fade-out.
    private static bool Step(Voice v)
    {
        if(v.Remaining>0){v.Envelope+=((v.Stopping?0:1)-v.Envelope)/v.Remaining;v.Remaining--;}
        else if(!v.Stopping)v.Envelope=1;
        return v.Stopping && v.Remaining==0;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static void Mixed(void* user,MixGroup group,SdlAudioSpec* spec,float* pcm,int samples)
    { var b=(BusMix)GCHandle.FromIntPtr((nint)user).Target!; Apply(b.Gains,ref b.Offset,spec->channels,pcm,samples); }
    // Per block, on the voices present at its start: the lowest active rule's gain per target bus, ramped over its attack;
    // back to 1 over the release of the rule that lowered it.
    private bool Ducking()
    {
        foreach(int r in duckRamp)if(r!=0)return true;
        return false;
    }
    private void UpdateDucking()
    {
        if(ducking.Length==0)return;
        int active=0;
        foreach(var v in voices)active|=1<<(int)v.Bus;
        for(int b=0;b<Buses;b++)
        {
            float goal=1;int attack=0,release=0;
            foreach(var d in ducking)
                if(d.Target==b && (active>>d.Trigger&1)!=0 && d.Gain<goal)(goal,attack,release)=(d.Gain,d.Attack,d.Release);
            if(goal==duckGoal[b])continue;
            bool lower=goal<duckGoal[b];
            duckRamp[b]=lower?attack:duckRelease[b];
            if(lower)duckRelease[b]=release;
            duckGoal[b]=goal;
            if(duckRamp[b]==0)duck[b]=goal;
        }
    }
    // Project buses: default levels, snapshots and ducking rules from GameSettings (validated there).
    internal void Configure(GameSettings settings)
    {
        Levels=settings.Mixer;
        lock(gate)
        {
            snapshots=settings.AudioSnapshots??[];
            var rules=settings.AudioDucking??[];
            ducking=new Duck[rules.Length];
            for(int i=0;i<rules.Length;i++)
            {
                var d=rules[i];
                ducking[i]=new((int)d.Trigger,(int)d.Target,MathF.Pow(10,d.Decibels/20),Duration(d.Attack),Duration(d.Release));
            }
        }
    }
    // Blends over output frames like the fades; without playing voices nothing is heard, so it applies at once.
    public bool TransitionTo(string? snapshot,float seconds=0)
    {
        int frames=Duration(seconds);
        lock(gate)
        {
            MixerLevels? target=snapshot==null?new():null;
            foreach(var s in snapshots)if(s.Name==snapshot)target=s.Levels??new();
            if(target==null)return false;
            snapMasterTarget=target.Master;
            for(int b=0;b<Buses;b++)snapTarget[b]=target.Level((AudioBus)b);
            snapRamp=voices.Count==0?0:frames;
            if(snapRamp==0){snapMaster=snapMasterTarget;snapTarget.CopyTo(snap,0);}
            return true;
        }
    }
    // SceneGame starts the auto-play sources of one tick inside this lock so they begin on the same output frame.
    internal object SyncRoot=>gate;
    private static void Apply(float[] gains,ref int offset,int channels,float* pcm,int samples)
    {
        for(int i=0;i<samples;i++)pcm[i]*=gains[Math.Min(offset+i/channels,Block-1)];
        offset+=samples/channels;
    }
    public void Dispose()
    {
        lock(gate)
        {
            if(disposed)return;
            foreach(var v in voices){SdlMixer.DestroyTrack(v.Track);v.Handle.Free();}
            voices.Clear();files.Clear();disposed=true;
            if(!mix.IsNull()){SdlMixer.DestroyMixer(mix);mix=default;foreach(var b in buses)if(b.Handle.IsAllocated)b.Handle.Free();}
        }
    }
}
