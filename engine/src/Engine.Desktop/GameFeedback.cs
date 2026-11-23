using System.Security.Cryptography;
using Brutal.SdlApi;
using Engine.Core;

namespace Engine.Desktop;

/// <summary>Opt-in record of rumble and cursor output (MONOON_FEEDBACK_LOG=&lt;file&gt;), for checks without a gamepad.</summary>
public static class FeedbackLog
{
    private static readonly string? path = Environment.GetEnvironmentVariable("MONOON_FEEDBACK_LOG") is { Length: > 0 } p ? p : null;
    public static void Write(string line) { if (path != null) File.AppendAllText(path, line + "\n"); }
}

/// <summary>Cursor pixels as straight RGBA, shared by the player's SDL cursor and the editor's Scene Play cursor.</summary>
public static class CursorPixels
{
    public static unsafe RgbaImage Load(GameCursor cursor)
    {
        var loaded = Sdl.PInvoke.LoadPNG(cursor.Path);
        if (loaded == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
        var rgba = Sdl.PInvoke.ConvertSurface(loaded, SdlPixelFormat.Rgba32);
        Sdl.PInvoke.DestroySurface(loaded);
        if (rgba == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
        try
        {
            int w = rgba->w, h = rgba->h;
            var pixels = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                new ReadOnlySpan<byte>((byte*)rgba->pixels + y * rgba->pitch, w * 4).CopyTo(pixels.AsSpan(y * w * 4));
            var image = new RgbaImage(w, h, pixels);
            FeedbackLog.Write($"cursor file={Path.GetFileName(cursor.Path)} size={w}x{h} hot={cursor.HotX},{cursor.HotY} rgba-sha256={Hash(image)}");
            return image;
        }
        finally { Sdl.PInvoke.DestroySurface(rgba); }
    }

    // Fully transparent pixels count as zero: their colour is invisible and premultiplying platforms drop it.
    private static string Hash(RgbaImage image)
    {
        var copy = (byte[])image.Pixels.Clone();
        for (int i = 0; i < copy.Length; i += 4) if (copy[i + 3] == 0) copy.AsSpan(i, 4).Clear();
        return Convert.ToHexStringLower(SHA256.HashData(copy));
    }
}

/// <summary>Applies the session's custom cursor to the SDL window, or the system cursor again.</summary>
internal sealed class WindowCursor : IDisposable
{
    private SdlCursor custom;
    private bool active;
    private int revision;

    public unsafe void Sync(SessionState? state)
    {
        if (state == null || state.CursorRevision == revision) return;
        revision = state.CursorRevision;
        if (state.Cursor is not { } cursor)
        {
            Restore();
            FeedbackLog.Write("cursor system");
            return;
        }
        var image = CursorPixels.Load(cursor);
        fixed (byte* pixels = image.Pixels)
        {
            var surface = Sdl.PInvoke.CreateSurfaceFrom(image.Width, image.Height, SdlPixelFormat.Rgba32, pixels, image.Width * 4);
            if (surface == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
            SdlCursor next;
            try { next = Sdl.Mouse.CreateColorCursor(surface, cursor.HotX, cursor.HotY); }
            finally { Sdl.PInvoke.DestroySurface(surface); }
            if (!Sdl.Mouse.SetCursor(next)) { Sdl.Mouse.DestroyCursor(next); throw new InvalidOperationException(Sdl.PInvoke.GetError()); }
            if (active) Sdl.Mouse.DestroyCursor(custom);
            (custom, active) = (next, true);
        }
    }

    private void Restore()
    {
        if (!active) return;
        Sdl.Mouse.SetCursor(Sdl.Mouse.GetDefaultCursor());
        Sdl.Mouse.DestroyCursor(custom);
        active = false;
    }

    public void Dispose() => Restore();
}
