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

[DisplayName("Point Light 2D"), YamlObject]
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

    internal void Validate(string name)
    {
        if (Radius is not (>0 and <=16384) || Intensity is not (>=0 and <=1) || Flicker is not (>=0 and <=1) ||
            Frequency is not (>=0 and <=30) || !SceneObject.TryColor(Color,out _))
            throw new InvalidDataException($"{name}: invalid point light radius, intensity, flicker, frequency or color.");
    }

    internal void Draw(ICanvas2D canvas, ShaderClip sheet, Vector2 center, RectangleF visible, Matrix3x2 view, double seconds)
    {
        var area = new RectangleF(center.X-Radius,center.Y-Radius,Radius*2,Radius*2);
        if (!visible.IntersectsWith(area) || Intensity == 0) return;
        double phase = seconds * Frequency * Math.Tau;
        double wave = .5 + .34 * Math.Sin(phase) + .16 * Math.Sin(phase * 2.71);
        double brightness = Intensity * (1 - Flicker * wave);
        SceneObject.TryColor(Color,out var rgb);
        canvas.DrawShader(sheet,0,area,view,System.Drawing.Color.FromArgb((int)Math.Round(255*brightness),rgb),true);
    }
}

public enum ParticleTrigger { Manual, Landing }

[DisplayName("Particle Emitter 2D"), YamlObject]
public sealed partial record ParticleEmitter2DComponent : SceneComponent
{
    public ParticleEmitter2DComponent() { }
    [YamlConstructor] public ParticleEmitter2DComponent(int count=16, float lifetime=.55f, float speed=70,
        float gravity=130, float size=5, string color="#E6BE8C") =>
        (Count,Lifetime,Speed,Gravity,Size,Color) = (count,lifetime,speed,gravity,size,color);
    public ParticleTrigger Trigger { get; set; }
    public int Count { get; set; } = 16;
    public float Lifetime { get; set; } = .55f;
    public float Speed { get; set; } = 70;
    public float Gravity { get; set; } = 130;
    public float Size { get; set; } = 5;
    public string Color { get; set; } = "#E6BE8C";
    internal void Validate(string name)
    {
        if (!Enum.IsDefined(Trigger) || Count is <1 or >256 || Lifetime is not (>0 and <=60) ||
            Speed is not (>=0 and <=4096) || Gravity is not (>=-4096 and <=4096) ||
            Size is not (>0 and <=256) || !SceneObject.TryColor(Color,out _))
            throw new InvalidDataException($"{name}: invalid particle trigger, count, lifetime, speed, gravity, size or color.");
    }
}

internal sealed class ParticleBurst(Guid owner, ParticleEmitter2DComponent config)
{
    private readonly record struct Particle(Vector2 Origin, Vector2 Velocity, float Size, double Birth);
    private readonly List<Particle> particles = [];
    private readonly Random random = new(owner.GetHashCode());
    private bool airborne;

    internal void Reset() { particles.Clear(); airborne=false; }
    internal void Step(bool inAir, Vector2 origin, double now)
    {
        int expired = 0;
        while (expired < particles.Count && now - particles[expired].Birth >= config.Lifetime) expired++;
        if (expired > 0) particles.RemoveRange(0,expired);
        if (config.Trigger == ParticleTrigger.Landing && airborne && !inAir) Emit(origin,now);
        airborne = inAir;
    }

    internal void Emit(Vector2 origin, double now)
    {
        int excess = particles.Count + config.Count - 512;
        if (excess > 0) particles.RemoveRange(0,excess);
        for (int i=0; i<config.Count; i++)
        {
            int sign = (i & 1) == 0 ? -1 : 1;
            var velocity = new Vector2(sign*(.35f+.65f*random.NextSingle()),-(.25f+.6f*random.NextSingle())) * config.Speed;
            particles.Add(new(origin,velocity,config.Size*(.65f+.7f*random.NextSingle()),now));
        }
    }

    internal void Draw(ICanvas2D canvas, ShaderClip sheet, RectangleF frame, Matrix3x2 view, double now)
    {
        SceneObject.TryColor(config.Color,out var rgb);
        foreach (var p in particles)
        {
            float age=(float)(now-p.Birth);
            if (age<0 || age>=config.Lifetime) continue;
            float life=1-age/config.Lifetime;
            var point=p.Origin+p.Velocity*age+new Vector2(0,config.Gravity*.5f*age*age);
            float size=p.Size*(.55f+.45f*life);
            var area=new RectangleF(point.X-size,point.Y-size*.5f,size*2,size);
            if (frame.IntersectsWith(area))
                canvas.DrawShader(sheet,0,area,view,System.Drawing.Color.FromArgb((int)(life*255),rgb),false);
        }
    }
}

public sealed partial class SceneGame
{
    private Body[] emitters=[], lights=[];
    private float effectStep;
    private static Vector2 EmitterOrigin(Body b) => Vector2.Transform(new(b.Size.X*.5f,b.Size.Y-2),b.MatrixAt(b.Position));

    public bool EmitParticles(Guid id)
    {
        if (!byId.TryGetValue(id,out var b) || !b.Active || b.Particles == null) return false;
        b.Particles.Emit(EmitterOrigin(b),Time.GameSeconds);
        return true;
    }

    private void StepEffects(float seconds)
    {
        effectStep=seconds;
        foreach (var b in emitters)
            if (b.Active) b.Particles!.Step(b.MotionOwner is { Support:null },EmitterOrigin(b),Time.GameSeconds);
    }
}
