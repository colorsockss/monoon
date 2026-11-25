using Engine.Core;

// Showcase: in the particle workshop (one 800-unit screen per station), raises each station's HUD tag flag as the hero walks
// into it: "shapes", "sorting", "presets", "curves", "weather".
public sealed class WorkshopTags : GameBehaviour
{
    public float Station = 800, Into = 200;
    private static readonly string[] Flags = ["shapes", "sorting", "presets", "curves", "weather"];

    public override void FixedUpdate(ScriptTick tick)
    {
        for (int i = 0; i < Flags.Length; i++)
            if (tick.Position.X > i * Station + Into) tick.Session.Set(Flags[i]);
    }
}
