using System;
using System.Numerics;
using Engine.Core;

public sealed class BulletTurret : GameBehaviour
{
    public PrefabReference Bullet;
    public SceneReference Sprites;
    public Guid Counters, Mode;
    private const int Volley = 4;
    private const float Speed = 260;
    private bool pooled = true, firing = true;
    private int patternTick, fired, instantiated;

    public override void OnCreate(ScriptLife life) => Show(life.Ui, life.Objects);

    public override void FixedUpdate(ScriptTick tick)
    {
        var objects = tick.Objects;
        if (tick.Input.Pressed("Next"))
        {
            tick.Session.LoadScene(Sprites.Id);
            return;
        }
        if (tick.Input.Pressed("Mode") || tick.Input.Pressed("Restart"))
        {
            Report(tick.Session, objects);
            if (tick.Input.Pressed("Mode")) pooled = !pooled;
            Restart(objects);
        }
        if (tick.Input.Pressed("Fire")) firing = !firing;
        if (firing)
        {
            // A sweeping fan; the same ticks give the same shots in both modes.
            float sweep = MathF.Sin(patternTick * (MathF.Tau / 240)) * .45f;
            var center = tick.Position + new Vector2(9, 9);
            for (int i = 0; i < Volley; i++)
            {
                var (sin, cos) = MathF.SinCos(sweep + (i - (Volley - 1) * .5f) * .16f);
                var direction = new Vector2(cos, sin);
                var at = center + direction * 14 - new Vector2(3, 3);
                Guid id;
                if (pooled) id = objects.Rent(Bullet, at);
                else
                {
                    id = objects.Instantiate(Bullet, at);
                    instantiated++;
                }
                objects.GetComponent<PooledBullet>(id)!.Launch(direction * Speed);
                fired++;
            }
        }
        patternTick++;
        if (patternTick % 15 == 0 || tick.Input.Pressed("Fire")) Show(tick.Ui, objects);
    }

    private void Restart(IGameObjects objects)
    {
        foreach (var id in objects.FindByGroup("탄환"))
            if (!objects.Return(id)) objects.Destroy(id);
        foreach (var id in objects.FindByGroup("표적")) objects.GetComponent<HitTarget>(id)!.Restart();
        patternTick = fired = 0;
        firing = true;
    }

    private (int Hits, int Exits) Score(IGameObjects objects)
    {
        int hits = 0, exits = 0;
        foreach (var id in objects.FindByGroup("표적"))
            if (objects.GetComponent<HitTarget>(id) is {} target) (hits, exits) = (hits + target.Hits, exits + target.Exits);
        return (hits, exits);
    }

    private void Report(SessionState session, IGameObjects objects)
    {
        var (hits, exits) = Score(objects);
        var pool = objects.PoolStats(Bullet);
        session.Log($"BarrageLab bullets mode={(pooled ? "pool" : "instantiate")} ticks={patternTick} fired={fired} hits={hits} exits={exits} " +
            $"active={objects.FindByGroup("탄환").Count} created={pool.Created + instantiated} reused={pool.Reused} returned={pool.Returned}");
    }

    private void Show(IGameUi ui, IGameObjects objects)
    {
        var (hits, _) = Score(objects);
        var pool = objects.PoolStats(Bullet);
        ui.SetText(Counters, $"발사 {fired:N0}    명중 {hits:N0}    활성 {objects.FindByGroup("탄환").Count:N0}    생성 {pool.Created + instantiated:N0}    재사용 {pool.Reused:N0}");
        ui.SetText(Mode, (pooled ? "프리팹 풀 (Rent / Return)" : "생성 / 제거 (Instantiate / Destroy)") + (firing ? "   /   발사 중" : "   /   발사 중지"));
    }
}
