using System.Buffers.Binary;
using NVorbis;

namespace Engine.Core;

internal sealed record VorbisInfo(string Path, int Channels, int Rate, long Frames) : AudioFile(Path, Channels, Rate, Frames)
{
    internal override IAudioStream Open() => new VorbisStream(this);

    internal static VorbisInfo ReadFile(string path)
    {
        using var file = File.OpenRead(path);
        try
        {
            long frames = CountFrames(file);
            file.Position = 0;
            using var reader = new VorbisReader(file, false);
            if (reader.Channels is not (1 or 2) || reader.SampleRate is < 1000 or > 384000)
                throw new InvalidDataException("Expected mono or stereo Vorbis at 1–384 kHz.");
            return new(path, reader.Channels, reader.SampleRate, frames);
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            throw new InvalidDataException($"{path}: invalid Ogg Vorbis audio. {ex.Message}", ex);
        }
    }

    // The final granule is the exact PCM frame count after encoder padding. Inspect page
    // headers without reading the audio payload, retaining an index or decoding the track.
    private static long CountFrames(FileStream file)
    {
        Span<byte> header = stackalloc byte[27];
        Span<byte> segments = stackalloc byte[255];
        int serial = 0;
        uint nextPage = 0;
        while (file.Position < file.Length)
        {
            file.ReadExactly(header);
            if (!header[..4].SequenceEqual("OggS"u8) || header[4] != 0)
                throw new InvalidDataException("Invalid Ogg page header.");
            int pageSerial = BinaryPrimitives.ReadInt32LittleEndian(header[14..]);
            if (nextPage == 0) serial = pageSerial;
            if (pageSerial != serial || BinaryPrimitives.ReadUInt32LittleEndian(header[18..]) != nextPage ||
                ((header[5] & 2) != 0) != (nextPage == 0))
                throw new InvalidDataException("Expected consecutive pages of one Ogg logical stream.");
            nextPage++;
            var sizes = segments[..header[26]];
            file.ReadExactly(sizes);
            int length = 0;
            foreach (byte size in sizes) length += size;
            if (length > file.Length - file.Position) throw new InvalidDataException("Truncated Ogg page.");
            file.Position += length;
            if ((header[5] & 4) == 0) continue;
            long frames = BinaryPrimitives.ReadInt64LittleEndian(header[6..]);
            if (file.Position != file.Length || frames <= 0)
                throw new InvalidDataException("Expected a single complete Ogg stream with a positive final granule.");
            return frames;
        }
        throw new InvalidDataException("Missing end of Ogg stream.");
    }
}

internal sealed class VorbisStream(VorbisInfo info) : IAudioStream
{
    private const int PageFrames = 1024;
    private readonly VorbisReader reader = new(info.Path);
    private float[] current = new float[PageFrames * info.Channels], previous = new float[PageFrames * info.Channels];
    private long currentStart = -PageFrames, previousStart = -PageFrames, nextFrame;

    public (float L, float R) Sample(long frame)
    {
        long start = frame / PageFrames * PageFrames;
        if (start != currentStart && start != previousStart) Load(start);
        var page = start == currentStart ? current : previous;
        int i = (int)(frame - start) * info.Channels;
        return (page[i], info.Channels == 1 ? page[i] : page[i + 1]);
    }

    private void Load(long start)
    {
        // The interpolator can straddle two pages, including the last and first page.
        // Keep both until it has advanced past the seam.
        if (nextFrame != start) reader.SamplePosition = start;
        int frames = (int)Math.Min(PageFrames, info.Frames - start);
        int wanted = frames * info.Channels;
        int read = 0;
        while (read < wanted)
        {
            int n = reader.ReadSamples(previous, read, wanted - read);
            if (n <= 0) throw new InvalidDataException($"{info.Path}: Vorbis stream ended before frame {start + frames}.");
            read += n;
        }
        (current, previous) = (previous, current);
        previousStart = currentStart;
        currentStart = start;
        nextFrame = start + frames;
    }

    public void Dispose() => reader.Dispose();
}
