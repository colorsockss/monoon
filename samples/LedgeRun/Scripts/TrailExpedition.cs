using System.Runtime.CompilerServices;
using Engine.Core;

internal sealed class TrailExpedition
{
    internal readonly record struct Leg(long Ticks, int Tokens, int Gems)
    {
        internal double Seconds => Ticks * GameTime.FixedStep;
    }

    // Scene changes keep the session. Returning to Trailhead or disposing the session ends the expedition.
    private static readonly ConditionalWeakTable<SessionState, TrailExpedition> Runs = new();
    private readonly Leg?[] legs = new Leg?[4];

    internal static void Begin(SessionState session) => Runs.Remove(session);

    internal static TrailExpedition Record(SessionState session, int stage, long ticks, int tokens, int gems)
    {
        var expedition = Runs.GetValue(session, static _ => new());
        expedition.legs[stage - 1] = new(ticks, tokens, gems);
        return expedition;
    }

    internal Leg? At(int index) => legs[index];
    internal bool Complete => legs[0].HasValue && legs[1].HasValue && legs[2].HasValue && legs[3].HasValue;

    internal Leg Total
    {
        get
        {
            long ticks = 0;
            int tokens = 0, gems = 0;
            foreach (var leg in legs)
            {
                if (leg is not { } clear) continue;
                ticks += clear.Ticks;
                tokens += clear.Tokens;
                gems += clear.Gems;
            }
            return new(ticks, tokens, gems);
        }
    }
}
