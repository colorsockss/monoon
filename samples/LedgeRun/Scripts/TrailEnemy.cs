using System;
using System.Numerics;
using Engine.Core;

public enum TrailEnemyKind { Patrol, Hopper, Sentry }

public sealed class TrailEnemy : GameBehaviour
{
    public TrailEnemyKind Kind;
    public Guid Player, Projectile, Art;
    public float PatrolWidth = 64;
    public int Stomps = 1;

    private const int RecoveryPose = 2;
    private Vector2 home, shot, shotVelocity;
    private int age, grace, projectileTicks, defeatTicks, pose;
    private float facing = 1;

    public override void OnCreate(ScriptLife life) => life.Objects.TryGetPosition(life.ObjectId, out home);

    public override void FixedUpdate(ScriptTick tick)
    {
        if (defeatTicks > 0)
        {
            if (--defeatTicks == 0)
                tick.Objects.Destroy(tick.ObjectId);
            return;
        }
        if (!tick.Objects.TryGetPosition(Player, out var runner)) return;
        if (grace > 0) grace--;
        if (projectileTicks > 0)
        {
            shot += shotVelocity;
            tick.Objects.SetLocalTransform(Projectile, new(shot, 0, Vector2.One));
            if (--projectileTicks == 0) tick.Objects.SetActive(Projectile, false);
        }

        if (MathF.Abs(runner.X - home.X) > 208 || MathF.Abs(runner.Y - home.Y) > 104) return;
        age++;
        switch (Kind)
        {
            case TrailEnemyKind.Patrol:
                Move(tick, home + new Vector2(PatrolWidth * Triangle(age, 180), 0));
                break;
            case TrailEnemyKind.Hopper:
                float chase = Math.Clamp((runner.X - tick.Position.X) * .12f, -1.05f, 1.05f);
                float x = Math.Clamp(tick.Position.X + chase, home.X - PatrolWidth, home.X + PatrolWidth);
                Move(tick, new(x, home.Y - 30 * Triangle(age, 72)));
                break;
            case TrailEnemyKind.Sentry:
                Face(tick.Objects, MathF.Sign(runner.X - tick.Position.X));
                Charge(tick, runner, age % 96, 18, 35, 2.2f);
                return;
        }
        SetPose(tick.Objects, grace > 0 ? RecoveryPose : 0);
    }

    private void Charge(ScriptTick tick, Vector2 runner, int phase, int warnAt, int fireAt, float speed)
    {
        if (phase == warnAt) tick.Objects.GetComponent<TrailRunner>(Player)?.Telegraph(tick.Objects, tick.Position + new Vector2(10, 4));
        bool warning = phase >= warnAt && phase < fireAt;
        SetPose(tick.Objects, grace > 0 ? RecoveryPose : warning ? 1 : 0);
        if (phase == fireAt) Fire(tick, runner, speed);
    }

    private void Fire(ScriptTick tick, Vector2 runner, float speed)
    {
        if (Projectile == Guid.Empty) return;
        var delta = runner - (tick.Position + new Vector2(10, 4));
        shotVelocity = new(delta.X < 0 ? -speed : speed, Math.Clamp(delta.Y * .014f, -.95f, .95f));
        shot = tick.Position + new Vector2(delta.X < 0 ? -14 : 21, 4);
        projectileTicks = 56;
        tick.Objects.SetLocalTransform(Projectile, new(shot, 0, Vector2.One));
        tick.Objects.SetActive(Projectile, true);
    }

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (defeatTicks != 0 || contact.OtherId != Player ||
            contact.Objects.GetComponent<TrailRunner>(Player) is not { Completed: false } runner ||
            !contact.Objects.TryGetPosition(contact.ObjectId, out var enemy) ||
            !contact.Objects.TryGetPosition(Player, out var player) ||
            !contact.Objects.Motion.TryGetMotion(Player, out var motion)) return;

        if (motion.Velocity.Y > 25 && player.Y + 13 < enemy.Y && Kind != TrailEnemyKind.Sentry)
        {
            if (grace != 0) return;
            grace = 24;
            contact.Objects.Motion.SetVelocity(Player, new(motion.Velocity.X, -218));
            bool final = --Stomps == 0;
            runner.EnemyStomp(enemy + new Vector2(10, 6), contact.Objects, contact.Session, final, warden: false);
            if (!final) return;
            defeatTicks = 13;
            SetPose(contact.Objects, RecoveryPose);
            contact.Objects.SetSpriteTint(Art, "#D28476");
            if (Projectile != Guid.Empty) contact.Objects.Destroy(Projectile);
            return;
        }
        runner.Damage(Player, player, contact.Objects, contact.Ui, contact.Session);
    }

    private void SetPose(IGameObjects objects, int next)
    {
        if (pose == next) return;
        pose = next;
        objects.Animator(Art)?.SetInt("Pose", next);
    }

    private void Move(ScriptTick tick, Vector2 at)
    {
        float delta = at.X - tick.Position.X;
        tick.Objects.SetLocalTransform(tick.ObjectId, new(at, 0, Vector2.One));
        if (MathF.Abs(delta) > .05f) Face(tick.Objects, MathF.Sign(delta));
    }

    private void Face(IGameObjects objects, float direction)
    {
        if (direction == 0 || direction == facing) return;
        facing = direction;
        if (objects.TryGetLocalTransform(Art, out var art))
            objects.SetLocalTransform(Art, art with { Scale = new(facing, 1) });
    }

    private static float Triangle(int tick, int period)
    {
        float phase = (tick % period) / (float)period;
        return 1 - MathF.Abs(2 * phase - 1);
    }
}
