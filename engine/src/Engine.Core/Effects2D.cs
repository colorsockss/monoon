using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

// A looping procedural sprite sampled in local UV space. The raster atlas is owned by each renderer.
[YamlObject]
public sealed partial record ShaderClip(int Width, int Height, int Frames, int Fps, string? Source)
{
    public Guid? Sheet { get; init; }
    [YamlIgnore] public string? SheetPath { get; internal init; }
    [YamlIgnore] public bool Additive { get; private init; }
    public const string Extension = ".shader";
    [YamlIgnore] public int Columns => Math.Min(Frames, 1024 / Width);
    public SpriteRegion Region(int frame) => new(frame % Columns * Width, frame / Columns * Height, Width, Height);
    public int FrameAt(double seconds) => (int)(Math.Floor(seconds * Fps) % Frames);

    public static ShaderClip Load(string file) => Load(file, null, null);

    internal static ShaderClip Load(string file, ProjectAssets? assets, string? root)
    {
        var clip = ProjectYaml.Read<ShaderClip>(file);
        if (clip.Width is < 1 or > 256 || clip.Height is < 1 or > 256 || clip.Frames is < 1 or > 128 ||
            clip.Fps is < 1 or > 60 || clip.Width * clip.Height * clip.Frames > 1048576)
            throw new InvalidDataException($"{file}: invalid shader size, frame count, FPS or source.");
        if (clip.Sheet is Guid id)
        {
            var relative = assets?.PathFor(id);
            if (root == null || !File.Exists(Path.Combine(root, ".monoon-player")) || clip.Source != null ||
                relative == null || !SpriteAssets.IsReference(relative))
                throw new InvalidDataException($"{file}: missing or invalid baked shader sheet {id}.");
            return clip with { SheetPath = Path.Combine(root, relative) };
        }
        if (string.IsNullOrWhiteSpace(clip.Source) || clip.Source.Length > 65536)
            throw new InvalidDataException($"{file}: invalid shader source.");
        return clip;
    }

    public static readonly ShaderClip LightFalloff = new(96,96,1,1,"""
        half4 shade(float2 uv, float t) {
            float d = max(0.0, 1.0 - length(uv * 2.0 - 1.0));
            return half4(d*d);
        }
        """) { Additive = true };

    internal static readonly ShaderClip Puff = new(12,12,1,1,"""
        half4 shade(float2 uv, float t) {
            float a = 1.0 - mnSmooth(0.64, 1.0, length(uv * 2.0 - 1.0));
            return half4(a);
        }
        """);
}

[DisplayName("Sprite Shader 2D"), YamlObject]
public sealed partial record SpriteShader2DComponent : SceneComponent
{
    public Guid? Shader { get; set; }
}

[DisplayName("Point Light 2D"), YamlObject, EngineFeature(EngineFeature.Lighting)]
public sealed partial record PointLight2DComponent : SceneComponent
{
    public PointLight2DComponent() { }
    [YamlConstructor] public PointLight2DComponent(float radius=72, float intensity=.75f, float frequency=2, string color="#FFD087") =>
        (Radius,Intensity,Frequency,Color) = (radius,intensity,frequency,color);
    public float Radius { get; set; } = 72;
    public float Intensity { get; set; } = .75f;
    public float Flicker { get; set; }
    public float Frequency { get; set; } = 2;
    public string Color { get; set; } = "#FFD087";
    // With a Global Light 2D in the scene: colliders and solid tiles inside the radius block this light (roadmap 16.7).
    public bool Shadows { get; set; }

    internal void Validate(string name)
    {
        if (Radius is not (>0 and <=16384) || Intensity is not (>=0 and <=1) || Flicker is not (>=0 and <=1) ||
            Frequency is not (>=0 and <=30) || !SceneObject.TryColor(Color,out _))
            throw new InvalidDataException($"{name}: invalid point light radius, intensity, flicker, frequency or color.");
    }

    // The falloff square and its tint at this time; false when it is off screen or dark.
    internal bool Shade(Vector2 center, RectangleF visible, double seconds, out RectangleF area, out Color tint)
    {
        area = new RectangleF(center.X-Radius,center.Y-Radius,Radius*2,Radius*2);
        tint = default;
        if (!visible.IntersectsWith(area) || Intensity == 0) return false;
        double phase = seconds * Frequency * Math.Tau;
        double wave = .5 + .34 * Math.Sin(phase) + .16 * Math.Sin(phase * 2.71);
        double brightness = Intensity * (1 - Flicker * wave);
        SceneObject.TryColor(Color,out var rgb);
        tint = System.Drawing.Color.FromArgb((int)Math.Round(255*brightness),rgb);
        return true;
    }

    internal void Draw(ICanvas2D canvas, ShaderClip sheet, Vector2 center, RectangleF visible, Matrix3x2 view, double seconds)
    {
        if (Shade(center, visible, seconds, out var area, out var tint)) canvas.DrawShader(sheet,0,area,view,tint,true);
    }
}

// Darkness for the whole scene (roadmap 16.7): the world is multiplied by a lightmap cleared to Color, and Point Light 2D
// adds to it instead of to the screen. Sorting layers from UnlitLayer up, and the UI, draw after it at full brightness.
[DisplayName("Global Light 2D"), YamlObject, EngineFeature(EngineFeature.Lighting)]
public sealed partial record GlobalLight2DComponent : SceneComponent
{
    public GlobalLight2DComponent() { }
    [YamlConstructor] public GlobalLight2DComponent(string color="#202430", int unlitLayer=32767) => (Color,UnlitLayer) = (color,unlitLayer);
    public string Color { get; set; } = "#202430";
    public int UnlitLayer { get; set; } = 32767;

    internal void Validate(string name)
    {
        if (!SceneObject.TryColor(Color,out _) || UnlitLayer is < -32768 or > 32767)
            throw new InvalidDataException($"{name}: invalid global light color or unlit layer.");
    }

    internal Color Ambient() { SceneObject.TryColor(Color,out var rgb); return System.Drawing.Color.FromArgb(255,rgb); }
}
