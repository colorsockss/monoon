using System.Globalization;
using System.Numerics;
using Engine.Core;

// Every 90 ticks from tick 20, 100 damage numbers rise over the training dummy (Text 2D, roadmap 16.6).
// The strings are made once; a burst only rents pooled labels and points them at a string.
public sealed class DamageBurst : GameBehaviour
{
    public PrefabReference Number;
    private readonly string[] values = new string[100];
    private int frame;

    public override void OnCreate(ScriptLife life)
    {
        for (int i = 0; i < values.Length; i++) values[i] = (i * 37 % 240 + 12).ToString(CultureInfo.InvariantCulture);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (frame++ % 90 != 20) return;
        for (int i = 0; i < values.Length; i++)
        {
            var id = tick.Objects.Rent(Number, new Vector2(450 + i % 10 * 28, 110 + i / 10 * 18));
            tick.Objects.SetWorldText(id, values[i]);
        }
    }
}
