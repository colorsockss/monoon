using System.Runtime.Loader;

namespace Engine.Core;

// One project assembly per Play session. All component instances see the same project types.
internal sealed class ScriptRuntime(ScriptLibrary library) : IDisposable
{
    private AssemblyLoadContext context = new("Project scripts", isCollectible: true);
    private ScriptLibrary library = library;
    private Func<ScriptComponent, GameBehaviour>? factory;

    public GameBehaviour Create(ScriptComponent component)
    {
        try
        {
            if (factory == null)
            {
                var type = library.LoadAssembly(context).GetType(ScriptLibrary.FactoryName, throwOnError: true)!;
                factory = type.GetMethod("Create")!.CreateDelegate<Func<ScriptComponent,GameBehaviour>>();
            }
            return factory(component);
        }
        catch (Exception ex) { throw new InvalidDataException($"{library.RelativePath(component.Script)}: {ex.GetBaseException().Message}",ex); }
    }

    public void Dispose() { factory = null; context.Unload(); }

    // Editor script reload: later instances come from `next`; the old assembly unloads once nothing uses its types.
    internal void Adopt(ScriptRuntime next)
    {
        var old = context;
        (context, library, factory) = (next.context, next.library, next.factory);
        old.Unload();
    }
}
