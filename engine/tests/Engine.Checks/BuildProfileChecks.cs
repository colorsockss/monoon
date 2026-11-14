using System.Diagnostics;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using Engine.Core;

internal static class BuildProfileChecks
{
    // Validate the exported product independently of the exporter's staging directory.
    internal static void CheckInventory(string root, string bin, string rid, bool native, BuildProfile? expectedProfile)
    {
        var file = Path.Combine(bin, BuildSymbols.Receipt);
        if (expectedProfile == null && !File.Exists(file)) return; // Existing exports predate profiles and can still be played/tested.
        var receipt = GameData.Load(file);
        var mode = native ? "native-aot" : "release";
        bool development = receipt.GetText("profile") == "development";
        var id = receipt.GetText("buildId");
        if (!Guid.TryParseExact(id, "N", out _) || receipt.GetText("target") != rid || receipt.GetText("mode") != mode ||
            receipt.GetText("game") != "InventoryLab" || !development && receipt.GetText("profile") != "distribution")
            throw new InvalidDataException($"Invalid build receipt at {file}.");
        if (expectedProfile != null && expectedProfile != (development ? BuildProfile.Development : BuildProfile.Distribution))
            throw new InvalidDataException($"Expected profile {expectedProfile} at {file}.");
        string symbols = development ? bin : Path.Combine(root, "Symbols", "InventoryLab", rid, mode, id);
        if (!development)
        {
            if (!File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(Path.Combine(symbols, BuildSymbols.Receipt))))
                throw new InvalidDataException($"The archived receipt differs from {file}.");
            var leaks = Directory.EnumerateFileSystemEntries(bin, "*", SearchOption.AllDirectories)
                .Where(p => Path.GetExtension(p).ToLowerInvariant() is ".dsym" or ".pdb" or ".dbg" or ".debug" or ".map" or ".dwarf");
            if (leaks.FirstOrDefault() is { } leak) throw new InvalidDataException($"Symbol in distribution player: {leak}");
        }
        string executable = Path.Combine(bin, rid == "win-x64" ? "MoNoonPlayer.exe" : "MoNoonPlayer");
        using var player = File.OpenRead(executable);
        if (Convert.ToHexString(SHA256.HashData(player)) != receipt.GetText("playerSha256"))
            throw new InvalidDataException($"Executable checksum mismatch for {file}.");
        if (!native)
        {
            string scripts = Path.Combine("Game", ".monoon-compiled", "MoNoon.GameScripts.dll");
            foreach (var dll in new[] { "Engine.Core.dll", "Engine.Desktop.dll", "MoNoonPlayer.dll", scripts })
            {
                var pdb = Path.ChangeExtension(Path.Combine(symbols, dll), ".pdb");
                var guid = BuildSymbols.VerifyPortablePdb(Path.Combine(bin, dll), pdb);
                if (guid.ToString("D") != receipt.GetText("pdbId/" + dll.Replace('\\', '/')))
                    throw new InvalidDataException($"PDB identity missing from {file}: {dll}");
            }
            using var scriptPdb = File.OpenRead(Path.ChangeExtension(Path.Combine(symbols, scripts), ".pdb"));
            using var provider = MetadataReaderProvider.FromPortablePdbStream(scriptPdb);
            var reader = provider.GetMetadataReader();
            bool mapped = reader.MethodDebugInformation.Select(reader.GetMethodDebugInformation)
                .SelectMany(info => info.GetSequencePoints())
                .Any(point => !point.IsHidden && !point.Document.IsNil &&
                    reader.GetString(reader.GetDocument(point.Document).Name).EndsWith("InventoryWorkbench.cs", StringComparison.Ordinal));
            if (!mapped) throw new InvalidDataException("Game script PDB contains no InventoryWorkbench source lines.");
        }
        else if (rid == "osx-arm64")
        {
            var dsym = Path.Combine(symbols, "MoNoonPlayer.dSYM");
            var result = new ProcessStartInfo("/usr/bin/dwarfdump") { RedirectStandardOutput = true, UseShellExecute = false };
            result.ArgumentList.Add("--uuid");
            result.ArgumentList.Add(executable);
            result.ArgumentList.Add(dsym);
            using var dump = Process.Start(result)!;
            var lines = dump.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            dump.WaitForExit();
            var ids = lines.Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
            if (dump.ExitCode != 0 || ids.Length != 2 || ids.Any(fields => fields.Length < 3 || fields[0] != "UUID:" || fields[2] != "(arm64)") ||
                !Guid.TryParse(ids[0][1], out _) || ids[0][1] != ids[1][1])
                throw new InvalidDataException($"Native executable/dSYM UUID mismatch at {dsym}.");
            Console.WriteLine($"PASS Native AOT dSYM UUID matches arm64 player: {ids[0][1]}");
        }
        else
        {
            string symbol = Path.Combine(symbols, rid == "win-x64" ? "MoNoonPlayer.pdb" : "MoNoonPlayer.dbg");
            if (!File.Exists(symbol) || new FileInfo(symbol).Length == 0) throw new InvalidDataException($"Missing {symbol}.");
        }
        Console.WriteLine($"PASS {mode} {receipt.GetText("profile")} symbols and build receipt: {id}");
    }
}
