using System.Drawing;

namespace Engine.Core;

/// <summary>Live settings of the standalone player's window, on the game thread.</summary>
public interface IDesktopDisplay
{
    bool Fullscreen { get; }
    bool VSync { get; }
    bool SetFullscreen(bool fullscreen);
    bool SetVSync(bool enabled);
}

public sealed partial class SessionState
{
    /// <summary>Available in standalone desktop windows. Embedded and headless games have no window to control.</summary>
    public IDesktopDisplay? Display { get; internal set; }
}

internal readonly record struct DesktopPreferences(Size WindowSize, bool Fullscreen, bool VSync);

public sealed partial class PlayerStorage
{
    internal DesktopPreferences LoadDisplay(Size initial)
    {
        var data = IsAvailable ? Read(Path.Combine(DirectoryPath, "display.settings"), 1, null) : null;
        if (data == null) return new(initial, false, true);
        var width = data.GetNumber("width");
        var height = data.GetNumber("height");
        // Invalid or unrepresentable sizes must not prevent the player from opening.
        var size = width is >= 1 and <= 32768 && height is >= 1 and <= 32768
            ? new Size((int)width, (int)height) : initial;
        return new(size, data.GetBool("fullscreen"), data.GetBool("vsync", true));
    }

    internal void SaveDisplay(DesktopPreferences preferences)
    {
        if (!IsAvailable) return;
        var data = new GameData();
        data.Set("width", preferences.WindowSize.Width);
        data.Set("height", preferences.WindowSize.Height);
        data.Set("fullscreen", preferences.Fullscreen);
        data.Set("vsync", preferences.VSync);
        Write(Path.Combine(DirectoryPath, "display.settings"), data);
    }
}
