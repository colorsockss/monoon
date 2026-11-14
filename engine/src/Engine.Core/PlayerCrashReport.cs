using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record CrashFrame(string Method, string? Assembly = null, string? ModuleId = null,
    int? MethodToken = null, int? IlOffset = null, string? ImageOffset = null);

[YamlObject]
public sealed partial record CrashCause(string Type, string Message, CrashFrame[] Frames);

[YamlObject]
public sealed partial record PlayerCrashReport(int Version, string Utc, string OS, string Architecture, string Run,
    Guid? Scene, GameData Build, CrashCause[] Causes)
{
    public static PlayerCrashReport Load(string file)
    {
        var report = ProjectYaml.Read<PlayerCrashReport>(file);
        if (report.Version != 1 || report.Build?.Values == null || report.Causes is not { Length: > 0 }
            || report.Causes.Any(c => c?.Frames == null || c.Frames.Any(f => f == null)))
            throw new InvalidDataException($"Invalid player crash report: {file}");
        return report;
    }

    public string Save(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.crash");
        ProjectYaml.WriteBytes(path, ProjectYaml.Encode(this), overwrite: false);
        return path;
    }
}
