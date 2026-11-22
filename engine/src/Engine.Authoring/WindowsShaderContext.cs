using System.Runtime.InteropServices;
using SkiaSharp;

namespace Engine.Desktop;

// ANGLE's D3D11 WARP device also works on a build agent without a physical GPU.
// Keep the single process-wide display initialized; each bake owns its EGL surface and context.
internal static class WindowsShaderContext
{
    private const string Angle = "av_libglesv2";
    private const int End = 0x3038, PlatformAngle = 0x3202, PlatformType = 0x3203, D3D11 = 0x3208,
        DeviceType = 0x3209, Warp = 0x320B, SurfaceType = 0x3033, Pbuffer = 1, RenderableType = 0x3040,
        Gles2 = 4, Red = 0x3024, Green = 0x3023, Blue = 0x3022, Alpha = 0x3021, ClientVersion = 0x3098,
        Width = 0x3057, Height = 0x3056;
    [DllImport(Angle)] private static extern nint EGL_GetPlatformDisplayEXT(int platform, nint native, int[] attributes);
    [DllImport(Angle)] private static extern int EGL_Initialize(nint display, out int major, out int minor);
    [DllImport(Angle)] private static extern int EGL_ChooseConfig(nint display, int[] attributes, out nint config, int capacity, out int count);
    [DllImport(Angle)] private static extern nint EGL_CreateContext(nint display, nint config, nint share, int[] attributes);
    [DllImport(Angle)] private static extern nint EGL_CreatePbufferSurface(nint display, nint config, int[] attributes);
    [DllImport(Angle)] private static extern int EGL_MakeCurrent(nint display, nint draw, nint read, nint context);
    [DllImport(Angle)] private static extern int EGL_DestroySurface(nint display, nint surface);
    [DllImport(Angle)] private static extern int EGL_DestroyContext(nint display, nint context);
    [DllImport(Angle)] private static extern int EGL_ReleaseThread();
    [DllImport(Angle)] private static extern int EGL_GetError();
    [DllImport(Angle)] private static extern nint EGL_GetProcAddress([MarshalAs(UnmanagedType.LPStr)] string name);

    private static readonly Lazy<(nint Display, nint Config)> Device = new(() =>
    {
        var display = EGL_GetPlatformDisplayEXT(PlatformAngle, 0, [PlatformType, D3D11, DeviceType, Warp, End]);
        if (display == 0 || EGL_Initialize(display, out _, out _) == 0) throw Error("initialize D3D11 WARP");
        if (EGL_ChooseConfig(display, [SurfaceType, Pbuffer, RenderableType, Gles2, Red, 8, Green, 8, Blue, 8, Alpha, 8, End],
                out var config, 1, out var count) == 0 || count != 1) throw Error("choose EGL config");
        return (display, config);
    });

    private static InvalidOperationException Error(string action) => new($"Cannot {action} for shader baking (EGL 0x{EGL_GetError():X}).");

    internal static SKBitmap Bake(Func<GRContext, SKBitmap> bake) =>
        Task.Factory.StartNew(() =>
        {
            var (display, config) = Device.Value;
            var context = EGL_CreateContext(display, config, 0, [ClientVersion, 2, End]);
            if (context == 0) throw Error("create EGL context");
            nint surface = 0;
            try
            {
                surface = EGL_CreatePbufferSurface(display, config, [Width, 1, Height, 1, End]);
                if (surface == 0 || EGL_MakeCurrent(display, surface, surface, context) == 0) throw Error("bind EGL surface");
                using var api = GRGlInterface.CreateGles(EGL_GetProcAddress) ?? throw Error("resolve GLES");
                using var graphics = GRContext.CreateGl(api) ?? throw Error("create Skia context");
                return bake(graphics);
            }
            finally
            {
                EGL_MakeCurrent(display, 0, 0, 0);
                if (surface != 0) EGL_DestroySurface(display, surface);
                EGL_DestroyContext(display, context);
                EGL_ReleaseThread();
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).GetAwaiter().GetResult();
}
