using System.Globalization;
using System.Text.Json;
using System.Text;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.Core;

public sealed record ScriptField(string Name, Type Type, object? Default, string? TargetScript = null, Type? TargetBuiltIn = null)
{
    public bool TypedReference => TargetScript != null || TargetBuiltIn != null;
    public bool Accepts(SceneObject target) => TargetScript != null
        ? target.Components.OfType<ScriptComponent>().Any(c => c.Script == TargetScript)
        : TargetBuiltIn == null || target.Components.Any(c => c.GetType() == TargetBuiltIn);

    public object? Value(ScriptComponent component)
    {
        if (!component.Values.TryGetValue(Name, out var stored)) return Default;
        try { var value = stored.Deserialize(Type) ?? Default; return value is Guid id ? component.InstanceId(id) : value; }
        catch (JsonException) { return Default; } // An override may predate a field type change.
    }
}

public sealed record ScriptDefinition(string Source, IReadOnlyList<ScriptField> Fields);

[VYaml.Annotations.YamlObject]
internal sealed partial record ScriptLocation(string ScriptId);

public sealed class ProjectScripts
{
    internal const string AssemblyFile = "MoNoon.GameScripts.dll";
    internal const string FactoryName = "MoNoon.Generated.ProjectScriptFactory";
    private readonly string root;
    public ProjectScripts(string project) => root = Path.GetFullPath(project);
    internal bool ScenePlay { get; private init; }
    private Func<CSharpCompilation, CSharpCompilation>? instrument;
    internal static ProjectScripts ForScenePlay(string project, Func<CSharpCompilation, CSharpCompilation>? instrument = null) =>
        new(project) { ScenePlay = true, instrument = instrument };
    internal string CompiledPath => Path.Combine(root, ".monoon-compiled", AssemblyFile);
    internal sealed record SourceFile(string Id, string Text, string Path)
    {
        // Old symbols use an ID relative to Scripts. Relocated sources carry a project-relative path.
        public string DebugPath => Path == $"Scripts/{Id}.cs" ? Id + ".cs" : "./" + Path;
    }
    internal sealed record Build(SourceFile[] Sources, Dictionary<string,ScriptDefinition> Components, byte[] Bytes, byte[] Symbols, string Factory);
    private static readonly UTF8Encoding ScriptEncoding = new(false);
    private Build? cache;
    private static readonly Lazy<MetadataReference[]> References = new(() =>
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return platform.Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal) ||
                Path.GetFileName(path) == "netstandard.dll").Append(typeof(GameBehaviour).Assembly.Location)
            .Distinct(StringComparer.Ordinal).Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
    });

    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 100 &&
        SyntaxFacts.IsValidIdentifier(name) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None &&
        name.All(c => char.IsLetterOrDigit(c) || c == '_') &&
        !System.Text.RegularExpressions.Regex.IsMatch(name, @"^(CON|AUX|NUL|PRN|COM[1-9]|LPT[1-9])$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    // Existing components retain their original ID when their source file moves.
    public static bool ValidReference(string? id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var parts = id.Split('/');
        return ValidName(parts[^1]) && parts.All(part => part.Length > 0 && part is not "." and not ".." &&
            !part.Any(c => char.IsControl(c) || "<>:\"\\|?*".Contains(c)));
    }

    internal static bool ReservedFolder(string name) => name is "bin" or "obj" or "Builds" or "build" || name.EndsWith(".app", StringComparison.OrdinalIgnoreCase);
    internal static bool ContentFolder(DirectoryInfo dir) => !dir.Name.StartsWith('.') && !ReservedFolder(dir.Name) &&
        !dir.Attributes.HasFlag(FileAttributes.ReparsePoint) && !File.Exists(Path.Combine(dir.FullName, ".monoon-player")) &&
        !File.Exists(Path.Combine(dir.FullName, "Game", ".monoon-player"));

    public Dictionary<string,string> Locations()
    {
        var locations = new Dictionary<string,string>(StringComparer.Ordinal);
        void Visit(DirectoryInfo directory)
        {
            foreach (var file in directory.EnumerateFiles("*.cs"))
            {
                if (file.Name.StartsWith('.') || !file.Name.EndsWith(".cs", StringComparison.Ordinal) || file.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                string path = Path.GetRelativePath(root, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
                string? id = path.StartsWith("Scripts/", StringComparison.Ordinal) ? path[8..^3] : null;
                var meta = new FileInfo(file.FullName + ".meta");
                if (meta.Exists)
                {
                    if (meta.LinkTarget != null) throw new IOException($"Linked script metadata: {path}");
                    id = ProjectYaml.Read<ScriptLocation>(meta.FullName).ScriptId;
                    if (string.IsNullOrEmpty(id) || !id.Split('/').All(ProjectImport.ValidName)) throw new InvalidDataException($"Invalid script ID in {path}.meta");
                }
                if (id != null && !locations.TryAdd(id, path)) throw new InvalidDataException($"Duplicate script ID '{id}' in {path} and {locations[id]}.");
            }
            foreach (var child in directory.EnumerateDirectories().Where(ContentFolder)) Visit(child);
        }
        Visit(new DirectoryInfo(root));
        return locations;
    }

    public string[] Names() => Locations().Keys.Order(StringComparer.Ordinal).ToArray();
    public string[] Components() => Current().Components.Keys.ToArray();
    public string ChangeStamp() => string.Join("\n", Locations().OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => {
        var info = new FileInfo(Path.Combine(root, p.Value));
        return $"{p.Key}:{p.Value}:{info.LastWriteTimeUtc.Ticks}:{info.Length}";
    }));
    public string RelativePath(string id) => Locations().GetValueOrDefault(id) ?? $"Scripts/{id}.cs";

    public string SourcePath(string name)
    {
        if (!ValidReference(name)) throw new InvalidDataException("Enter a valid component script path.");
        return Path.Combine(root, RelativePath(name));
    }

    public string Read(string name) => File.ReadAllText(SourcePath(name));

    public static string Template(string name) => $$"""
        using Engine.Core;

        public sealed class {{name}} : GameBehaviour
        {
        }
        """;

    public ScriptDefinition Get(string name) => Current().Components.TryGetValue(name,out var def) ? def :
        throw new InvalidDataException($"Scripts/{name}.cs: no attachable public GameBehaviour matching the file name.");

    private SourceFile[] Sources() => Locations().OrderBy(p => p.Key, StringComparer.Ordinal)
        .Select(p => new SourceFile(p.Key, File.ReadAllText(Path.Combine(root, p.Value)), p.Value)).ToArray();
    internal Build Current()
    {
        var files = Sources();
        return cache != null && cache.Sources.SequenceEqual(files) ? cache : cache = Compile(files);
    }

    public void Save(string name, string source, string? previousSource, bool requireComponent = true)
    {
        var path = SourcePath(name);
        if (previousSource == null && Names().Contains(name, StringComparer.OrdinalIgnoreCase))
            throw new IOException($"Scripts/{name}.cs already exists. Choose another name.");
        var files = Sources().Where(f => f.Id != name).Append(new SourceFile(name,source,Path.GetRelativePath(root,path).Replace(Path.DirectorySeparatorChar, '/'))).OrderBy(f => f.Id,StringComparer.Ordinal).ToArray();
        var build = Compile(files);
        if (requireComponent && !build.Components.ContainsKey(name)) throw new InvalidDataException($"Declare a public GameBehaviour matching {name}.cs.");
        var directory = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = Path.Combine(directory.FullName, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, source);
            if (previousSource != null && (!File.Exists(path) || Read(name) != previousSource))
                throw new IOException("The file changed on disk. Reopen the script before saving.");
            File.Move(temporary, path, overwrite: previousSource != null);
            cache = build;
        }
        finally { File.Delete(temporary); }
    }

    private Build Compile(SourceFile[] files)
    {
        SyntaxTree Parse(SourceFile source)
        {
            if (!ScenePlay) return CSharpSyntaxTree.ParseText(source.Text, path: source.DebugPath, encoding: ScriptEncoding);
            // Use the bytes on disk (including the BOM) for the same checksum the IDE verifies.
            var path = Path.GetFullPath(Path.Combine(root, source.Path));
            using var stream = File.OpenRead(path);
            var text = SourceText.From(stream, ScriptEncoding, SourceHashAlgorithm.Sha256);
            if (text.ToString() != source.Text) throw new IOException($"Script changed during compilation: {source.Path}");
            return CSharpSyntaxTree.ParseText(text, path: path);
        }
        var trees = files.Select(Parse).ToArray();
        var compilation = CSharpCompilation.Create("MoNoon.GameScripts", trees, References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: ScenePlay ? OptimizationLevel.Debug : OptimizationLevel.Release,
                deterministic: true));
        var baseType = compilation.GetTypeByMetadataName(typeof(GameBehaviour).FullName!);
        var candidates = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        for (int i = 0; i < trees.Length; i++)
        {
            var model = compilation.GetSemanticModel(trees[i]);
            foreach (var decl in trees[i].GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (decl.Identifier.ValueText != Path.GetFileName(files[i].Id) || !ValidReference(files[i].Id) ||
                    model.GetDeclaredSymbol(decl) is not INamedTypeSymbol t || t.ContainingType != null ||
                    t.IsGenericType || t.IsAbstract || t.DeclaredAccessibility != Accessibility.Public ||
                    !t.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public)) continue;
                for (var ancestor = t.BaseType; ancestor != null; ancestor = ancestor.BaseType)
                    if (SymbolEqualityComparer.Default.Equals(ancestor,baseType))
                    {
                        if (!candidates.TryAdd(files[i].Id,t) && !SymbolEqualityComparer.Default.Equals(candidates[files[i].Id],t))
                            throw new InvalidDataException($"{files[i].Id}.cs: multiple attachable classes match the file name.");
                        break;
                    }
            }
        }
        var defs = new Dictionary<string,ScriptDefinition>(StringComparer.Ordinal);
        var factory = new System.Text.StringBuilder("namespace MoNoon.Generated { public static class ProjectScriptFactory { public static global::Engine.Core.GameBehaviour Create(global::Engine.Core.ScriptComponent component) { switch(component.Script) {");
        foreach (var f in files)
        {
            if (!candidates.TryGetValue(f.Id,out var t)) continue;
            var fields = Fields(compilation,t,candidates);
            defs.Add(f.Id,new(f.Text,fields));
            factory.AppendLine($"case {SymbolDisplay.FormatLiteral(f.Id,true)}: {{ var instance = new {t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}();");
            foreach (var field in fields)
            {
                var member = "instance.@" + field.Name;
                var read = $"component.ReadOverride({SymbolDisplay.FormatLiteral(field.Name,true)}, {member}{(field.TypedReference ? ".ObjectId" : "")})";
                factory.AppendLine($"{member} = {(field.TypedReference ? "new(" + read + ")" : read)};");
            }
            factory.AppendLine("return instance; }");
        }
        factory.AppendLine("default: throw new global::System.IO.InvalidDataException(component.Script); } } } }");
        if (instrument != null) compilation = instrument(compilation);
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(factory.ToString(),path: "MoNoon.Factory.g.cs", encoding: Encoding.UTF8));
        using var bytes = new MemoryStream();
        using var symbols = new MemoryStream();
        var result = compilation.Emit(bytes, symbols, options: new EmitOptions(
            debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: "MoNoon.GameScripts.pdb"));
        if (!result.Success) throw new InvalidDataException(string.Join(Environment.NewLine,
            result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(12)));
        return new(files, defs, bytes.ToArray(), symbols.ToArray(), factory.ToString());
    }

    private static List<ScriptField> Fields(CSharpCompilation compilation, INamedTypeSymbol type,
        Dictionary<string,INamedTypeSymbol> components)
    {
        var fields = new List<ScriptField>();
        var guid = compilation.GetTypeByMetadataName("System.Guid");
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (var level = type; level != null && level.ToDisplayString() != "Engine.Core.GameBehaviour"; level = level.BaseType)
        foreach (var field in level.GetMembers().OfType<IFieldSymbol>())
        {
            if (!used.Add(field.Name) || field.DeclaredAccessibility != Accessibility.Public || field.IsStatic || field.IsReadOnly || field.IsConst) continue;
            Type? fieldType = field.Type.SpecialType switch
            {
                SpecialType.System_Int32 => typeof(int), SpecialType.System_Single => typeof(float),
                SpecialType.System_Double => typeof(double), SpecialType.System_Boolean => typeof(bool),
                SpecialType.System_String => typeof(string),
                _ when field.Type.ToDisplayString() == "Engine.Core.TableReference" => typeof(TableReference),
                _ when field.Type.ToDisplayString() == "Engine.Core.SpriteReference" => typeof(SpriteReference),
                _ when field.Type.ToDisplayString() == "Engine.Core.DataReference" => typeof(DataReference),
                _ when field.Type.ToDisplayString() == "Engine.Core.PrefabReference" => typeof(PrefabReference),
                _ when field.Type.ToDisplayString() == "Engine.Core.SceneReference" => typeof(SceneReference),
                _ when SymbolEqualityComparer.Default.Equals(field.Type, guid) => typeof(Guid), _ => null
            };
            string? targetScript = null;
            Type? targetBuiltIn = null;
            if (field.Type is INamedTypeSymbol { IsGenericType: true } reference &&
                reference.ConstructedFrom.ToDisplayString() == "Engine.Core.ComponentReference<T>")
            {
                var target = reference.TypeArguments[0];
                targetScript = components.FirstOrDefault(c => SymbolEqualityComparer.Default.Equals(c.Value,target)).Key;
                targetBuiltIn = typeof(SceneComponent).Assembly.GetType(target.ToDisplayString());
                if (targetScript == null && (targetBuiltIn == null || targetBuiltIn.IsAbstract || !targetBuiltIn.IsSubclassOf(typeof(SceneComponent))))
                    throw new InvalidDataException($"{type.Name}.{field.Name}: choose a concrete built-in or attachable script as the component target.");
                fieldType = typeof(Guid);
            }
            if (fieldType == null) continue;
            if (field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax variable) continue;
            object? initial = fieldType == typeof(string) ? null : Activator.CreateInstance(fieldType);
            if (variable.Initializer is { } initializer)
            {
                var constant = compilation.GetSemanticModel(variable.SyntaxTree).GetConstantValue(initializer.Value);
                if (!constant.HasValue) continue; // Runtime-initialized fields remain the script's own state.
                initial = Convert.ChangeType(constant.Value, fieldType, CultureInfo.InvariantCulture);
            }
            fields.Add(new ScriptField(field.Name, fieldType, initial,targetScript,targetBuiltIn));
        }
        return fields;
    }
}
