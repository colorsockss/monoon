using System.Diagnostics;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Engine.Core;

internal static class CrashSymbols
{
    public static void Explain(string reportPath, string directory)
    {
        var report = PlayerCrashReport.Load(reportPath);
        var receipt = GameData.Load(Path.Combine(directory, BuildSymbols.Receipt));
        foreach (string key in new[] { "buildId", "game", "projectId", "mode", "target", "profile", "playerSha256" })
        {
            if (string.IsNullOrWhiteSpace(receipt.GetText(key)) || receipt.GetText(key) != report.Build.GetText(key))
                throw new InvalidDataException($"Report and symbol archive do not match ({key}).");
        }
        foreach (var identity in report.Build.Values.Keys.Where(k => k == "nativeUuid" ||
            k.StartsWith("pdbId/", StringComparison.Ordinal) || k.StartsWith("mvid/", StringComparison.Ordinal)))
        {
            if (receipt.GetText(identity) != report.Build.GetText(identity))
                throw new InvalidDataException($"Report and symbol archive do not match ({identity}).");
        }
        var frames = report.Causes.SelectMany(c => c.Frames).Distinct().ToArray();
        var locations = receipt.GetText("mode") switch
        {
            "release" => Managed(receipt, directory, frames),
            "native-aot" when receipt.GetText("target") == "osx-arm64" => MacNative(receipt, directory, frames),
            _ => throw new NotSupportedException("Automatic Native AOT symbolication currently supports osx-arm64 dSYM archives.")
        };
        Console.WriteLine($"{receipt.GetText("game")} {receipt.GetText("mode")} {receipt.GetText("target")}  build {receipt.GetText("buildId")}");
        Console.WriteLine($"{report.Utc}  {report.OS} {report.Architecture}  {report.Run}  scene {report.Scene}");
        foreach (var cause in report.Causes)
        {
            Console.WriteLine($"{cause.Type}: {cause.Message}");
            foreach (var frame in cause.Frames)
                Console.WriteLine(locations.TryGetValue(frame, out var location) ? $"  {location}" : $"  {frame.Method} [no source location]");
        }
        if (locations.Count == 0) throw new InvalidDataException("The archived symbols resolved no source locations for this report.");
    }

    private static Dictionary<CrashFrame, string> Managed(GameData receipt, string directory, CrashFrame[] frames)
    {
        var locations = new Dictionary<CrashFrame, string>();
        // The receipt names each archived assembly: the scripts sit next to the player (per-game publish) or in Game/.monoon-compiled.
        foreach (var assembly in receipt.Values.Keys.Where(k => k.StartsWith("pdbId/", StringComparison.Ordinal)).Select(k => k["pdbId/".Length..]))
        {
            string name = Path.GetFileNameWithoutExtension(assembly);
            var matches = frames.Where(f => f.Assembly == name).ToArray();
            if (matches.Length == 0) continue;
            using var stream = File.OpenRead(Path.ChangeExtension(Path.Combine(directory, assembly), ".pdb"));
            using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
            var reader = provider.GetMetadataReader();
            var pdb = new BlobContentId(reader.DebugMetadataHeader!.Id);
            if (!Guid.TryParse(receipt.GetText("pdbId/" + assembly), out var id) || pdb.Guid != id)
                throw new InvalidDataException($"Portable PDB does not match the build: {assembly}");
            foreach (var frame in matches)
            {
                if (frame.ModuleId != receipt.GetText("mvid/" + assembly))
                    throw new InvalidDataException($"Frame module does not match the build: {assembly}");
                if (frame.MethodToken is not int token || frame.IlOffset is not >= 0 || (token >> 24) != 6) continue;
                int row = token & 0xffffff;
                if (row == 0 || row > reader.MethodDebugInformation.Count) continue;
                var debug = reader.GetMethodDebugInformation(MetadataTokens.MethodDebugInformationHandle(row));
                SequencePoint? found = null;
                foreach (var point in debug.GetSequencePoints())
                {
                    if (point.Offset > frame.IlOffset) break;
                    found = point;
                }
                if (found is not { IsHidden: false } line || line.Document.IsNil) continue;
                string source = reader.GetString(reader.GetDocument(line.Document).Name).Replace('\\', '/');
                // Roslyn in the builder names "Id.cs" or "./path"; the per-game publish maps its snapshot to "/_/path".
                if (name == "MoNoon.GameScripts") source = source.StartsWith("/_/", StringComparison.Ordinal) ? source[3..]
                    : source.StartsWith("./", StringComparison.Ordinal) ? source[2..] : "Scripts/" + source;
                locations[frame] = $"{frame.Method}  {source}:{line.StartLine}  (IL 0x{frame.IlOffset:x})";
            }
        }
        return locations;
    }

    private static Dictionary<CrashFrame, string> MacNative(GameData receipt, string directory, CrashFrame[] frames)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("Run macOS dSYM symbolication on a Mac with atos.");
        string dwarf = Path.Combine(directory, "MoNoonPlayer.dSYM", "Contents", "Resources", "DWARF", "MoNoonPlayer");
        if (!Guid.TryParse(receipt.GetText("nativeUuid"), out var uuid) || BuildSymbols.MacUuid(dwarf) != uuid)
            throw new InvalidDataException("The dSYM UUID does not match this build.");
        var native = frames.Where(f => f.ImageOffset != null).ToArray();
        var locations = new Dictionary<CrashFrame, string>();
        if (native.Length == 0) return locations;
        var start = new ProcessStartInfo("/usr/bin/atos") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        const string delimiter = "--monoon-address--";
        foreach (var arg in new[] { "-arch", "arm64", "-o", dwarf, "--fullPath", "--inlineFrames", "-d", delimiter, "--offset" })
            start.ArgumentList.Add(arg);
        foreach (var frame in native)
        {
            string offset = frame.ImageOffset!;
            if (!offset.StartsWith("0x", StringComparison.Ordinal) ||
                !ulong.TryParse(offset.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong pc) || pc < 4)
                throw new InvalidDataException($"Invalid native image offset: {offset}");
            // Exception frames from the arm64 unwinder contain return PCs. Look up the calling instruction.
            start.ArgumentList.Add($"0x{pc - 4:x}");
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start atos.");
        var stderr = process.StandardError.ReadToEndAsync();
        string[] lines = process.StandardOutput.ReadToEnd().Split(delimiter + "\n", StringSplitOptions.RemoveEmptyEntries);
        process.WaitForExit();
        if (process.ExitCode != 0 || lines.Length != native.Length)
            throw new InvalidDataException($"atos could not resolve this report: {stderr.GetAwaiter().GetResult()}");
        for (int i = 0; i < native.Length; i++)
        {
            string symbol = lines[i].Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(symbol, @":\d+\)")) continue;
            // AOT compiles a temporary snapshot. Show the project-relative script path from its DWARF record.
            symbol = System.Text.RegularExpressions.Regex.Replace(symbol, @"\([^()\n]*/GameScripts/(?:GameSources/|(?=Scripts/))([^()\n]+:\d+)\)", "($1)");
            locations[native[i]] = $"{symbol.Replace("\n", "\n    ")}  (image {native[i].ImageOffset})";
        }
        return locations;
    }
}
