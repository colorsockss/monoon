using System.Reflection;
using System.Runtime.Loader;

namespace Engine.Core;

// The player loads the factory emitted by the build. Only authoring hosts register a source compiler.
public class ScriptLibrary
{
    internal const string AssemblyFile = "MoNoon.GameScripts.dll";
    internal const string FactoryName = "MoNoon.Generated.ProjectScriptFactory";
    internal static Func<string, ScriptLibrary>? OpenSource { private get; set; }
    protected readonly string root;
    protected internal ScriptLibrary(string project) => root = Path.GetFullPath(project);

    internal static ScriptLibrary Open(string project) =>
        OpenSource != null && !File.Exists(Path.Combine(project, ".monoon-player"))
            ? OpenSource(project) : new ScriptLibrary(project);

    internal string CompiledPath => Path.Combine(root, ".monoon-compiled", AssemblyFile);
    internal virtual Assembly LoadAssembly(AssemblyLoadContext context)
    {
        using var assembly = File.OpenRead(CompiledPath);
        var path = Path.ChangeExtension(CompiledPath, ".pdb");
        using var symbols = File.Exists(path) ? File.OpenRead(path) : null;
        return context.LoadFromStream(assembly, symbols);
    }

    internal virtual void NormalizeFields(SceneDocument scene) { }

    public virtual string RelativePath(string id) => $"Scripts/{id}.cs";
    public virtual ScriptDefinition Get(string name) =>
        throw new InvalidDataException($"Script field descriptions for {name} require the source project.");

    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 100 &&
        (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c == '_') &&
        !Keywords.Contains(name) && !System.Text.RegularExpressions.Regex.IsMatch(name, @"^(CON|AUX|NUL|PRN|COM[1-9]|LPT[1-9])$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    // Reserved C# identifiers cannot name script components. Contextual keywords are valid.
    private static readonly HashSet<string> Keywords = new(("""
        abstract as base bool break byte case catch char checked class const continue decimal default delegate do double
        else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is
        lock long namespace new null object operator out override params private protected public readonly ref return
        sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked
        unsafe ushort using virtual void volatile while __arglist __makeref __reftype __refvalue
        """).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    public static bool ValidReference(string? id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var parts = id.Split('/');
        return ValidName(parts[^1]) && parts.All(part => part.Length > 0 && part is not "." and not ".." &&
            !part.Any(c => char.IsControl(c) || "<>:\"\\|?*".Contains(c)));
    }
}
