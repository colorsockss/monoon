using System.Runtime.InteropServices;
using SkiaSharp;

namespace Engine.Desktop;

// Only used while baking a sheet. Leave SDL's (or Avalonia's) current context intact.
internal sealed class MacShaderContext : IDisposable
{
    private const string OpenGl="/System/Library/Frameworks/OpenGL.framework/OpenGL";
    [DllImport(OpenGl)] private static extern unsafe int CGLChoosePixelFormat(int* attributes,out nint format,out int count);
    [DllImport(OpenGl)] private static extern void CGLReleasePixelFormat(nint format);
    [DllImport(OpenGl)] private static extern int CGLCreateContext(nint format,nint share,out nint context);
    [DllImport(OpenGl)] private static extern nint CGLGetCurrentContext();
    [DllImport(OpenGl)] private static extern int CGLSetCurrentContext(nint context);
    [DllImport(OpenGl)] private static extern void CGLReleaseContext(nint context);
    private readonly nint previous=CGLGetCurrentContext();
    private readonly nint handle;
    public GRContext Graphics {get;}

    public unsafe MacShaderContext()
    {
        // kCGLPFAAccelerated, kCGLPFAOpenGLProfile, kCGLOGLPVersion_3_2_Core
        int* attributes=stackalloc int[] {73,99,0x3200,0};
        if (CGLChoosePixelFormat(attributes,out var format,out _)!=0 || format==0)
            throw new InvalidOperationException("No accelerated offscreen shader surface is available.");
        try
        {
            if (CGLCreateContext(format,0,out handle)!=0 || handle==0)
                throw new InvalidOperationException("Cannot create offscreen shader context.");
        }
        finally { CGLReleasePixelFormat(format); }
        try
        {
            if (CGLSetCurrentContext(handle)!=0) throw new InvalidOperationException("Cannot activate shader context.");
            Graphics=GRContext.CreateGl() ?? throw new InvalidOperationException("Cannot initialize Skia shader rendering.");
        }
        catch { CGLSetCurrentContext(previous); CGLReleaseContext(handle); throw; }
    }

    public void Dispose()
    {
        Graphics.Dispose();
        CGLSetCurrentContext(previous);
        CGLReleaseContext(handle);
    }
}
