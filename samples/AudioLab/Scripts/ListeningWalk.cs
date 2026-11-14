using System;
using System.Numerics;
using Engine.Core;

public sealed class ListeningWalk : GameBehaviour
{
    public Guid Music,Beacon,Levels,Readout;
    private bool beacon,music=true,quietMusic,quietFx,mute;
    private string lastStatus="",lastLevels="";
    public override void FixedUpdate(ScriptTick tick)
    {
        var audio=tick.Objects.Audio;
        if(tick.Input.Pressed("Interact"))
        {
            beacon=!beacon;if(beacon)audio.Play(Beacon);else audio.Stop(Beacon);
        }
        if(tick.Input.Pressed("Fade"))
        {
            music=!music;if(music)audio.Play(Music);else audio.Stop(Music);
        }
        var levels=audio.Levels;
        if(tick.Input.Pressed("MoveUp")){quietMusic=!quietMusic;levels=levels with { Music=quietMusic?.2f:.8f };}
        if(tick.Input.Pressed("MoveDown")){quietFx=!quietFx;levels=levels with { Effects=quietFx?.375f:.75f };}
        if(tick.Input.Pressed("MasterMute")){mute=!mute;levels=levels with { Master=mute?0:.8f };}
        audio.Levels=levels;
        var x=Math.Clamp(tick.Position.X+tick.Direction.X*600*tick.DeltaTime,144,624);
        tick.Move(new Vector2(x-tick.Position.X,0));
        var status=$"Listener X  {x+16:0}     |     Beacon  {(beacon?"on":"off")}     |     Music  {(music?"playing":"fading / stopped")}";
        var line=$"Master  {levels.Master:P0}       Music  {levels.Music:P0}       Effects  {levels.Effects:P0}";
        if(lastStatus!=status){lastStatus=status;tick.Ui.SetText(Readout,status);}
        if(lastLevels!=line){lastLevels=line;tick.Ui.SetText(Levels,line);}
    }
}
