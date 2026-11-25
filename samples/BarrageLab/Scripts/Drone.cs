using System;
using System.Numerics;
using Engine.Core;

// Showcase: a drone that drifts on a sine path, flashes white on each hit (its Flash child, a white silhouette) and bursts
// after a few (its Burst child emits particles in world space), then comes back after a pause. Raises the "burst" flag.
public sealed class Drone : GameBehaviour
{
    public float Amplitude = 60, Period = 180, Respawn = 1.4f;
    public int Health = 4;
    public Guid Flash, Burst;
    private Vector2 home;
    private int phase, hits, flash;
    private GameTimer? back;

    public override void OnCreate(ScriptLife life)
    {
        life.Objects.TryGetPosition(life.ObjectId, out home);
        if (Flash != Guid.Empty) life.Objects.SetActive(Flash, false);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var target = home + new Vector2(0, Amplitude * MathF.Sin(phase++ * MathF.Tau / Period));
        tick.Move(target - tick.Position);
        if (flash > 0 && --flash == 0) tick.Objects.SetActive(Flash, false);
        if (back?.Consume() != true) return;
        back = null;
        hits = 0;
        tick.Objects.SetSpriteTint(tick.ObjectId, null);
        tick.Objects.SetColliderEnabled(tick.ObjectId, true);
    }

    public override void OnTriggerEnter(ScriptContact contact)
    {
        var objects = contact.Objects;
        if (++hits < Health)
        {
            objects.SetActive(Flash, true);
            flash = 3;
            return;
        }
        objects.SetActive(Flash, false);
        objects.SetSpriteTint(contact.ObjectId, "#FFFFFF00");   // hidden until it comes back
        objects.SetColliderEnabled(contact.ObjectId, false);
        if (Burst != Guid.Empty) objects.EmitParticles(Burst);
        contact.Session.Set("burst");
        back = contact.Session.Time.StartTimer(Respawn);
    }
}
