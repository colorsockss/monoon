using System.Diagnostics;
using System.Globalization;
using Engine.Core;

/// <summary>Opt-in per-frame update time and managed allocations of a headless replay.</summary>
internal sealed class ReplayCapture : IDisposable
{
    private readonly StreamWriter output;
    private readonly GameSession game;
    private long time = Stopwatch.GetTimestamp(), bytes = GC.GetAllocatedBytesForCurrentThread();

    private ReplayCapture(string path, GameSession game)
    {
        this.game = game;
        output = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        output.WriteLine("frame,tick,update_us,update_bytes,gen0_collections");
    }

    public static ReplayCapture? Open(GameSession game) =>
        Environment.GetEnvironmentVariable("MONOON_REPLAY_CAPTURE") is { Length: > 0 } path ? new(path, game) : null;

    public void Frame(int frame)
    {
        long now = Stopwatch.GetTimestamp(), allocated = GC.GetAllocatedBytesForCurrentThread();
        double micros = (now - time) * 1e6 / Stopwatch.Frequency;
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{frame},{game.Time.TickCount},{micros:F1},{allocated - bytes},{GC.CollectionCount(0)}"));
        // Writing the row is excluded from the next frame.
        bytes = GC.GetAllocatedBytesForCurrentThread();
        time = Stopwatch.GetTimestamp();
    }

    public void Dispose() => output.Dispose();
}
