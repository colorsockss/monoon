using System.Runtime.InteropServices;
using VYaml.Annotations;

namespace Engine.Core;

// The Build Game dialog's per-build choices, shared with `monoon build`. Build-time only: the file has no extension and is
// not GameSettings, so the asset scanner, game builds and project packages never pick it up and the player never reads it.
// Values use the CLI's words so the file and the command line read the same.
[YamlObject]
public sealed partial record BuildSettings
{
    public const string FileName = "BuildSettings";
    public static readonly string[] Targets = ["osx-arm64", "win-x64", "linux-x64"];
    public static string Host => RuntimeInformation.RuntimeIdentifier;

    public string Target { get => field ??= Host; init; }
    public string Type { get => field ??= "release"; init; }
    public string Profile { get => field ??= "distribution"; init; }
    public string Content { get => field ??= "incremental"; init; }
    public bool Package { get; init; }

    [YamlIgnore] public bool NativeAot => Type == "native-aot";
    [YamlIgnore] public BuildProfile BuildProfile => Profile == "development" ? BuildProfile.Development : BuildProfile.Distribution;
    [YamlIgnore] public bool FullContent => Content == "full";

    // Native AOT compiles with this machine's toolchain, so it builds only for the host platform.
    public static bool CanBuildNativeAot(string target) => target == Host;

    public static BuildSettings Load(string project)
    {
        var path = Path.Combine(project, FileName);
        if (!File.Exists(path)) return new();
        var settings = ProjectYaml.Read<BuildSettings>(path);
        try { settings.Validate(); }
        catch (InvalidDataException ex) { throw new InvalidDataException($"{path}: {ex.Message}", ex); }
        return settings;
    }

    public void Save(string project)
    {
        Validate();
        ProjectYaml.Write(Path.Combine(project, FileName), this);
    }

    public void Validate()
    {
        if (!Targets.Contains(Target)) throw new InvalidDataException($"Unsupported target '{Target}'.");
        if (Type is not ("release" or "native-aot")) throw new InvalidDataException($"Unknown build type '{Type}'. Use release or native-aot.");
        if (Profile is not ("distribution" or "development")) throw new InvalidDataException($"Unknown build profile '{Profile}'. Use development or distribution.");
        if (Content is not ("incremental" or "full")) throw new InvalidDataException($"Unknown content mode '{Content}'. Use incremental or full.");
        if (Package && BuildProfile != BuildProfile.Distribution) throw new InvalidDataException("Packages require the distribution profile.");
    }

    // `monoon build <project>` arguments after the project: an optional build type, then option pairs. Each one replaces the
    // saved value for this run only. Throws ArgumentException for usage errors.
    public static BuildSettings Override(BuildSettings saved, IReadOnlyList<string> args, out string? output)
    {
        output = null;
        int start = args.Count > 0 && args[0] is "release" or "native-aot" ? 1 : 0;
        if ((args.Count - start) % 2 != 0) throw new ArgumentException("Expected option pairs after the build type.");
        var result = start == 1 ? saved with { Type = args[0] } : saved;
        var seen = new HashSet<string>();
        for (int i = start; i < args.Count; i += 2)
        {
            string option = args[i], value = args[i + 1];
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Missing value for {option}.");
            if (!seen.Add(option)) throw new ArgumentException($"Unknown or repeated option '{option}'.");
            result = option switch
            {
                "--profile" when value is "development" or "distribution" => result with { Profile = value },
                "--profile" => throw new ArgumentException($"Unknown build profile '{value}'. Use development or distribution."),
                "--content" when value is "full" or "incremental" => result with { Content = value },
                "--package" when value is "on" or "off" => result with { Package = value == "on" },
                "--target" => result with { Target = value },
                "--output" => result,
                _ => throw new ArgumentException($"Unknown or repeated option '{option}'.")
            };
            if (option == "--output") output = value;
        }
        // Like the dialog, a development build turns packaging off unless --package on asks for it (which then fails below).
        if (seen.Contains("--profile") && !seen.Contains("--package") && result.BuildProfile == BuildProfile.Development)
            result = result with { Package = false };
        if (seen.Contains("--package") && result.Package && result.BuildProfile != BuildProfile.Distribution)
            throw new ArgumentException("--package on requires the distribution profile.");
        if (!Targets.Contains(result.Target)) throw new ArgumentException($"Unsupported target '{result.Target}'.");
        return result;
    }
}
