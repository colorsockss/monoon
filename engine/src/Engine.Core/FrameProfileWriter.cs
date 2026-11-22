using System.Globalization;

namespace Engine.Core;

/// <summary>
/// Streams every profiled frame of a development-profile player to CSV, one row per call-tree node in depth-first order.
/// Distribution builds ignore the request.
/// </summary>
public sealed class FrameProfileWriter : IDisposable
{
    public const string Variable = "MONOON_FRAME_PROFILE";
    public const string Header = "frame,node,parent,depth,name,calls,inclusive_us,self_us,alloc_bytes,gen0,dropped";
    private readonly StreamWriter output;
    private readonly char[] line = new char[128];
    public FrameProfiler Profiler { get; } = new();

    private FrameProfileWriter(string path)
    {
        output = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        output.WriteLine(Header);
        Profiler.Completed += Write;
        Profiler.Install();
    }

    public static bool IsDevelopmentBuild(string playerDirectory)
    {
        string receipt = Path.Combine(playerDirectory, "MoNoonBuild");
        return File.Exists(receipt) && GameData.Load(receipt).GetText("profile") == "development";
    }

    /// <summary>Starts capturing to the path in MONOON_FRAME_PROFILE when the build receipt says development.</summary>
    public static FrameProfileWriter? Open(string playerDirectory)
    {
        if (Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 } path) return null;
        if (IsDevelopmentBuild(playerDirectory)) return new(path);
        Console.Error.WriteLine($"{Variable} is ignored: frame profiling needs a development-profile build.");
        return null;
    }

    private void Write(ProfiledFrame frame) => Write(frame, 0, 0);

    // Explicit TryFormat calls: interpolation handlers box in unoptimized JIT code.
    private int at;
    private void Put(long value) { value.TryFormat(line.AsSpan(at), out int n, default, CultureInfo.InvariantCulture); at += n; line[at++] = ','; }
    private void Put(double ms) { (ms * 1000).TryFormat(line.AsSpan(at), out int n, "F1", CultureInfo.InvariantCulture); at += n; line[at++] = ','; }

    private void Write(ProfiledFrame f, int node, int depth)
    {
        at = 0;
        Put(f.Sequence); Put(node); Put(f.Parent(node)); Put(depth);
        output.Write(line, 0, at);
        output.Write(f.Name(node));
        at = 0;
        line[at++] = ',';
        Put(f.Calls(node)); Put(f.InclusiveMilliseconds(node)); Put(f.SelfMilliseconds(node));
        if (node == 0) { Put(f.AllocatedBytes); Put(f.Gen0); Put(f.Dropped); }
        else { line[at++] = ','; line[at++] = ','; line[at++] = ','; }
        line[at - 1] = '\n';
        output.Write(line, 0, at);
        for (int c = f.FirstChildOf(node); c >= 0; c = f.NextSibling(c)) Write(f, c, depth + 1);
    }

    public void Dispose()
    {
        Profiler.Uninstall();
        output.Dispose();
    }
}
