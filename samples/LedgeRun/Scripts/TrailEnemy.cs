using System;
using System.Numerics;
using Engine.Core;

public enum TrailEnemyKind { Patrol, Hopper, Sentry, Guardian }

public sealed class TrailEnemy : GameBehaviour
{
    public TrailEnemyKind Kind;
    public Guid Player, Projectile, Unlock;
    public float PatrolWidth = 64;
    public int Stomps = 1;

    private Vector2 home;
    private int age, grace, projectileTicks;
    private Vector2 shot, shotVelocity;

    public override void OnCreate(ScriptLife life) => life.Objects.TryGetPosition(life.ObjectId, out home);

    public override void FixedUpdate(ScriptTick tick)
    {
        if (!tick.Objects.TryGetPosition(Player, out var runner)) return;
        if (grace > 0) grace--;
        if (projectileTicks > 0)
        {
            shot += shotVelocity;
            Put(tick.Objects, Projectile, shot);
            if (--projectileTicks == 0) tick.Objects.SetActive(Projectile, false);
        }

        if (MathF.Abs(runner.X - home.X) > 208 || MathF.Abs(runner.Y - home.Y) > 104) return;
        age++;
        switch (Kind)
        {
            case TrailEnemyKind.Patrol:
                Put(tick.Objects, tick.ObjectId, home + new Vector2(PatrolWidth * Triangle(age, 180), 0));
                break;
            case TrailEnemyKind.Hopper:
                float chase = Math.Clamp((runner.X - tick.Position.X) * .12f, -1.05f, 1.05f);
                float x = Math.Clamp(tick.Position.X + chase, home.X - PatrolWidth, home.X + PatrolWidth);
                Put(tick.Objects, tick.ObjectId, new(x, home.Y - 30 * Triangle(age, 72)));
                break;
            case TrailEnemyKind.Sentry:
                if (age % 96 == 35) Fire(tick, runner, 2.2f);
                break;
            case TrailEnemyKind.Guardian:
                Guard(tick, runner);
                break;
        }
    }

    private void Guard(ScriptTick tick, Vector2 runner)
    {
        int phase = age % 390;
        if (phase < 130)
            Put(tick.Objects, tick.ObjectId, home + new Vector2(PatrolWidth * Triangle(phase, 130), 0));
        else if (phase < 260)
            Put(tick.Objects, tick.ObjectId, home + new Vector2(0, -23 * Triangle(phase - 130, 65)));
        else if (phase is 270 or 330)
            Fire(tick, runner, 2.65f);
        else
            Put(tick.Objects, tick.ObjectId, home);
    }

    private void Fire(ScriptTick tick, Vector2 runner, float speed)
    {
        if (Projectile == Guid.Empty) return;
        var delta = runner - tick.Position;
        shotVelocity = new(delta.X < 0 ? -speed : speed, Math.Clamp(delta.Y * .014f, -.95f, .95f));
        shot = tick.Position + new Vector2(delta.X < 0 ? -14 : 21, 4);
        projectileTicks = 56;
        Put(tick.Objects, Projectile, shot);
        tick.Objects.SetActive(Projectile, true);
    }

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId != Player ||
            contact.Objects.GetComponent<TrailRunner>(Player) is not { Completed: false } runner ||
            !contact.Objects.TryGetPosition(contact.ObjectId, out var enemy) ||
            !contact.Objects.TryGetPosition(Player, out var player) ||
            !contact.Objects.Motion.TryGetMotion(Player, out var motion)) return;

        if (motion.Velocity.Y > 25 && player.Y + 13 < enemy.Y && Kind != TrailEnemyKind.Sentry)
        {
            if (grace != 0) return;
            grace = 24;
            contact.Objects.Motion.SetVelocity(Player, new(motion.Velocity.X, -218));
            if (--Stomps > 0)
            {
                contact.Session.Rumble(.22f, .3f, .11);
                return;
            }
            if (Projectile != Guid.Empty) contact.Objects.Destroy(Projectile);
            if (Unlock != Guid.Empty) contact.Objects.SetActive(Unlock, true);
            contact.Objects.Destroy(contact.ObjectId);
            return;
        }
        runner.Damage(Player, contact.Objects, contact.Ui);
    }

    private static float Triangle(int tick, int period)
    {
        float phase = (tick % period) / (float)period;
        return 1 - MathF.Abs(2 * phase - 1);
    }

    private static void Put(IGameObjects objects, Guid id, Vector2 position) =>
        objects.SetLocalTransform(id, new(position, 0, Vector2.One));
}
