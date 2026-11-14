using System.Runtime.InteropServices;
using SkiaSharp;

namespace Engine.Desktop;

internal static class LinuxShaderContext
{
    private const string Mesa = "libOSMesa.so.8";
    private const uint GlRgba = 0x1908, GlUnsignedByte = 0x1401;

    [DllImport(Mesa)] private static extern nint OSMesaCreateContextExt(uint format, int depth, int stencil, int accum, nint share);
    [DllImport(Mesa)] private static extern unsafe byte OSMesaMakeCurrent(nint context, uint* buffer, uint type, int width, int height);
    [DllImport(Mesa)] private static extern nint OSMesaGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string name);
    [DllImport(Mesa)] private static extern void OSMesaDestroyContext(nint context);

    internal static SKBitmap Bake(Func<GRContext, SKBitmap> bake)
    {
        // Mesa binds GL to the current thread. Leave the editor/SDL rendering thread's context intact.
        return Task.Factory.StartNew(Render, CancellationToken.None, TaskCreationOptions.LongRunning,
            TaskScheduler.Default).GetAwaiter().GetResult();

        unsafe SKBitmap Render()
        {
            nint context = OSMesaCreateContextExt(GlRgba, 0, 0, 0, 0);
            if (context == 0) throw new InvalidOperationException("Cannot create the offscreen Mesa context.");
            try
            {
                uint pixel = 0;
                if (OSMesaMakeCurrent(context, &pixel, GlUnsignedByte, 1, 1) == 0)
                    throw new InvalidOperationException("Cannot activate the offscreen Mesa context.");
                using var api = GRGlInterface.CreateOpenGl(OSMesaGetProcAddress)
                    ?? throw new InvalidOperationException("Cannot resolve Mesa OpenGL functions.");
                using var graphics = GRContext.CreateGl(api)
                    ?? throw new InvalidOperationException("Cannot initialize Mesa shader rendering.");
                return bake(graphics);
            }
            finally { OSMesaDestroyContext(context); }
        }
    }
}
