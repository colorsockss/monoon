using System.Drawing;
using Brutal.SdlApi;
using Engine.Core;

namespace Engine.Desktop;

internal sealed class SdlDesktopDisplay
    (SdlWindow window, SdlRenderer renderer, SessionState? session, DesktopPreferences initial) : IDesktopDisplay
{
    private DesktopPreferences saved = initial;
    private Size windowedSize = initial.WindowSize;
    public bool Fullscreen => (window.GetFlags() & SdlWindowFlags.Fullscreen) != 0;
    public bool VSync => renderer.GetRenderVSync(out var interval) && interval != 0;

    internal static DesktopPreferences Read(SessionState? session, Size logicalSize)
    {
        try
        {
            if (session != null) return session.Storage.LoadDisplay(logicalSize);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { session?.Log(ex.Message, GameLogLevel.Error); }
        return new(logicalSize, false, true);
    }

    internal void Restore()
    {
        renderer.SetRenderVSync(saved.VSync ? 1 : 0);
        if (saved.Fullscreen) SetFullscreen(true);
    }

    public bool SetFullscreen(bool fullscreen)
    {
        if (Fullscreen == fullscreen) return true;
        if (fullscreen) RememberWindowSize();
        if (!window.SetFullscreen(fullscreen)) return false;
        window.Sync();
        if (Fullscreen != fullscreen) return false;
        if (!fullscreen && (window.GetFlags() & SdlWindowFlags.Maximized) == 0 &&
            window.GetSize(out var width, out var height) && new Size(width, height) != windowedSize)
        {
            window.SetSize(windowedSize.Width, windowedSize.Height);
            window.Sync();
        }
        Save();
        return true;
    }

    public bool SetVSync(bool enabled)
    {
        if (!renderer.SetRenderVSync(enabled ? 1 : 0) || VSync != enabled) return false;
        Save();
        return true;
    }

    internal void RememberWindowSize()
    {
        if ((window.GetFlags() & (SdlWindowFlags.Fullscreen | SdlWindowFlags.Minimized | SdlWindowFlags.Maximized)) == 0 &&
            window.GetSize(out var width, out var height) && width > 0 && height > 0)
            windowedSize = new(width, height);
    }

    internal void Save()
    {
        RememberWindowSize();
        var current = new DesktopPreferences(windowedSize, Fullscreen, VSync);
        if (current == saved || session == null) return;
        try
        {
            session.Storage.SaveDisplay(current);
            saved = current;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { session.Log(ex.Message, GameLogLevel.Error); }
    }
}
