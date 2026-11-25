using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

// CameraLab framing showcase: the hero gathers herbs for the herbalist. With the Follow camera (deadzone 120 × 80, look-ahead
// 0.25 s) he picks three herbs by the campfire without leaving the deadzone, so the camera stays put, then runs east along a
// trail of four and the camera leads him. On the last herb the Frame camera takes over (group "Party"): the partner walks in
// from far off, the view widens to hold both and closes in as they meet, and they walk to the herbalist together. Each
// walker feeds its picture's animator (Move vector, Speed). G toggles debug gizmos drawn with tick.Debug (Scene Play and
// development builds only): pick reach and the line to the next stop, the herbs left, the deadzone and the framed group's box.
// Raises "deadzone", "lead", "frame", "gizmo".
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
    private bool switched, delivered, gizmos;

    public override void FixedUpdate(ScriptTick tick)
    {
        var objects = tick.Objects;
        float dt = tick.DeltaTime;
        if (!switched && heroNext >= 1) tick.Session.Set("deadzone");
        if (!switched && heroNext >= 5) tick.Session.Set("lead");
        Walk(tick, dt, Player, PlayerPicture, HeroStops, ref heroNext, ref heroWait);
        if (partnerNext >= 0) Walk(tick, dt, Partner, PartnerPicture, PartnerStops, ref partnerNext, ref partnerWait);
#if MONOON_DEBUG_DRAW
        if (tick.Input.Pressed("DebugDraw")) { gizmos = !gizmos; if (gizmos) tick.Session.Set("gizmo"); }
        if (gizmos) Gizmos(tick);
#endif
        if (!delivered && heroNext >= HeroStops.Length && partnerNext >= PartnerStops.Length)
        {
            delivered = true;
            objects.TryGetPosition(Player, out var at);
            objects.SetLocalTransform(Spark, new LocalTransform2D(at + new Vector2(40, -30), 0, Vector2.One));
            objects.EmitParticles(Spark);
            tick.Ui.SetText(Counter, "<color=#9FE6A0>Delivered</color>");
        }
    }

#if MONOON_DEBUG_DRAW
    private void Gizmos(ScriptTick tick)
    {
        var objects = tick.Objects;
        var debug = tick.Debug;
        objects.TryGetPosition(Player, out var hero);
        objects.TryGetPosition(Partner, out var partner);
        hero += new Vector2(11, 6);      // the feet: middle of the 22 x 12 body
        partner += new Vector2(11, 6);
        debug.Circle(hero, 18, Color.Gold, 2);
        if (heroNext < HeroStops.Length) debug.Line(hero, new Vector2(HeroStops[heroNext].X + 11, HeroStops[heroNext].Y + 6), Color.Cyan, 2);
        debug.Text(hero + new Vector2(-30, 14), $"{hero.X:0}, {hero.Y:0}", Color.White, 13);
        for (int i = 0; i < Picks.Length; i++)
            if (objects.IsActive(Picks[i]) && objects.TryGetPosition(Picks[i], out var herb))
            {
                debug.Circle(herb + new Vector2(17, 20), 10, Color.LimeGreen, 1);
                debug.Text(herb + new Vector2(12, -6), (i + 1).ToString(), Color.LimeGreen, 13);
            }
        if (!switched && tick.Cameras.TryGetView(Follow, out var view))
            Box(debug, view.Center - new Vector2(60, 40), view.Center + new Vector2(60, 40), Color.White, "deadzone");
        if (switched)
        {
            if (partnerNext < PartnerStops.Length) debug.Line(partner, new Vector2(PartnerStops[partnerNext].X + 11, PartnerStops[partnerNext].Y + 6), Color.HotPink, 2);
            var min = Vector2.Min(hero, partner) - new Vector2(48, 78);   // Padding 48 around both, pictures reach 30 above the feet
            var max = Vector2.Max(hero, partner) + new Vector2(48, 48);
            Box(debug, min, max, Color.Magenta, "Party");
        }
    }

    private static void Box(ScriptDebug debug, Vector2 min, Vector2 max, Color color, string label)
    {
        debug.Line(min, new Vector2(max.X, min.Y), color, 1);
        debug.Line(new Vector2(max.X, min.Y), max, color, 1);
        debug.Line(max, new Vector2(min.X, max.Y), color, 1);
        debug.Line(new Vector2(min.X, max.Y), min, color, 1);
        debug.Text(min + new Vector2(4, 2), label, color, 13);
    }
#endif

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
