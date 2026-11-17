using System.Globalization;
using System.Numerics;
using Microsoft.CodeAnalysis.Operations;
using System.Text;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.Core;

[VYaml.Annotations.YamlObject]
internal sealed partial record ScriptLocation(string ScriptId);

public sealed class ProjectScripts : ScriptLibrary
{
    // Register only when an authoring host loads this assembly; the player never references it.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Installs the source-project provider for authoring hosts.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterSourceLibrary() => OpenSource = path => new ProjectScripts(path);

    public ProjectScripts(string project) : base(project) { }
    internal bool ScenePlay { get; private init; }
    private Func<CSharpCompilation, CSharpCompilation>? instrument;
    internal static ProjectScripts ForScenePlay(string project, Func<CSharpCompilation, CSharpCompilation>? instrument = null) =>
        new(project) { ScenePlay = true, instrument = instrument };
    internal override System.Reflection.Assembly LoadAssembly(System.Runtime.Loader.AssemblyLoadContext context)
    {
        if (!ScenePlay && File.Exists(CompiledPath)) return base.LoadAssembly(context);
        var build = Current();
        using var assembly = new MemoryStream(build.Bytes, writable: false);
        using var symbols = new MemoryStream(build.Symbols, writable: false);
        return context.LoadFromStream(assembly, symbols);
    }

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
    public override string RelativePath(string id) => Locations().GetValueOrDefault(id) ?? $"Scripts/{id}.cs";

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

    public override ScriptDefinition Get(string name) => Current().Components.TryGetValue(name,out var def) ? def :
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
            var events = new List<string>();
            var handlers = new List<string>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var level = t; level != null && !SymbolEqualityComparer.Default.Equals(level, baseType); level = level.BaseType)
                foreach (var group in level.GetMembers().GroupBy(m => m.Name))
                {
                    if (!names.Add(group.Key)) continue;
                    foreach (var member in group)
                    {
                        if (member is IEventSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } signal &&
                            SymbolEqualityComparer.Default.Equals(signal.Type, compilation.GetTypeByMetadataName("System.Action")))
                            events.Add(signal.Name);
                        if (member is not IMethodSymbol method || !method.GetAttributes().Any(a =>
                            a.AttributeClass?.ToDisplayString() == typeof(GameEventHandlerAttribute).FullName)) continue;
                        if (method.DeclaredAccessibility != Accessibility.Public || method.IsStatic || method.IsGenericMethod ||
                            method.IsAsync || !method.ReturnsVoid || method.MethodKind != MethodKind.Ordinary ||
                            method.Parameters is not [{ RefKind: RefKind.None } parameter] ||
                            parameter.Type.ToDisplayString() != typeof(GameEventContext).FullName)
                            throw new InvalidDataException($"{f.Id}.{method.Name}: expected public void handler(GameEventContext message).");
                        handlers.Add(method.Name);
                    }
                }
            defs.Add(f.Id,new(f.Text,fields) { Events = events.ToArray(), Handlers = handlers.ToArray() });
            factory.AppendLine($"case {SymbolDisplay.FormatLiteral(f.Id,true)}: {{ var instance = new {t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}();");
            foreach (var field in fields)
            {
                var member = "instance.@" + field.Name;
                var read = $"component.ReadOverride({SymbolDisplay.FormatLiteral(field.Name,true)}, {member}{(field.TypedReference ? ".ObjectId" : "")})";
                if (field.IsAssetList)
                    read = $"component.ReadAssetList({SymbolDisplay.FormatLiteral(field.Name,true)}, {member}, static id => new global::{field.Type.GetElementType()!.FullName}(id))";
                else if (field.EnumNames != null || field.Type == typeof(Vector2))
                {
                    string method = field.EnumNames != null ? "ReadEnum" : "ReadVector";
                    read = $"component.{method}({SymbolDisplay.FormatLiteral(field.Name,true)}, {member})";
                }
                factory.AppendLine($"{member} = {(field.TypedReference ? "new(" + read + ")" : read)};");
            }
            foreach (var name in events)
                factory.AppendLine($"instance.@{name} += ((global::Engine.Core.GameBehaviour)instance).GameEventPublisher({SymbolDisplay.FormatLiteral(name,true)});");
            foreach (var name in handlers)
                factory.AppendLine($"((global::Engine.Core.GameBehaviour)instance).RegisterGameEventHandler({SymbolDisplay.FormatLiteral(name,true)}, instance.@{name});");
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

    private static Type? AssetType(ITypeSymbol symbol) =>
        ScriptField.AssetExtensions.Keys.FirstOrDefault(t => t.FullName == symbol.ToDisplayString());

    private static Vector2? VectorDefault(IOperation? operation)
    {
        if (operation == null || operation is IDefaultValueOperation) return Vector2.Zero;
        if (operation is IPropertyReferenceOperation property &&
            property.Property.ContainingType.ToDisplayString() == typeof(Vector2).FullName)
            return property.Property.Name switch { "Zero" => Vector2.Zero, "One" => Vector2.One,
                "UnitX" => Vector2.UnitX, "UnitY" => Vector2.UnitY, _ => null };
        if (operation is not IObjectCreationOperation creation) return null;
        var values = new float[2];
        foreach (var argument in creation.Arguments)
        {
            if (argument.Parameter?.Type.SpecialType != SpecialType.System_Single ||
                argument.Value.ConstantValue is not { HasValue: true, Value: float value } || !float.IsFinite(value))
                return null;
            values[argument.Parameter.Ordinal] = value;
        }
        var vector = creation.Arguments.Length == 1 ? new Vector2(values[0]) : new Vector2(values[0],values[1]);
        if (creation.Initializer != null)
            foreach (var assignment in creation.Initializer.Initializers)
            {
                if (assignment is not ISimpleAssignmentOperation { Target: IFieldReferenceOperation target,
                    Value.ConstantValue: { HasValue: true, Value: float value } } || !float.IsFinite(value)) return null;
                if (target.Field.Name == "X") vector.X = value;
                else if (target.Field.Name == "Y") vector.Y = value;
                else return null;
            }
        return vector;
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
            if (field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax variable) continue;
            var model = compilation.GetSemanticModel(variable.SyntaxTree);
            var expression = variable.Initializer?.Value;
            if (field.Type is IArrayTypeSymbol { Rank: 1 } array)
            {
                var element = SymbolEqualityComparer.Default.Equals(array.ElementType, guid) ? typeof(Guid)
                    : AssetType(array.ElementType);
                if (element != null) fields.Add(new(field.Name, element.MakeArrayType(), Array.Empty<Guid>()));
                continue;
            }
            if (field.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration)
            {
                var members = enumeration.GetMembers().OfType<IFieldSymbol>().Where(m => m.HasConstantValue).ToArray();
                var constant = expression == null ? new Optional<object?>(null) : model.GetConstantValue(expression);
                if (!constant.HasValue) continue;
                var initialName = members.FirstOrDefault(m =>
                    Convert.ToDecimal(m.ConstantValue) == Convert.ToDecimal(constant.Value))?.Name;
                fields.Add(new(field.Name, typeof(string), initialName, EnumNames: members.Select(m => m.Name).ToArray()));
                continue;
            }
            if (field.Type.ToDisplayString() == typeof(Vector2).FullName)
            {
                if (VectorDefault(expression == null ? null : model.GetOperation(expression)) is Vector2 vector)
                    fields.Add(new(field.Name, typeof(Vector2), vector));
                continue;
            }
            Type? fieldType = field.Type.SpecialType switch
            {
                SpecialType.System_Int32 => typeof(int), SpecialType.System_Single => typeof(float),
                SpecialType.System_Double => typeof(double), SpecialType.System_Boolean => typeof(bool),
                SpecialType.System_String => typeof(string),
                _ when SymbolEqualityComparer.Default.Equals(field.Type, guid) => typeof(Guid), _ => AssetType(field.Type)
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

public static class SceneScriptExtensions
{
    public static ProjectScripts OpenScripts(this SceneDocument scene, string root) => new(root);
}
