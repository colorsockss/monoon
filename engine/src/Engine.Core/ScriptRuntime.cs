using System.Runtime.Loader;

namespace Engine.Core;

// One project assembly per Play session. All component instances see the same project types.
internal sealed class ScriptRuntime(ScriptLibrary library) : IDisposable
{
    private readonly AssemblyLoadContext context = new("Project scripts", isCollectible: true);
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
}
