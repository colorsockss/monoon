using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Brutal.SdlApi;
using Brutal.SdlApi.Mixer;
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

// SDL_mixer (through BRUTAL) decodes, resamples, loops, pans and sums the voices. MoNoon keeps what
// SDL_mixer does differently: sample-exact fades and the mono fold of spatial sources (track cooked
// callback, at the 48 kHz output rate), ramped bus gains (group post-mix callbacks), the voice limit
// and the output clock. The SDL output and offline checks share this render path.
public sealed unsafe class AudioMixer : IDisposable
{
    public const int SampleRate=48000;
    private const int GainRamp=240,MaxVoices=96,Block=512;
    private readonly object gate=new();
    private readonly Dictionary<string,AudioFile> files=new(StringComparer.Ordinal);
    private readonly List<Voice> voices=[];
    private MixerLevels levels=new();
    private float master=1,music=1,effects=1;
    private int busRamp;
    private bool disposed;
    private readonly AudioOutputClock clock = new();
    private long renderedFrames;
    private MixMixer mix;
    private MixGroup musicGroup,effectsGroup;
    private GCHandle self;
    private readonly float[] musicGains=new float[Block],effectsGains=new float[Block];
    private int musicOffset,effectsOffset;
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
                if(busRamp==0)(master,music,effects)=(value.Master,value.Music,value.Effects);
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
        self=GCHandle.Alloc(this,GCHandleType.Weak);
        musicGroup=SdlMixer.CreateGroup(mix);effectsGroup=SdlMixer.CreateGroup(mix);
        SdlMixer.PInvoke.SetGroupPostMixCallback(musicGroup,&MusicBus,(void*)GCHandle.ToIntPtr(self));
        SdlMixer.PInvoke.SetGroupPostMixCallback(effectsGroup,&EffectsBus,(void*)GCHandle.ToIntPtr(self));
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
            if(c.Bus==AudioBus.Music || c.Loop)Stop(owner,0);
            if(voices.Count>=MaxVoices)Remove(Math.Max(0,voices.FindIndex(v=>v.Bus==AudioBus.Effects && !v.Loop)));
            voice.Track=SdlMixer.CreateTrack(mix);
            voice.Handle=GCHandle.Alloc(voice);
            try
            {
                SdlMixer.SetTrackGroup(voice.Track,c.Bus==AudioBus.Music?musicGroup:effectsGroup);
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
                for(int j=0;j<n;j++)
                {
                    if(busRamp>0)
                    {
                        master+=(levels.Master-master)/busRamp;music+=(levels.Music-music)/busRamp;effects+=(levels.Effects-effects)/busRamp;busRamp--;
                    }
                    musicGains[j]=master*music;effectsGains[j]=master*effects;
                }
                musicOffset=effectsOffset=0;
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
    private static void MusicBus(void* user,MixGroup group,SdlAudioSpec* spec,float* pcm,int samples)
    { var m=(AudioMixer)GCHandle.FromIntPtr((nint)user).Target!; Apply(m.musicGains,ref m.musicOffset,spec->channels,pcm,samples); }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static void EffectsBus(void* user,MixGroup group,SdlAudioSpec* spec,float* pcm,int samples)
    { var m=(AudioMixer)GCHandle.FromIntPtr((nint)user).Target!; Apply(m.effectsGains,ref m.effectsOffset,spec->channels,pcm,samples); }
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
            if(!mix.IsNull()){SdlMixer.DestroyMixer(mix);mix=default;self.Free();}
        }
    }
}
