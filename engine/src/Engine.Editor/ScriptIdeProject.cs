using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Engine.Core;

namespace Engine.Editor;

internal sealed record ScriptIdeProject(string Project, string Solution, string Workspace)
{
    private static string Folder(string root) => Path.Combine(root, ".monoon", "ide");
    public static bool Exists(string root) => File.Exists(Path.Combine(Folder(root), "ScenePlay.code-workspace"));

    public static ScriptIdeProject Generate(string root, SceneDebugServer? debugger = null)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(Path.Combine(root, "Scenes"))) throw new IOException("Choose a MoNoon project folder.");
        string folder = Folder(root);
        string name = new DirectoryInfo(root).Name + ".Scripts";
        var files = new ScriptIdeProject(Path.Combine(folder, name + ".csproj"), Path.Combine(folder, name + ".slnx"),
            Path.Combine(folder, "ScenePlay.code-workspace"));
        string Relative(string path) => Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/');
        static string EscapeMsBuild(string path) => string.Concat(path.Select(c =>
            "%$@();'*?".Contains(c) ? "%" + ((int)c).ToString("X2") : c.ToString()));
        var sources = new ProjectScripts(root).Locations().Values.Order(StringComparer.Ordinal);
        var sdk = new XDocument(new XElement("Project",
            new XElement("PropertyGroup", new XElement("ImportDirectoryBuildProps", "false"), new XElement("ImportDirectoryBuildTargets", "false")),
            new XElement("Import", new XAttribute("Project","Sdk.props"), new XAttribute("Sdk","Microsoft.NET.Sdk")),
            new XElement("PropertyGroup", new XElement("TargetFramework","net10.0"), new XElement("AssemblyName","MoNoon.GameScripts"),
                new XElement("EnableDefaultItems","false"), new XElement("ImplicitUsings","disable"),
                new XElement("LangVersion", "13.0"),
                new XElement("Optimize","false"), new XElement("DebugType","portable")),
            new XElement("ItemGroup", new XElement("Reference", new XAttribute("Include","Engine.Core"),
                new XElement("HintPath", EscapeMsBuild(Relative(typeof(GameBehaviour).Assembly.Location))), new XElement("Private","false"))),
            new XElement("ItemGroup", sources.Select(path => new XElement("Compile",
                new XAttribute("Include", EscapeMsBuild(Relative(Path.Combine(root,path)))), new XAttribute("Link", EscapeMsBuild(path))))),
            new XElement("Import", new XAttribute("Project","Sdk.targets"), new XAttribute("Sdk","Microsoft.NET.Sdk"))));
        var solution = new XDocument(new XElement("Solution",new XElement("Project",new XAttribute("Path", Path.GetFileName(files.Project)))));
        var configurations = new List<object>();
        if (debugger != null)
            configurations.Add(new { name = "Attach to MoNoon Scene Play", type = "coreclr", request = "attach",
                processId = Environment.ProcessId, debugServer = debugger.Port, monoonSession = debugger.Token });
        configurations.Add(new { name = "Attach .NET debugger to MoNoon PID", type = "coreclr", request = "attach",
            processId = "${command:pickProcess}", justMyCode = true, requireExactSource = true });
        var workspace = new {
            folders = new[] { new { path = Relative(root) } },
            settings = new Dictionary<string,string> { ["dotnet.defaultSolution"] = ".monoon/ide/" + Path.GetFileName(files.Solution) },
            launch = new { version = "0.2.0", configurations }
        };
        Directory.CreateDirectory(folder);
        Write(files.Project, sdk.ToString());
        Write(files.Solution, solution.ToString());
        if (debugger != null)
            files = files with { Workspace = debugger.Workspace };
        Write(files.Workspace, JsonSerializer.Serialize(workspace, new JsonSerializerOptions { WriteIndented = true }), privateFile: debugger != null);
        return files;
    }

    private static void Write(string path, string content, bool privateFile = false)
    {
        content += "\n";
        if (File.Exists(path) && File.ReadAllText(path) == content) return;
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        if (!privateFile || OperatingSystem.IsWindows()) { ProjectYaml.WriteBytes(path, bytes); return; }
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite })) stream.Write(bytes);
            File.Move(temp, path, overwrite: true);
        }
        finally { File.Delete(temp); }
    }
}
