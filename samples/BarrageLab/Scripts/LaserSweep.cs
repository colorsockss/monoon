using System;
using System.Globalization;
using System.Numerics;
using Engine.Core;

// A rotating laser that reflects off the arena walls (Line 2D points set every tick) and launches comets with trails.
public sealed class LaserSweep : GameBehaviour
{
    public Guid Laser, Counters, Mode;
    public PrefabReference Comet;
    public SceneReference Next;
    private const float Left = 20, Right = 780, Top = 60, Bottom = 372, Reach = 1400;
    private const int Launch = 24;
    // One turn in 600 ticks, starting at 0.35 rad. The aim is rotated by fixed factors rather than MathF.SinCos per tick.
    private static readonly Vector2 Start = new(.93937271f, .34289781f);
    private const float Cos = .99994517f, Sin = .010471784f;
    private Vector2 aim = Start;
    private int patternTick, launched, bounces;
    private bool sweeping = true;

    public override void OnCreate(ScriptLife life) => Show(life.Ui, life.Objects);

    public override void FixedUpdate(ScriptTick tick)
    {
        var objects = tick.Objects;
        if (tick.Input.Pressed("Next"))
        {
            tick.Session.LoadScene(Next.Id);
            return;
        }
        if (tick.Input.Pressed("Fire")) sweeping = !sweeping;
        if (tick.Input.Pressed("Restart"))
        {
            foreach (var id in objects.FindByGroup("혜성"))
                if (!objects.Return(id)) objects.Destroy(id);
            patternTick = launched = 0;
            aim = Start;
            sweeping = true;
        }
        var origin = tick.Position + new Vector2(8, 8);
        // Up to three reflections inside the arena, at most Reach long in total.
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
        bounces = count - 2;
        objects.SetLinePoints(Laser, beam[..count]);
        if (sweeping && patternTick % Launch == 0)
        {
            var id = objects.Rent(Comet, origin - new Vector2(4, 4));
            objects.GetComponent<Comet>(id)!.Launch(aim * 230, 150);
            launched++;
        }
        if (sweeping)
        {
            patternTick++;
            aim = Vector2.Normalize(new(aim.X * Cos - aim.Y * Sin, aim.X * Sin + aim.Y * Cos));
        }
        if (patternTick % 15 == 0 || tick.Input.Pressed("Fire")) Show(tick.Ui, objects);
    }

    private void Show(IGameUi ui, IGameObjects objects)
    {
        ui.SetText(Counters, string.Create(CultureInfo.InvariantCulture,
            $"각도 {(20.05f + patternTick * .6f) % 360:F0}°    반사 {bounces}    발사 {launched:N0}    비행 {objects.FindByGroup("혜성").Count:N0}"));
        ui.SetText(Mode, "선 · 궤적" + (sweeping ? "   /   회전 중" : "   /   정지"));
    }
}
