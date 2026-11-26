using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

public sealed partial class RealmBoard
{
    public PrefabReference ArrowArt, ArrowDust, ArrowDamage;
    private const float DrawSeconds = .16f, NearFlight = .30f, FarFlight = .42f;
    private readonly ArrowShot[] arrows = new ArrowShot[RealmRules.UnitLimit];
    private int activeArrows;
    private long lastBowCue = -1;
    private bool arrowCaptureWait;

    private struct ArrowShot
    {
        internal bool Active, Aiming, Capture, FacesLeft;
        internal int Archer;
        internal RealmRules.Unit? Owner;
        internal Guid Bow, Arrow;
        internal Vector2 Start, End;
        internal float WalkLeft, AimAge, Age, Duration, Rise;
        internal Impact Hit, Counter;
    }

    private void AimArrow(int index, Point target, Vector2 from, Vector2 to, RealmRules.Battle battle, Impact hit, Impact counter)
    {
        var attacker = rules.Units[index];
        ref var view = ref pieces[index];
        bool distant = RealmRules.Distance(attacker.Cell, target) == 2;
        float duration = distant ? FarFlight : NearFlight;
        float walk = view.Steps > 0 ? Math.Max(0, (view.Steps - 1) * StepSeconds - view.Age) : 0;
        bool capture = battle.City && rules.CityAt(target)?.Side == attacker.Side && turnPhase == TurnPhase.None;
        for (int i = 0; i < arrows.Length; i++)
        {
            if (arrows[i].Active) continue;
            bool facesLeft = to.X == from.X ? view.FacesLeft : to.X < from.X;
            arrows[i] = new()
            {
                Active = true, Archer = index, Owner = attacker, Capture = capture, FacesLeft = facesLeft,
                Start = from + new Vector2(facesLeft ? -14 : 14, -15), End = to - new Vector2(0, 11),
                WalkLeft = walk, Duration = duration, Rise = distant ? 37 : 23,
                Hit = hit, Counter = counter
            };
            activeArrows++;
            HoldDamage(hit);
            if (counter.Damage > 0) HoldDamage(counter);
            if (walk == 0) BeginDraw(ref arrows[i]);
            if (turnPhase == TurnPhase.RivalDelay)
                rivalMotion = Math.Max(rivalMotion, walk + DrawSeconds + duration + (counter.Damage > 0 ? counter.Wait : 0) + .04f);
            arrowCaptureWait |= capture;
            return;
        }
        // Preserve hit feedback if a crowded battle fills the presentation pool.
        QueueImpact(hit with { Wait = .13f, Subject = null });
        if (counter.Damage > 0) QueueImpact(counter with { Subject = null });
    }

    private void HoldDamage(Impact item)
    {
        if (item.Piece < 0) return;
        ref var view = ref pieces[item.Piece];
        if (view.Unit != item.Subject)
        {
            ReturnPiece(ref view);
            view.Unit = item.Subject;
        }
        view.HeldDamage += item.Damage;
        view.Routed |= item.Routed;
    }

    private void BeginDraw(ref ArrowShot shot)
    {
        ref var view = ref pieces[shot.Archer];
        shot.Aiming = true;
        shot.Bow = view.Actor;
        view.FacesLeft = shot.FacesLeft;
        view.BowLit = true;
        if (shot.Bow == Guid.Empty) return;
        DrawPiece(shot.Archer, true);
        objects.Animator(shot.Bow)?.SetTrigger("Shoot");
    }

    internal void ReleaseArrow(Guid archer)
    {
        for (int i = 0; i < arrows.Length; i++)
        {
            ref var shot = ref arrows[i];
            if (!shot.Active || !shot.Aiming || shot.Bow != archer || shot.Arrow != Guid.Empty) continue;
            Launch(ref shot);
            return;
        }
    }

    private void Launch(ref ArrowShot shot)
    {
        shot.Arrow = objects.Rent(ArrowArt, shot.Start);
        DrawArrow(shot, 0);
        long tick = session.Time.TickCount;
        if (tick != lastBowCue)
        {
            objects.Audio.Play(BowSound);
            lastBowCue = tick;
        }
    }

    private void AdvanceArrows(float seconds)
    {
        if (activeArrows == 0) return;
        for (int i = 0; i < arrows.Length; i++)
        {
            ref var shot = ref arrows[i];
            if (!shot.Active) continue;
            if (!shot.Aiming)
            {
                shot.WalkLeft -= seconds;
                if (shot.WalkLeft <= .0001f) BeginDraw(ref shot);
                continue;
            }
            if (shot.Arrow == Guid.Empty)
            {
                // A shot may originate outside charted sight or its pose may have been hidden.
                shot.AimAge += seconds;
                float fallback = shot.Bow == Guid.Empty ? DrawSeconds : DrawSeconds + .06f;
                if (shot.AimAge >= fallback) Launch(ref shot);
                continue;
            }
            shot.Age += seconds;
            if (shot.Age + .0001f < shot.Duration)
            {
                DrawArrow(shot, shot.Age / shot.Duration);
                continue;
            }

            objects.Return(shot.Arrow);
            ref var archer = ref pieces[shot.Archer];
            if (archer.Unit == shot.Owner)
            {
                archer.BowLit = false;
                if (archer.Actor != Guid.Empty) DrawPiece(shot.Archer, true);
            }
            var hit = shot.Hit;
            var counter = shot.Counter;
            bool capture = shot.Capture;
            shot = default;
            activeArrows--;
            ShowImpact(hit);
            if (counter.Damage > 0) QueueImpact(counter);
            if (capture)
            {
                arrowCaptureWait = false;
                RefreshAfterOrder();
            }
        }
    }

    private void DrawArrow(in ArrowShot shot, float t)
    {
        Vector2 delta = shot.End - shot.Start;
        Vector2 tip = shot.Start + delta * t - new Vector2(0, 4 * shot.Rise * t * (1 - t));
        float angle = MathF.Atan2(delta.Y - 4 * shot.Rise * (1 - 2 * t), delta.X);
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        // Rotate about the head rather than the sprite's top-left so the head meets the impact point.
        Vector2 origin = tip - direction * 22 - new Vector2(-direction.Y, direction.X) * 2;
        objects.SetLocalTransform(shot.Arrow, new(origin, float.RadiansToDegrees(angle), Vector2.One));
    }

    private void ResetArrows()
    {
        foreach (ref var shot in arrows.AsSpan())
        {
            if (shot.Arrow != Guid.Empty) objects.Return(shot.Arrow);
            shot = default;
        }
        activeArrows = 0;
        lastBowCue = -1;
        arrowCaptureWait = false;
    }
}
