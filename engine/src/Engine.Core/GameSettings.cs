using VYaml.Annotations;
using System.Drawing;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Engine.Core;

[YamlObject]
public sealed partial record GameSettings(int Width = 800, int Height = 450)
{
    public Guid? ProjectId { get; init; }
    public string? GameTitle { get; init; }
    public Guid? UiTheme { get; init; }
    public Guid? StartScene { get; init; }
    public Guid[] IncludedScenes { get => field ??= []; set; }
    public string[]? ContentBundles { get; init; }
    // PNG asset for distributable packages (Linux desktop entry); never needed by the player.
    public Guid? Icon { get; init; }
    public LocalizationSettings? Localization { get; init; }
    // Builder only: Text, Audio or Physics keep that feature's native library even when nothing shipped uses it (mods,
    // content updates). Strings, not EngineFeature: an enum array would add VYaml formatters to every AOT player.
    public string[]? Features { get; init; }
    // Pixel art: whole device pixels per game pixel, nearest sprite sampling and cameras on whole game pixels. Null is off,
    // so existing settings files stay byte-identical when saved.
    public bool? IntegerScale { get; init; }
    public UiDefaults Accessibility { get=>field??=new();init; }
    public MixerLevels Mixer { get => field ??= new(); init; }
    // Null when unused, so settings files without them stay byte-identical when saved.
    public AudioSnapshot[]? AudioSnapshots { get; init; }
    public AudioDucking[]? AudioDucking { get; init; }
    public InputBindings Input { get => field ??= new(); set; }
    public const int MaxDimension = 16384;
    public const string FileName = "GameSettings";

    [JsonIgnore, YamlIgnore] public Size Resolution => new(Width, Height);

    public RectangleF CameraFrame(Vector2 center, float zoom)
    {
        var size = new SizeF(Width / zoom, Height / zoom);
        return new RectangleF(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height);
    }

    public static GameSettings Load(string project)
    {
        var path = Path.Combine(project, FileName);
        if (!File.Exists(path)) return new();
        var settings = ProjectYaml.Read<GameSettings>(path);
        settings.Validate();
        return settings with { Input = settings.Input.AsMap(), ProjectId = settings.ProjectId ?? settings.StartScene };
    }

    internal byte[] SerializedSettings() => ProjectYaml.Encode(this with { Input = Input.AsMap() });

    public void Save(string project)
    {
        Validate();
        var path = Path.Combine(project, FileName);
        ProjectYaml.Write(path, this with { Input = Input.AsMap() });
    }

    internal void Validate()
    {
        Localization?.Validate();
        ContentBundle.ValidateFolders(ContentBundles ?? []);
        Input.Validate();
        if(string.IsNullOrWhiteSpace(Accessibility.Locale) || Accessibility.Locale.Length>64 || Accessibility.TextScale is not (>=.75f and <=2)) throw new InvalidDataException("Invalid default UI locale or text scale.");
        if(!Mixer.Valid) throw new InvalidDataException("Mixer gains must be between 0 and 1.");
        var snapshots = AudioSnapshots ?? [];
        for (int i = 0; i < snapshots.Length; i++)
            if (snapshots[i] is not { Name.Length: > 0 and <= 64 } s || string.IsNullOrWhiteSpace(s.Name) || s.Levels?.Valid == false ||
                Array.FindIndex(snapshots, 0, i, o => o?.Name == s.Name) >= 0)
                throw new InvalidDataException("Audio snapshots need unique names (1–64 characters) and gains between 0 and 1.");
        foreach (var d in AudioDucking ?? [])
            if (d is null || !d.Valid)
                throw new InvalidDataException("Audio ducking needs two different buses, −80–0 dB and attack/release of 0–10 seconds.");
        if (Width is < 1 or > MaxDimension || Height is < 1 or > MaxDimension)
            throw new InvalidDataException($"Game resolution must be between 1 and {MaxDimension} pixels on each axis.");
    }
}
