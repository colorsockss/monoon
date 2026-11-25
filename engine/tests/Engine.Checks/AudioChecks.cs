using System.Numerics;
using Engine.Core;

internal static class AudioChecks
{
    [Check(4200, "audio")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private static void Check(bool result,string why) { if(!result)throw new Exception("Audio: "+why); }
    private static float[] Render(AudioMixer m,int frames){var buffer=new float[frames*2];m.Render(buffer);return buffer;}
    private static double Power(float[] buf,int channel)=>buf.Where((_,i)=>(i&1)==channel).Average(x=>(double)x*x);
    public static void Run(string repo,string scratch)
    {
        var scene=SceneDocument.Load(Path.Combine(repo,"samples","AudioLab","Scenes","AudioLab.scene"));
        var music=scene.Objects.Single(o=>o.Name=="Tide").Id;
        var fx=scene.Objects.Single(o=>o.Name=="Beacon").Id;
        var player=scene.Objects.Single(o=>o.Name=="Player").Id;
        var old=SceneDocument.Load(Path.Combine(repo,"samples","KeyRoom","Scenes","Showcase.scene")).Objects
            .SelectMany(o=>o.Components.OfType<AudioSourceComponent>()).First();
        Check(old.Bus==AudioBus.Effects && old.Volume==1 && !old.Loop && !old.PlayOnStart,"old source defaults");
        var save=SceneDocument.Empty(new());save.RestoreObjects(scene.CaptureObjects());
        Check(save.Objects.First(o=>o.Id==music).GetComponent<AudioSourceComponent>()==scene.Objects.First(o=>o.Id==music).GetComponent<AudioSourceComponent>() &&
            save.Objects.First(o=>o.Id==player).GetComponent<AudioListener2DComponent>()!=null,"source/listener YAML round trip");
        using var session=new GameSession(scene);
        var world=session.Current;var mixer=session.Mixer;
        world.FixedUpdate(Vector2.Zero,1/60f);
        Check(world.IsPlaying(music) && !world.IsPlaying(fx),"auto-play only assigned sources");
        Check(world.Levels==new MixerLevels(.8f,.8f,.75f),"project bus defaults");
        var first=Render(mixer,57600);var second=Render(mixer,57600);var third=Render(mixer,57600);
        Check(first.AsSpan(40000).SequenceEqual(second.AsSpan(40000)) && second.SequenceEqual(third),"sample-exact, gapless streamed loop after fade-in");
        Check(Power(first[..4800],0)<.02*Power(second[..4800],0),"fade-in ramps up");
        world.Stop(music,.01f);
        Render(mixer,479);Check(world.IsPlaying(music),"fade-out not early");Render(mixer,1);
        Check(!world.IsPlaying(music) && Render(mixer,1).All(v=>v==0),"fade-out stops on last sample");
        // The resampled beacon starts one frame late; compare whole beacon cycles after its first window.
        world.Play(fx,0);Render(mixer,4800);var west=Render(mixer,4800);
        Check(Math.Abs(Power(west,1)/Power(west,0)-4)<.001,"listener at west, beacon louder on right");
        world.Levels=world.Levels with { Music=0 };
        var sameEffects=Render(mixer,4800);
        Check(west.SequenceEqual(sameEffects),"music mute leaves isolated FX waveform unchanged");
        world.Levels=world.Levels with { Effects=.375f };Render(mixer,4800);
        var quiet=Render(mixer,4800);
        Check(Math.Abs(Power(quiet,0)/Power(west,0)-.25)<.001,"half gain is quarter power");
        for(int i=0;i<60;i++)world.FixedUpdate(Vector2.UnitX,1/60f);
        Render(mixer,512);var east=Render(mixer,4800);
        Check(Math.Abs(Power(east,0)/Power(east,1)-4)<.001,"listener moves to opposite side");
        world.SetActive(player,false);world.FixedUpdate(Vector2.Zero,1/60f);
        Render(mixer,512);var center=Render(mixer,4800);
        Check(Math.Abs(Power(center,0)/Power(center,1)-1)<.0001 && Power(center,0)>Power(east,0),"camera fallback and distance attenuation");
        world.SetActive(fx,false);Check(!world.IsPlaying(fx),"deactivation releases stream");
        world.SetActive(music,false);world.SetActive(music,true);world.FixedUpdate(Vector2.Zero,1/60f);
        Check(world.IsPlaying(music),"auto music starts again on re-enable");
        world.Destroy(music);Check(!mixer.HasVoices,"destroy releases owned sources");
        var far=SceneDocument.Load(Path.Combine(repo,"samples","AudioLab","Scenes","AudioLab.scene"));
        var distantPlayer=far.Objects.Single(o=>o.Id==player);distantPlayer.X=-2000;distantPlayer.RemoveComponent<ScriptComponent>();
        far.Objects.Single(o=>o.Id==music).GetComponent<AudioSourceComponent>()!.PlayOnStart=false;
        using(var distance=new SceneGame(far))
        {
            distance.FixedUpdate(Vector2.Zero,0); // Set the listener before the first output block.
            distance.Play(fx,0);
            Check(Render(distance.Mixer,1000).All(v=>v==0) && distance.IsPlaying(fx),"out-of-range loop is silent without losing phase");
        }
        // 96 voices: a new sound replaces the oldest one-shot effect, never a loop or music.
        using(var limit=new AudioMixer())
        {
            var beacon=Path.Combine(repo,"samples","AudioLab","Assets","Beacon.wav");
            var loop=new object();limit.Start(loop,beacon,new AudioSourceComponent { Loop=true },default,0);
            var shots=Enumerable.Range(0,96).Select(_=>new object()).ToArray();
            foreach(var shot in shots)limit.Start(shot,beacon,new AudioSourceComponent(),default,0);
            Render(limit,512);
            Check(limit.Playing(loop) && !limit.Playing(shots[0]) && shots.Skip(1).All(limit.Playing),"voice limit steals the oldest one-shot effect");
        }
        // A 24 kHz source starts one frame late with no silent gap; starting it leaves a 48 kHz voice's samples unchanged.
        var afterbeat=Path.Combine(repo,"samples","RhythmLab","Assets","Afterbeat.wav");
        float[] Rhythm(bool joined)
        {
            using var m=new AudioMixer();
            m.Start(new object(),afterbeat,new AudioSourceComponent { Bus=AudioBus.Music,Loop=true },default,0);
            var head=Render(m,1000);
            if(joined)m.Start(new object(),Path.Combine(repo,"samples","AudioLab","Assets","Beacon.wav"),new AudioSourceComponent { Volume=0 },default,0);
            return [..head,..Render(m,4800)];
        }
        Check(Rhythm(false).SequenceEqual(Rhythm(true)),"a resampled voice's start leaves 48 kHz samples unchanged");
        using(var resampled=new AudioMixer())
        {
            resampled.Start(new object(),Path.Combine(repo,"samples","AudioLab","Assets","Beacon.wav"),new AudioSourceComponent { Loop=true },default,0);
            var start=Render(resampled,4800);int lead=Array.FindIndex(start,v=>v!=0)/2,run=0,longest=0;
            for(int f=lead;f<4800;f++){run=start[2*f]==0&&start[2*f+1]==0?run+1:0;longest=Math.Max(longest,run);}
            Check(lead<=2 && longest<4,$"resampled start: first sample at frame {lead}, longest silence {longest} frames");
        }
        // A large sparse WAV proves preparation reads the header rather than allocating the data chunk.
        var file=Path.Combine(scratch,"large.wav");
        using(var f=File.Create(file))
        using(var w=new BinaryWriter(f))
        {
            const int bytes=96*1024*1024;w.Write("RIFF"u8);w.Write(bytes+36);w.Write("WAVEfmt "u8);w.Write(16);
            w.Write((short)1);w.Write((short)2);w.Write(48000);w.Write(192000);w.Write((short)4);w.Write((short)16);
            w.Write("data"u8);w.Write(bytes);f.SetLength(44L+bytes);
        }
        long before=GC.GetAllocatedBytesForCurrentThread();mixer.Prepare(file);
        long bytesUsed=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytesUsed<1024*1024,"large WAV metadata allocation");
        var broken=Path.Combine(scratch,"broken.wav");File.WriteAllBytes(broken,File.ReadAllBytes(Path.Combine(repo,"samples","KeyRoom","Assets","Unlock.wav"))[..48]);
        try {mixer.Prepare(broken);throw new Exception("Accepted truncated WAV");}catch(InvalidDataException){}
        Console.WriteLine($"PASS audio: gapless streamed loops, exact fades, bus isolation, listener pan/attenuation/fallback, source lifecycle, 96-voice stealing, WAV/YAML compatibility; preparing 96 MiB allocated {bytesUsed} bytes");
    }
}
