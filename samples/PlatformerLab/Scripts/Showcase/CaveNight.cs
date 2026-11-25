using System;
using System.Numerics;
using Engine.Core;

// PlatformerLab lighting showcase: the ruins start in plain dusk. At NightAt the Global Light 2D object and the hero's lantern
// switch on, so the world drops to its dark ambient and only the lantern and the unlit-layer moon show. Walking east the hero
// lights each torch as he reaches it (sparks, the shader flame and its shadow-casting Point Light 2D), so the shadows of the
// pillars, the beam and the crates build up one light at a time; at the far end he turns and walks back through them.
// Raises "dark", "shader", "shadow", "unlit".
public sealed class CaveNight : GameBehaviour
{
    public Guid Night, Hero, Lantern;
    public Guid[] Flames = [], Lights = [], Sparks = [];
    public SpriteReference WalkE1, WalkE2, WalkE3, WalkE4, WalkW1, WalkW2, WalkW3, WalkW4;
    private const float Left = 20, Right = 470, Speed = 55, NightAt = 1.0f, HeroHalf = 21;
    private float x = Left, time;
    private int direction = 1, lit;
    private bool night;

    public override void FixedUpdate(ScriptTick tick)
    {
        time += tick.DeltaTime;
        var objects = tick.Objects;
        var session = tick.Session;
        if (!night && time >= NightAt)
        {
            night = true;
            objects.SetActive(Night, true);
            objects.SetActive(Lantern, true);
            session.Set("dark");
        }
        // Light the next torch when the hero's middle reaches it.
        if (night && lit < Flames.Length && objects.TryGetPosition(Flames[lit], out var flame) && x + HeroHalf >= flame.X + 12)
        {
            objects.SetActive(Flames[lit], true);
            objects.SetActive(Lights[lit], true);
            objects.EmitParticles(Sparks[lit]);
            session.Set("shader");
            if (lit == 1) session.Set("shadow");
            if (++lit == Flames.Length) session.Set("unlit");
        }

        x += direction * Speed * tick.DeltaTime;
        if (x > Right || x < Left) { direction = -direction; x = Math.Clamp(x, Left, Right); }
        objects.TryGetPosition(Hero, out var at);
        objects.SetLocalTransform(Hero, new LocalTransform2D(new Vector2(MathF.Round(x), at.Y), 0, Vector2.One));
        int step = (int)(time / .12f) % 4;
        objects.SetSprite(Hero, direction > 0 ? step switch { 0 => WalkE1, 1 => WalkE2, 2 => WalkE3, _ => WalkE4 }
                                              : step switch { 0 => WalkW1, 1 => WalkW2, 2 => WalkW3, _ => WalkW4 });
    }
}
