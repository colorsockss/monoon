using System.Diagnostics;
using System.Runtime.InteropServices;
using Engine.Core;

internal static class CrashDiagnostics
{
    public static void Report(Exception failure, string run, Guid? scene)
    {
        try
        {
            var build = GameData.Load(Path.Combine(AppContext.BaseDirectory, "MoNoonBuild"));
            var causes = new List<CrashCause>();
            for (Exception? error = failure; error != null; error = error.InnerException)
            {
                var frames = new StackTrace(error, true).GetFrames().Select(Capture).ToArray();
                causes.Add(new(error.GetType().FullName ?? error.GetType().Name, error.Message, frames));
            }
            causes.Reverse(); // Start with the original throw, then the engine's callback context.
            var report = new PlayerCrashReport(1, DateTime.UtcNow.ToString("O"), RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(), run, scene, build, causes.ToArray());
            string path = report.Save(PlayerStorage.CrashDirectory(Guid.Parse(build.GetText("projectId"))));
            Console.Error.WriteLine($"Crash report: {path}");
        }
        catch (Exception ex) { Console.Error.WriteLine($"Could not write crash report: {ex.Message}"); }
    }

    private static CrashFrame Capture(StackFrame frame)
    {
#if MONOON_COMPILED_SCRIPTS
        var method = DiagnosticMethodInfo.Create(frame);
        string name = method == null ? "(unknown)" : $"{method.DeclaringTypeName}.{method.Name}";
        var ip = frame.GetNativeIP();
        var image = frame.GetNativeImageBase();
        // A Native AOT player links the compiled game scripts and engine into the main image.
        if (ip > image && image != 0 && image == MainImage.Value)
            return new(name, ImageOffset: $"0x{ip - image:x}");
        return new(name);
#else
        var method = frame.GetMethod();
        if (method == null) return new("(unknown)");
        string name = $"{method.DeclaringType?.FullName}.{method.Name}";
        if (method is System.Reflection.Emit.DynamicMethod) return new(name);
        var module = method.Module;
        int offset = frame.GetILOffset();
        return new(name, module.Assembly.GetName().Name, module.ModuleVersionId.ToString("D"),
            method.MetadataToken, offset < 0 ? null : offset);
#endif
    }

#if MONOON_COMPILED_SCRIPTS
    private static readonly Lazy<nint> MainImage = new(() =>
    {
        if (OperatingSystem.IsMacOS()) return GetMacImageHeader(0);
        using var process = Process.GetCurrentProcess();
        return process.MainModule?.BaseAddress ?? 0;
    });

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_get_image_header")]
    private static extern nint GetMacImageHeader(uint index);
#endif
}
