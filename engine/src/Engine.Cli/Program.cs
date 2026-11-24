using Engine.Core;
using Engine.Desktop;

const string usage = "monoon build <project> [release|native-aot] [--profile <development|distribution>] [--target <osx-arm64|win-x64|linux-x64>] [--output <directory>] [--content <incremental|full>] [--package <off|on>]";
const string updateUsage = "monoon content-update pack <exported Game directory> <replacement directory> <output.bundle>";
if (args.FirstOrDefault() == "crash")
{
    if (args is not ["crash", "explain", var report, var symbols]) return UsageError("monoon crash explain <report.crash> <build symbol directory>");
    try { CrashSymbols.Explain(report, symbols); return 0; }
    catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
}
if (args.FirstOrDefault() == "profile")
{
    const string profileUsage = "monoon profile <capture.csv> [--frame <number>]";
    long? pick = args switch { ["profile", _] => null, ["profile", _, "--frame", var n] when long.TryParse(n, out var v) => v, _ => -1 };
    if (pick == -1) return UsageError(profileUsage);
    try { FrameProfileReport.Print(args[1], pick); return 0; }
    catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException) { Console.Error.WriteLine(ex.Message); return 1; }
}
if (args.FirstOrDefault() == "migrate")
{
    if (args is not ["migrate", var folder]) return UsageError("monoon migrate <project>");
    try
    {
        LegacyProject.Upgrade(folder, paths => Console.WriteLine("Migration backup: " + MigrationBackup.Create(folder, paths)));
        Console.WriteLine($"YAML project: {Path.GetFullPath(folder)}");
        return 0;
    }
    catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
}
if (args.FirstOrDefault() == "test")
{
    if (args is not ["test", "project", var testProject]) return UsageError("monoon test project <path>");
    try { return ProjectTests.Run(testProject, Console.Out); }
    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { Console.Error.WriteLine(ex.Message); return 1; }
}
if (args.FirstOrDefault() == "content-update")
{
    if (args is not ["content-update", "pack", var game, var replacements, var bundle]) return UsageError(updateUsage);
    try
    {
        if (!File.Exists(Path.Combine(game, ".monoon-player"))) throw new InvalidDataException("Expected an exported Game directory.");
        var updates = new ContentUpdates(game, GameSettings.Load(game));
        Console.WriteLine(updates.Publish(replacements, bundle));
        return 0;
    }
    catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
}
if (args is ["--help"] or ["build", "--help"])
{
    Console.WriteLine(usage);
    return 0;
}
if (args is not ["build", var project, ..] || string.IsNullOrWhiteSpace(project) || project.StartsWith("--", StringComparison.Ordinal))
    return UsageError(usage);
try
{
    string root = Path.GetFullPath(project);
    if (!File.Exists(Path.Combine(root, GameSettings.FileName)) || File.Exists(Path.Combine(root, ".monoon-player")))
        throw new InvalidDataException($"Expected a source project containing {GameSettings.FileName} at {root}.");
    BuildSettings build;
    string? destination;
    try { build = BuildSettings.Override(BuildSettings.Load(root), args[2..], out destination); }
    catch (ArgumentException ex) { return UsageError(ex.Message); }
    string output = destination == null ? GameBuild.DefaultOutputDirectory(root) : Path.GetFullPath(destination);
    Console.Error.WriteLine($"Building {build.Type} ({build.BuildProfile}) for {build.Target} from {root}");
    var gameSettings = GameSettings.Load(root);
    var result = DesktopBuild.Export(root, gameSettings, build.Target, output, build.NativeAot, build.BuildProfile, build.FullContent);
    foreach (var line in result.ContentDecisions) Console.Error.WriteLine(line);
    Console.Error.WriteLine($"Content report: {result.ContentReport}");
    foreach (var line in result.AssetSummary) Console.Error.WriteLine(line);
    if (result.SymbolDirectory != null) Console.Error.WriteLine($"Debug symbols: {result.SymbolDirectory}");
    if (build.Package) DistributionPackage.Create(root, gameSettings, build.Target, result, SigningSettings.Load(), Console.Error.WriteLine);
    Console.WriteLine(result.Product);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static int UsageError(string message)
{
    Console.Error.WriteLine(message);
    return 2;
}
