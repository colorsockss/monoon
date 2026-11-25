using System;
using System.Drawing;
using Engine.Core;

// AccessLab showcase: the two launchers fire in turn every 2.6 s; when the rockets burst the screen shakes and flashes.
// Session.Screen applies the player's ShakeScale and FlashScale, so lowering them in the settings panel calms the next burst.
public sealed class FireworkShow : GameBehaviour
{
    public Guid[] Rockets = [];
    private const float Period = 2.6f, BurstDelay = 1.05f;
    private float time = 1.6f, nextLaunch, nextBurst = float.MaxValue;
    private int launcher;

    public override void FixedUpdate(ScriptTick tick)
    {
        time += tick.DeltaTime;
        if (time >= nextLaunch)
        {
            tick.Objects.EmitParticles(Rockets[launcher]);
            launcher = (launcher + 1) % Rockets.Length;
            nextLaunch = time + Period;
            nextBurst = time + BurstDelay;
        }
        if (time >= nextBurst)
        {
            nextBurst = float.MaxValue;
            tick.Session.Screen.Shake(7, .5f);
            tick.Session.Screen.Flash(Color.FromArgb(150, 255, 236, 190), .3f);
        }
    }
}
