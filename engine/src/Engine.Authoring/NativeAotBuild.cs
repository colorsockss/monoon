using System.Diagnostics;
using System.Xml.Linq;

namespace Engine.Core;

internal static class NativeAotBuild
{
    internal static void Publish(string playerProject, string rid, ProjectScripts.Build scripts, string output, BuildProfile profile)
    {
        playerProject = RealPath(playerProject);
        // The compiler sees the real path (/private/var on macOS), so the path maps below must use it too.
        var work = new DirectoryInfo(RealPath(Directory.CreateTempSubdirectory("monoon-aot-").FullName));
        try
        {
            WriteScriptRegistry(work.FullName, playerProject, scripts);
            bool compactMac = rid == "osx-arm64" && profile == BuildProfile.Distribution;
            // Map the random work folder (generated scripts, obj and PDB paths) to fixed names: identical builds give identical players.
            string pathMap = $"{Path.Combine(work.FullName, "GameScripts", "GameSources")}=/_/%2C{work.FullName}=/_/monoon-aot";
            var args = new List<string> { "publish", playerProject, "-c", "Release", "-r", rid, "--self-contained", "true",
                "-p:PublishAot=true", "-p:DebugType=portable", "-p:CopyOutputSymbolsToPublishDirectory=true", "-p:UseSharedCompilation=false", "-p:NuGetAudit=false", "-nr:false", "-m:1",
                "-p:PathMap=" + pathMap,
                "-p:MoNoonGameReferences=" + Path.Combine(work.FullName, "Scripts.props"),
                "--artifacts-path", Path.Combine(work.FullName, "artifacts"), "-o", Path.GetFullPath(output) };
            if (compactMac) args.Add("-p:OptimizationPreference=Size");
            Run(DotnetHost(), work.FullName, args);
        }
        finally { work.Delete(true); }
    }

    // NuGet restores relative project references under the canonical path. A kit reached through a symlink (macOS
    // /var -> /private/var, /tmp) was then restored under two spellings and Engine.Desktop's packages (Brutal.Sdl, SDL3)
    // silently dropped out of the AOT link: the game failed at start with "Could not find file 'Brutal.Sdl'".
    internal static string RealPath(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (new FileInfo(current).LinkTarget is { } target)
                current = RealPath(Path.Combine(Path.GetDirectoryName(current)!, target));
        }
        return current;
    }

    private static string Run(string tool, string work, IEnumerable<string> arguments)
    {
        var command = new ProcessStartInfo(tool)
        {
            WorkingDirectory = work, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in arguments) command.ArgumentList.Add(arg);
        using var process = Process.Start(command) ?? throw new IOException($"Could not start {tool}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var output = stdout.GetAwaiter().GetResult();
        var error = stderr.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Native AOT build failed in {Path.GetFileName(tool)} (exit {process.ExitCode}).\n{output}{error}".TrimEnd());
        return output.Trim();
    }

    private static string DotnetHost()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { } host && File.Exists(host)) return host;
        var executable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        if (Environment.GetEnvironmentVariable("DOTNET_ROOT") is { } root && File.Exists(Path.Combine(root, executable)))
            return Path.Combine(root, executable);
        if (OperatingSystem.IsMacOS() && File.Exists("/usr/local/share/dotnet/dotnet")) return "/usr/local/share/dotnet/dotnet";
        return executable;
    }

    // Build the same source snapshot and generated factory against the target SDK reference assemblies.
    private static void WriteScriptRegistry(string work, string playerProject, ProjectScripts.Build scripts)
    {
        var dir = Directory.CreateDirectory(Path.Combine(work,"GameScripts")).FullName;
        var sources = new XElement("ItemGroup");
        foreach (var file in scripts.Sources)
        {
            var path = Path.Combine(dir, "GameSources", file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Text);
        }
        sources.Add(new XElement("Compile", new XAttribute("Include", "GameSources/**/*.cs")));
        File.WriteAllText(Path.Combine(dir,"Factory.g.cs"),scripts.Factory);
        sources.Add(new XElement("Compile",new XAttribute("Include","Factory.g.cs")));
        var project = Path.Combine(dir,"GameScripts.csproj");
        var core = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(playerProject)!,"..","Engine.Core","Engine.Core.csproj"));
        new XDocument(new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",new XElement("TargetFramework","net10.0"),
                new XElement("AssemblyName","MoNoon.GameScripts"),new XElement("EnableDefaultCompileItems","false")),
            sources,new XElement("ItemGroup",new XElement("ProjectReference",new XAttribute("Include",core))))).Save(project);
        var source = Path.Combine(work,"CompiledGameScripts.g.cs");
        File.WriteAllText(source, """
            extern alias projectScripts;
            internal static class CompiledGameScripts
            {
                internal static Engine.Core.GameBehaviour Create(Engine.Core.ScriptComponent component) =>
                    projectScripts::MoNoon.Generated.ProjectScriptFactory.Create(component);
            }
            """);
        new XDocument(new XElement("Project",new XElement("ItemGroup",
            new XElement("ProjectReference",new XAttribute("Include",project),new XElement("Aliases","projectScripts")),
            new XElement("Compile",new XAttribute("Include",source))))).Save(Path.Combine(work,"Scripts.props"));
    }
}
