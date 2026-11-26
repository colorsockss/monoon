using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Engine.Core;

// Roadmap 15.6: The built-in sample distribution builds for osx-arm64 must not grow more than
// Tolerance over engine/tests/results/build-size/baseline.tsv. Start-up (headless first frame, main -> session_ready)
// is printed and stored with --update but never gated: it moves with machine load. Update the baseline with
// ./monoon test size --update after an intended size change, and commit the file with the change.
// A targeted guard runs first: the Native AOT kit's ILC map must hold no VYaml generated Serialize code. One reference-type
// ProjectYaml.Encode<T>/Write<T> call in runtime code brings all of it back (2.3 MB per game, results/aot-serializer).
internal static class BuildSizeChecks
{
    // Stays last (roadmap 15.6, osx-arm64 only); --update records a new baseline only through --only build-size.
    [Check(1_000_000, "build-size", Templates = true)] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch, c.Only == "build-size" && c.Args.Contains("--update"));
    internal const double Tolerance = 0.01;
    private static readonly string[] Samples = ["KeyRoom", "PlatformerLab", "InventoryLab", "LedgeRun", "IsoBand", "HexRealm"];

    internal static void Run(string repository, string scratch, bool update)
    {
        Require(Over(1_000_000, 1_010_001) && !Over(1_000_000, 1_010_000) && !Over(1_000_000, 900_000), "size tolerance arithmetic");
        // Roadmap 15.9: Linux Distribution AOT builds with the Size preference too (built and measured on the Linux guest).
        Require(NativeAotBuild.PrefersSize("osx-arm64", BuildProfile.Distribution) && NativeAotBuild.PrefersSize("linux-x64", BuildProfile.Distribution)
            && !NativeAotBuild.PrefersSize("win-x64", BuildProfile.Distribution) && !NativeAotBuild.PrefersSize("linux-x64", BuildProfile.Development),
            "Native AOT Size preference for osx-arm64 and linux-x64 Distribution only");
        if (BuildSettings.Host != "osx-arm64")
        {
            Console.WriteLine($"SKIP build size baseline: recorded for osx-arm64, this host is {BuildSettings.Host}");
            return;
        }
        SerializerGuard(repository, scratch);
        string file = Path.Combine(repository, "engine", "tests", "results", "build-size", "baseline.tsv");
        var baseline = update || !File.Exists(file) ? [] : File.ReadLines(file).Skip(1).Select(l => l.Split('\t'))
            .ToDictionary(c => (c[0], c[1]), c => (Total: long.Parse(c[2], CultureInfo.InvariantCulture), Executable: long.Parse(c[3], CultureInfo.InvariantCulture)));
        var rows = new List<string> { "sample\tmode\tapp_bytes\texecutable_bytes\tfirst_frame_ms\tsession_ready_ms" };
        var failures = new List<string>();
        foreach (var sample in Samples)
            foreach (bool native in new[] { false, true })
            {
                string mode = native ? "native-aot" : "release", project = Path.Combine(repository, "samples", sample);
                var output = Path.Combine(scratch, "BuildSize", sample);
                var settings = GameSettings.Load(project);
                var result = native
                    ? GameBuild.ExportNativeAot(project, Path.Combine(repository, "engine"), "osx-arm64", output, settings, BuildProfile.Distribution)
                    : GameBuild.Export(project, GameBuild.FindPlayerTemplate(Path.Combine(repository, "engine"), "osx-arm64"), "osx-arm64", output, settings, BuildProfile.Distribution);
                // Symbolic links (the shared SDL loader name) take no space in the archive users download.
                long total = Directory.EnumerateFiles(result.Product, "*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f)).Where(f => f.LinkTarget == null).Sum(f => f.Length);
                var bin = Path.Combine(result.Product, "Contents", "MacOS");
                long executable = new FileInfo(Path.Combine(bin, "MoNoonPlayer")).Length;
                var (frame, ready) = Startup(bin, Path.Combine(scratch, "BuildSize", sample + "-" + mode));
                rows.Add(string.Join('\t', sample, mode, total.ToString(CultureInfo.InvariantCulture), executable.ToString(CultureInfo.InvariantCulture),
                    frame.ToString("0", CultureInfo.InvariantCulture), ready.ToString("0.0", CultureInfo.InvariantCulture)));
                string line = $"{sample} {mode}: app {total:N0} B, executable {executable:N0} B, first frame {frame:0} ms, session_ready {ready:0.0} ms";
                if (baseline.TryGetValue((sample, mode), out var was))
                {
                    line += $" (baseline {was.Total:N0} / {was.Executable:N0})";
                    if (Over(was.Total, total)) failures.Add($"{sample} {mode} app {total:N0} B > baseline {was.Total:N0} B + {Tolerance:P0}");
                    if (Over(was.Executable, executable)) failures.Add($"{sample} {mode} executable {executable:N0} B > baseline {was.Executable:N0} B + {Tolerance:P0}");
                }
                else if (!update) failures.Add($"{sample} {mode} has no baseline row");
                Console.WriteLine(line);
            }
        if (update)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllLines(file, rows);
            Console.WriteLine($"Updated {file}; commit it with the change that moved the sizes.");
            return;
        }
        if (failures.Count > 0)
            throw new Exception("FAIL build size baseline:\n  " + string.Join("\n  ", failures) +
                "\nIf the growth is intended, record it with ./monoon test size --update and commit engine/tests/results/build-size/baseline.tsv.");
        Console.WriteLine($"PASS build size baseline: {Samples.Length * 2} osx-arm64 distribution builds within +{Tolerance:P0}");
    }

    // Publishes the Native AOT kit without game scripts with IlcGenerateMapFile and counts generated formatter Serialize code.
    private static void SerializerGuard(string repository, string scratch)
    {
        var templates = Path.GetDirectoryName(GameBuild.FindPlayerTemplate(Path.Combine(repository, "engine"), "osx-arm64"))!;
        // A worktree reaches the shared kit through a symlink; restore it under one spelling as NativeAotBuild does.
        var kit = NativeAotBuild.RealPath(Path.Combine(templates, "native-aot", "src", "Engine.Player", "Engine.Player.csproj"));
        var work = Path.Combine(scratch, "BuildSize", "kit-map");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "publish", kit, "-c", "Release", "-r", "osx-arm64", "--self-contained", "true", "-p:PublishAot=true",
            "-p:OptimizationPreference=Size", "-p:IlcGenerateMapFile=true", "-p:NuGetAudit=false", "-nr:false", "-m:1",
            "--artifacts-path", Path.Combine(work, "a"), "-o", Path.Combine(work, "bin") }) start.ArgumentList.Add(arg);
        using (var process = Process.Start(start)!)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new Exception($"FAIL Native AOT kit map publish: {output.Result}{error}");
        }
        var map = File.ReadAllText(Directory.EnumerateFiles(Path.Combine(work, "a"), "MoNoonPlayer.map.xml", SearchOption.AllDirectories).Single());
        var serialize = Regex.Matches(map, @"<MethodCode Name=""[^""]*GeneratedFormatter__Serialize[^""]*"" Length=""(\d+)""");
        long bytes = serialize.Sum(m => long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        if (serialize.Count != 0)
            throw new Exception($"FAIL Native AOT player has {serialize.Count} VYaml generated Serialize methods ({bytes:N0} B): runtime code calls " +
                "ProjectYaml.Encode<T>/Write<T> for a reference type. Write it with ProjectYamlWriters.cs instead (engine/tests/results/aot-serializer).");
        // StandardResolver's BuiltinResolver (~0.4 MB with its formatters' BCL code) comes back if runtime code names StandardResolver
        // or constructs YamlSerializerOptions (engine/tests/results/aot-size-2).
        if (map.Contains("VYaml_VYaml_Serialization_BuiltinResolver", StringComparison.Ordinal))
            throw new Exception("FAIL Native AOT player contains VYaml's BuiltinResolver: runtime code reaches StandardResolver. Use ProjectYaml.Options " +
                "(ScalarYamlResolver) instead (engine/tests/results/aot-size-2).");
        // VYaml.Substitutions.xml removes YamlSerializerOptions' constructor and Standard, which YamlSerializer's pooled
        // contexts call: in the player that static API would throw. ProjectYaml.Decode and ProjectYamlWriters do not use it.
        if (Regex.Match(map, @"<MethodCode Name=""VYaml_VYaml_Serialization_YamlSerializer__\w+") is { Success: true } pooled)
            throw new Exception($"FAIL Native AOT player calls VYaml's YamlSerializer ({pooled.Value[17..]}), which throws there: use ProjectYaml.Decode " +
                "or a ProjectYamlWriters overload (engine/tests/results/aot-size-2).");
        // Runtime JSON is read and written by hand (JsonElements, WorldCheckpointJsonCodec); JsonSerializer's converter layer is
        // about 0.7 MB of KeyRoom's player (engine/tests/results/aot-feature-switches). Game scripts can still bring it back.
        if (Regex.Match(map, @"<MethodCode Name=""System_Text_Json_System_Text_Json_JsonSerializer__\w+") is { Success: true } json)
            throw new Exception($"FAIL Native AOT player calls JsonSerializer ({json.Value[17..]}): read or write the value with JsonElements " +
                "instead (engine/tests/results/aot-feature-switches).");
        int deserialize = Regex.Matches(map, @"<MethodCode Name=""[^""]*GeneratedFormatter__Deserialize").Count;
        Require(deserialize > 0, "the kit map still names generated formatter methods (the pattern matches ILC's naming)");
        Console.WriteLine($"PASS Native AOT kit has no VYaml generated Serialize code or BuiltinResolver ({deserialize} Deserialize methods remain)");
    }

    internal static bool Over(long baseline, long measured) => measured > baseline + (long)Math.Floor(baseline * Tolerance);

    // Median of three warm headless runs that draw frame 0 to a PNG (software renderer) and exit.
    private static (double FirstFrameMs, double SessionReadyMs) Startup(string bin, string work)
    {
        var game = GameSettings.Load(Path.Combine(bin, "Game"));
        Directory.CreateDirectory(work);
        var replay = Path.Combine(work, "one.inputreplay");
        File.WriteAllText(replay, $"version: 1\nproject: {game.ProjectId}\nstartScene: {game.StartScene}\nframeTicks: [166667]\nevents: []\n");
        var frames = new List<double>();
        var ready = new List<double>();
        for (int run = 0; run < 4; run++)
        {
            string trace = Path.Combine(work, $"trace{run}.csv"), profile = Path.Combine(work, $"profile{run}");
            var start = new ProcessStartInfo(Path.Combine(bin, "MoNoonPlayer")) { WorkingDirectory = bin, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "--replay-input", replay, profile }) start.ArgumentList.Add(arg);
            start.Environment["SDL_VIDEO_DRIVER"] = "monoon-no-video";
            start.Environment["SDL_AUDIO_DRIVER"] = "monoon-no-audio";
            start.Environment["MONOON_USER_DATA"] = profile;
            start.Environment["MONOON_STARTUP_TRACE"] = trace;
            start.Environment["MONOON_REPLAY_SNAPSHOTS"] = Path.Combine(work, $"snap{run}") + ";0";
            var clock = Stopwatch.StartNew();
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            clock.Stop();
            if (process.ExitCode != 0) throw new Exception($"FAIL headless start of {bin}: {output.Result}{error}");
            if (run == 0) continue; // the first run after a build also pays for macOS checking the new files
            frames.Add(clock.Elapsed.TotalMilliseconds);
            ready.Add(double.Parse(File.ReadLines(trace).First(l => l.StartsWith("session_ready,", StringComparison.Ordinal)).Split(',')[1], CultureInfo.InvariantCulture));
        }
        return (frames.Order().ElementAt(1), ready.Order().ElementAt(1));
    }

    private static void Require(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL " + label);
    }
}
