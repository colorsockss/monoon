using System.Numerics;

namespace Engine.Core;

public sealed partial class SceneGame : IGameAudio
{
    private Body[] audioSources=[],listeners=[];
    private readonly bool ownsAudio;
    public AudioMixer Mixer=>State.Mixer;
    public IGameAudio Audio=>this;
    public MixerLevels Levels { get=>Mixer.Levels;set=>Mixer.Levels=value; }
    public bool TransitionTo(string? snapshot,float seconds=0)=>Mixer.TransitionTo(snapshot,seconds);
    private static Vector2 AudioCenter(Body b)=>Affine2D.Transform(b.Size*.5f,b.MatrixAt(b.Position));
    private void IndexAudio()
    {
        audioSources=bodies.Where(b=>b.AudioSource!=null && b.Sound!=null).ToArray();
        listeners=bodies.Where(b=>b.Source.GetComponent<AudioListener2DComponent>()!=null).ToArray();
    }
    private void SyncAudio()
    {
        if (!EngineFeatures.Audio) return;
        var ear=Array.Find(listeners,b=>b.Active);
        var center=ear!=null?AudioCenter(ear):ViewCamera is { Active:true } lens?Sample(lens,1).Center:Vector2.Zero;
        Mixer.MoveListener(center);
        lock(Mixer.SyncRoot)
        foreach(var b in audioSources)
        {
            if(!b.Active)continue;
            if(b.AudioSource!.Spatial)Mixer.MoveSource(b,AudioCenter(b));
            if(b.AutoStarted || !b.AudioSource.PlayOnStart)continue;
            Play(b.Id);
        }
    }
    public bool Play(Guid source,float? fadeSeconds=null)
    {
        if (!EngineFeatures.Audio) return false; // no Audio Source without the feature
        if(closing || !byId.TryGetValue(source,out var b) || !b.Active || b.Sound==null || b.AudioSource is not {} c)return false;
        b.Playback = Mixer.Start(b,b.Sound,c,AudioCenter(b),fadeSeconds??c.FadeIn);
        b.AutoStarted=true;
        SoundRequested?.Invoke(b.Sound);
        return true;
    }
    public bool Stop(Guid source,float? fadeSeconds=null)
    {
        if (!EngineFeatures.Audio) return false;
        if(!byId.TryGetValue(source,out var b) || b.AudioSource is not {} c)return false;
        b.AutoStarted=true;
        return Mixer.Stop(b,fadeSeconds??c.FadeOut);
    }
    public bool IsPlaying(Guid source)=>byId.TryGetValue(source,out var b)&&Mixer.Playing(b);
    public bool TryGetPlaybackFrame(Guid source, long timestamp, out long frame)
    {
        frame = 0;
        return byId.TryGetValue(source, out var body) && Mixer.TryGetPlaybackFrame(body.Playback, timestamp, out frame);
    }
    private void Silence(Body b)
    {
        if (!EngineFeatures.Audio) return;
        if(b.AudioSource==null)return;
        Mixer.Stop(b,0);b.AutoStarted=false;
    }
}
