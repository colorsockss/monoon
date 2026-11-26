using System;
using System.Numerics;
using Engine.Core;

public sealed class TrailRunner : GameBehaviour
{
    public Guid Status, Totals, Portrait, Chime, Hurt, Clear;
    public SpriteReference Idle, Running, Airborne;
    public SceneReference NextStage, TrailheadScene;
    public int Stage;
    public float FallY = 256;

    private const float Speed = 104, JumpSpeed = 290;
    private readonly PhysicsFilter terrain = new(1);
    private Vector2 checkpoint;
    private int health = 3, tokens, gems, dashTicks, wallKickTicks, recoveryTicks;
    private float facing = 1, dashDirection, wallKickDirection;
    private long startedAt;
    private bool completed, airDashSpent, wasGrounded;
    private string status = "";
    private SpriteReference pose;
    public int Tokens => tokens;
    public int Gems => gems;
    public int Health => health;
    public bool Completed => completed;

    public override void OnCreate(ScriptLife life)
    {
        life.Objects.TryGetPosition(life.ObjectId, out checkpoint);
        startedAt = life.Session.Time.TickCount;
        pose = Idle;
        if (!HasCollectibles) SetStatus(life.Ui, "Land three stomps to raise the summit flag.");
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var motion = tick.Objects.Motion;
        if (completed)
        {
            motion.DriveCharacter(tick.ObjectId, 0);
            if (tick.Input.Pressed("Proceed")) tick.Session.LoadScene(NextStage.Id == Guid.Empty ? TrailheadScene.Id : NextStage.Id);
            return;
        }
        if (tick.Position.Y > FallY)
        {
            Damage(tick.ObjectId, tick.Objects, tick.Ui, true);
            return;
        }
        if (recoveryTicks > 0) recoveryTicks--;
        if (!motion.TryGetMotion(tick.ObjectId, out var body)) return;
        if (body.Grounded && (!wasGrounded || dashTicks == 0)) airDashSpent = false;
        wasGrounded = body.Grounded;

        float axis = tick.Direction.X;
        if (axis != 0 && dashTicks == 0 && wallKickTicks == 0 && MathF.Sign(axis) != facing)
        {
            facing = MathF.Sign(axis);
            if (tick.Objects.TryGetLocalTransform(Portrait, out var local))
                tick.Objects.SetLocalTransform(Portrait, local with { Scale = new(facing, 1) });
        }

        bool wantsJump = tick.Input.Triggered("Jump");
        bool wallContact = !body.Grounded && axis != 0 && (body.Velocity.Y >= 0 || wantsJump) && TouchesWall(tick, axis);
        if (tick.Input.Pressed("Dash") && dashTicks == 0 && !airDashSpent)
        {
            airDashSpent = true;
            dashDirection = facing;
            dashTicks = 8;
            wallKickTicks = 0;
        }
        if (dashTicks > 0)
        {
            dashTicks--;
            motion.SetVelocity(tick.ObjectId, new(260 * dashDirection, 0));
            motion.DriveCharacter(tick.ObjectId, 260 * dashDirection);
        }
        else if (wallContact && wantsJump && tick.Input.Consume("Jump"))
        {
            wallKickDirection = -MathF.Sign(axis);
            wallKickTicks = 9;
            motion.SetVelocity(tick.ObjectId, new(126 * wallKickDirection, -235));
            motion.DriveCharacter(tick.ObjectId, 126 * wallKickDirection);
        }
        else if (wallKickTicks > 0)
        {
            wallKickTicks--;
            motion.DriveCharacter(tick.ObjectId, 126 * wallKickDirection);
        }
        else
        {
            float jump = body.Grounded && tick.Input.Consume("Jump") ? JumpSpeed : 0;
            if (wallContact && body.Velocity.Y > 56)
                motion.SetVelocity(tick.ObjectId, new(body.Velocity.X, 56));
            else if (tick.Input.Released("Jump") && body.Velocity.Y < -105)
                motion.SetVelocity(tick.ObjectId, new(body.Velocity.X, -105));
            motion.DriveCharacter(tick.ObjectId, axis * Speed, jump);
        }

        SpriteReference next = !body.Grounded ? Airborne : axis != 0 ? Running : Idle;
        if (pose != next && tick.Objects.SetSprite(Portrait, next)) pose = next;
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
        if (tick.Input.Pressed("BackToTrailhead"))
        {
            tick.Session.Time.IsPaused = false;
            tick.Session.LoadScene(TrailheadScene.Id);
            return;
        }
        if (!tick.Input.Pressed("Pause")) return;
        var time = tick.Session.Time;
        time.IsPaused = !time.IsPaused;
        tick.Ui.SetText(Status, time.IsPaused ? "Paused  /  P or controller Back to resume" : status);
    }

    private bool HasCollectibles => Stage < 4;
    private string FinishPrompt => NextStage.Id == Guid.Empty ? "Trail complete  /  Enter for trailhead" : "Stage clear  /  Enter to continue";

    private void SetStatus(IGameUi ui, string value)
    {
        status = value;
        ui.SetText(Status, status);
    }

    public void Collect(bool gem, IGameObjects objects, IGameUi ui)
    {
        if (gem) gems++;
        else tokens++;
        objects.Audio.Play(Chime);
        UpdateTotals(ui);
    }

    public void SaveCheckpoint(Vector2 at, IGameUi ui)
    {
        checkpoint = at;
        SetStatus(ui, "Checkpoint reached");
    }

    public void Finish(SessionState session, IGameObjects objects, IGameUi ui)
    {
        if (completed) return;
        completed = true;
        objects.Audio.Play(Clear);
        SetStatus(ui, FinishPrompt);
        long ticks = session.Time.TickCount - startedAt;
        TrailProfile.ClearStage(session, Stage, tokens, gems, ticks);
        session.Log(FormattableString.Invariant($"LedgeRun stage {Stage}: {tokens} tokens, {gems} gems, {ticks} ticks."));
    }

    public void Damage(Guid runner, IGameObjects objects, IGameUi ui, bool fell = false)
    {
        if (completed || (recoveryTicks > 0 && !fell)) return;
        health--;
        objects.Audio.Play(Hurt);
        if (fell || health <= 0)
        {
            if (health <= 0) health = 3;
            objects.SetLocalTransform(runner, new(checkpoint, 0, Vector2.One));
            objects.Motion.SetVelocity(runner, Vector2.Zero);
            objects.Motion.DriveCharacter(runner, 0);
            dashTicks = wallKickTicks = 0;
            airDashSpent = false;
        }
        recoveryTicks = 50;
        UpdateTotals(ui);
    }

    private void UpdateTotals(IGameUi ui) => ui.SetText(Totals, HasCollectibles
        ? $"HEARTS  {health}       TOKENS  {tokens:D2}       GEMS  {gems}/3"
        : $"HEARTS  {health}");
}
