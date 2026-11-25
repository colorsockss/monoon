using System;
using System.Numerics;
using Engine.Core;

// CameraLab framing showcase: the hero gathers herbs for the herbalist. With the Follow camera (deadzone 120 × 80, look-ahead
// 0.25 s) he picks three herbs by the campfire without leaving the deadzone, so the camera stays put, then runs east along a
// trail of four and the camera leads him. On the last herb the Frame camera takes over (group "Party"): the partner walks in
// from far off, the view widens to hold both and closes in as they meet, and they walk to the herbalist together. Each
// walker feeds its picture's animator (Move vector, Speed). Raises "deadzone", "lead", "frame".
public sealed class FramingTour : GameBehaviour
{
    public Guid Player, Partner, PlayerPicture, PartnerPicture, Follow, Frame, Deadzone, Spark, Counter;
    public Guid[] Picks = [];

    // A stop: walk to (X, Y) at Speed, pick herb Pick there (-1 for none), then wait Wait seconds.
    private readonly record struct Stop(float X, float Y, float Speed, int Pick, float Wait);
    private static readonly Stop[] HeroStops =
    [
        new(420, 520, 0, -1, 1.0f),
        new(455, 505, 70, 0, .35f), new(392, 535, 70, 1, .35f), new(448, 548, 70, 2, .6f),        // inside the deadzone
        new(600, 520, 165, 3, 0), new(760, 515, 165, 4, 0), new(920, 525, 165, 5, 0), new(1080, 520, 165, 6, .5f),   // the trail east
        new(1225, 640, 110, -1, .8f),                                                              // meet the partner
        new(1290, 488, 100, -1, 0),                                                                // to the herbalist
    ];
    private static readonly Stop[] PartnerStops = [new(1400, 760, 0, -1, .4f), new(1268, 652, 120, -1, 1.0f), new(1322, 522, 100, -1, 0)];
    private const int SwitchAfterPick = 6;
    private int heroNext, partnerNext = -1, picked;
    private float heroWait, partnerWait;
    private bool switched, delivered;

    public override void FixedUpdate(ScriptTick tick)
    {
        var objects = tick.Objects;
        float dt = tick.DeltaTime;
        if (!switched && heroNext >= 1) tick.Session.Set("deadzone");
        if (!switched && heroNext >= 5) tick.Session.Set("lead");
        Walk(tick, dt, Player, PlayerPicture, HeroStops, ref heroNext, ref heroWait);
        if (partnerNext >= 0) Walk(tick, dt, Partner, PartnerPicture, PartnerStops, ref partnerNext, ref partnerWait);
        if (!delivered && heroNext >= HeroStops.Length && partnerNext >= PartnerStops.Length)
        {
            delivered = true;
            objects.TryGetPosition(Player, out var at);
            objects.SetLocalTransform(Spark, new LocalTransform2D(at + new Vector2(40, -30), 0, Vector2.One));
            objects.EmitParticles(Spark);
            tick.Ui.SetText(Counter, "<color=#9FE6A0>Delivered</color>");
        }
    }

    private void Walk(ScriptTick tick, float dt, Guid who, Guid picture, Stop[] stops, ref int next, ref float wait)
    {
        var objects = tick.Objects;
        var animator = objects.Animator(picture);
        if (next >= stops.Length || !objects.TryGetPosition(who, out var at)) { animator?.SetFloat("Speed", 0); return; }
        if (wait > 0) { wait -= dt; animator?.SetFloat("Speed", 0); return; }
        var stop = stops[next];
        var to = new Vector2(stop.X, stop.Y) - at;
        float step = stop.Speed * dt;
        if (to.Length() <= step || stop.Speed == 0)
        {
            objects.SetLocalTransform(who, new LocalTransform2D(new Vector2(stop.X, stop.Y), 0, Vector2.One));
            animator?.SetFloat("Speed", 0);
            if (stop.Pick >= 0) Pick(tick, stop.Pick);
            wait = stop.Wait;
            next++;
            return;
        }
        var move = Vector2.Normalize(to) * step;
        objects.SetLocalTransform(who, new LocalTransform2D(at + move, 0, Vector2.One));
        animator?.SetVector2("Move", MathF.Abs(move.X) >= MathF.Abs(move.Y) ? new Vector2(MathF.Sign(move.X), 0) : new Vector2(0, MathF.Sign(move.Y)));
        animator?.SetFloat("Speed", 1);
    }

    private void Pick(ScriptTick tick, int index)
    {
        var objects = tick.Objects;
        objects.TryGetPosition(Picks[index], out var herb);
        objects.SetActive(Picks[index], false);
        objects.SetLocalTransform(Spark, new LocalTransform2D(herb + new Vector2(17, 17), 0, Vector2.One));
        objects.EmitParticles(Spark);
        tick.Ui.SetText(Counter, $"{++picked} / {Picks.Length}");
        if (index == SwitchAfterPick && !switched)
        {
            switched = true;
            objects.SetActive(Frame, true);
            tick.Cameras.TrySetActive(Frame);
            tick.Ui.SetVisible(Deadzone, false);
            tick.Session.Set("frame");
            partnerNext = 0;
        }
    }
}
