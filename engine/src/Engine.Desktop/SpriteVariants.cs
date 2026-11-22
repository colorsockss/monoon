using System.Drawing;
using Engine.Core;

namespace Engine.Desktop;

public sealed class SpriteVariants<T>(Func<SpriteFilter,Color,T> create) : IDisposable where T : IDisposable
{
    private readonly Dictionary<(SpriteFilter,Color),T> cache = [];
    public int TintCacheCapacity { get; init; } = int.MaxValue;
    private readonly LinkedList<(SpriteFilter Filter, Color Color)> tinted = new();

    public T Get(SpriteFilter filter,Color tint)
    {
        if (cache.TryGetValue((filter,tint),out var value)) return value;
        value = create(filter,tint);
        if (TintCacheCapacity != int.MaxValue && tint.ToArgb() != Color.White.ToArgb())
        {
            if (tinted.Count >= TintCacheCapacity && tinted.First is {} oldest)
            {
                cache.Remove(oldest.Value,out var evicted);
                evicted?.Dispose();
                tinted.RemoveFirst();
            }
            tinted.AddLast((filter,tint));
        }
        return cache[(filter,tint)] = value;
    }
    public void Invalidate(string path)
    {
        foreach (var key in cache.Keys.Where(k => k.Item1.Image == path || k.Item1.Mask == path).ToArray())
        { cache[key].Dispose(); cache.Remove(key); tinted.Remove(key); }
    }
    public void Dispose() { foreach (var item in cache.Values) item.Dispose(); cache.Clear(); tinted.Clear(); }
}
