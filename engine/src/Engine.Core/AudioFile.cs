using Brutal.SdlApi;
using Brutal.SdlApi.Mixer;

namespace Engine.Core;

// Validated stream format. SDL_mixer probes the header without reading the audio payload.
internal sealed record AudioFile(string Path, int Channels, int Rate, long Frames)
{
    private static readonly Lazy<bool> Library = new(() => { SdlMixer.Init(); return true; });
    internal static void Init() => _ = Library.Value;

    internal static bool IsSupported(string path) =>
        path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase);

    internal static AudioFile Read(string path) => Read(path, System.IO.Path.GetExtension(path));

    // The import stage names files without their extension, so the caller passes the expected one.
    internal static unsafe AudioFile Read(string path, string extension)
    {
        Init();
        bool ogg = extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase);
        var decoder = SdlMixer.PInvoke.CreateAudioDecoder(path, 0);
        if (decoder.IsNull()) throw new InvalidDataException($"{path}: {(ogg ? "invalid Ogg Vorbis audio" : "use mono or stereo PCM or float32 WAV")}. {Sdl.PInvoke.GetError()}");
        try
        {
            SdlAudioSpec spec;
            if (SdlMixer.PInvoke.GetAudioDecoderFormat(decoder, &spec) == 0) throw new InvalidDataException($"{path}: {Sdl.PInvoke.GetError()}");
            uint props = SdlMixer.GetAudioDecoderProperties(decoder);
            string name = Sdl.PInvoke.GetStringProperty(props, SdlMixer.Constants.PropAudioDecoderString, "") ?? "";
            long frames = Sdl.PInvoke.GetNumberProperty(props, SdlMixer.Constants.PropMetadataDurationFramesNumber, -1);
            if (ogg ? !name.Contains("VORBIS", StringComparison.Ordinal) : name != "WAV")
                throw new InvalidDataException($"{path}: expected {(ogg ? "Ogg Vorbis" : "WAV")} audio, found {name}.");
            if (spec.channels is not (1 or 2) || spec.freq is < 1000 or > 384000 || frames <= 0)
                throw new InvalidDataException($"{path}: expected mono or stereo audio at 1–384 kHz with a known length.");
            return new(path, spec.channels, spec.freq, frames);
        }
        finally { SdlMixer.DestroyAudioDecoder(decoder); }
    }
}
