using System;
using System.Numerics;
using Engine.Core;

// PlatformerLab lighting showcase: the ruins start in plain dusk with no lights, then the Global Light 2D object and every
// Point Light 2D (torches and the hero's lantern) are switched on together, so the world drops to its dark ambient and only
// the lights, the shader flames and the unlit-layer moon show. The hero walks back and forth so the lantern's shadows sweep
// across the pillars, the beam and the crates. Raises "shader", "dark", "shadow", "unlit".
public sealed class CaveNight : GameBehaviour
{
    public Guid Night, Hero;
    public Guid[] Lights = [];
    public SpriteReference WalkE1, WalkE2, WalkE3, WalkE4, WalkW1, WalkW2, WalkW3, WalkW4;
    private const float Left = 20, Right = 470, Speed = 60, NightAt = 2.2f;
    private float x = Left, time;
    private int direction = 1;
    private bool night;

    public override void FixedUpdate(ScriptTick tick)
    {
        time += tick.DeltaTime;
        var session = tick.Session;
        if (time > .6f) session.Set("shader");
        if (!night && time >= NightAt)
        {
            night = true;
            tick.Objects.SetActive(Night, true);
            foreach (var light in Lights) tick.Objects.SetActive(light, true);
            session.Set("dark");
        }
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
