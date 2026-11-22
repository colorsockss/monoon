using VYaml.Annotations;
using System.ComponentModel;
using System.Drawing;

namespace Engine.Core;

[DisplayName("Sprite Animator")]
[YamlObject]
public sealed partial record SpriteAnimatorComponent : SceneComponent
{
    public Guid? Controller { get; set; }
    public Guid? Clip { get; set; }
    public bool PlayOnStart { get; set; }
}

[DisplayName("Audio Source")]
[YamlObject]
public sealed partial record AudioSourceComponent : SceneComponent
{
    public AudioSourceComponent() { }
    [YamlConstructor] public AudioSourceComponent(float volume=1,float range=400) => (Volume,Range)=(volume,range);
    public Guid? Sound { get; set; }
    public AudioBus Bus { get; set; }
    public float Volume { get; set; }=1;
    public bool Loop { get; set; }
    public bool PlayOnStart { get; set; }
    public float FadeIn { get; set; }
    public float FadeOut { get; set; }
    public bool Spatial { get; set; }
    public float Range { get; set; }=400;
    internal bool Extended => Bus!=AudioBus.Effects || Volume!=1 || Loop || PlayOnStart || FadeIn!=0 || FadeOut!=0 || Spatial || Range!=400;
    internal void Validate(string name)
    {
        if (Bus is not (AudioBus.Music or AudioBus.Effects) || !MixerLevels.Unit(Volume) ||
            FadeIn is not (>=0 and <=3600) || FadeOut is not (>=0 and <=3600) || !float.IsFinite(Range) || Range<=0)
            throw new InvalidDataException($"{name}: invalid audio bus, gain, fade or spatial range.");
    }
}

[DisplayName("Audio Listener 2D"), YamlObject]
public sealed partial record AudioListener2DComponent : SceneComponent;

[DisplayName("HUD")]
[YamlObject]
public sealed partial record HudComponent : SceneComponent
{
    public HudComponent() { }
    [YamlConstructor] public HudComponent(string text = "", string requiredFlag = "") => (Text, RequiredFlag) = (text, requiredFlag);
    public string Text { get; set; } = "";
    public Guid? Image { get; set; }
    public string RequiredFlag { get; set; } = "";
}

[YamlObject]
[method: YamlConstructor]
public sealed partial record SpriteFrame(Guid Sprite, float Seconds, SpriteRegion? Source = null, SpriteCanvas? Canvas = null, string? Event = null, bool UseRenderer = false)
{
    public SpriteFrame(Guid sprite,float seconds,SpriteRegion? source,SpriteCanvas? canvas,string? marker)
        : this(sprite,seconds,source,canvas,marker,false) { }
}
[YamlObject]
[method: YamlConstructor]
public sealed partial record SpriteClip(SpriteFrame[] Frames, bool Loop, ComponentTrack[]? Tracks = null)
{
    public SpriteClip(SpriteFrame[] frames,bool loop) : this(frames,loop,null) { }

    // Authoring can repair empty clips, missing sprites and invalid durations.
    public static SpriteClip OpenForEditing(string file)
    {
        var clip = ProjectYaml.Read<SpriteClip>(file);
        if (clip.Frames == null || clip.Frames.Any(f => f == null)) throw new InvalidDataException($"Missing frame data in {file}.");
        return clip;
    }

    public static SpriteClip Load(string file)
    {
        var clip = ProjectYaml.Read<SpriteClip>(file);
        clip.Validate(file);
        return clip;
    }

    internal SceneAppearance[] ResolveFrames(string file, string root, ProjectAssets assets, Dictionary<string, Size> dimensions)
    {
        var paths = new SceneAppearance[Frames.Length];
        for (int i = 0; i < Frames.Length; i++)
        {
            var frame = Frames[i];
            if (frame.UseRenderer) continue;
            var relative = assets.PathFor(frame.Sprite);
            var label = $"{file}, frame {i + 1}";
            var packed = frame.Source == null && frame.Canvas == null ? assets.Sprites(root).Find(frame.Sprite) : null;
            if (packed != null)
            {
                paths[i] = new(Path.Combine(root,packed.Sheet),Color.White,packed.Entry.Source,packed.Entry.Canvas);
                continue;
            }
            if (relative == null || !SpriteAssets.IsPng(relative) || !File.Exists(Path.Combine(root, relative)))
                throw new InvalidDataException($"{label}: missing PNG {relative ?? frame.Sprite.ToString()}.");
            var filePath = Path.Combine(root,relative);
            paths[i] = new(filePath, Color.White, frame.Source, frame.Canvas);
            if (frame.Canvas is {} canvas && !canvas.Fits(frame.Source))
                throw new InvalidDataException($"{label}: trimmed sprite does not fit its frame canvas.");
            if (frame.Source is not {} source) continue;
            if (!dimensions.TryGetValue(filePath, out var size))
            {
                try { size = SpriteRegion.ReadPngSize(filePath); }
                catch (Exception ex) when (ex is IOException or InvalidDataException)
                { throw new InvalidDataException($"{label}: cannot read {relative}.", ex); }
                dimensions.Add(filePath, size);
            }
            if (!source.Fits(size.Width, size.Height))
                throw new InvalidDataException($"{label}: source ({source.X}, {source.Y}, {source.Width}, {source.Height}) is outside {relative} ({size.Width} × {size.Height}).");
        }
        return paths;
    }

    internal void Validate(string file)
    {
        if (Frames is not { Length: > 0 } || Frames.Any(f => f == null || (f.UseRenderer ? f.Sprite != Guid.Empty || f.Source != null || f.Canvas != null : f.Sprite == Guid.Empty) || !float.IsFinite(f.Seconds) || f.Seconds <= 0 || f.Event != null && !AnimationController.ValidName(f.Event)))
            throw new InvalidDataException($"Invalid animation frames in {file}.");
        if (Tracks is { Length: > 0 }) _ = new ComponentCurves(Tracks, new SpriteClipTimeline(Frames).Duration);
    }
}

public static class PixelText
{
    // Five vertical columns per glyph, least significant bit at the top.
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-.:!?/ ";
    private static readonly string[] Glyphs = ("7E0909097E 7F49494936 3E41414122 7F4141221C 7F49494941 7F09090901 " +
        "3E41495132 7F0808087F 00417F4100 2040413F01 7F08142241 7F40404040 7F020C027F 7F0408107F 3E4141413E " +
        "7F09090906 3E4151215E 7F09192946 2649494932 01017F0101 3F4040403F 1F2040201F 7F2018207F 6314081463 " +
        "0304780403 6151494543 3E5149453E 00427F4000 4261514946 2141454B31 1814127F10 2745454539 3C4A494930 " +
        "0171090503 3649494936 064949291E 0808080808 0060600000 0036360000 00005F0000 0201510906 2010080402 0000000000").Split(' ');
    private static readonly byte[][] Columns = Glyphs.Select(Convert.FromHexString).ToArray();

    public static void Draw(ICanvas2D canvas, string text, float x, float y, float pixel, Color color)
    {
        foreach (char ch in text.ToUpperInvariant())
        {
            int index = Alphabet.IndexOf(ch);
            var glyph = Columns[index >= 0 ? index : Alphabet.IndexOf('?')];
            for (int col = 0; col < glyph.Length; col++)
                for (int row = 0; row < 7; row++)
                    if ((glyph[col] & (1 << row)) != 0) canvas.FillRectangle(new(x + col * pixel, y + row * pixel, pixel, pixel), color);
            x += 6 * pixel;
        }
    }
}
