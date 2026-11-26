using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

public sealed class TrailRunner : GameBehaviour
{
    public Guid Portrait, HeartCount, TokenCount, GemCount;
    public Guid StageCard, Notice, PausePanel, Results, ResultTime, ResultHaul, ResultGems, ResultPressed, FieldKit;
    public Guid Chime, Hurt, Clear, Step, Rush, Stomp, Warning, Spark, DashWake, HitBurst;
    public Guid DashGhostA, DashGhostB, StompNumber;
    public Guid[] SplitTime = [], SplitTokens = [], SplitGems = [];
    public Guid HeartPill, TokenPill, GemPill, DeliveredBadge;
    public SceneReference NextStage, TrailheadScene;
    public int Stage;
    public float FallY = 256;

    private enum Pose { Idle, Run, Rise, Fall, Wall, Dash, Land, Hurt, Ready, Apex, Stomp, Win }
    private const float Speed = 104, JumpSpeed = 290;
    private static readonly Color HitFlash = Color.FromArgb(62, 236, 107, 92);
    private static readonly Color ClearFlash = Color.FromArgb(44, 255, 215, 140);
    private readonly PhysicsFilter terrain = new(1);
    private Vector2 checkpoint;
    private int health = 3, tokens, gems, dashTicks, wallKickTicks, recoveryTicks;
    private int coyoteTicks, airTicks, landingTicks, noticeTicks, ghostATicks, ghostBTicks;
    private float facing = 1, dashDirection, wallKickDirection;
    private long startedAt;
    private int stompNumberTicks, jumpPoseTicks, stompPoseTicks;
    private double hitStopUntil, leaveAt, tallyStart;
    private int shownTime, shownTokens, shownGems;
    private TrailExpedition.Leg tally;
    private bool tallyRunning;
    private ScriptTween heartPop, tokenPop, gemPop;
    private Guid destination;
    private bool completed, airDashSpent, wasGrounded, tinted;
    private Pose pose;
    public int Tokens => tokens;
    public int Gems => gems;
    public int Health => health;
    public bool Completed => completed;
    public bool HasCheckpoint { get; private set; }

    public override void OnCreate(ScriptLife life)
    {
        life.Objects.TryGetPosition(life.ObjectId, out checkpoint);
        startedAt = life.Session.Time.TickCount;
        life.Ui.SetVisible(PausePanel, false);
        life.Ui.SetVisible(Notice, false);
        life.Ui.SetVisible(Results, false);
        life.Ui.SetVisible(ResultPressed, false);
        if (life.Ui.Preferences.TextScale <= 1 && life.Ui is ScreenUi screen)
        {
            screen.SetBoxOffset(HeartPill, new(0, 0, -10, 0));
            if (Stage < 4)
            {
                screen.SetBoxOffset(TokenPill, new(-10, 0, -10, 0));
                screen.SetBoxOffset(GemPill, new(-20, 0, -13, 0));
            }
        }
        if (StageCard != Guid.Empty && life.Ui.Preferences.ShakeScale > 0)
            life.Objects.Tween(StageCard, TweenClock.Ui)
                .UiRect(Stage == 4 ? new(-44, 0, 0, 0) : new(0, -62, 0, 0), 0)
                .UiRect(new(0, 4, 0, 0), .28f, TweenEase.EaseIn)
                .UiRect(new(0, -2, 0, 0), .085f, TweenEase.EaseOut)
                .UiRect(RectangleF.Empty, .085f);
        life.Session.Screen.Fade(Color.Black, 0);
        life.Session.Screen.Fade(Color.Transparent, .3);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var motion = tick.Objects.Motion;
        if (stompNumberTicks > 0 && --stompNumberTicks == 0) tick.Objects.SetActive(StompNumber, false);
        if (ghostATicks > 0 && --ghostATicks == 0) tick.Objects.SetActive(DashGhostA, false);
        if (ghostBTicks > 0 && --ghostBTicks == 0) tick.Objects.SetActive(DashGhostB, false);
        if (completed || destination != Guid.Empty)
        {
            motion.DriveCharacter(tick.ObjectId, 0);
            return;
        }
        if (tick.Session.Time.TickCount == startedAt + 120) tick.Ui.SetVisible(StageCard, false);
        if (noticeTicks > 0 && --noticeTicks == 0) tick.Ui.SetVisible(Notice, false);
        if (recoveryTicks > 0) recoveryTicks--;

        if (tick.Position.Y > FallY)
        {
            Damage(tick.ObjectId, tick.Position, tick.Objects, tick.Ui, tick.Session, true);
            return;
        }
        if (!motion.TryGetMotion(tick.ObjectId, out var body)) return;
        if (landingTicks > 0) landingTicks--;
        if (jumpPoseTicks > 0) jumpPoseTicks--;
        if (stompPoseTicks > 0) stompPoseTicks--;
        if (body.Grounded)
        {
            if (!wasGrounded || dashTicks == 0) airDashSpent = false;
            if (airTicks >= 8)
            {
                landingTicks = 5;
                tick.Objects.Audio.Play(Step);
            }
            coyoteTicks = 6;
            airTicks = 0;
        }
        else airTicks++;
        wasGrounded = body.Grounded;

        float axis = tick.Direction.X;
        if (axis != 0 && dashTicks == 0 && wallKickTicks == 0) Face(tick.Objects, MathF.Sign(axis));
        bool wantsJump = tick.Input.Triggered("Jump");
        bool wallContact = !body.Grounded && axis != 0 && (body.Velocity.Y >= 0 || wantsJump) && TouchesWall(tick, axis);
        if (tick.Input.Pressed("Dash") && dashTicks == 0 && !airDashSpent)
        {
            airDashSpent = true;
            dashDirection = facing;
            dashTicks = 8;
            wallKickTicks = coyoteTicks = 0;
            tick.Objects.Audio.Play(Rush);
        }

        bool rising = body.Velocity.Y < -10;
        if (dashTicks > 0)
        {
            if ((dashTicks & 3) == 0)
            {
                Emit(tick.Objects, DashWake, tick.Position + new Vector2(7 - 7 * facing, 12));
                Guid ghost = dashTicks == 8 ? DashGhostA : DashGhostB;
                tick.Objects.SetLocalTransform(ghost, new(tick.Position + new Vector2(-11, -19), 0, new(facing, 1)));
                tick.Objects.SetActive(ghost, true);
                if (dashTicks == 8) ghostATicks = 9;
                else ghostBTicks = 8;
            }
            dashTicks--;
            motion.SetVelocity(tick.ObjectId, new(260 * dashDirection, 0));
            motion.DriveCharacter(tick.ObjectId, 260 * dashDirection);
            SetPose(tick.Objects, Pose.Dash);
            return;
        }
        if (wallContact && wantsJump && tick.Input.Consume("Jump"))
        {
            wallKickDirection = -MathF.Sign(axis);
            wallKickTicks = 9;
            coyoteTicks = 0;
            motion.SetVelocity(tick.ObjectId, new(126 * wallKickDirection, -235));
            motion.DriveCharacter(tick.ObjectId, 126 * wallKickDirection);
            Face(tick.Objects, wallKickDirection);
            Emit(tick.Objects, DashWake, tick.LocalToWorld(new(7, 12)));
            tick.Objects.Audio.Play(Step);
            rising = true;
            wallContact = false;
        }
        else if (wallKickTicks > 0)
        {
            wallKickTicks--;
            motion.DriveCharacter(tick.ObjectId, 126 * wallKickDirection);
            wallContact = false;
        }
        else
        {
            bool jumped = coyoteTicks > 0 && tick.Input.Consume("Jump");
            if (jumped)
            {
                jumpPoseTicks = 4;
                coyoteTicks = 0;
                rising = true;
                if (!body.Grounded) motion.SetVelocity(tick.ObjectId, new(body.Velocity.X, -JumpSpeed));
            }
            else if (wallContact && body.Velocity.Y > 56)
                motion.SetVelocity(tick.ObjectId, new(body.Velocity.X, 56));
            else if (tick.Input.Released("Jump") && body.Velocity.Y < -105)
                motion.SetVelocity(tick.ObjectId, new(body.Velocity.X, -105));
            motion.DriveCharacter(tick.ObjectId, axis * Speed, jumped && body.Grounded ? JumpSpeed : 0);
        }
        if (!body.Grounded && coyoteTicks > 0) coyoteTicks--;
        if (wallContact && pose != Pose.Wall)
            Emit(tick.Objects, DashWake, tick.Position + new Vector2(7 + 7 * facing, 13));
        SetPose(tick.Objects, recoveryTicks > 39 ? Pose.Hurt : stompPoseTicks > 0 ? Pose.Stomp
            : wallContact ? Pose.Wall : jumpPoseTicks > 0 ? Pose.Ready
            : !body.Grounded && airTicks >= 4 && MathF.Abs(body.Velocity.Y) <= 34 ? Pose.Apex
            : rising ? Pose.Rise : !body.Grounded ? Pose.Fall
            : landingTicks > 0 ? Pose.Land : axis != 0 ? Pose.Run : Pose.Idle);
        bool blink = recoveryTicks > 0 && (recoveryTicks / 4 & 1) != 0 && tick.Ui.Preferences.FlashScale > 0;
        if (blink != tinted)
        {
            tick.Objects.SetSpriteTint(Portrait, blink ? "#E89284" : null);
            tinted = blink;
        }
    }

    private void Face(IGameObjects objects, float direction)
    {
        if (direction == facing) return;
        facing = direction;
        if (objects.TryGetLocalTransform(Portrait, out var local))
            objects.SetLocalTransform(Portrait, local with { Scale = new(facing, 1) });
    }

    private void SetPose(IGameObjects objects, Pose next)
    {
        if (pose == next) return;
        pose = next;
        objects.Animator(Portrait)?.SetInt("Pose", (int)pose);
    }

    private bool TouchesWall(ScriptTick tick, float axis)
    {
        Vector2 direction = new(MathF.Sign(axis), 0);
        Vector2 center = tick.LocalToWorld(new(7, 10));
        return tick.Objects.Physics.Raycast(center, direction, 10, terrain) is { Normal.X: var normal }
            && direction.X * normal < -.7f;
    }

    public override void UpdateUi(UiTick tick)
    {
        var time = tick.Session.Time;
        if (tallyRunning) AnimateTally(tick.Ui, time.UiSeconds - tallyStart);
        if (hitStopUntil != 0 && time.UiSeconds >= hitStopUntil)
        {
            time.Scale = 1;
            hitStopUntil = 0;
        }
        if (destination != Guid.Empty)
        {
            if (time.UiSeconds >= leaveAt) tick.Session.LoadScene(destination);
            return;
        }
        Guid target = tick.Input.Pressed("BackToTrailhead") ? TrailheadScene.Id
            : completed && tick.Input.Pressed("Proceed") ? NextStage.Id : Guid.Empty;
        if (target == Guid.Empty && completed && tick.Input.Pressed("Proceed")) target = TrailheadScene.Id;
        if (target != Guid.Empty)
        {
            if (completed && tick.Input.Pressed("Proceed")) tick.Ui.SetVisible(ResultPressed, true);
            time.IsPaused = false;
            time.Scale = 1;
            hitStopUntil = 0;
            tick.Ui.SetVisible(PausePanel, false);
            tick.Session.Screen.Fade(Color.Black, .14);
            leaveAt = time.UiSeconds + .15;
            destination = target;
            return;
        }
        if (!tick.Input.Pressed("Pause") || completed) return;
        time.IsPaused = !time.IsPaused;
        tick.Ui.SetVisible(PausePanel, time.IsPaused);
        tick.Ui.SetVisible(StageCard, !time.IsPaused && time.TickCount - startedAt < 120);
        tick.Ui.SetVisible(Notice, !time.IsPaused && noticeTicks > 0);
    }

    public void Collect(bool gem, Vector2 at, IGameObjects objects, IGameUi ui)
    {
        if (gem)
        {
            ArcadeUi.HudNumber(ui, GemCount, $"{++gems}/3");
            Pop(GemCount, objects, ui, ref gemPop);
        }
        else
        {
            ArcadeUi.HudNumber(ui, TokenCount, $"{++tokens:D2}");
            Pop(TokenCount, objects, ui, ref tokenPop);
        }
        objects.Audio.Play(Chime);
        Emit(objects, Spark, at);
    }

    public void SaveCheckpoint(Vector2 at, IGameObjects objects, IGameUi ui)
    {
        checkpoint = at;
        HasCheckpoint = true;
        noticeTicks = 72;
        ui.SetVisible(Notice, true);
        objects.Audio.Play(Chime);
        Emit(objects, Spark, at - new Vector2(0, 10));
    }

    public void Finish(SessionState session, IGameObjects objects, IGameUi ui)
    {
        if (completed || destination != Guid.Empty) return;
        completed = true;
        SetPose(objects, Pose.Win);
        objects.Audio.Play(Clear);
        session.Screen.Flash(ClearFlash, .24);
        long ticks = session.Time.TickCount - startedAt;
        ui.SetVisible(StageCard, false);
        ui.SetVisible(Notice, false);
        ui.SetVisible(Results, true);
        if (ui.Preferences.ShakeScale > 0)
            objects.Tween(Results, TweenClock.Ui).UiRect(new(0, 5, 0, 0), 0)
                .UiRect(RectangleF.Empty, .18f, TweenEase.EaseOut);
        var expedition = TrailExpedition.Record(session, Stage, ticks, tokens, gems);
        tally = Stage < 4 ? new(ticks, tokens, gems) : expedition.Total;
        shownTime = shownTokens = shownGems = -1;
        if (DeliveredBadge != Guid.Empty && ui.Preferences.ShakeScale > 0)
        {
            objects.Tween(DeliveredBadge, TweenClock.Ui).UiRect(new(-12, -31, 24, 10), 0)
                .UiRect(new(2, 3, -4, -3), .16f, TweenEase.EaseIn)
                .UiRect(new(-3, -2, 6, 4), .075f, TweenEase.EaseOut)
                .UiRect(RectangleF.Empty, .08f);
            tallyStart = session.Time.UiSeconds;
            tallyRunning = true;
        }
        AnimateTally(ui, tallyRunning ? 0 : 1.25);
        if (Stage == 4)
        {
            ui.SetVisible(FieldKit, false);
            for (int i = 0; i < SplitTime.Length; i++)
            {
                var leg = expedition.At(i);
                ArcadeUi.RightNumber(ui, SplitTime[i], leg is {} time ? FormattableString.Invariant($"{time.Seconds:0.0}s") : "-", 60, 7.5f);
                ArcadeUi.RightNumber(ui, SplitTokens[i], leg?.Tokens.ToString() ?? "-", 34, 7.5f);
                ArcadeUi.RightNumber(ui, SplitGems[i], leg?.Gems.ToString() ?? "-", 28, 7.5f);
            }
        }
        TrailProfile.ClearStage(session, Stage, tokens, gems, ticks, Stage == 4 && expedition.Complete ? tally.Ticks : null);
        session.Log(FormattableString.Invariant($"LedgeRun stage {Stage}: {tokens} tokens, {gems} gems, {ticks} ticks."));
    }

    private static void Pop(Guid pill, IGameObjects objects, IGameUi ui, ref ScriptTween previous)
    {
        if (pill == Guid.Empty || ui.Preferences.ShakeScale <= 0) return;
        previous.Stop();
        previous = objects.Tween(pill, TweenClock.Ui)
            .UiRect(new(0, -2, 0, 0), .07f, TweenEase.EaseOut)
            .UiRect(RectangleF.Empty, .16f);
    }

    private void AnimateTally(IGameUi ui, double elapsed)
    {
        int time = (int)Math.Round(tally.Seconds * 10);
        int timeNow = (int)(time * Math.Clamp((elapsed - .08) / .36, 0, 1));
        int tokensNow = (int)(tally.Tokens * Math.Clamp((elapsed - .42) / .34, 0, 1));
        int gemsNow = (int)(tally.Gems * Math.Clamp((elapsed - .75) / .30, 0, 1));
        if (timeNow != shownTime)
        {
            shownTime = timeNow;
            ArcadeUi.RightNumber(ui, ResultTime, FormattableString.Invariant($"{timeNow / 10.0:0.0}s"), 60, 7.5f);
        }
        if (tokensNow != shownTokens || gemsNow != shownGems)
        {
            (shownTokens, shownGems) = (tokensNow, gemsNow);
            ArcadeUi.RightNumber(ui, ResultHaul, tokensNow.ToString(), 34, 7.5f);
            ArcadeUi.RightNumber(ui, ResultGems, gemsNow.ToString(), 28, 7.5f);
        }
        if (elapsed >= 1.1) tallyRunning = false;
    }

    public void Damage(Guid runner, Vector2 at, IGameObjects objects, IGameUi ui, SessionState session, bool fell = false)
    {
        if (completed || destination != Guid.Empty || (recoveryTicks > 0 && !fell)) return;
        health--;
        objects.Audio.Play(Hurt);
        session.Screen.Shake(2.8f, .19);
        session.Screen.Flash(HitFlash, .16);
        Emit(objects, HitBurst, at + new Vector2(7, 10));
        if (fell || health <= 0)
        {
            if (health <= 0) health = 3;
            objects.SetLocalTransform(runner, new(checkpoint, 0, Vector2.One));
            objects.Motion.SetVelocity(runner, Vector2.Zero);
            objects.Motion.DriveCharacter(runner, 0);
            dashTicks = wallKickTicks = coyoteTicks = airTicks = 0;
            airDashSpent = false;
        }
        else
        {
            session.Time.Scale = .24;
            hitStopUntil = session.Time.UiSeconds + .047;
        }
        recoveryTicks = 50;
        SetPose(objects, Pose.Hurt);
        ArcadeUi.HudNumber(ui, HeartCount, health.ToString("D2"));
        Pop(HeartCount, objects, ui, ref heartPop);
    }

    public void EnemyStomp(Vector2 at, IGameObjects objects, SessionState session, bool final, bool warden)
    {
        stompPoseTicks = 10;
        SetPose(objects, Pose.Stomp);
        objects.Audio.Play(Stomp);
        Emit(objects, HitBurst, at);
        session.Screen.Shake(final ? 3.1f : 1.1f, final ? .24 : .12);
        if (warden)
        {
            session.Time.Scale = final ? .28 : .15;
            hitStopUntil = session.Time.UiSeconds + (final ? .30 : .10);
            session.Screen.Flash(final ? Color.FromArgb(145, 255, 226, 150) : Color.FromArgb(82, 255, 200, 121),
                final ? .26 : .19);
            Vector2 start = at + new Vector2(17, -17);
            objects.SetLocalTransform(StompNumber, new(start, 0, Vector2.One));
            objects.SetActive(StompNumber, true);
            objects.Tween(StompNumber).Move(start - new Vector2(0, 22), .5f);
            stompNumberTicks = 33;
        }
        else if (final) session.Screen.Flash(ClearFlash, .2);
        session.Rumble(.22f, .3f, .11);
    }

    public void BounceFromWarden(Guid runner, IGameObjects objects, int direction)
    {
        dashTicks = 0;
        wallKickDirection = direction;
        wallKickTicks = 8;
        objects.Motion.SetVelocity(runner, new(126 * direction, -232));
    }

    public void WardenImpact(IGameObjects objects) => objects.Audio.Play(Stomp);
    public void WardenSignal(IGameObjects objects) => objects.Audio.Play(Chime);

    public void Telegraph(IGameObjects objects, Vector2 at)
    {
        objects.Audio.Play(Warning);
        if (Stage == 4) Emit(objects, Spark, at);
    }

    private static void Emit(IGameObjects objects, Guid emitter, Vector2 position)
    {
        objects.SetLocalTransform(emitter, new(position, 0, Vector2.One));
        objects.EmitParticles(emitter);
    }
}
