using System.Drawing;
using Engine.Core;

namespace Engine.Desktop;

public sealed class ShaderSheets<T>(Func<ShaderClip,Color,bool,T> create,Action? beforeRelease=null) : IDisposable where T:IDisposable
{
    private readonly Dictionary<(ShaderClip,Color,bool),T> sheets=[];
    public T Get(ShaderClip shader,Color tint,bool additive)
    {
        var key=(shader,tint,additive);
        if (sheets.TryGetValue(key,out var sheet)) return sheet;

        return sheets[key]=create(shader,tint,additive);
    }
    public void Release(ShaderClip shader)
    {
        beforeRelease?.Invoke();
        foreach (var key in sheets.Keys.Where(k=>k.Item1==shader).ToArray())
            if (sheets.Remove(key,out var sheet)) sheet.Dispose();
    }
    public void Dispose()
    {
        beforeRelease?.Invoke();
        foreach (var sheet in sheets.Values) sheet.Dispose();
        sheets.Clear();
    }
}
