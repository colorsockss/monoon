using System.Diagnostics;
using System.Globalization;
using Engine.Core;

namespace Engine.Desktop;

// The trace is opt-in. Only milestones write to disk, including one bounded frame window.
public sealed class PlayerStartupTrace : IDisposable
{
    public const int FrameWindow = 120;
    private readonly long start;
    private readonly StreamWriter output;
    private long previous;
    private double longest;
    private int frames;

    private PlayerStartupTrace(string path, long start)
    {
        this.start = start;
        output = new(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        output.WriteLine("event,elapsed_ms,value,archive_bytes,asset_count,inflated_bytes,read_ms");
        Mark("entry");
    }

    public static PlayerStartupTrace? Open(long start) =>
        Environment.GetEnvironmentVariable("MONOON_STARTUP_TRACE") is { Length: > 0 } file ? new(file, start) : null;

    public void Mark(string name, string value = "") => output.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{name},{Stopwatch.GetElapsedTime(start).TotalMilliseconds:F3},{value},,,,"));

    public void BundleRead(BundleReadSample sample)
    {
        string folder = sample.Folder.Replace("\"", "\"\"");
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"bundle,{Stopwatch.GetElapsedTime(start).TotalMilliseconds:F3},\"{folder}\",{sample.ArchiveBytes},{sample.AssetCount},{sample.InflatedBytes},{sample.ReadTime.TotalMilliseconds:F3}"));
    }

    public void Mounted(ContentBundle content)
    {
        Mark("content_ready", content.CacheStatus);
        // Size of the player's verified mount, in file bytes. Additional file-system overhead
        // and transient writes observed from outside the process are measured by the Mac probe.
        long bytes = Directory.EnumerateFiles(content.Root, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);
        Mark(content.IsTemporary ? "temp_mount_bytes" : content.CacheStatus == "loose" ? "loose_bytes" : "cache_mount_bytes",
            bytes.ToString(CultureInfo.InvariantCulture));
    }

    internal void BeginFrames() => previous = Stopwatch.GetTimestamp();
    internal void Presented()
    {
        if (frames >= FrameWindow) return;
        long now = Stopwatch.GetTimestamp();
        longest = Math.Max(longest, Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds);
        previous = now;
        if (++frames == 1) Mark("first_present");
        if (frames == FrameWindow) WriteFrameWindow();
    }

    private void WriteFrameWindow()
    {
        Mark("frames", frames.ToString(CultureInfo.InvariantCulture));
        Mark("max_frame_ms", longest.ToString("F3", CultureInfo.InvariantCulture));
    }

    public void Dispose()
    {
        if (frames is > 0 and < FrameWindow) WriteFrameWindow();
        output.Dispose();
    }
}
