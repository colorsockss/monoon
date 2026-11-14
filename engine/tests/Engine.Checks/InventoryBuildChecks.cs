using System.Diagnostics;
using System.Runtime.InteropServices;
using Engine.Core;

internal static class InventoryBuildChecks
{
    internal static async Task Run(string output, string scratch, BuildProfile? expectedProfile)
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var release = await Exercise("Release", PlayerPath(output, false));
        var native = await Exercise("Native AOT", PlayerPath(output, true));
        for (int i = 0; i < release.Count; i++)
            if (!release[i].SequenceEqual(native[i]))
                throw new InvalidDataException($"Release and Native AOT produced different saves at checkpoint {i + 1}.");
        Console.WriteLine($"PASS InventoryLab: {release.Count} byte-identical checkpoints across Release and Native AOT.");

        async Task<List<byte[]>> Exercise(string mode, string player)
        {
            var bin = Path.GetDirectoryName(player)!;
            if (!File.Exists(player)) throw new FileNotFoundException($"Missing {mode} player at {player}.");
            var scripts = Path.Combine(bin, "Game", ".monoon-compiled", "MoNoon.GameScripts.dll");
            if (File.Exists(scripts) != (mode == "Release") || File.Exists(Path.Combine(bin, "MoNoonPlayer.dll")) != (mode == "Release"))
                throw new InvalidDataException($"Unexpected player or script compilation mode at {bin}.");
            BuildProfileChecks.CheckInventory(output, bin, rid, mode != "Release", expectedProfile);
            var profile = Directory.CreateDirectory(Path.Combine(scratch, mode)).FullName;
            var checkpoints = new List<byte[]>();
            // Each input sequence runs a fresh copy of the exported executable.
            await Checkpoint("offer", "T 1 S", 0, false);
            await Checkpoint("paid", "L 1 1 1 1 1 T 2 1 S", 3, true);
            await Checkpoint("reward", "L 1 T 2 1 S", 3, true);
            return checkpoints;

            async Task Checkpoint(string node, string input, int herbs, bool claimed)
            {
                await RunPlayer(player, profile, mode, ["--smoke-test", profile, ..input.Split(' ')]);
                string path = Path.Combine(profile, "slot-001.save");
                var save = GameData.Load(path);
                var expected = new GameData { Version = 3 };
                expected.Set("capacity", 3);
                expected.Set("dialogue", node);
                expected.Values["quests"] = GameData.ParseValue(claimed ? "{herbalist: {collected: 3, claimed: true}}" : "{}");
                if (herbs > 0)
                {
                    expected.Set("item0", "herb");
                    expected.Set("count0", herbs);
                }
                if (claimed)
                {
                    expected.Set("item1", "gem");
                    expected.Set("count1", 1);
                }
                if (save.Version != expected.Version || save.Values.Count != expected.Values.Count ||
                    !expected.Values.All(pair => save.Values.TryGetValue(pair.Key, out var value) &&
                        System.Text.Json.JsonElement.DeepEquals(value, pair.Value)))
                    throw new InvalidDataException($"{mode} input [{input}] expected:\n{expected.ToYaml()}Actual save:\n{save.ToYaml()}");
                checkpoints.Add(File.ReadAllBytes(path));
                Console.WriteLine($"PASS {mode} InventoryLab: {node}, herbs={herbs}, gems={(claimed ? 1 : 0)}");
            }
        }
    }

    internal static string PlayerPath(string output, bool native)
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var bin = rid switch
        {
            "osx-arm64" => Path.Combine("InventoryLab.app", "Contents", "MacOS"),
            "win-x64" or "linux-x64" => "InventoryLab-" + rid,
            _ => throw new PlatformNotSupportedException($"Cannot run exported InventoryLab players on {rid}.")
        };
        return Path.GetFullPath(Path.Combine(native ? Path.Combine(output, "NativeAOT") : output, bin,
            OperatingSystem.IsWindows() ? "MoNoonPlayer.exe" : "MoNoonPlayer"));
    }

    internal static async Task RunPlayer(string player, string profile, string mode, string[] arguments)
    {
        var start = new ProcessStartInfo(player) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        start.Environment["MONOON_USER_DATA"] = profile;
        // A test must fail even on a desktop if it accidentally starts the interactive host.
        start.Environment["SDL_VIDEO_DRIVER"] = "monoon-no-video";
        start.Environment["SDL_AUDIO_DRIVER"] = "monoon-no-audio";
        string input = string.Join(" ", arguments);
        using var process = Process.Start(start) ?? throw new IOException($"Cannot start {player}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException($"{mode} input [{input}] timed out. Rebuild the player template. {await stderr}");
        }
        string log = (await stdout) + (await stderr);
        Console.Write(log);
        if (process.ExitCode != 0) throw new InvalidOperationException($"{mode} input [{input}] exited with code {process.ExitCode}.");
    }
}
