namespace Engine.Core;

/// <summary>Version 1 deterministic random stream. Given the same seed and calls, the integer
/// sequence is identical on every backend. Each instance belongs to one consumer.</summary>
public sealed class SeededRandom
{
    private readonly ulong seed;
    private ulong state;

    public SeededRandom(ulong seed) => this.seed = state = seed;

    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    public ulong NextUInt64() => Mix(unchecked(state += 0x9E3779B97F4A7C15UL));
    public int NextInt(int exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);
        uint bound = (uint)exclusiveMax, threshold = unchecked(0u - bound) % bound;
        uint value;
        do { value = (uint)(NextUInt64() >> 32); } while (value < threshold);
        return (int)(value % bound);
    }

    // Derive from the original seed, never the current draw count. Adding loot rolls need
    // not change the generation of terrain. Use stable numeric channel IDs, not GetHashCode().
    public SeededRandom Fork(ulong channel) => new(Mix(seed ^ Mix(unchecked(channel + 0x9E3779B97F4A7C15UL))));
}
