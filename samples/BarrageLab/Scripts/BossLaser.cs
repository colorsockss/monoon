using System;
using System.Numerics;
using Engine.Core;

// Showcase: the battleship's second attack. Space (Fire) switches it on as the spiral (which also reads Fire) stops: a
// laser that turns and reflects off the screen edges (Line 2D points set every tick, as LaserSweep does) and launches
// pooled comets with Trail 2D. Space again switches back; while it is off the beam keeps its angle. R returns every comet and
// starts the sweep over. Raises the "laser" HUD tag flag.
public sealed class BossLaser : GameBehaviour
{
    public Guid Laser;
    public PrefabReference Comet;
    public float Left = 0, Right = 800, Top = 0, Bottom = 388, Reach = 1400;
    public int Launch = 18;
    // One turn in 600 ticks, by fixed rotation factors (no per-tick SinCos, as in LaserSweep).
    private const float Cos = .99994517f, Sin = .010471784f;
    private static readonly Vector2 Start = Vector2.Normalize(new(-1, .35f));
    private Vector2 aim = Start;
    private bool sweeping;
    private int tickCount;

    public override void OnCreate(ScriptLife life) => life.Objects.SetActive(Laser, false);

    public override void FixedUpdate(ScriptTick tick)
    {
        var objects = tick.Objects;
        if (tick.Input.Pressed("Restart"))
        {
            foreach (var id in objects.FindByGroup("혜성"))
                if (!objects.Return(id)) objects.Destroy(id);
            (aim, tickCount) = (Start, 0);
        }
        if (tick.Input.Pressed("Fire"))
        {
            sweeping = !sweeping;
            objects.SetActive(Laser, sweeping);
            if (sweeping) tick.Session.Set("laser");
        }
        if (!sweeping) return;
        var origin = tick.Position + new Vector2(4, 4);
        Span<Vector2> beam = stackalloc Vector2[5];
        int count = 0;
        var point = origin;
        var direction = aim;
        float left = Reach;
        beam[count++] = point;
        while (count < beam.Length && left > 0)
        {
            float toX = direction.X > 0 ? (Right - point.X) / direction.X : direction.X < 0 ? (Left - point.X) / direction.X : float.MaxValue;
            float toY = direction.Y > 0 ? (Bottom - point.Y) / direction.Y : direction.Y < 0 ? (Top - point.Y) / direction.Y : float.MaxValue;
            float step = MathF.Min(left, MathF.Min(toX, toY));
            point += direction * step;
            left -= step;
            beam[count++] = point;
            if (step == toX) direction.X = -direction.X;
            else if (step == toY) direction.Y = -direction.Y;
        }
        objects.SetLinePoints(Laser, beam[..count]);
        if (tickCount++ % Launch == 0)
        {
            var id = objects.Rent(Comet, origin - new Vector2(4, 4));
            objects.GetComponent<Comet>(id)!.Launch(aim * 260, 150);
        }
        aim = Vector2.Normalize(new(aim.X * Cos - aim.Y * Sin, aim.X * Sin + aim.Y * Cos));
    }
}
