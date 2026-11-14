namespace Engine.Core;

// Renderer-thread ownership. A file has one native resource regardless of the number
// of live objects (or incoming scenes) that need it.
public sealed class SceneResources(Action<string> loadSprite, Action<string> releaseSprite,
    Action<string> loadSound, Action<string> releaseSound, Action<ShaderClip>? loadShader = null, Action<ShaderClip>? releaseShader = null) : IDisposable
{
    internal readonly record struct Asset(string Path, bool Sound, ShaderClip? Shader=null)
    {
        internal Asset(ShaderClip clip) : this("",false,clip) { }
    }
    private readonly HashSet<Asset> resident = [];
    private bool disposed;

    internal static HashSet<Asset> Required(SceneGame scene)
    {
        var paths = new HashSet<Asset>();
        scene.PrepareAssets(p => paths.Add(new(p,false)), p => paths.Add(new(p,true)), s=>paths.Add(new(s)));
        return paths;
    }

    internal void Acquire(Asset asset)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (resident.Contains(asset)) return;
        try
        {
            if (asset.Shader is {} clip) loadShader?.Invoke(clip);
            else (asset.Sound ? loadSound : loadSprite)(asset.Path);
        }
        catch { Release(asset); throw; }
        resident.Add(asset);
    }

    private void Release(Asset asset)
    {
        if (asset.Shader is {} clip) releaseShader?.Invoke(clip);
        else (asset.Sound ? releaseSound : releaseSprite)(asset.Path);
    }

    internal void Retain(HashSet<Asset> needed)
    {
        if (disposed) return;
        foreach (var asset in resident.Where(p => !needed.Contains(p)).ToArray())
        {
            Release(asset);
            resident.Remove(asset);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        Retain([]);
        disposed = true;
    }
}
