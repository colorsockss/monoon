using Brutal.SdlApi;
using Engine.Core;

namespace Engine.Desktop;

// One stereo stream for the entire session. The worker services the device queue, not the game clock.
public sealed unsafe class SdlSounds : IDisposable
{
    private SdlAudioStream stream;
    private Thread? worker;
    private AudioMixer? mixer;
    private volatile bool stopping;
    private bool initialized;
    private Exception? failure;
    private readonly float[] buffer=new float[512*2];
    private FileStream? trace;
    public void Attach(AudioMixer value)=>mixer=value;
    public void PreparePlayback(string path)
    {
        mixer!.Prepare(path);
        // Validate assets even on a machine without sound. Report the device error when playback starts.
        if(worker==null && failure==null)
            try { Open(); } catch(Exception e) when(e is IOException or SdlException) { failure=e; }
    }
    public void Release(string path)=>mixer?.Release(path);
    private void Open()
    {
        if(!initialized){ Sdl.Init.InitSubSystem(SdlInitFlags.Audio); initialized=true; }
        // SDL_AUDIO_F32LE, native on all supported desktop targets.
        var spec=new SdlAudioSpec { format=(SdlAudioFormat)0x8120, channels=2,freq=AudioMixer.SampleRate };
        stream=Sdl.PInvoke.OpenAudioDeviceStream(uint.MaxValue,&spec,null,null);
        if(stream.IsNull())throw new IOException(Sdl.PInvoke.GetError());
        if(Sdl.PInvoke.ResumeAudioStreamDevice(stream)==0)throw new IOException(Sdl.PInvoke.GetError());
        worker=new Thread(Pump) { IsBackground=true,Name="MoNoon Audio" };worker.Start();
    }
    private long OutputDelayTicks()
    {
        // SDL 3.4 CoreAudio refills one AudioQueue buffer while the others are
        // already scheduled. The device then plays one hardware buffer.
        if (!OperatingSystem.IsMacOS() || Sdl.PInvoke.GetCurrentAudioDriver() != "coreaudio") return 0;
        SdlAudioSpec format;
        int frames;
        if (Sdl.PInvoke.GetAudioDeviceFormat(Sdl.PInvoke.GetAudioStreamDevice(stream), &format, &frames) == 0 ||
            frames <= 0 || format.freq <= 0) return 0;
        double packet = (double)frames / format.freq;
        int buffers = packet >= .015 ? 3 : 2 * (int)Math.Ceiling(.015 / packet);
        double delay = (buffers - 1) * packet + MacAudioOutput.DeviceBufferSeconds();
        return (long)Math.Round(delay * System.Diagnostics.Stopwatch.Frequency);
    }

    private void Pump()
    {
        try
        {
            // Optional diagnostics: only create a new, explicitly named file; never overwrite user data.
            if(Environment.GetEnvironmentVariable("MONOON_AUDIO_CAPTURE") is { Length:>0 } path)
                trace=new(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
            long presentationDelay = OutputDelayTicks();
            bool draining = false;
            while(!stopping)
            {
                if(!mixer!.HasVoices && !draining){Thread.Sleep(3);continue;}
                int queued=Sdl.PInvoke.GetAudioStreamQueued(stream);
                if(queued<0)throw new IOException(Sdl.PInvoke.GetError());
                mixer.ObserveOutput(queued / (2 * sizeof(float)), System.Diagnostics.Stopwatch.GetTimestamp() + presentationDelay);
                if (!mixer.HasVoices)
                {
                    draining = queued != 0;
                    Thread.Sleep(3);
                    continue;
                }
                if(queued>=4096*sizeof(float)*2){Thread.Sleep(3);continue;}
                mixer.Render(buffer);
                fixed(float* samples=buffer)
                    if(Sdl.PInvoke.PutAudioStreamData(stream,samples,buffer.Length*sizeof(float))==0)
                        throw new IOException(Sdl.PInvoke.GetError());
                draining = true;
                trace?.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(buffer.AsSpan()));
                // Deliver the final partial device packet after a one-shot or completed fade.
                if(!mixer.HasVoices && Sdl.PInvoke.FlushAudioStream(stream)==0)throw new IOException(Sdl.PInvoke.GetError());
            }
        }
        catch(Exception e){Interlocked.Exchange(ref failure,e);}
        finally {trace?.Dispose();trace=null;}
    }
    private bool reported;
    public string? TakeError()
    {
        if(reported || mixer?.HasVoices!=true || Volatile.Read(ref failure) is not {} e)return null;
        reported=true;return e.Message;
    }
    public void Dispose()
    {
        stopping=true; worker?.Join();worker=null;
        if(!stream.IsNull()){Sdl.PInvoke.DestroyAudioStream(stream);stream=default;}
        if(initialized){Sdl.Init.QuitSubSystem(SdlInitFlags.Audio);initialized=false;}
        mixer=null;failure=null;reported=false;stopping=false;
    }
}
