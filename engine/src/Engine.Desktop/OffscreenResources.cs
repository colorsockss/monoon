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
    /// <summary>Test captures of a headless replay: <c>&lt;directory&gt;;&lt;frame&gt;,&lt;frame&gt;…</c> saves those frames as PNG.</summary>
    public const string SnapshotVariable = "MONOON_REPLAY_SNAPSHOTS";
    /// <summary>Whole factor of the snapshot size over the logical size (default 1), for showcase videos.</summary>
    public const string SnapshotScaleVariable = "MONOON_REPLAY_SNAPSHOT_SCALE";
    private readonly SdlSurface* target;
    private readonly SdlRenderer renderer;
    private readonly SdlCanvas canvas;
    private readonly SdlSounds sounds = new();
    public SceneResources Resources { get; }

    private unsafe OffscreenResources(GameSession game)
    {
        var size = game.LogicalSize;
        int scale = int.TryParse(Environment.GetEnvironmentVariable(SnapshotScaleVariable), out int s) && s > 1 ? s : 1;
        target = Sdl.PInvoke.CreateSurface(size.Width * scale, size.Height * scale, SdlPixelFormat.Rgba32);
        if (target == null) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        renderer = Sdl.Render.CreateSoftwareRenderer(target);
        if (scale > 1)
        {
            // Like a high-density game window (DesktopHost): logical presentation over a larger surface, so labels drawn
            // from their 32 px masters stay sharp while pixel art keeps nearest sampling.
            bool integer = game.Current?.IntegerScale == true;
            renderer.SetRenderLogicalPresentation(size.Width, size.Height,
                integer ? SdlRendererLogicalPresentation.IntegerScale : SdlRendererLogicalPresentation.Letterbox);
            if (integer && !renderer.SetDefaultTextureScaleMode(SdlScaleMode.Nearest)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
            var clip = new SdlRect { w = size.Width, h = size.Height };
            renderer.SetRenderClipRect(&clip);
        }
        canvas = new SdlCanvas(renderer) { LogicalSize = game.LogicalSize };
        sounds.Attach(game.Mixer);
        Resources = DesktopHost.Resources(canvas, sounds);
        game.UseResources(Resources);
    }

    /// <summary>Attaches when <see cref="Variable"/> is 1 or <see cref="SnapshotVariable"/> is set.</summary>
    public static OffscreenResources? Open(GameSession game) =>
        Paced || Snapshots() != null ? new(game) : null;
    public static bool Paced => Environment.GetEnvironmentVariable(Variable) == "1";

    public static (string Directory, HashSet<int> Frames)? Snapshots()
    {
        if (Environment.GetEnvironmentVariable(SnapshotVariable) is not { Length: > 0 } value) return null;
        int split = value.LastIndexOf(';');
        if (split <= 0) throw new ArgumentException($"{SnapshotVariable} expects <directory>;<frame>,<frame>…");
        return (value[..split], value[(split + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet());
    }

    /// <summary>Draws the current frame with the player's SDL canvas and saves it.</summary>
    public void Snapshot(GameSession game, string path)
    {
        game.Draw(canvas, 1);
        canvas.FlushSprites();
        SdlFrameCapture.SaveImage(renderer, path);
    }

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
