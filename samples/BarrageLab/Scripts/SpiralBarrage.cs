using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

public sealed class SpiralBarrage : GameBehaviour
{
    public Guid Counters, Mode;
    public SceneReference Bullets;
    private const int Capacity = 4096, Arms = 24;
    private static readonly Color Mint = Color.FromArgb(126, 237, 208);
    private static readonly Color Coral = Color.FromArgb(254, 156, 127);
    private SpritePool2D pool;
    private int patternTick;
    private bool firing = true;

    public override void OnCreate(ScriptLife life)
    {
        pool = life.Objects.CreateSpritePool(life.ObjectId, Capacity);
        Show(life.Ui);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("Next"))
        {
            tick.Session.LoadScene(Bullets.Id);
            return;
        }
        if (tick.Input.Pressed("Compare"))
        {
            pool.Batched = !pool.Batched;
            Show(tick.Ui);
        }
        if (tick.Input.Pressed("Restart"))
        {
            pool.Reset();
            patternTick = 0;
            firing = true;
            Show(tick.Ui);
        }
        if (tick.Input.Pressed("Fire"))
        {
            firing = !firing;
            Show(tick.Ui);
        }
        if (tick.Cameras.ActiveCameraId is not Guid camera || !tick.Cameras.TryGetView(camera, out var shot)) return;
        pool.Advance(tick.DeltaTime, shot.World);
        if (firing)
        {
            // Two counter-rotating fans with different speeds share one tinted sprite.
            float phase = (patternTick % 720) * (MathF.Tau / 720);
            for (int i = 0; i < Arms; i++)
            {
                bool clockwise = (i & 1) == 0;
                float angle = i * (MathF.Tau / Arms) + (clockwise ? 3 : -2) * phase;
                // Separate Sin/Cos: MathF.SinCos gives different last bits under JIT and Native AOT.
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                pool.TrySpawn(tick.Position + direction * 13, direction * (clockwise ? 152 : 192), clockwise ? Mint : Coral);
            }
        }
        patternTick = (patternTick + 1) % 720;
        if (patternTick % 15 == 0) Show(tick.Ui);
    }

    private void Show(IGameUi ui)
    {
        var stats = pool.Stats;
        ui.SetText(Counters, $"활성 {stats.Active:N0}    회수 {stats.Returned:N0}    재사용 {stats.Reused:N0}");
        ui.SetText(Mode, (pool.Batched ? "묶음 제출" : "개별 제출") + (firing ? "   /   발사 중" : "   /   발사 중지"));
    }
}
