using System.Runtime.Loader;

namespace Engine.Core;

// One project assembly per Play session. All component instances see the same project types.
internal sealed class ScriptRuntime(ProjectScripts library) : IDisposable
{
    private readonly AssemblyLoadContext context = new("Project scripts", isCollectible: true);
    private Func<ScriptComponent, GameBehaviour>? factory;

    public GameBehaviour Create(ScriptComponent component)
    {
        try
        {
            if (factory == null)
            {
                var build = !library.ScenePlay && File.Exists(library.CompiledPath) ? null : library.Current();
                using Stream stream = build == null ? File.OpenRead(library.CompiledPath) : new MemoryStream(build.Bytes, writable: false);
                var pdb = Path.ChangeExtension(library.CompiledPath, ".pdb");
                using Stream? symbols = build != null ? new MemoryStream(build.Symbols, writable: false) :
                    File.Exists(pdb) ? File.OpenRead(pdb) : null;
                var type = context.LoadFromStream(stream, symbols).GetType(ProjectScripts.FactoryName, throwOnError: true)!;
                factory = type.GetMethod("Create")!.CreateDelegate<Func<ScriptComponent,GameBehaviour>>();
            }
            return factory(component);
        }
        catch (Exception ex) { throw new InvalidDataException($"{library.RelativePath(component.Script)}: {ex.GetBaseException().Message}",ex); }
    }

    public void Dispose() { factory = null; context.Unload(); }
}
