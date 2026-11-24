using System.Text.RegularExpressions;

// Roadmap decision 15 (W10a): a source scan for math APIs whose result bits differ between JIT and Native AOT or between CPUs
// (FMA only where the CPU path has it, or sincos vs separate sin/cos). Simulation code uses Affine2D and MathF.Sin/Cos instead.
// See docs/manual/transform-2d.md.
internal static class DeterminismScanChecks
{
    [Check(300, StopAfter = "determinism-scan")] static void Registered(CheckRun c) => Run(c.Repository);
    private static readonly Regex Banned = new(
        // System.Numerics: Transform/TransformNormal/Lerp go through MultiplyAddEstimate.
        @"\b(?:Vector[234]|Matrix3x2|Matrix4x4|Quaternion|Plane)\s*\.\s*(?:Transform|TransformNormal|Lerp|Slerp|FusedMultiplyAdd)\s*\(" +
        @"|\b(?:float|double|Single|Double)\s*\.\s*Lerp\s*\(|\bMultiplyAddEstimate\b" +
        // JIT calls sincosf once; Native AOT computes Sin and Cos separately (2.6% of inputs differ in the last bit on arm64).
        @"|\bMathF?\s*\.\s*SinCos\s*\(" +
        // Brutal.Numerics (docs/brutal.md): fused paths in matrices, quaternions and the float2/3/4 helpers.
        @"|\b(?:float|double)[234](?:x[234])?\s*\.\s*(?:Lerp|Transform|FusedMultiplyAdd|Invert)\s*\(|\bfloat4x4\b|\bfloatQuat\b|\.Rows\s*\.\s*Transform\b",
        RegexOptions.Compiled);

    // Drawing-only code: the result reaches the GPU vertex buffer and never feeds simulation state, traces or saves.
    private static readonly Dictionary<string, string> Allowed = new()
    {
        ["engine/src/Engine.Desktop/SdlSpriteBatch.cs"] = "SDL vertex positions for the current frame only (sprite, shader and line quads)",
    };

    internal static void Run(string repository)
    {
        var roots = new[] { "engine/src/Engine.Core", "engine/src/Engine.Desktop" }.Select(r => Path.Combine(repository, r))
            .Concat(Directory.EnumerateDirectories(Path.Combine(repository, "samples")).Select(s => Path.Combine(s, "Scripts")))
            .Where(Directory.Exists);
        var hits = new List<string>(); var used = new HashSet<string>(); int files = 0;
        foreach (var file in roots.SelectMany(r => Directory.EnumerateFiles(r, "*.cs", SearchOption.AllDirectories)))
        {
            files++;
            var relative = Path.GetRelativePath(repository, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var code = lines[i].Split("//", 2)[0];
                if (Banned.Match(code) is not { Success: true } match) continue;
                if (Allowed.ContainsKey(relative)) { used.Add(relative); continue; }
                hits.Add($"{relative}:{i + 1}: {match.Value.TrimEnd('(')}");
            }
        }
        if (hits.Count > 0)
            throw new Exception("Simulation code uses math APIs that differ between JIT/Native AOT or CPUs (use Affine2D, MathF.Sin/Cos):\n  "
                + string.Join("\n  ", hits));
        if (Allowed.Keys.Except(used).FirstOrDefault() is { } stale) throw new Exception($"Stale determinism allowlist entry: {stale}");
        Console.WriteLine($"PASS determinism API scan: {files} simulation source files, {used.Count} drawing-only file allowed");
    }
}
