using System.Drawing;
using Engine.Core;

namespace Engine.Desktop;

public sealed class SpriteVariants<T>(Func<SpriteFilter,Color,T> create) : IDisposable where T : IDisposable
{
    private readonly Dictionary<(SpriteFilter,Color),T> cache = [];
    public T Get(SpriteFilter filter,Color tint)
    {
        if (cache.TryGetValue((filter,tint),out var value)) return value;
        return cache[(filter,tint)] = create(filter,tint);
    }
    public void Invalidate(string path)
    {
        foreach (var key in cache.Keys.Where(k => k.Item1.Image == path || k.Item1.Mask == path).ToArray())
        { cache[key].Dispose(); cache.Remove(key); }
    }
    public void Dispose() { foreach (var item in cache.Values) item.Dispose(); cache.Clear(); }
}
