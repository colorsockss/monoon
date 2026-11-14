using System.Globalization;

namespace Engine.Core;

/// <summary>The clip's authored time axis. Intervals include their start and exclude their end.</summary>
public sealed class SpriteClipTimeline
{
    private readonly double[] starts;
    public int Count => starts.Length - 1;
    public double Duration => starts[^1];
    public double Start(int index) => starts[index]; // Count is the end marker.

    public SpriteClipTimeline(IReadOnlyList<SpriteFrame> frames)
    {
        starts = new double[frames.Count + 1];
        decimal sum = 0;
        bool decimalAxis = true;
        for (int i = 0; i < frames.Count; i++)
        {
            var seconds = frames[i].Seconds;
            if (!float.IsFinite(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(frames));
            // The YAML stores decimal seconds. Use the same decimal boundary a user scrubs to.
            var literal = seconds.ToString("R", CultureInfo.InvariantCulture);
            if (decimalAxis && decimal.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var part) &&
                part > 0 && decimal.MaxValue - sum >= part)
                starts[i + 1] = (double)(sum += part);
            else
            {
                decimalAxis = false;
                starts[i + 1] = starts[i] + double.Parse(literal, CultureInfo.InvariantCulture);
            }
        }
    }

    public double Position(double elapsed, bool loop)
    {
        if (Count == 0 || elapsed <= 0 || double.IsNaN(elapsed)) return 0;
        if (!double.IsFinite(elapsed)) return loop ? 0 : Duration;
        return loop ? elapsed % Duration : Math.Min(elapsed, Duration);
    }

    public int FrameAt(double elapsed, bool loop = false)
    {
        if (Count == 0) return -1;
        double at = Position(elapsed, loop);
        int lo = 0, hi = Count;
        while (lo + 1 < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (starts[mid] <= at) lo = mid; else hi = mid;
        }
        return lo;
    }
}
