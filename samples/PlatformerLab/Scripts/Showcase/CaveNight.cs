using System;
using System.Numerics;
using Engine.Core;

// PlatformerLab lighting showcase: the hall starts lit, then the Global Light 2D object is switched on and the hall drops to
// its dark ambient, so only the torches, the hero's lantern and the unlit-layer crystals show. The hero walks back and forth
// so the lantern's shadows sweep across pillars, barrels and the sliding crate. Raises "shader", "dark", "shadow", "unlit".
public sealed class CaveNight : GameBehaviour
{
    public Guid Night, Hero;
    public SpriteReference WalkE1, WalkE2, WalkE3, WalkE4, WalkW1, WalkW2, WalkW3, WalkW4;
    private const float Left = 40, Right = 700, Speed = 75, NightAt = 3f;
    private float x = Left, time;
    private int direction = 1;
    private bool night;

    public override void FixedUpdate(ScriptTick tick)
    {
        time += tick.DeltaTime;
        var session = tick.Session;
        if (time > .6f) session.Set("shader");
        if (!night && time >= NightAt) { tick.Objects.SetActive(Night, true); night = true; session.Set("dark"); }
        if (time > NightAt + 1.6f) session.Set("shadow");
        if (time > NightAt + 4f) session.Set("unlit");

        x += direction * Speed * tick.DeltaTime;
        if (x > Right || x < Left) { direction = -direction; x = Math.Clamp(x, Left, Right); }
        tick.Objects.TryGetPosition(Hero, out var at);
        tick.Objects.SetLocalTransform(Hero, new LocalTransform2D(new Vector2(MathF.Round(x), at.Y), 0, Vector2.One));
        int step = (int)(time / .12f) % 4;
        tick.Objects.SetSprite(Hero, direction > 0 ? step switch { 0 => WalkE1, 1 => WalkE2, 2 => WalkE3, _ => WalkE4 }
                                                   : step switch { 0 => WalkW1, 1 => WalkW2, 2 => WalkW3, _ => WalkW4 });
    }
}
