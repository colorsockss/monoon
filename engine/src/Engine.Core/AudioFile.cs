namespace Engine.Core;

internal abstract record AudioFile(string Path, int Channels, int Rate, long Frames)
{
    internal static bool IsSupported(string path) =>
        path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase);

    internal static AudioFile Read(string path) =>
        path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? VorbisInfo.ReadFile(path) : WaveInfo.ReadFile(path);

    internal abstract IAudioStream Open();
}

internal interface IAudioStream : IDisposable
{
    (float L, float R) Sample(long frame);
}
