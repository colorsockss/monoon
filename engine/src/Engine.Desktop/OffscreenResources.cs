using Brutal.SdlApi;
using Engine.Core;

namespace Engine.Desktop;

/// <summary>
/// Textures and sounds of a headless replay: an SDL software renderer on a memory surface, no window or audio device.
/// With <see cref="Variable"/>=1 the player also paces replayed frames to the recorded frame times, so scene loading
/// is measured with the same decode, upload and idle time as a 60 Hz game window.
/// </summary>
public sealed unsafe class OffscreenResources : IDisposable
{
    public const string Variable = "MONOON_REPLAY_OFFSCREEN";
    private readonly SdlSurface* target;
    private readonly SdlRenderer renderer;
    private readonly SdlCanvas canvas;
    private readonly SdlSounds sounds = new();
    public SceneResources Resources { get; }

    private OffscreenResources(GameSession game)
    {
        target = Sdl.PInvoke.CreateSurface(game.LogicalSize.Width, game.LogicalSize.Height, SdlPixelFormat.Rgba32);
        if (target == null) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        renderer = Sdl.Render.CreateSoftwareRenderer(target);
        canvas = new SdlCanvas(renderer) { LogicalSize = game.LogicalSize };
        sounds.Attach(game.Mixer);
        Resources = DesktopHost.Resources(canvas, sounds);
        game.UseResources(Resources);
    }

    /// <summary>Attaches when <see cref="Variable"/> is 1.</summary>
    public static OffscreenResources? Open(GameSession game) =>
        Environment.GetEnvironmentVariable(Variable) == "1" ? new(game) : null;

    public string Counts => $"resident={Resources.ResidentCount} decoded={Resources.DecodedCount} textures={canvas.SpriteTextures}";

    public void Dispose()
    {
        Resources.Dispose();
        sounds.Dispose();
        canvas.Dispose();
        renderer.Dispose();
        Sdl.PInvoke.DestroySurface(target);
    }
}
