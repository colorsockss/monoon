using System.Runtime.InteropServices;
using Engine.Core;
using Engine.Desktop;

const string usage = "monoon build <project> <release|native-aot> [--profile <development|distribution>] [--target <osx-arm64|win-x64|linux-x64>] [--output <directory>] [--content <incremental|full>]";
const string updateUsage = "monoon content-update pack <exported Game directory> <replacement directory> <output.bundle>";
if (args.FirstOrDefault() == "crash")
{
    if (args is not ["crash", "explain", var report, var symbols]) return UsageError("monoon crash explain <report.crash> <build symbol directory>");
    try { CrashSymbols.Explain(report, symbols); return 0; }
    catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
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
if (args is not ["build", var project, "release" or "native-aot", ..] || string.IsNullOrWhiteSpace(project) || (args.Length - 3) % 2 != 0)
    return UsageError(usage);

string? target = null, destination = null, content = null;
BuildProfile? profile = null;
for (int i = 3; i < args.Length; i += 2)
{
    string value = args[i + 1];
    if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
        return UsageError($"Missing value for {args[i]}.");
    switch (args[i])
    {
        case "--profile" when profile == null:
            if (value == "development") profile = BuildProfile.Development;
            else if (value == "distribution") profile = BuildProfile.Distribution;
            else return UsageError($"Unknown build profile '{value}'. Use development or distribution.");
            break;
        case "--content" when content == null && value is "full" or "incremental": content = value; break;
        case "--target" when target == null: target = value; break;
        case "--output" when destination == null: destination = value; break;
        default: return UsageError($"Unknown or repeated option '{args[i]}'.");
    }
}
target ??= RuntimeInformation.RuntimeIdentifier;
if (target is not ("osx-arm64" or "win-x64" or "linux-x64")) return UsageError($"Unsupported target '{target}'.");
try
{
    string root = Path.GetFullPath(project);
    if (!File.Exists(Path.Combine(root, GameSettings.FileName)) || File.Exists(Path.Combine(root, ".monoon-player")))
        throw new InvalidDataException($"Expected a source project containing {GameSettings.FileName} at {root}.");
    string output = destination == null ? GameBuild.DefaultOutputDirectory(root) : Path.GetFullPath(destination);
    var selected = profile ?? BuildProfile.Distribution;
    Console.Error.WriteLine($"Building {args[2]} ({selected}) for {target} from {root}");
    var result = DesktopBuild.Export(root, GameSettings.Load(root), target, output, args[2] == "native-aot", selected, fullContent: content == "full");
    foreach (var line in result.ContentDecisions) Console.Error.WriteLine(line);
    Console.Error.WriteLine($"Content report: {result.ContentReport}");
    if (result.SymbolDirectory != null) Console.Error.WriteLine($"Debug symbols: {result.SymbolDirectory}");
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
