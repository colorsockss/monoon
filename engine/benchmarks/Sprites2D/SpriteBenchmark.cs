using System.Diagnostics;
using System.Globalization;

internal sealed class SpriteBenchmark
{
    public const int Width = 800, Height = 450, SpriteSize = 16;
    private const int WarmupFrames = 120, MeasuredFrames = 600;
    private static readonly int[] Counts = [100, 1_000, 10_000];
    private readonly long[] drawTicks = new long[MeasuredFrames], totalTicks = new long[MeasuredFrames];
    private readonly long[] intervalTicks = new long[MeasuredFrames];
    private int workload, frame;
    private long start, submitted, previousPresent;

    public int Count => Counts[workload];
    public bool FirstWarmup => frame == 0;
    public bool Finished => workload == Counts.Length;

    // Prepare the same native rectangles and tints for both renderers before timing.
    public static T[] CreateSprites<T>(Func<int, int, byte, byte, byte, T> create)
    {
        var random = new Random(2026);
        var sprites = new T[Counts[^1]];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i] = create(random.Next(Width - SpriteSize + 1), random.Next(Height - SpriteSize + 1),
                (byte)random.Next(32, 256), (byte)random.Next(32, 256), (byte)random.Next(32, 256));
        return sprites;
    }

    public static void ReportConfiguration(string backend, int width, int height)
    {
        if (width != Width || height != Height)
            throw new InvalidOperationException($"Unexpected back buffer {width}x{height}, expected {Width}x{Height}.");
        Console.WriteLine($"{backend}, {width}x{height}, warmup={WarmupFrames}, measured={MeasuredFrames}");
        Console.WriteLine("sprites,draw_p50_ms,draw_p95_ms,draw_present_p50_ms,draw_present_p95_ms,present_interval_p50_ms,present_interval_p95_ms");
    }

    public void BeginDraw() => start = Stopwatch.GetTimestamp();
    public void EndDraw() => submitted = Stopwatch.GetTimestamp();

    public void EndPresent()
    {
        long end = Stopwatch.GetTimestamp();
        if (frame >= WarmupFrames)
        {
            int index = frame - WarmupFrames;
            drawTicks[index] = submitted - start;
            totalTicks[index] = end - start;
            intervalTicks[index] = end - previousPresent;
        }
        previousPresent = end;
        if (++frame < WarmupFrames + MeasuredFrames) return;

        Array.Sort(drawTicks);
        Array.Sort(totalTicks);
        Array.Sort(intervalTicks);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{Count},{Milliseconds(drawTicks, 50):F4},{Milliseconds(drawTicks, 95):F4},{Milliseconds(totalTicks, 50):F4},{Milliseconds(totalTicks, 95):F4},{Milliseconds(intervalTicks, 50):F4},{Milliseconds(intervalTicks, 95):F4}"));
        workload++;
        frame = 0;
    }

    private static double Milliseconds(long[] sortedTicks, int percentile) =>
        sortedTicks[(int)Math.Ceiling(sortedTicks.Length * percentile / 100.0) - 1] * 1000.0 / Stopwatch.Frequency;
}
