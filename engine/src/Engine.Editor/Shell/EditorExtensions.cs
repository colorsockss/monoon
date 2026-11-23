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
    /// <summary>A labelled row on the Inspector's label column, for Inspectors and panels.</summary>
    public Control Row(string label, Control input)
    {
        var row = new Grid { ColumnDefinitions = EditorTokens.LabelColumns(EditorTokens.FieldLabelWidth), ColumnSpacing = EditorTokens.FieldGap };
        row.Children.Add(new TextBlock { Text = label, Classes = { "fieldLabel" }, TextTrimming = TextTrimming.CharacterEllipsis });
        ToolTip.SetTip(row, label);
        AutomationProperties.SetName(input, label);
        Grid.SetColumn(input, 1);
        row.Children.Add(input);
        return row;
    }
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
