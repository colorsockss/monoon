using System.Drawing;
using Engine.Core;
using SkiaSharp;

namespace Engine.Desktop;

// Compile SkSL once per sheet. The fixed game clock selects a cell at draw time.
public static class ShaderPixels
{
    private static SKRuntimeEffect Compile(ShaderClip clip)
    {
        string program = """
            float mnSmooth(float low, float high, float x) {
                float a = clamp((x - low) / (high - low), 0.0, 1.0);
                return a * a * (3.0 - 2.0 * a);
            }

            """ + clip.Source + """

            uniform float2 mnSize;
            uniform float mnTime;
            half4 main(float2 pixel) { return shade(pixel / mnSize, mnTime); }
            """;
        return SKRuntimeEffect.Create(program,out var errors) ?? throw new InvalidDataException($"Sprite shader: {errors}");
    }

    public static void Validate(ShaderClip clip)
    {
        using var effect=Compile(clip);
    }

    public static SKBitmap Bake(ShaderClip clip, Color tint)
    {
        using var effect=Compile(clip);
        var uniforms=new SKRuntimeEffectUniforms(effect);
        if (uniforms.Contains("mnSize")) uniforms["mnSize"]=new float[]{clip.Width,clip.Height};
        int columns=clip.Columns, rows=(clip.Frames+columns-1)/columns;
        var info=new SKImageInfo(clip.Width*columns,clip.Height*rows,SKColorType.Rgba8888,SKAlphaType.Premul);
        using var mac=OperatingSystem.IsMacOS() ? new MacShaderContext() : null;
        using var surface=(mac==null ? SKSurface.Create(info) : SKSurface.Create(mac.Graphics,true,info)) ??
            throw new InvalidDataException("Cannot allocate a shader sheet.");
        var canvas=surface.Canvas;
        using var paint=new SKPaint { BlendMode=SKBlendMode.Src };
        canvas.Clear(SKColors.Transparent);
        for (int frame=0; frame<clip.Frames; frame++)
        {
            if (uniforms.Contains("mnTime")) uniforms["mnTime"]=(float)frame/clip.Fps;
            using var shader=effect.ToShader(false,uniforms);
            paint.Shader=shader;
            var cell=clip.Region(frame);
            canvas.Save();
            canvas.Translate(cell.X,cell.Y);
            canvas.DrawRect(0,0,clip.Width,clip.Height,paint);
            canvas.Restore();
        }
        if (tint.ToArgb()!=Color.White.ToArgb())
        {
            using var modulation=new SKPaint { BlendMode=SKBlendMode.Modulate, Color=new(tint.R,tint.G,tint.B,255) };
            canvas.DrawPaint(modulation);
        }
        // SDL texture upload expects straight RGBA. Preserve edge alpha on the conversion.
        var result = new SKBitmap(info.WithAlphaType(SKAlphaType.Unpremul));
        using var image=surface.Snapshot();
        if (image.ReadPixels(result.Info,result.GetPixels(),result.RowBytes,0,0)) return result;
        result.Dispose();
        throw new InvalidDataException("Cannot read procedural shader pixels.");
    }
}

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
