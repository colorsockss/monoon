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

public sealed partial class ProjectScripts : ScriptLibrary
{
    // Register only when an authoring host loads this assembly; the player never references it.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Installs the source-project provider and the full YAML formatter set for authoring hosts.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterSourceLibrary()
    {
        OpenSource = OpenAuthoringProject;
        ProjectYaml.ToolFormatters = VYaml.Serialization.BuiltinResolver.Instance;
    }

    // Scene and nested prefab loads share one source compilation on the owning thread.
    // Current() verifies the source contents before reusing it.
    [ThreadStatic] private static ProjectScripts? lastProject;
    private static ProjectScripts OpenAuthoringProject(string path) =>
        lastProject?.root == Path.GetFullPath(path) ? lastProject : lastProject = new ProjectScripts(path);

    public ProjectScripts(string project) : base(project) { }
    internal bool ScenePlay { get; private init; }
    private Func<CSharpCompilation, CSharpCompilation>? instrument;
    internal static ProjectScripts ForScenePlay(string project, Func<CSharpCompilation, CSharpCompilation>? instrument = null) =>
        new(project) { ScenePlay = true, instrument = instrument };
    // Test scripts live in Tests/ and are compiled with the game scripts only by `monoon test project`; builds never see them.
    internal const string TestsFolder = "Tests";
    // Editor extensions live in Editor/ and are compiled by the editor against its API; game scripts and builds never see them.
    internal const string EditorFolder = "Editor";
    internal bool WithTests { get; private init; }
    internal static ProjectScripts ForTests(string project) => new(project) { WithTests = true };
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
    internal sealed record Build(SourceFile[] Sources, Dictionary<string,ScriptDefinition> Components, byte[] Bytes, byte[] Symbols, string Factory)
    {
        // Scanned only when a game build asks; holds the compilation until then.
        internal required Lazy<Dictionary<EngineFeature, string>> Features { get; init; }
        // Editor-only field presentation, read from the source symbols (the game assembly has none of it).
        public Dictionary<string, ScriptInspectorInfo> Inspector { get; init; } = [];
    }
    // Header, tooltip, range and the fields the Inspector leaves out (with the reason), for one attachable script.
    public ScriptInspectorInfo? Inspector(string name) => Current().Inspector.GetValueOrDefault(name);
    private static readonly UTF8Encoding ScriptEncoding = new(false);
    private Build? cache;
    private static readonly Lazy<MetadataReference[]> References = new(() =>
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return platform.Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal) ||
                Path.GetFileName(path) == "netstandard.dll").Append(typeof(GameBehaviour).Assembly.Location)
            // The PDB lists references in order and the host's TPA order differs (editor app vs CLI): sort for identical builds.
            .Distinct(StringComparer.Ordinal).OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
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
            foreach (var child in directory.EnumerateDirectories().Where(ContentFolder))
                if (Path.GetRelativePath(root, child.FullName) is not (TestsFolder or EditorFolder)) Visit(child);
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

    private SourceFile[] Sources()
    {
        var files = Locations().OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new SourceFile(p.Key, File.ReadAllText(Path.Combine(root, p.Value)), p.Value));
        var tests = Path.Combine(root, TestsFolder);
        if (WithTests && Directory.Exists(tests))
            files = files.Concat(Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(path =>
            {
                var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
                return new SourceFile(relative[..^3], File.ReadAllText(path), relative);
            }));
        return files.ToArray();
    }
    private static readonly CSharpParseOptions DebugDraw = CSharpParseOptions.Default.WithPreprocessorSymbols(ScriptDebug.Symbol);
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
            // Scene Play keeps tick.Debug calls (roadmap 16.14); other compilations drop them.
            return CSharpSyntaxTree.ParseText(text, DebugDraw, path: path);
        }
        var trees = files.Select(Parse).ToArray();
        var references = WithTests ? References.Value.Append(MetadataReference.CreateFromFile(typeof(ProjectTests).Assembly.Location)) : References.Value;
        var compilation = CSharpCompilation.Create("MoNoon.GameScripts", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: ScenePlay || WithTests ? OptimizationLevel.Debug : OptimizationLevel.Release,
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
        var inspector = new Dictionary<string,ScriptInspectorInfo>(StringComparer.Ordinal);
        var accessors = new System.Text.StringBuilder();
        int accessorCount = 0;
        var factory = new System.Text.StringBuilder("namespace MoNoon.Generated { public static class ProjectScriptFactory { public static global::Engine.Core.GameBehaviour Create(global::Engine.Core.ScriptComponent component) { switch(component.Script) {");
        foreach (var f in files)
        {
            if (!candidates.TryGetValue(f.Id,out var t)) continue;
            var info = new ScriptInspectorInfo();
            var fields = Fields(compilation,t,candidates,info);
            inspector.Add(f.Id, info);
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
                // [SerializeField] private fields: an UnsafeAccessor ref, no reflection (Native AOT safe).
                if (info.Declaring.TryGetValue(field.Name, out var symbol) && symbol.DeclaredAccessibility != Accessibility.Public)
                {
                    var accessor = $"__field{accessorCount++}_{field.Name}";
                    accessors.AppendLine($"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = {SymbolDisplay.FormatLiteral(field.Name,true)})] " +
                        $"private static extern ref {symbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {accessor}({symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} instance);");
                    member = accessor + "(instance)";
                }
                var key = SymbolDisplay.FormatLiteral(field.Name,true);
                if (field.FormerNames.Length != 0)
                {
                    var aliases = string.Join(", ", field.FormerNames.Select(n => SymbolDisplay.FormatLiteral(n,true)));
                    key = $"component.SerializedFieldName({key}, {aliases})";
                }
                if (info.Reads.TryGetValue(field.Name, out var statement)) { factory.AppendLine(statement(key, member)); continue; }
                var read = $"component.ReadOverride({key}, {member}{(field.TypedReference ? ".ObjectId" : "")})";
                if (field.IsAssetList)
                    read = $"component.ReadAssetList({key}, {member}, static id => new global::{field.Type.GetElementType()!.FullName}(id))";
                else if (field.EnumNames != null || field.Type == typeof(Vector2))
                {
                    string method = field.EnumNames != null ? "ReadEnum" : "ReadVector";
                    read = $"component.{method}({key}, {member})";
                }
                factory.AppendLine($"{member} = {(field.TypedReference ? "new(" + read + ")" : read)};");
            }
            foreach (var name in events)
                factory.AppendLine($"instance.@{name} += ((global::Engine.Core.GameBehaviour)instance).GameEventPublisher({SymbolDisplay.FormatLiteral(name,true)});");
            foreach (var name in handlers)
                factory.AppendLine($"((global::Engine.Core.GameBehaviour)instance).RegisterGameEventHandler({SymbolDisplay.FormatLiteral(name,true)}, instance.@{name});");
            factory.AppendLine("return instance; }");
        }
        factory.AppendLine("default: throw new global::System.IO.InvalidDataException(component.Script); } }");
        factory.Append(accessors).AppendLine("} }");
        var scripts = compilation;
        if (instrument != null) compilation = instrument(compilation);
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(factory.ToString(),path: "MoNoon.Factory.g.cs", encoding: Encoding.UTF8));
        using var bytes = new MemoryStream();
        using var symbols = new MemoryStream();
        var result = compilation.Emit(bytes, symbols, options: new EmitOptions(
            debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: "MoNoon.GameScripts.pdb"));
        if (!result.Success) throw new InvalidDataException(string.Join(Environment.NewLine,
            result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(12)));
        return new(files, defs, bytes.ToArray(), symbols.ToArray(), factory.ToString()) { Features = new(() => FeatureUses(scripts, files)), Inspector = inspector };
    }

    private static bool HasAttribute(ISymbol symbol, Type attribute) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attribute.FullName);

    private static bool Supported(ITypeSymbol type) => type is IArrayTypeSymbol array ? array.ElementType.ToDisplayString() == "System.Guid" || AssetType(array.ElementType) != null :
        type is INamedTypeSymbol { TypeKind: TypeKind.Enum } ||
        type.ToDisplayString() == typeof(Vector2).FullName || AssetType(type) != null ||
        type.SpecialType is SpecialType.System_Int32 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Boolean or SpecialType.System_String ||
        type.ToDisplayString() == "System.Guid" || type is INamedTypeSymbol { IsGenericType: true } g && g.ConstructedFrom.ToDisplayString() == "Engine.Core.ComponentReference<T>";

    // [Conditional] attributes stay on the source symbols although the compiler leaves them out of the assembly.
    private static ScriptFieldUi Presentation(IFieldSymbol field)
    {
        AttributeData? Find(Type attribute) => field.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == attribute.FullName);
        static float? Number(AttributeData? a, int index) =>
            a != null && index < a.ConstructorArguments.Length && a.ConstructorArguments[index].Value is { } v ? Convert.ToSingle(v, CultureInfo.InvariantCulture) : null;
        static string? Text(AttributeData? a) => a is { ConstructorArguments.Length: > 0 } ? a.ConstructorArguments[0].Value as string : null;
        var range = Find(typeof(RangeAttribute));
        var space = Find(typeof(SpaceAttribute));
        var lines = Find(typeof(MultilineAttribute));
        return new(Text(Find(typeof(TooltipAttribute))) ?? Summary(field.GetDocumentationCommentXml()), Text(Find(typeof(HeaderAttribute))),
            space == null ? null : Number(space, 0) ?? 8, Number(range, 0) ?? Number(Find(typeof(MinAttribute)), 0), Number(range, 1),
            lines == null ? null : (int)(Number(lines, 0) ?? 3), Find(typeof(HideInInspectorAttribute)) != null)
        {
            // 13.13: field drawers match an attribute class name (without "Attribute") or the C# type name.
            Attributes = field.GetAttributes().Select(a => a.AttributeClass?.Name is { } n && n.EndsWith("Attribute", StringComparison.Ordinal) ? n[..^9] : a.AttributeClass?.Name ?? "").ToArray(),
            TypeName = field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
        };
    }

    private static string? Summary(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return null;
        try
        {
            var text = System.Xml.Linq.XElement.Parse(xml).Element("summary")?.Value;
            return string.IsNullOrWhiteSpace(text) ? null : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
        catch (System.Xml.XmlException) { return null; }
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
        Dictionary<string,INamedTypeSymbol> components, ScriptInspectorInfo info)
    {
        var fields = new List<ScriptField>();
        var guid = compilation.GetTypeByMetadataName("System.Guid");
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (var level = type; level != null && level.ToDisplayString() != "Engine.Core.GameBehaviour"; level = level.BaseType)
        foreach (var field in level.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsImplicitlyDeclared || !used.Add(field.Name)) continue;
            var declarator = field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as VariableDeclaratorSyntax;
            bool shown = field.DeclaredAccessibility == Accessibility.Public || HasAttribute(field, typeof(SerializeFieldAttribute));
            // Fields the Inspector leaves out, for the Debug view; Expected ones also get the grey line under the section.
            void Skip(string reason, bool expected) => info.Skipped.Add(new(field.Name,
                field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), reason, declarator?.Initializer?.Value.ToString(), expected));
            if (field.IsConst) { Skip("const", false); continue; }
            if (field.IsStatic) { Skip("static", false); continue; }
            if (field.IsReadOnly) { Skip("readonly", shown); continue; }
            if (!shown) { Skip("private: add [SerializeField] to show it", false); continue; }
            if (declarator is not { } variable) continue;
            var model = compilation.GetSemanticModel(variable.SyntaxTree);
            var expression = variable.Initializer?.Value;
            int before = fields.Count;
            AddField();
            if (fields.Count == before) Skip(Supported(field.Type) || SupportedMore(field.Type) ? "set at run time (not a constant initializer)" :
                field.Type is INamedTypeSymbol { TypeKind: TypeKind.Struct } s && s.DeclaringSyntaxReferences.Length > 0 && !s.IsSerializable
                    ? "add [System.Serializable] to its struct to show it" : "unsupported type", true);
            continue;
            // The former loop body; it returns without adding a field the Inspector cannot show.
            void AddField()
            {
            void Add(ScriptField description)
            {
                info.Declaring[field.Name] = field;
                info.Fields[field.Name] = Presentation(field);
                var names = field.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == typeof(FormerlySerializedAsAttribute).FullName)
                    .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string).ToArray();
                if (names.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException($"{type.Name}.{field.Name}: a former field name cannot be empty.");
                // 13.8: [ColorUsage] edits a string as a colour.
                if (description.Type == typeof(string) && description.EnumNames == null && description.ColorAlpha == null &&
                    field.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == typeof(ColorUsageAttribute).FullName) is { } usage)
                    description = description with { ColorAlpha = usage.ConstructorArguments is not [{ Value: false }] };
                fields.Add(description with { FormerNames = names.Select(n => n!).ToArray() });
            }
            if (AddMoreField(field, variable.Initializer == null ? null : (model.GetOperation(variable.Initializer) as IFieldInitializerOperation)?.Value, Add, info)) return;
            if (field.Type is IArrayTypeSymbol { Rank: 1 } array)
            {
                var element = SymbolEqualityComparer.Default.Equals(array.ElementType, guid) ? typeof(Guid)
                    : AssetType(array.ElementType);
                if (element != null) Add(new(field.Name, element.MakeArrayType(), Array.Empty<Guid>()));
                return;
            }
            if (field.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration)
            {
                var members = enumeration.GetMembers().OfType<IFieldSymbol>().Where(m => m.HasConstantValue).ToArray();
                var constant = expression == null ? new Optional<object?>(null) : model.GetConstantValue(expression);
                if (!constant.HasValue) return;
                var initialName = members.FirstOrDefault(m =>
                    Convert.ToDecimal(m.ConstantValue) == Convert.ToDecimal(constant.Value))?.Name;
                Add(new(field.Name, typeof(string), initialName, EnumNames: members.Select(m => m.Name).ToArray()));
                return;
            }
            if (field.Type.ToDisplayString() == typeof(Vector2).FullName)
            {
                if (VectorDefault(expression == null ? null : model.GetOperation(expression)) is Vector2 vector)
                    Add(new(field.Name, typeof(Vector2), vector));
                return;
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
            if (fieldType == null) return;
            object? initial = fieldType == typeof(string) ? null : Activator.CreateInstance(fieldType);
            if (variable.Initializer is { } initializer)
            {
                var constant = compilation.GetSemanticModel(variable.SyntaxTree).GetConstantValue(initializer.Value);
                if (!constant.HasValue) return; // Runtime-initialized fields remain the script's own state.
                initial = Convert.ChangeType(constant.Value, fieldType, CultureInfo.InvariantCulture);
            }
            Add(new ScriptField(field.Name, fieldType, initial,targetScript,targetBuiltIn));
            }
        }
        var owners = fields.ToDictionary(f => f.Name, f => f.Name, StringComparer.Ordinal);
        foreach (var field in fields)
        foreach (var old in field.FormerNames)
            if (!owners.TryAdd(old, field.Name))
                throw new InvalidDataException($"{type.Name}: serialized name '{old}' is claimed by '{owners[old]}' and '{field.Name}'.");
        return fields;
    }
}

public static class SceneScriptExtensions
{
    public static ProjectScripts OpenScripts(this SceneDocument scene, string root) => new(root);
}
