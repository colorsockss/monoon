using System.Diagnostics;
using System.Xml.Linq;

namespace Engine.Core;

internal static class NativeAotBuild
{
    internal static void Publish(string playerProject, string rid, ProjectScripts.Build scripts, string output, BuildProfile profile)
    {
        var work = Directory.CreateTempSubdirectory("monoon-aot-");
        try
        {
            WriteScriptRegistry(work.FullName, playerProject, scripts);
            bool compactMac = rid == "osx-arm64" && profile == BuildProfile.Distribution;
            var args = new List<string> { "publish", playerProject, "-c", "Release", "-r", rid, "--self-contained", "true",
                "-p:PublishAot=true", "-p:DebugType=portable", "-p:CopyOutputSymbolsToPublishDirectory=true", "-p:UseSharedCompilation=false", "-p:NuGetAudit=false", "-nr:false", "-m:1",
                "-p:MoNoonGameReferences=" + Path.Combine(work.FullName, "Scripts.props"),
                "--artifacts-path", Path.Combine(work.FullName, "artifacts"), "-o", Path.GetFullPath(output) };
            if (compactMac) args.Add("-p:OptimizationPreference=Size");
            Run(DotnetHost(), work.FullName, args);
        }
        finally { work.Delete(true); }
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
                new XElement("AssemblyName","MoNoon.GameScripts"),new XElement("EnableDefaultCompileItems","false"),
                new XElement("PathMap", "$(MSBuildProjectDirectory)/GameSources=/_/")),
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
