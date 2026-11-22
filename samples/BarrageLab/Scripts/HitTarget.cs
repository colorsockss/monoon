using System;
using System.Numerics;
using Engine.Core;

public sealed class HitTarget : GameBehaviour
{
    public float Amplitude = 80, Period = 180;
    private Vector2 home;
    private int phase;
    public int Hits { get; private set; }
    public int Exits { get; private set; }

    public override void OnCreate(ScriptLife life) => life.Objects.TryGetPosition(life.ObjectId, out home);

    public void Restart() => (Hits, Exits, phase) = (0, 0, 0);

    public override void FixedUpdate(ScriptTick tick)
    {
        var target = home + new Vector2(0, Amplitude * MathF.Sin(phase++ * MathF.Tau / Period));
        tick.Move(target - tick.Position);
    }

    public override void OnTriggerEnter(ScriptContact contact) => Hits++;
    public override void OnTriggerExit(ScriptContact contact) => Exits++;
}
