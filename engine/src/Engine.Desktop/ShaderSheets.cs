using System.Drawing;
using Engine.Core;

namespace Engine.Desktop;

public sealed class ShaderSheets<T>(Func<ShaderClip,Color,bool,T> create,Action? beforeRelease=null) : IDisposable where T:IDisposable
{
    private readonly Dictionary<(ShaderClip Clip,Color Tint,bool Additive),(T Sheet,SpriteRegion[] Cells)> sheets=new(SizeKey.Instance);
    public T Get(ShaderClip shader,Color tint,bool additive) => Get(shader,tint,additive,0,out _);

    // Draws read the frame's source cell from the list made with the sheet, so a draw allocates no SpriteRegion.
    public T Get(ShaderClip shader,Color tint,bool additive,int frame,out SpriteRegion cell)
    {
        var key=(shader,tint,additive);
        if (!sheets.TryGetValue(key,out var entry))
        {
            var cells=new SpriteRegion[shader.Frames];
            for (int i=0;i<cells.Length;i++) cells[i]=shader.Region(i);
            sheets[key]=entry=(create(shader,tint,additive),cells);
        }
        cell=(uint)frame<(uint)entry.Cells.Length ? entry.Cells[frame] : shader.Region(frame);
        return entry.Sheet;
    }
    public void Release(ShaderClip shader)
    {
        beforeRelease?.Invoke();
        foreach (var key in sheets.Keys.Where(k=>k.Clip==shader).ToArray())
            if (sheets.Remove(key,out var entry)) entry.Sheet.Dispose();
    }
    public void Dispose()
    {
        beforeRelease?.Invoke();
        foreach (var entry in sheets.Values) entry.Sheet.Dispose();
        sheets.Clear();
    }

    // The record's own hash covers the whole SkSL source on every lookup. Equal clips have equal sizes, so hashing
    // the sizes keeps the record's value equality (and which sheet a clip shares) unchanged.
    private sealed class SizeKey : IEqualityComparer<(ShaderClip Clip,Color Tint,bool Additive)>
    {
        public static readonly SizeKey Instance=new();
        public bool Equals((ShaderClip Clip,Color Tint,bool Additive) a,(ShaderClip Clip,Color Tint,bool Additive) b) => a.Equals(b);
        public int GetHashCode((ShaderClip Clip,Color Tint,bool Additive) k) =>
            HashCode.Combine(k.Clip.Width,k.Clip.Height,k.Clip.Frames,k.Clip.Fps,k.Clip.Sheet,k.Clip.Source?.Length,k.Tint,k.Additive);
    }
}
