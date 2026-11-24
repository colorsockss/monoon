using System.Text.Json;

/// <summary>engine/tests/harnesses.json lists exactly the harness folders, so run-harness.py and --changed never miss one
/// (deeper checks: python3 engine/scripts/run-harness.py --check).</summary>
static class HarnessManifestChecks
{
    [Check(10050)]
    static void Registered(CheckRun c)
    {
        var tests = Path.Combine(c.Repository, "engine", "tests");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(tests, "harnesses.json")));
        var listed = manifest.RootElement.GetProperty("harnesses"u8).EnumerateArray().Select(h => h.GetProperty("id"u8).GetString()!).Order(StringComparer.Ordinal).ToArray();
        // __pycache__: Python writes it next to imported harness modules (desktop_players.py) where bytecode caching is on (Linux CI).
        string[] notHarnesses = ["Engine.Checks", "Shared", "fixtures", "results", "__pycache__"];
        var folders = Directory.GetDirectories(tests).Select(Path.GetFileName).Where(n => !notHarnesses.Contains(n) && !n!.StartsWith('.'))
            .Order(StringComparer.Ordinal).ToArray();
        if (!listed.SequenceEqual(folders))
            throw new Exception($"engine/tests/harnesses.json is out of date: add {string.Join(", ", folders.Except(listed))}; remove {string.Join(", ", listed.Except(folders))}.");
        Console.WriteLine($"PASS harness manifest lists all {folders.Length} engine/tests harness folders");
    }
}
