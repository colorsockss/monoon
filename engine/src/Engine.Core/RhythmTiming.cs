namespace Engine.Core;

public enum BeatGrade { Early, OnTime, Late, Outside }

public readonly record struct BeatHit(long Beat, long OffsetFrames, BeatGrade Grade)
{
    public double OffsetMilliseconds => OffsetFrames * (1000d / AudioMixer.SampleRate);
}

/// <summary>A constant-tempo beat grid in 48 kHz output frames. Beat one starts at FirstBeatFrame.</summary>
public sealed class RhythmTiming
{
    private readonly decimal period;
    private readonly long exact, window;
    public long FirstBeatFrame { get; }

    public RhythmTiming(decimal bpm, int exactMilliseconds, int windowMilliseconds, long firstBeatFrame = 0)
    {
        if (bpm is <= 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(bpm));
        if (firstBeatFrame < 0) throw new ArgumentOutOfRangeException(nameof(firstBeatFrame));
        if (exactMilliseconds < 0 || windowMilliseconds < exactMilliseconds || windowMilliseconds * bpm >= 30000)
            throw new ArgumentOutOfRangeException(nameof(windowMilliseconds), "Hit windows must fit inside half a beat.");
        period = AudioMixer.SampleRate * 60m / bpm;
        FirstBeatFrame = firstBeatFrame;
        exact = (long)exactMilliseconds * AudioMixer.SampleRate / 1000;
        window = (long)windowMilliseconds * AudioMixer.SampleRate / 1000;
    }

    public BeatHit Judge(long songFrame)
    {
        if (songFrame < 0) throw new ArgumentOutOfRangeException(nameof(songFrame));
        decimal index = Math.Max(0, decimal.Floor((songFrame - FirstBeatFrame) / period + .5m));
        long offset = (long)(songFrame - FirstBeatFrame - decimal.Round(index * period, 0, MidpointRounding.AwayFromZero));
        var grade = offset < -window || offset > window ? BeatGrade.Outside :
            offset < -exact ? BeatGrade.Early : offset > exact ? BeatGrade.Late : BeatGrade.OnTime;
        return new((long)index + 1, offset, grade);
    }
}
