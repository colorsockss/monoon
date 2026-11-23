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
    public UiDefaults Accessibility { get=>field??=new();init; }
    public MixerLevels Mixer { get => field ??= new(); init; }
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
        if (Width is < 1 or > MaxDimension || Height is < 1 or > MaxDimension)
            throw new InvalidDataException($"Game resolution must be between 1 and {MaxDimension} pixels on each axis.");
    }
}
