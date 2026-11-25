using System;
using System.Numerics;
using Engine.Core;

// SpriteLab showcase: walks the hero round a loop of foot positions. Along the top it passes behind the well and along the
// bottom in front of it (both renderers sort by foot Y); on the pond bank a flipped, tinted, ripple-masked copy of the current
// frame is its reflection. A firefly circles the lantern with a Trail 2D. Raises the HUD flags in the order they show up.
public sealed class SpriteTour : GameBehaviour
{
    public Guid Hero, Shadow, Reflection, Firefly;
    public SpriteReference WalkN1, WalkN2, WalkN3, WalkN4, WalkE1, WalkE2, WalkE3, WalkE4, WalkS1, WalkS2, WalkS3, WalkS4, WalkW1, WalkW2, WalkW3, WalkW4;
    private static readonly Vector2[] Loop = [new(150, 205), new(420, 205), new(462, 326), new(728, 326), new(740, 290), new(150, 290)];
    private const float Speed = 85, Bank = 324;   // the straight top edge of the pond is at y 328
    private Vector2 foot = Loop[0];
    private int next = 1;
    private float time;
    private bool reflecting;   // the reflection starts inactive in the scene

    public override void FixedUpdate(ScriptTick tick)
    {
        time += tick.DeltaTime;
        var objects = tick.Objects;
        var session = tick.Session;
        if (time > .5f) session.Set("slice");
        if (time > 2.6f) session.Set("keys");
        if (time > 3.6f) session.Set("trail");
        if (time > 4.6f) session.Set("blink");

        var to = Loop[next] - foot;
        float step = Speed * tick.DeltaTime;
        if (to.Length() <= step) { foot = Loop[next]; next = (next + 1) % Loop.Length; }
        else foot += Vector2.Normalize(to) * step;
        if (foot.X > 280 && foot.X < 386 && foot.Y < 230) session.Set("ysort");

        var frame = Frame(to, (int)(time / .12f) % 4);
        objects.SetSprite(Hero, frame);
        objects.SetLocalTransform(Hero, new LocalTransform2D(Round(foot - new Vector2(21, 58)), 0, Vector2.One));
        objects.SetLocalTransform(Shadow, new LocalTransform2D(Round(foot - new Vector2(16, 5)), 0, Vector2.One));

        bool onBank = foot.Y >= Bank;
        if (onBank != reflecting) { objects.SetActive(Reflection, onBank); reflecting = onBank; }
        if (onBank)
        {
            session.Set("mask");
            objects.SetSprite(Reflection, frame);
            objects.SetLocalTransform(Reflection, new LocalTransform2D(Round(foot + new Vector2(-21, 2)), 0, Vector2.One));
        }

        var fly = new Vector2(650 + 62 * MathF.Sin(time * 1.3f), 132 + 30 * MathF.Sin(time * 2.1f));
        objects.SetLocalTransform(Firefly, new LocalTransform2D(fly, 0, Vector2.One));
    }

    private static Vector2 Round(Vector2 v) => new(MathF.Round(v.X), MathF.Round(v.Y));

    private SpriteReference Frame(Vector2 heading, int step) =>
        MathF.Abs(heading.X) >= MathF.Abs(heading.Y)
            ? heading.X >= 0 ? Pick(step, WalkE1, WalkE2, WalkE3, WalkE4) : Pick(step, WalkW1, WalkW2, WalkW3, WalkW4)
            : heading.Y >= 0 ? Pick(step, WalkS1, WalkS2, WalkS3, WalkS4) : Pick(step, WalkN1, WalkN2, WalkN3, WalkN4);

    private static SpriteReference Pick(int step, SpriteReference a, SpriteReference b, SpriteReference c, SpriteReference d) =>
        step switch { 0 => a, 1 => b, 2 => c, _ => d };
}
