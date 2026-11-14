using System.Diagnostics;

namespace Engine.Core;

// Device reads arrive in packets. Between reads, advance through the handed-off packet;
// during a stall never predict frames that have yet to leave the SDL queue.
internal sealed class AudioOutputClock
{
    private readonly record struct Packet(long Time, long First, long End);
    private readonly Packet[] packets = new Packet[512];
    private int next, count;
    private long consumed;

    public void Observe(long frames, long timestamp)
    {
        if (frames <= consumed) return;
        packets[next] = new(timestamp, consumed, frames);
        consumed = frames;
        next = (next + 1) % packets.Length;
        count = Math.Min(count + 1, packets.Length);
    }

    public bool TryFrame(long timestamp, out long frame)
    {
        for (int age = 1; age <= count; age++)
        {
            var packet = packets[(next + packets.Length - age) % packets.Length];
            if (timestamp < packet.Time) continue;
            double elapsed = Stopwatch.GetElapsedTime(packet.Time, timestamp).TotalSeconds;
            frame = packet.First + (long)Math.Min(packet.End - packet.First, elapsed * AudioMixer.SampleRate);
            return true;
        }
        frame = 0;
        return false;
    }
}

internal sealed class AudioPlayback(long start)
{
    internal long Start { get; } = start;
    internal long End { get; set; } = long.MaxValue;
}
