using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using Engine.Core;
using Engine.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class SceneScriptDebugChecks
{
    internal static void Run(string scratch)
    {
        string root = Directory.CreateDirectory(Path.Combine(scratch,"Debug Project")).FullName;
        string dir = Directory.CreateDirectory(Path.Combine(root,"Assets","Moved")).FullName;
        string file = Path.Combine(dir,"Relocated.cs");
        File.WriteAllText(file, "using Engine.Core; public sealed class Probe : GameBehaviour { public int Count = 12; }", new UTF8Encoding(true));
        ProjectYaml.Write(file + ".meta", new ScriptLocation("Probe"));
        var debug = ProjectScripts.ForScenePlay(root);
        var release = new ProjectScripts(root);
        using var stream = new MemoryStream(debug.Current().Symbols);
        using var pdb = MetadataReaderProvider.FromPortablePdbStream(stream);
        var reader = pdb.GetMetadataReader();
        var doc = reader.Documents.Select(reader.GetDocument).Single(d => reader.GetString(d.Name) == file);
        if (!reader.GetBlobBytes(doc.Hash).SequenceEqual(SHA256.HashData(File.ReadAllBytes(file))))
            throw new Exception("Scene Play PDB must match the on-disk source bytes, including BOM.");
        using var releasePdb = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(release.Current().Symbols));
        var releaseReader = releasePdb.GetMetadataReader();
        if (!releaseReader.Documents.Any(handle => releaseReader.GetString(releaseReader.GetDocument(handle).Name) == "./Assets/Moved/Relocated.cs"))
            throw new Exception("A release PDB must retain its relocatable document paths.");

        Directory.CreateDirectory(Path.GetDirectoryName(release.CompiledPath)!);
        File.WriteAllBytes(release.CompiledPath, release.Current().Bytes);
        File.WriteAllText(file, File.ReadAllText(file).Replace("12","13"), new UTF8Encoding(true));
        int Count(ProjectScripts library)
        {
            using var runtime = new ScriptRuntime(library);
            var probe = runtime.Create(new ScriptComponent { Script = "Probe" });
            return (int)probe.GetType().GetField("Count")!.GetValue(probe)!;
        }
        if (Count(debug) != 13 || Count(release) != 12)
            throw new Exception("Play should compile current sources while the player loads its packaged module.");
        File.WriteAllText(file, """
            using Engine.Core;
            public sealed class Probe : GameBehaviour
            {
                public int Mark(int input)
                {
                    int doubled = 2 * input;
                    return doubled + 1;
                }
            }
            """);
        const int breakpoint = 7;
        var debugLibrary = ProjectScripts.ForScenePlay(root, compilation => {
            var tree = compilation.SyntaxTrees.Single();
            var lines = new HashSet<int>();
            var rewriter = new SceneDebugInstrumentation(compilation.GetSemanticModel(tree), lines);
            var rewritten = rewriter.Visit(tree.GetRoot())!;
            if (!lines.SetEquals([6,7])) throw new Exception("Only executable lines should be advertised.");
            return compilation.ReplaceSyntaxTree(tree,tree.WithRootAndOptions(rewritten,tree.Options));
        });
        using var scriptRuntime = new ScriptRuntime(debugLibrary);
        var instance = scriptRuntime.Create(new ScriptComponent { Script = "Probe" });
        SceneScriptBreakpoint.Filter = (path,line) => path == file && line == breakpoint;
        bool hit = false;
        SceneScriptBreakpoint.Handler = (_,_,method,names,values) => {
            var locals = names.Zip(values).ToDictionary();
            hit = method.Contains("Probe.Mark") && ReferenceEquals(locals["this"],instance) &&
                Equals(locals["input"],3) && Equals(locals["doubled"],6);
        };
        try
        {
            object? result = instance.GetType().GetMethod("Mark")!.Invoke(instance,[3]);
            if (!hit || !Equals(result,7)) throw new Exception("Breakpoint must inspect the actual argument and initialized local, then continue.");
        }
        finally { SceneScriptBreakpoint.Filter = null; SceneScriptBreakpoint.Handler = null; }
        Console.WriteLine("PASS source breakpoint coverage, actual captured argument and local, continuation result");
        Console.WriteLine("PASS relocated Scene Play source, exact portable PDB checksum, fresh debug script and packaged Release module");
    }
}
