using System.Drawing;
using System.Numerics;

namespace Engine.Core;

/// <summary>One drawn frame of <see cref="ScreenEffects"/>, after the player's comfort settings.</summary>
public readonly record struct ScreenFrame(Vector2 Shake, float Zoom, Color Fade, Color Flash, Color Tint, float Brightness)
{
    internal static readonly ScreenFrame None = new(Vector2.Zero, 1, default, default, Color.White, 0);
    public bool Moves => Shake != Vector2.Zero || Zoom != 1;
    public bool Adjusted => Tint.ToArgb() != Color.White.ToArgb() || Brightness != 0;
    // Brightness lerps toward black (below 0) or white (above 0): colour × Multiply + Lift.
    internal Color Multiply => Color.FromArgb(255, Scale(Tint.R), Scale(Tint.G), Scale(Tint.B));
    internal byte Lift => (byte)Math.Round(Math.Max(0, Brightness) * 255);
    private byte Scale(byte c) => (byte)Math.Round(c * (1 - Math.Abs(Brightness)));
}

/// <summary>
/// Camera shake, zoom punch, fade, flash and colour adjustment of the whole game screen. Effects run on game time and shake
/// phases come from a fixed seed, so the same calls on the same ticks draw the same frames in a replay; pausing freezes them.
/// Shake and zoom move every Primary and Extra view (not minimaps) without changing <see cref="IGameCameras.TryGetView"/>.
/// <see cref="For"/> gives the same effects for one camera: they move only that view and cover only its viewport.
/// </summary>
public sealed class ScreenEffects
{
    private const ulong Seed = 0x5348414B45;
    private readonly SessionState state;
    private readonly bool single;
    private Dictionary<Guid, ScreenEffects>? views;
    internal ScreenEffects(SessionState state) => (this.state, seeds) = (state, new(Seed));
    // Guid.GetHashCode xors the four ints: the same per-camera phases on every platform and run.
    private ScreenEffects(SessionState state, Guid camera) => (this.state, seeds, single) = (state, new(Seed ^ (uint)camera.GetHashCode()), true);

    /// <summary>
    /// Effects of one camera only: shake and zoom move that view (minimaps included), and flash, fade and colour adjustment cover
    /// its viewport but not the HUD. They combine with the screen-wide effects and last for the session like them.
    /// </summary>
    public ScreenEffects For(Guid camera)
    {
        if (single) throw new InvalidOperationException("Call For on Session.Screen, not on one camera's effects.");
        views ??= new();
        if (!views.TryGetValue(camera, out var effects)) views[camera] = effects = new(state, camera);
        return effects;
    }
    internal ScreenFrame Sample(Guid camera, double t) =>
        views != null && views.TryGetValue(camera, out var effects) ? effects.Sample(t) : ScreenFrame.None;

    private readonly record struct Ramp(double Start, double Seconds, Vector4 From, Vector4 To)
    {
        public Vector4 At(double t) => Seconds <= 0 || t >= Start + Seconds ? To : Affine2D.Lerp(From, To, (float)Math.Clamp((t - Start) / Seconds, 0, 1));
    }
    private readonly record struct Pulse(double Start, double Seconds, float Strength, Color Color = default)
    {
        // Full strength on the first tick, easing out to zero at the end.
        public float At(double t) => t < Start || t >= Start + Seconds ? 0 : Strength * (float)Math.Pow(1 - (t - Start) / Seconds, 2);
    }
    private readonly SeededRandom seeds;
    private Pulse shake, zoom, flash;
    private float frequency;
    private double phaseX, phaseY;
    private Ramp fade = new(0, 0, default, default), adjust = new(0, 0, new(1, 1, 1, 0), new(1, 1, 1, 0));
    private double Now => state.Time.GameSeconds;

    private static void Require(bool ok, string message) { if (!ok) throw new ArgumentOutOfRangeException(null, message); }
    private static void Duration(double seconds) => Require(double.IsFinite(seconds) && seconds is >= 0 and <= 600, "Use a duration from 0 to 600 seconds.");

    /// <summary>Shakes the views by up to <paramref name="amplitude"/> logical pixels, fading out over <paramref name="seconds"/>. Replaces a running shake.</summary>
    public void Shake(float amplitude, double seconds, float frequency = 18)
    {
        Require(float.IsFinite(amplitude) && amplitude is >= 0 and <= 1000, "Use a shake amplitude from 0 to 1000 pixels.");
        Require(float.IsFinite(frequency) && frequency is > 0 and <= 120, "Use a shake frequency above 0 and at most 120 Hz.");
        Duration(seconds);
        shake = new(Now, seconds, amplitude);
        this.frequency = frequency;
        phaseX = seeds.NextUInt64() * (Math.Tau / ulong.MaxValue);
        phaseY = seeds.NextUInt64() * (Math.Tau / ulong.MaxValue);
    }

    /// <summary>Zooms the views in by <paramref name="amount"/> (0.1 = 10%) at once and eases back over <paramref name="seconds"/>.</summary>
    public void ZoomPunch(float amount, double seconds)
    {
        Require(float.IsFinite(amount) && amount is >= -.9f and <= 4, "Use a zoom punch from -0.9 to 4.");
        Duration(seconds);
        zoom = new(Now, seconds, amount);
    }

    /// <summary>Covers the screen with <paramref name="color"/> at full alpha and fades it out over <paramref name="seconds"/>.</summary>
    public void Flash(Color color, double seconds)
    {
        Duration(seconds);
        flash = new(Now, seconds, color.A / 255f, color);
    }

    /// <summary>Fades a full-screen overlay to <paramref name="color"/>: Black fades out, Transparent fades back in. The overlay stays until the next fade.</summary>
    public void Fade(Color color, double seconds)
    {
        Duration(seconds);
        var start = fade.At(Now);
        var end = new Vector4(color.R, color.G, color.B, color.A) / 255;
        if (start.W == 0) start = end with { W = 0 }; // Start a fade-out in the target colour.
        if (end.W == 0) end = start with { W = 0 };
        fade = new(Now, seconds, start, end);
    }

    /// <summary>Multiplies the world by <paramref name="tint"/> and moves it toward black (brightness below 0) or white (above 0). Stays until changed.</summary>
    public void Adjust(Color tint, float brightness, double seconds = 0)
    {
        Require(float.IsFinite(brightness) && brightness is >= -1 and <= 1, "Use a brightness from -1 to 1.");
        Duration(seconds);
        adjust = new(Now, seconds, adjust.At(Now), new(tint.R / 255f, tint.G / 255f, tint.B / 255f, brightness));
    }

    /// <summary>Stops every effect of this screen or camera at once (Session.Screen.Clear leaves the per-camera effects).</summary>
    public void Clear()
    {
        shake = zoom = flash = default;
        fade = new(0, 0, default, default);
        adjust = new(0, 0, new(1, 1, 1, 0), new(1, 1, 1, 0));
    }

    /// <summary>The effects at the last completed game tick.</summary>
    public ScreenFrame Current => Sample(Now);

    internal ScreenFrame Sample(double t)
    {
        var comfort = state.UiPreferences;
        float motion = comfort?.ShakeScale ?? 1, flashes = comfort?.FlashScale ?? 1;
        var offset = Vector2.Zero;
        float strength = shake.At(t) * motion;
        if (strength > 0)
        {
            double phase = (t - shake.Start) * frequency * Math.Tau;
            offset = new Vector2((float)(.65 * Math.Sin(phase + phaseX) + .35 * Math.Sin(phase * 2.13 + phaseY)),
                (float)(.65 * Math.Sin(phase * .91 + phaseY) + .35 * Math.Sin(phase * 1.87 + phaseX))) * strength;
        }
        static Color Paint(Vector4 c) => Color.FromArgb((int)Math.Round(c.W * 255), (int)Math.Round(c.X * 255), (int)Math.Round(c.Y * 255), (int)Math.Round(c.Z * 255));
        var grade = adjust.At(t);
        return new(offset, 1 + zoom.At(t) * motion, Paint(fade.At(t)),
            Color.FromArgb((int)Math.Round(flash.At(t) * flashes * 255), flash.Color), Paint(grade with { W = 1 }), grade.W);
    }

    /// <summary>Applies shake and zoom to a camera view around its centre.</summary>
    internal static CameraView Move(CameraView view, ScreenFrame frame)
    {
        float zoom = view.Zoom * frame.Zoom, w = view.Viewport.Width / zoom, h = view.Viewport.Height / zoom;
        var center = view.Center + frame.Shake / zoom;
        return view with { World = new(center.X - w / 2, center.Y - h / 2, w, h), Zoom = zoom };
    }
}

/// <summary>A gamepad rumble request: motor strengths from 0 to 1 and a duration.</summary>
public readonly record struct GamepadRumble(float Low, float High, double Seconds);

/// <summary>A custom mouse cursor: a project PNG and its hotspot in image pixels.</summary>
public sealed record GameCursor(string Path, int HotX, int HotY);

public sealed partial class SessionState
{
    /// <summary>Screen-wide camera shake, zoom punch, fade, flash and colour adjustment.</summary>
    public ScreenEffects Screen => field ??= new(this);

    /// <summary>Resolves a project asset ID to its file in the running scene's project.</summary>
    internal Func<Guid, string?>? AssetPath;

    internal GamepadRumble? PendingRumble;
    /// <summary>
    /// Rumbles the connected gamepad's low- and high-frequency motors (0–1) for <paramref name="seconds"/>, replacing a running rumble.
    /// Ignored while the player's <see cref="UiPreferences.Rumble"/> is off, in headless games and without a gamepad.
    /// </summary>
    public void Rumble(float low, float high, double seconds)
    {
        if (!(low is >= 0 and <= 1) || !(high is >= 0 and <= 1) || !double.IsFinite(seconds) || seconds is < 0 or > 60)
            throw new ArgumentOutOfRangeException(null, "Use motor strengths from 0 to 1 and a duration from 0 to 60 seconds.");
        if (UiPreferences?.Rumble == false) return;
        PendingRumble = new(low, high, seconds);
    }

    /// <summary>The custom cursor, or null for the system cursor.</summary>
    public GameCursor? Cursor { get; private set; }
    internal int CursorRevision { get; private set; }

    /// <summary>Uses a project PNG (at most 256×256) as the mouse cursor; the hotspot is the clicking pixel.</summary>
    public void SetCursor(SpriteReference image, int hotX, int hotY)
    {
        var path = AssetPath?.Invoke(image.Id);
        if (path == null || !SpriteAssets.IsPng(path)) throw new ArgumentException($"Missing cursor PNG {image.Id}.", nameof(image));
        var size = SpriteRegion.ReadPngSize(path);
        if (size.Width > 256 || size.Height > 256) throw new ArgumentException("Cursor images can be at most 256×256 pixels.", nameof(image));
        if (hotX < 0 || hotY < 0 || hotX >= size.Width || hotY >= size.Height)
            throw new ArgumentOutOfRangeException(nameof(hotX), $"The hotspot must be inside the {size.Width}×{size.Height} image.");
        Cursor = new(path, hotX, hotY);
        CursorRevision++;
    }

    /// <summary>Restores the system cursor.</summary>
    public void ResetCursor()
    {
        if (Cursor == null) return;
        Cursor = null;
        CursorRevision++;
    }
}
