using System.Runtime.Loader;
using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Engine.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Engine.Editor;

/// <summary>A public class in a project's Editor/ folder. The editor creates one of each when it loads the folder.</summary>
public interface IEditorExtension
{
    void Register(EditorExtensionRegistry editor);
}

public sealed class EditorExtensionRegistry
{
    internal readonly List<(string Label, Action<EditorContext> Run)> Commands = [];
    internal readonly Dictionary<string, Action<ScriptInspector>> Inspectors = new(StringComparer.Ordinal);
    internal readonly List<(string Title, Func<EditorContext, Control> Build)> Panels = [];
    internal readonly Dictionary<string, Func<FieldDrawer, Control>> Drawers = new(StringComparer.Ordinal);

    /// <summary>
    /// Draws the value of every script field marked with the attribute <paramref name="key"/> (its class name, with or without
    /// "Attribute", e.g. "Stars" for [Stars]) or, when no attribute matches, of the C# type <paramref name="key"/> (e.g. "int",
    /// "Vector2"). The control goes in the value column of the field's row; change the value with <see cref="FieldDrawer.Set"/>.
    /// </summary>
    public void AddFieldDrawer(string key, Func<FieldDrawer, Control> build)
    {
        key = key?.EndsWith("Attribute", StringComparison.Ordinal) == true ? key[..^"Attribute".Length] : key ?? "";
        if (string.IsNullOrWhiteSpace(key) || !Drawers.TryAdd(key, build ?? throw new ArgumentNullException(nameof(build))))
            throw new InvalidDataException($"Invalid or duplicate field drawer '{key}'.");
    }

    /// <summary>Adds Project → Extensions → <paramref name="label"/>. It runs only while the scene is being edited.</summary>
    public void AddMenuCommand(string label, Action<EditorContext> run)
    {
        if (string.IsNullOrWhiteSpace(label) || Commands.Any(c => c.Label == label)) throw new InvalidDataException($"Invalid or duplicate menu command '{label}'.");
        Commands.Add((label, run ?? throw new ArgumentNullException(nameof(run))));
    }

    /// <summary>Replaces the Inspector field rows of the script component <paramref name="script"/> (its script ID, e.g. "CollectCoin").</summary>
    public void AddInspector(string script, Action<ScriptInspector> build)
    {
        if (!ScriptLibrary.ValidReference(script) || !Inspectors.TryAdd(script, build ?? throw new ArgumentNullException(nameof(build))))
            throw new InvalidDataException($"Invalid or duplicate Inspector for '{script}'.");
    }

    /// <summary>Adds a dockable panel listed in the Window menu.</summary>
    public void AddPanel(string title, Func<EditorContext, Control> build)
    {
        if (string.IsNullOrWhiteSpace(title) || Panels.Any(p => p.Title == title)) throw new InvalidDataException($"Invalid or duplicate panel '{title}'.");
        Panels.Add((title, build ?? throw new ArgumentNullException(nameof(build))));
    }
}

/// <summary>What an extension may read and change in the open editor.</summary>
public sealed class EditorContext
{
    private readonly MainWindow window;
    internal EditorContext(MainWindow window) => this.window = window;
    public string ProjectDirectory => window.ExtensionProject;
    public SceneDocument Scene => window.ExtensionScene;
    public SceneObject? Selected => window.ExtensionSelection;
    public bool IsPlaying => window.ExtensionPlaying;
    /// <summary>Places a prefab (project-relative path) at the Scene view centre as one undoable edit. Errors go to the Console and return null.</summary>
    public SceneObject? PlacePrefab(string path) => window.ExtensionPlacePrefab(path);
    /// <summary>Writes an information line to the Console.</summary>
    public void Log(string message) => window.ExtensionLog(message);
    /// <summary>A labelled row on the Inspector's label column, for Inspectors and panels (the built-in Inspector's row).</summary>
    public Control Row(string label, Control input)
    {
        AutomationProperties.SetName(input, label);
        return MainWindow.InspectorRowControl(label, input);
    }

    /// <summary>
    /// Records what the extension changed in the scene since the last record as one Undo step (as an Inspector edit does).
    /// False when nothing changed or while playing.
    /// </summary>
    public bool RecordEdit() => window.ExtensionRecordEdit();

    /// <summary>
    /// Sets the saved value of the script field <paramref name="name"/> of <paramref name="component"/> and records it as one Undo
    /// step. A value equal to the script's default removes the saved key; null also removes it. Values are the field's type
    /// (Vector2 for a vector, a #RRGGBB text for a colour, a name or "A, B" for an enum, a JsonElement for arrays, lists,
    /// dictionaries and structs). Throws for a field the script does not show in the Inspector.
    /// </summary>
    public void SetField(ScriptComponent component, string name, object? value) => window.ExtensionSetField(component, name, value);
}

/// <summary>One script field drawn by an <see cref="EditorExtensionRegistry.AddFieldDrawer"/> drawer.</summary>
public sealed class FieldDrawer
{
    internal FieldDrawer(EditorContext editor, ScriptComponent component, ScriptField field) => (Editor, Component, Field) = (editor, component, field);
    public EditorContext Editor { get; }
    public ScriptComponent Component { get; }
    public ScriptField Field { get; }
    public string Name => Field.Name;
    /// <summary>The current value (the saved one, or the script's default); see <see cref="EditorContext.SetField"/> for the types.</summary>
    public object? Value => Field.Value(Component);
    /// <summary>Saves a new value as one Undo step.</summary>
    public void Set(object? value) => Editor.SetField(Component, Field.Name, value);
}

/// <summary>Builds the Inspector rows of one script component. Rows share the label column of the built-in Inspector.</summary>
public sealed class ScriptInspector
{
    private readonly Panel panel;
    private readonly Action<string> field;
    internal ScriptInspector(EditorContext editor, ScriptComponent component, Panel panel, Action<string> field) =>
        (Editor, Component, this.panel, this.field) = (editor, component, panel, field);
    public EditorContext Editor { get; }
    public ScriptComponent Component { get; }
    /// <summary>The built-in row for a public field of the script, with its usual editing and Undo.</summary>
    public void Field(string name) => field(name);
    /// <summary>A labelled row aligned with the field rows.</summary>
    public void Row(string label, Control input) => panel.Children.Add(Editor.Row(label, input));
    public void Add(Control control) => panel.Children.Add(control);
}

// Compiles Editor/**/*.cs against the editor (MoNoon), Engine.Core and Avalonia into a collectible context.
// Game scripts are a separate compilation that references none of this, so they cannot use an extension or the editor API.
internal sealed class ProjectEditorExtensions : IDisposable
{
    private readonly AssemblyLoadContext context = new("Editor extensions", isCollectible: true);
    internal EditorExtensionRegistry Registry { get; } = new();
    private static readonly Lazy<MetadataReference[]> References = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray());
    private static readonly EnumerationOptions Walk = new() { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.ReparsePoint };

    private static string[] Sources(string root)
    {
        var folder = Path.Combine(root, ProjectScripts.EditorFolder);
        return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.cs", Walk).Order(StringComparer.Ordinal).ToArray() : [];
    }

    internal static string Stamp(string root) => string.Join("\n", Sources(root).Select(path =>
    {
        var info = new FileInfo(path);
        return $"{path}:{info.LastWriteTimeUtc.Ticks}:{info.Length}";
    }));

    /// <summary>Null when the project has no Editor/ C# files.</summary>
    internal static ProjectEditorExtensions? Load(string root)
    {
        var files = Sources(root);
        if (files.Length == 0) return null;
        var trees = files.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path, encoding: Encoding.UTF8)).ToArray();
        var compilation = CSharpCompilation.Create("MoNoon.EditorExtensions", trees, References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug));
        using var assembly = new MemoryStream();
        using var symbols = new MemoryStream();
        var result = compilation.Emit(assembly, symbols, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
        if (!result.Success) throw new InvalidDataException(string.Join(Environment.NewLine,
            result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(12)));
        assembly.Position = symbols.Position = 0;
        var loaded = new ProjectEditorExtensions();
        try
        {
            var types = loaded.context.LoadFromStream(assembly, symbols).GetExportedTypes()
                .Where(t => typeof(IEditorExtension).IsAssignableFrom(t) && t is { IsAbstract: false, IsClass: true }).OrderBy(t => t.FullName, StringComparer.Ordinal);
            foreach (var type in types)
                try { ((IEditorExtension)Activator.CreateInstance(type)!).Register(loaded.Registry); }
                catch (Exception ex) { throw new InvalidDataException($"{ProjectScripts.EditorFolder}/{type.FullName}: {ex.GetBaseException().Message}", ex); }
            return loaded;
        }
        catch { loaded.Dispose(); throw; }
    }

    public void Dispose() => context.Unload();
}
