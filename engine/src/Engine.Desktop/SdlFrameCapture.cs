using System.Globalization;
using Brutal.SdlApi;
using Engine.Core;

namespace Engine.Desktop;

/// <summary>Opt-in measurements of game work on the presenting thread, shared by Scene Play and the player.</summary>
public sealed class SdlFrameCapture : IDisposable
{
    private readonly StreamWriter output;
    private long updateBytes, start;
    private int frames;

    private SdlFrameCapture(string path)
    {
        output = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        output.WriteLine("tick,active,capacity,spawned,returned,reused,dropped,unbatched_pools,draw_calls,update_bytes,draw_bytes");
    }

    public static SdlFrameCapture? Open() => Environment.GetEnvironmentVariable("MONOON_FRAME_CAPTURE") is { Length: > 0 } path
        ? new(path) : null;

    public void BeginUpdate() => start = GC.GetAllocatedBytesForCurrentThread();

    internal static unsafe void SaveImage(SdlRenderer renderer, string path)
    {
        var surface = Sdl.PInvoke.RenderReadPixels(renderer, null);
        if (surface == null) throw new IOException(Sdl.PInvoke.GetError());
        try
        {
            if (Sdl.PInvoke.SavePNG(surface, path) == 0) throw new IOException(Sdl.PInvoke.GetError());
        }
        finally { Sdl.PInvoke.DestroySurface(surface); }
    }
    public void EndUpdate() => updateBytes += GC.GetAllocatedBytesForCurrentThread() - start;

    public void Frame(SceneGame? world, long calls, long drawStartBytes)
    {
        long drawBytes = GC.GetAllocatedBytesForCurrentThread() - drawStartBytes;
        if (world != null)
        {
            var pool = world.PoolStatistics(out int unbatched);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{world.Time.TickCount},{pool.Active},{pool.Capacity},{pool.Spawned},{pool.Returned},{pool.Reused},{pool.Dropped},{unbatched},{calls},{updateBytes},{drawBytes}"));
            if (++frames % 30 == 0) output.Flush();
        }
        updateBytes = 0;
    }

    public void Dispose() => output.Dispose();
}
