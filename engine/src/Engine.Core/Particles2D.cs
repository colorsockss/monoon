using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Brutal.Maths;
using VYaml.Annotations;

namespace Engine.Core;

public enum ParticleTrigger { Manual, Landing, Start }
public enum ParticleShape { Point, Circle, Edge, Box, Cone }
public enum ParticleSpace { World, Local }
public enum ParticleBlend { Alpha, Additive, Multiply }

// A start value: Min == Max is a constant, otherwise a uniform random value between them.
[YamlObject]
public sealed partial record ParticleRange(float Min, float Max)
{
    internal bool Within(float low, float high) => float.IsFinite(Min) && float.IsFinite(Max) && low <= Min && Min <= Max && Max <= high;
}

// #RRGGBB or #RRGGBBAA. Max null is a constant colour; otherwise each particle picks a point between the two.
[YamlObject]
public sealed partial record ParticleColors(string Min, string? Max = null);

// Count particles at Time seconds into the cycle, repeated Cycles times (0 or 1: once) every Interval seconds.
[YamlObject]
public readonly partial record struct ParticleBurst(float Time, int Count, int Cycles = 1, float Interval = 0);

// A texture split into equal cells (0 columns or rows count as 1), read left to right then top to bottom. Frames 0 uses
// every cell; Fps 0 plays the frames once over each particle's lifetime.
[YamlObject]
public sealed partial record ParticleSheet(int Columns = 1, int Rows = 1, int Frames = 0, float Fps = 0);

// A key on a lifetime curve. Time runs 0–1 over each particle's life. In and Out are slopes (value per unit of
// lifetime) that smooth curves use at the key; linear curves join keys with straight lines.
[YamlObject]
public readonly partial record struct ParticleKey(float Time, float Value, float In = 0, float Out = 0);

// A value over each particle's lifetime: 1–8 keys in time order (equal times make a step). Max keys give a second
// curve; each particle then takes its own point between the two.
[YamlObject]
public sealed partial record ParticleCurve(ParticleKey[] Keys, ParticleKey[]? Max = null, bool Smooth = false);

[YamlObject]
public readonly partial record struct ParticleColorKey(float Time, string Color);

[YamlObject]
public readonly partial record struct ParticleAlphaKey(float Time, float Alpha);

// Colour and alpha over lifetime (up to 8 keys each, joined linearly), multiplied with each particle's start colour.
// No colour keys keep the start colour; no alpha keys keep End alpha.
[YamlObject]
public sealed partial record ParticleGradient(ParticleColorKey[]? Colors = null, ParticleAlphaKey[]? Alphas = null);

// A value-noise force field: Strength in pixels/s², Frequency in cells per pixel, Scroll moves the field (cells/s).
[YamlObject]
public sealed partial record ParticleNoise(float Strength, float Frequency = .02f, float Scroll = 0);

public enum ParticleCollisionMode { Tiles, Colliders }
public enum ParticleEvent { Birth, Death, Collision }

// When a particle is born, dies or collides, the emitter on a direct child object that uses the Particles asset emits
// Count particles there (World space). Depth 1: a sub-emitter target never fires its own sub-emitters. A full target
// drops the rest and counts them.
[YamlObject]
public sealed partial record ParticleSubEmitter(ParticleEvent When, Guid Particles, int Count = 1, float InheritVelocity = 0, bool InheritColor = false);

// An emitter's last game tick: particles born (sub-emitted ones included), died and collided, and all dropped so far.
public readonly record struct ParticleEvents(int Born, int Died, int Collided, long Dropped);

// Tiles looks up the tile under each particle (orthogonal maps). Colliders raycasts up to MaxQueries particles per tick
// against colliders and tiles. Bounce keeps that share of the speed into the surface, Dampen removes a share of all
// speed, Lifetime loss removes a share of the lifetime per hit (1 kills).
[YamlObject]
public sealed partial record ParticleCollision(ParticleCollisionMode Mode = ParticleCollisionMode.Tiles, float Bounce = .5f,
    float Dampen = 0, float LifetimeLoss = 0, uint Mask = uint.MaxValue, int MaxQueries = 64);

/// <summary>A .particles asset: what an emitter spawns and how particles move and look. Emitters reference it and keep
/// only per-instance values (trigger, seed, offset, sorting).</summary>
[YamlObject]
public sealed partial record ParticleEffect
{
    public const string Extension = ".particles";
    public float Duration { get; init; } = 1;
    public bool Looping { get; init; } = true;
    // Starts as if a looping effect had already run for one Duration.
    public bool Prewarm { get; init; }
    public int MaxParticles { get; init; } = 256;
    public float Rate { get; init; } = 10;
    public ParticleBurst[]? Bursts { get; init; }
    public ParticleShape Shape { get; init; }
    public float Radius { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    // Degrees clockwise from +X in screen space (-90 is up). Spread is the full angle around it.
    public float Direction { get; init; } = -90;
    public float Spread { get; init; }
    public ParticleRange Lifetime { get; init; } = new(1, 1);
    public ParticleRange Speed { get; init; } = new(40, 40);
    // Width in pixels; height is Width × Aspect.
    public ParticleRange Size { get; init; } = new(8, 8);
    public float Aspect { get; init; } = 1;
    public ParticleRange Rotation { get; init; } = new(0, 0);
    public ParticleColors Color { get; init; } = new("#FFFFFF");
    // Size and alpha multipliers reached at the end of each particle's life (linear from 1). Size over lifetime replaces
    // End size, and gradient alpha keys replace End alpha.
    public float EndSize { get; init; } = 1;
    public float EndAlpha { get; init; }
    // Multiplies the start size over lifetime.
    public ParticleCurve? SizeOverLifetime { get; init; }
    public ParticleGradient? ColorOverLifetime { get; init; }
    // Degrees per second over lifetime (positive is clockwise), added to the start rotation.
    public ParticleCurve? RotationSpeed { get; init; }
    public float Gravity { get; init; }
    // A constant acceleration (pixels/s²) on top of gravity.
    public float ForceX { get; init; }
    public float ForceY { get; init; }
    // Velocity over lifetime (pixels/s), added to each particle's own velocity. These, Drag, Noise and Collision move
    // particles by integrating every tick; without them positions follow the closed-form path.
    public ParticleCurve? VelocityX { get; init; }
    public ParticleCurve? VelocityY { get; init; }
    // Linear drag: the velocity loses Drag × dt of itself each tick (1/s).
    public float Drag { get; init; }
    public ParticleNoise? Noise { get; init; }
    public ParticleCollision? Collision { get; init; }
    public ParticleSubEmitter[]? SubEmitters { get; init; }
    public ParticleSpace Space { get; init; }
    // A PNG asset; none uses the built-in soft dot.
    public Guid? Texture { get; init; }
    public ParticleSheet? Sheet { get; init; }
    public ParticleBlend Blend { get; init; }

    [YamlIgnore] internal ParticleTexture? Resolved { get; init; }

    public ParticleEffect() { }
    // VYaml fills keys missing from the file with these parameter defaults (an init property would get default(T)). Ranges and
    // colours are reference records so a missing one arrives here as null.
    [YamlConstructor]
    public ParticleEffect(float duration = 1, bool looping = true, bool prewarm = false, int maxParticles = 256, float rate = 10,
        ParticleBurst[]? bursts = null, ParticleShape shape = ParticleShape.Point, float radius = 0, float width = 0, float height = 0,
        float direction = -90, float spread = 0, ParticleRange? lifetime = null, ParticleRange? speed = null, ParticleRange? size = null,
        float aspect = 1, ParticleRange? rotation = null, ParticleColors? color = null, float endSize = 1, float endAlpha = 0,
        float gravity = 0, ParticleSpace space = ParticleSpace.World, Guid? texture = null, ParticleSheet? sheet = null,
        ParticleBlend blend = ParticleBlend.Alpha, ParticleCurve? sizeOverLifetime = null, ParticleGradient? colorOverLifetime = null,
        ParticleCurve? rotationSpeed = null, float forceX = 0, float forceY = 0, ParticleCurve? velocityX = null, ParticleCurve? velocityY = null,
        float drag = 0, ParticleNoise? noise = null, ParticleCollision? collision = null, ParticleSubEmitter[]? subEmitters = null)
    {
        SubEmitters = subEmitters;
        (Duration, Looping, Prewarm, MaxParticles, Rate, Bursts, Shape, Radius, Width, Height, Direction, Spread) =
            (duration, looping, prewarm, maxParticles, rate, bursts, shape, radius, width, height, direction, spread);
        (Lifetime, Speed, Size, Aspect, Rotation, Color) =
            (lifetime ?? Lifetime, speed ?? Speed, size ?? Size, aspect, rotation ?? Rotation, color ?? Color);
        (EndSize, EndAlpha, Gravity, Space, Texture, Sheet, Blend) = (endSize, endAlpha, gravity, space, texture, sheet, blend);
        (SizeOverLifetime, ColorOverLifetime, RotationSpeed) = (sizeOverLifetime, colorOverLifetime, rotationSpeed);
        (ForceX, ForceY, VelocityX, VelocityY, Drag, Noise, Collision) = (forceX, forceY, velocityX, velocityY, drag, noise, collision);
    }

    // Forces that need a per-tick integration instead of p0 + v·t + ½a·t².
    [YamlIgnore] internal bool Integrates => VelocityX != null || VelocityY != null || Drag > 0 || Noise is { Strength: > 0 } || Collision != null;

    public static ParticleEffect Load(string file)
    {
        var effect = ProjectYaml.Read<ParticleEffect>(file);
        effect.Validate(file);
        return effect;
    }

    internal void Validate(string name)
    {
        static bool In(float v, float low, float high) => float.IsFinite(v) && v >= low && v <= high;
        if (!In(Duration, .001f, 3600) || MaxParticles is < 1 or > 65536 || !In(Rate, 0, 100000) || Prewarm && !Looping ||
            Bursts is { Length: > 32 } || Bursts?.Any(b => !In(b.Time, 0, 3600) || b.Count is < 0 or > 65536 || b.Cycles is < 0 or > 10000 ||
                !In(b.Interval, 0, 3600) || b.Cycles > 1 && b.Interval <= 0) == true ||
            !Enum.IsDefined(Shape) || !In(Radius, 0, 16384) || !In(Width, 0, 16384) || !In(Height, 0, 16384) ||
            !In(Direction, -3600, 3600) || !In(Spread, 0, 360) || !Lifetime.Within(.001f, 600) || !Speed.Within(-16384, 16384) ||
            !Size.Within(0, 4096) || !In(Aspect, .01f, 100) || !Rotation.Within(-3600, 3600) ||
            !TryColor(Color.Min, out _) || Color.Max != null && !TryColor(Color.Max, out _) ||
            !In(EndSize, 0, 100) || !In(EndAlpha, 0, 1) || !In(Gravity, -16384, 16384) || !Enum.IsDefined(Space) || !Enum.IsDefined(Blend) ||
            Sheet is { } s && (s.Columns is < 0 or > 64 || s.Rows is < 0 or > 64 || s.Frames < 0 || s.Frames > Math.Max(1, s.Columns) * Math.Max(1, s.Rows) || !In(s.Fps, 0, 240)))
            throw new InvalidDataException($"{name}: invalid particle effect (check ranges: duration, max particles 1–65536, rate, bursts, " +
                "shape, lifetime, speed, size, aspect, rotation, #RRGGBB[AA] colours, end size/alpha, gravity, sheet; prewarm needs looping).");
        if (!Curve(SizeOverLifetime, 0, 100) || !Curve(RotationSpeed, -36000, 36000) || ColorOverLifetime is { } g &&
            (!Keys(g.Colors, k => k.Time, k => TryColor(k.Color, out _)) || !Keys(g.Alphas, k => k.Time, k => In(k.Alpha, 0, 1))))
            throw new InvalidDataException($"{name}: invalid lifetime curve or gradient (1–8 keys with times 0–1 in order, values in range, " +
                "#RRGGBB colours, alpha 0–1).");
        if (!In(ForceX, -16384, 16384) || !In(ForceY, -16384, 16384) || !Curve(VelocityX, -16384, 16384) || !Curve(VelocityY, -16384, 16384) ||
            !In(Drag, 0, 60) || Noise is { } n && (!In(n.Strength, 0, 16384) || !In(n.Frequency, 0, 16) || !In(n.Scroll, -1000, 1000)) ||
            Collision is { } c && (!Enum.IsDefined(c.Mode) || !In(c.Bounce, 0, 1) || !In(c.Dampen, 0, 1) || !In(c.LifetimeLoss, 0, 1) ||
                c.MaxQueries is < 1 or > 65536 || Space != ParticleSpace.World))
            throw new InvalidDataException($"{name}: invalid force, velocity curve, drag (0–60), noise or collision " +
                "(bounce, dampen and lifetime loss 0–1, max queries 1–65536, collision needs World space).");
        if (SubEmitters is { Length: > 8 } || SubEmitters?.Any(e => e == null || !Enum.IsDefined(e.When) || e.Particles == Guid.Empty ||
                e.Count is < 1 or > 1024 || !In(e.InheritVelocity, 0, 1)) == true)
            throw new InvalidDataException($"{name}: invalid sub-emitters (up to 8, each with Birth/Death/Collision, a .particles asset, count 1–1024, inherit velocity 0–1).");
    }

    internal static bool Curve(ParticleCurve? curve, float low, float high) => curve == null ||
        curve.Keys is { Length: > 0 } && Keys(curve.Keys, k => k.Time, k => Finite(k, low, high)) &&
        (curve.Max == null || curve.Max.Length > 0 && Keys(curve.Max, k => k.Time, k => Finite(k, low, high)));
    private static bool Finite(ParticleKey k, float low, float high) =>
        float.IsFinite(k.Value) && k.Value >= low && k.Value <= high && float.IsFinite(k.In) && float.IsFinite(k.Out);
    private static bool Keys<T>(T[]? keys, Func<T, float> time, Func<T, bool> valid)
    {
        if (keys == null) return true;
        if (keys.Length > 8) return false;
        for (int i = 0; i < keys.Length; i++)
            if (!(time(keys[i]) is >= 0 and <= 1) || i > 0 && time(keys[i]) < time(keys[i - 1]) || !valid(keys[i])) return false;
        return true;
    }

    internal static bool TryColor(string? value, out Color color)
    {
        color = default;
        if (value is not { Length: 7 or 9 } || value[0] != '#' ||
            !uint.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex)) return false;
        color = System.Drawing.Color.FromArgb(unchecked((int)(value.Length == 7 ? 0xFF000000 | hex : hex >> 8 | hex << 24)));
        return true;
    }

    // Resolves the PNG and its cells once per loaded asset.
    internal ParticleEffect WithTexture(string root, ProjectAssets assets)
    {
        if (Texture is not Guid id) return this;
        if (assets.PathFor(id) is not { } relative || !SpriteAssets.IsPng(relative))
            throw new InvalidDataException($"Missing particle texture PNG {id}.");
        string path = Path.Combine(root, relative);
        var size = SpriteRegion.ReadPngSize(path);
        int columns = Math.Max(1, Sheet?.Columns ?? 1), rows = Math.Max(1, Sheet?.Rows ?? 1);
        if (size.Width % columns != 0 || size.Height % rows != 0)
            throw new InvalidDataException($"{relative}: {size.Width}×{size.Height} does not split into {columns}×{rows} particle cells.");
        int w = size.Width / columns, h = size.Height / rows;
        var cells = new SpriteRegion[Sheet is { Frames: > 0 } ? Sheet.Frames : columns * rows];
        for (int i = 0; i < cells.Length; i++) cells[i] = new(i % columns * w, i / columns * h, w, h);
        return this with { Resolved = new(path, null, cells) };
    }
}

/// <summary>What a canvas draws particles with: a PNG (Path) or a procedural sheet (Shader), and the source cells.</summary>
public sealed class ParticleTexture(string? path, ShaderClip? shader, SpriteRegion[] cells)
{
    public string? Path { get; } = path;
    public ShaderClip? Shader { get; } = shader;
    public SpriteRegion[] Cells { get; } = cells;
}

// Corners are Center ∓ Right ∓ Down: Right and Down are half the quad's edges, already rotated and scaled.
public readonly record struct ParticleQuad(Vector2 Center, Vector2 Right, Vector2 Down, int Cell, Color Tint);

// One live particle as of the emitter's last game tick, in world space.
public readonly record struct Particle2D(Vector2 Position, float Size, float Age, Color Color);

[DisplayName("Particle Emitter 2D"), YamlObject, EngineFeature(EngineFeature.Particles)]
public sealed partial record ParticleEmitter2DComponent : SceneComponent
{
    // A .particles asset. None uses the default effect.
    public Guid? Particles { get; set; }
    public ParticleTrigger Trigger { get; set; }
    // 0 derives the stream from the object ID: copies of one effect differ, and every run replays the same.
    public uint Seed { get; set; }
    // Emission origin relative to the object's centre, in its local pixels.
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    // Used when the object has no sprite, skeleton, line or trail; otherwise particles draw right after them.
    public int SortingLayer { get; set; }
    public int Order { get; set; }

    // Inline settings before scene schema 31. Unmigrated scenes still play; `monoon migrate` moves them to an asset.
    public int? Count { get; set; }
    public float? Lifetime { get; set; }
    public float? Speed { get; set; }
    public float? Gravity { get; set; }
    public float? Size { get; set; }
    public string? Color { get; set; }
    [YamlIgnore] internal bool Legacy => Count != null || Lifetime != null || Speed != null || Gravity != null || Size != null || Color != null;

    internal void Validate(string name)
    {
        if (!Enum.IsDefined(Trigger) || !float.IsFinite(OffsetX) || !float.IsFinite(OffsetY))
            throw new InvalidDataException($"{name}: invalid particle trigger or offset.");
        if (Legacy && (Particles != null || Count is <1 or >256 || Lifetime is not (null or (>0 and <=60)) ||
            Speed is not (null or (>=0 and <=4096)) || Gravity is not (null or (>=-4096 and <=4096)) ||
            Size is not (null or (>0 and <=256)) || Color != null && !SceneObject.TryColor(Color,out _)))
            throw new InvalidDataException($"{name}: invalid particle count, lifetime, speed, gravity, size or color.");
    }

    // The inline emitter's look: Count particles at once from the bottom centre in an upward fan.
    internal ParticleEffect LegacyEffect() => new()
    {
        Duration = 1, Looping = false, Rate = 0, MaxParticles = 512,
        Bursts = [new(0, Count ?? 16)], Direction = -90, Spread = 152,
        Lifetime = new(Lifetime ?? .55f, Lifetime ?? .55f), Speed = new((Speed ?? 70) * .43f, (Speed ?? 70) * 1.31f),
        Size = new((Size ?? 5) * 1.3f, (Size ?? 5) * 2.7f), Aspect = .5f,
        Color = new(Color ?? "#E6BE8C"), EndSize = .55f, EndAlpha = 0, Gravity = Gravity ?? 130,
    };
    internal static float LegacyOffsetY(float height) => height * .5f - 2;
}

// Lifetime curves and gradients baked to 64 samples over t = 0…1 when an emitter is created; particles read two
// neighbouring samples and interpolate, so evaluation costs the same for any number of keys and never allocates.
internal static class ParticleLut
{
    internal const int Size = 64;

    internal static float[] Bake(ParticleKey[] keys, bool smooth)
    {
        var lut = new float[Size];
        for (int k = 0; k < Size; k++) lut[k] = Evaluate(keys, smooth, k / (float)(Size - 1));
        return lut;
    }

    // Keys hold their first and last values outside their times; a zero-length segment is a step.
    internal static float Evaluate(ReadOnlySpan<ParticleKey> keys, bool smooth, float t)
    {
        if (t <= keys[0].Time) return keys[0].Value;
        for (int j = 1; j < keys.Length; j++)
        {
            var (a, b) = (keys[j - 1], keys[j]);
            if (t >= b.Time) continue;
            float h = b.Time - a.Time, s = (t - a.Time) / h;
            if (!smooth) return a.Value + (b.Value - a.Value) * s;
            float s2 = s * s, s3 = s2 * s;
            return (2 * s3 - 3 * s2 + 1) * a.Value + (s3 - 2 * s2 + s) * h * a.Out + (3 * s2 - 2 * s3) * b.Value + (s3 - s2) * h * b.In;
        }
        return keys[^1].Value;
    }

    // The running integral of a rate over t (trapezoids), for an angle reached after t of the lifetime.
    internal static float[] Integral(float[] rate)
    {
        var sum = new float[Size];
        for (int k = 1; k < Size; k++) sum[k] = sum[k - 1] + (rate[k - 1] + rate[k]) * (.5f / (Size - 1));
        return sum;
    }

    // Four multipliers per sample: red, green, blue (0–1 from the colour keys) and alpha.
    internal static float[] Bake(ParticleGradient gradient)
    {
        var lut = new float[Size * 4];
        var colors = gradient.Colors ?? [];
        var channels = new ParticleKey[3][];
        for (int c = 0; c < 3; c++)
            channels[c] = Array.ConvertAll(colors, k =>
            {
                ParticleEffect.TryColor(k.Color, out var color);
                return new ParticleKey(k.Time, (c == 0 ? color.R : c == 1 ? color.G : color.B) / 255f);
            });
        var alphas = Array.ConvertAll(gradient.Alphas ?? [], k => new ParticleKey(k.Time, k.Alpha));
        for (int k = 0; k < Size; k++)
        {
            float t = k / (float)(Size - 1);
            for (int c = 0; c < 3; c++) lut[k * 4 + c] = colors.Length == 0 ? 1 : Evaluate(channels[c], false, t);
            lut[k * 4 + 3] = alphas.Length == 0 ? 1 : Evaluate(alphas, false, t);
        }
        return lut;
    }

}

// One emitter's particles in fixed arrays (structure of arrays), sized once by Max particles. Without integrated
// forces, positions follow p0 + v·age + ½a·age² from the birth time, so ticks and draws evaluate the same expression
// and ticks only expire (swap-remove) and emit. With velocity curves, drag, noise or collision each tick integrates
// velocity and position (semi-implicit Euler) and draws interpolate from the previous tick. Never allocates after
// construction.
internal sealed class ParticleSystem2D
{
    // Brutal.Maths' samplers take System.Random; this one draws from the emitter's SeededRandom.
    private sealed class Stream(SeededRandom source) : Random
    {
        public override float NextSingle() => (source.NextUInt64() >> 40) * (1f / (1 << 24));
        public override double NextDouble() => (source.NextUInt64() >> 11) * (1.0 / (1UL << 53));
        protected override double Sample() => NextDouble();
    }

    // Not readonly: a Scene Play reload (Reload) swaps the effect into this object, which the scene's body keeps.
    private ParticleEffect fx = null!;
    private ParticleTrigger trigger;
    private Vector2 origin, axis;
    private Stream random = null!;
    private Color colorMin, colorMax;
    private float[] x = [], y = [], vx = [], vy = [], life = [], size = [], cos = [], sin = [];
    private double[] birth = [];
    private int[] color = [];
    private ParticleQuad[] quads = [];
    // Baked lifetime curves (null when unused). Spin is the integral of the rotation speed curve; pick places each
    // particle between a curve and its Max curve; angle keeps the start rotation for spinning particles.
    private float[]? sizeLow, sizeHigh, spinLow, spinHigh, tint, pick, angle;
    private bool gradientAlpha, curves;
    // Integrated motion: the previous tick's positions for drawing, velocity curves, and constant acceleration.
    private float[]? prevX, prevY, velocityLowX, velocityHighX, velocityLowY, velocityHighY;
    private bool integrate;
    private float accelerationX, accelerationY;
    private uint noiseSeed;
    private PhysicsFilter? filter;
    private float step;
    // Sub-emitter rules bound to their target systems (null without any); targets never fire their own (depth 1).
    private (ParticleSubEmitter Rule, ParticleSystem2D Target)[]? subs;
    private Matrix3x2 pose = Matrix3x2.Identity;
    private int born, died, collided;
    private int cursor;
    private int count;
    private float time, carry;
    private bool playing, started, airborne;
    private double stepped;

    internal int Count => count;
    internal long Dropped { get; private set; }
    internal ParticleEffect Effect => fx;
    // Tile layers and physics for Collision; null skips collision.
    internal ParticleWorld? World { get; set; }
    internal long Collisions { get; private set; }
    // False while the owner is inactive: sub-emitters skip it.
    internal bool Live { get; set; } = true;
    internal bool SubTarget { get; set; }
    internal ParticleEvents Events => new(born, died, collided, Dropped);

    internal void BindSubEmitters((ParticleSubEmitter Rule, ParticleSystem2D Target)[] rules)
    {
        subs = rules.Length == 0 ? null : rules;
        foreach (var (_, target) in rules) target.SubTarget = true;
    }

    // Starts a game tick's event counts; the scene calls it for every emitter before stepping any of them.
    internal void BeginTick() => born = died = collided = 0;

    internal ParticleSystem2D(SceneObject owner, ParticleEmitter2DComponent emitter, ParticleEffect effect) => Configure(owner, emitter, effect, keep: false);

    // Editor Scene Play reload of a saved asset. Live particles stay when the pool layout is unchanged: the same Max particles
    // and the same per-particle arrays (integrated motion, a Rotation speed curve, any Max curve). They keep what they drew
    // at birth (lifetime, size, velocity, rotation, start colour); the cycle time, emission carry and random stream go on;
    // curves, gradients, forces and every later birth use the new asset. Otherwise the emitter restarts as if just created:
    // empty, the seed's stream from its start, the trigger waiting again. Returns true when the particles were kept.
    internal bool Reload(SceneObject owner, ParticleEmitter2DComponent emitter, ParticleEffect effect)
    {
        bool keep = effect.MaxParticles == fx.MaxParticles && effect.Integrates == integrate && (effect.RotationSpeed != null) == (angle != null) &&
            Picks(effect) == (pick != null);
        Configure(owner, emitter, effect, keep);
        if (!keep) { Reset(); Dropped = Collisions = 0; cursor = 0; }
        else if (time >= fx.Duration)
        {
            if (fx.Looping) time %= fx.Duration;
            else playing = false;
        }
        return keep;
    }

    private static bool Picks(ParticleEffect e) => e.SizeOverLifetime?.Max != null || e.RotationSpeed?.Max != null || e.VelocityX?.Max != null || e.VelocityY?.Max != null;

    // keep: the per-particle arrays and the random stream stay (Reload); everything derived from the asset is rebuilt.
    private void Configure(SceneObject owner, ParticleEmitter2DComponent emitter, ParticleEffect effect, bool keep)
    {
        fx = effect;
        trigger = emitter.Trigger;
        origin = new(owner.Width * .5f + emitter.OffsetX, owner.Height * .5f + (emitter.Legacy ? ParticleEmitter2DComponent.LegacyOffsetY(owner.Height) : emitter.OffsetY));
        axis = Unit(fx.Direction);
        int n = fx.MaxParticles;
        integrate = fx.Integrates;
        if (!keep)
        {
            ulong seed = emitter.Seed;
            if (seed == 0)
            {
                Span<byte> id = stackalloc byte[16];
                owner.Id.TryWriteBytes(id);
                seed = BitConverter.ToUInt64(id) ^ BitConverter.ToUInt64(id[8..]);
            }
            random = new(new SeededRandom(seed).Fork(1));
            noiseSeed = (uint)new SeededRandom(seed).Fork(4).NextUInt64();
            (x, y, vx, vy, life, size, cos, sin) = (new float[n], new float[n], new float[n], new float[n], new float[n], new float[n], new float[n], new float[n]);
            birth = new double[n];
            color = new int[n];
            quads = new ParticleQuad[n];
            angle = fx.RotationSpeed != null ? new float[n] : null;
            (prevX, prevY) = integrate ? (new float[n], new float[n]) : (null, null);
            pick = Picks(fx) ? new float[n] : null;
        }
        ParticleEffect.TryColor(fx.Color.Min, out colorMin);
        colorMax = colorMin;
        if (fx.Color.Max != null) ParticleEffect.TryColor(fx.Color.Max, out colorMax);
        (sizeLow, sizeHigh) = fx.SizeOverLifetime is { } grow
            ? (ParticleLut.Bake(grow.Keys, grow.Smooth), grow.Max == null ? null : ParticleLut.Bake(grow.Max, grow.Smooth)) : (null, null);
        (spinLow, spinHigh) = fx.RotationSpeed is { } spin ? (ParticleLut.Integral(ParticleLut.Bake(spin.Keys, spin.Smooth)),
            spin.Max == null ? null : ParticleLut.Integral(ParticleLut.Bake(spin.Max, spin.Smooth))) : (null, null);
        (tint, gradientAlpha) = fx.ColorOverLifetime is { } gradient ? (ParticleLut.Bake(gradient), gradient.Alphas is { Length: > 0 }) : (null, false);
        (velocityLowX, velocityHighX) = fx.VelocityX is { } velocityX
            ? (ParticleLut.Bake(velocityX.Keys, velocityX.Smooth), velocityX.Max == null ? null : ParticleLut.Bake(velocityX.Max, velocityX.Smooth)) : (null, null);
        (velocityLowY, velocityHighY) = fx.VelocityY is { } velocityY
            ? (ParticleLut.Bake(velocityY.Keys, velocityY.Smooth), velocityY.Max == null ? null : ParticleLut.Bake(velocityY.Max, velocityY.Smooth)) : (null, null);
        (accelerationX, accelerationY) = (fx.ForceX, fx.Gravity + fx.ForceY);
        filter = fx.Collision is { Mode: ParticleCollisionMode.Colliders } hits ? new(hits.Mask) : null;
        curves = sizeLow != null || spinLow != null || tint != null;
    }

    internal void Reset() { count = 0; time = carry = 0; playing = started = airborne = false; }

    // Starts the cycle from its beginning: bursts at time 0 fire on the next step. Live particles stay.
    internal void Play() { time = carry = 0; playing = true; started = true; }

    internal void Step(float seconds, double now, Matrix3x2 matrix, bool inAir)
    {
        if (!started && trigger == ParticleTrigger.Start)
        {
            Play();
            if (fx.Prewarm)
            {
                int ticks = (int)MathF.Ceiling(fx.Duration / seconds);
                for (int k = ticks; k > 0; k--) Advance(seconds, now - k * (double)seconds, matrix);
            }
        }
        if (trigger == ParticleTrigger.Landing && airborne && !inAir) Play();
        airborne = inAir;
        Advance(seconds, now, matrix);
    }

    private void Advance(float seconds, double now, Matrix3x2 matrix)
    {
        stepped = now;
        step = seconds;
        pose = matrix;
        for (int i = 0; i < count; i++)
        {
            double age = now - birth[i];
            if (age >= life[i])
            {
                died++;
                if (subs != null && !SubTarget)
                {
                    float end = life[i];
                    Fire(ParticleEvent.Death, i, At(i, end), integrate ? new(vx[i], vy[i]) : new(vx[i] + accelerationX * end, vy[i] + accelerationY * end), end, now);
                }
                Move(--count, i); i--;
            }
            // Particles born this tick (including sub-emitted ones) start moving on the next.
            else if (integrate && age > 0) Integrate(i, (float)age, seconds, now);
        }
        if (integrate && fx.Collision is { Mode: ParticleCollisionMode.Colliders } && World?.Physics != null) CastColliders();
        if (!playing) return;
        float t0 = time, t1 = time + seconds;
        carry += fx.Rate * seconds;
        int n = (int)carry;
        carry -= n;
        if (t1 < fx.Duration || !fx.Looping) n += Bursts(t0, t1);
        else n += Bursts(t0, fx.Duration) + Bursts(0, t1 - fx.Duration);
        time = t1;
        if (time >= fx.Duration)
        {
            if (fx.Looping) time -= fx.Duration;
            else playing = false;
        }
        for (int i = 0; i < n; i++)
        {
            if (count == x.Length) { Dropped += n - i; break; }
            Spawn(count++, now, matrix, origin);
            born++;
            if (subs != null && !SubTarget) Fire(ParticleEvent.Birth, count - 1, new(x[count - 1], y[count - 1]), new(vx[count - 1], vy[count - 1]), 0, now);
        }
    }

    // Sends one event to the sub-emitters that listen for it, at a point in this emitter's space.
    private void Fire(ParticleEvent kind, int i, Vector2 at, Vector2 velocity, float age, double now)
    {
        if (fx.Space == ParticleSpace.Local) (at, velocity) = (Affine2D.Transform(at, pose), Affine2D.TransformNormal(velocity, pose));
        float t = age / life[i];
        int k = Cell(t, out float u);
        foreach (var (rule, target) in subs!)
            if (rule.When == kind && target.Live)
                target.EmitAt(at, velocity * rule.InheritVelocity, rule.InheritColor ? Tint(i, t, k, u) : null, rule.Count, now);
    }

    // A sub-emitter target's burst: Count particles from its shape around a world point, born this tick.
    internal void EmitAt(Vector2 at, Vector2 velocity, Color? tint, int n, double now)
    {
        var place = Matrix3x2.CreateTranslation(at);
        for (int j = 0; j < n; j++)
        {
            if (count == x.Length) { Dropped += n - j; return; }
            int i = count++;
            Spawn(i, now, place, Vector2.Zero);
            vx[i] += velocity.X; vy[i] += velocity.Y;
            if (tint is { } c)
            {
                uint own = (uint)color[i];
                color[i] = (int)((own >> 24) * c.A / 255) << 24 | (int)((own >> 16 & 255) * c.R / 255) << 16 |
                    (int)((own >> 8 & 255) * c.G / 255) << 8 | (int)((own & 255) * c.B / 255);
            }
            born++;
        }
    }

    private int Bursts(float from, float to)
    {
        int n = 0;
        foreach (var b in fx.Bursts ?? [])
            for (int c = 0; c < Math.Max(1, b.Cycles); c++)
            {
                float at = b.Time + c * b.Interval;
                if (at >= from && at < to) n += b.Count;
            }
        return n;
    }

    // One tick of velocity (gravity, force, noise, drag) and position (plus the velocity curves), then tile collision.
    private void Integrate(int i, float age, float seconds, double now)
    {
        float t = age / life[i];
        int k = Cell(t, out float u);
        float ax = accelerationX, ay = accelerationY;
        if (fx.Noise is { Strength: > 0 } noise)
        {
            var n = ParticleNoise2D.Sample(x[i] * noise.Frequency, y[i] * noise.Frequency + (float)(now * noise.Scroll), noiseSeed);
            ax += n.X * noise.Strength;
            ay += n.Y * noise.Strength;
        }
        float keep = fx.Drag > 0 ? MathF.Max(0, 1 - fx.Drag * seconds) : 1;
        float nvx = (vx[i] + ax * seconds) * keep, nvy = (vy[i] + ay * seconds) * keep;
        float mx = nvx, my = nvy;
        if (velocityLowX != null) mx += Mix(velocityLowX, velocityHighX, i, k, u);
        if (velocityLowY != null) my += Mix(velocityLowY, velocityHighY, i, k, u);
        float px = x[i] + mx * seconds, py = y[i] + my * seconds;
        prevX![i] = x[i]; prevY![i] = y[i];
        if (fx.Collision is { Mode: ParticleCollisionMode.Tiles } hits && World != null)
            foreach (var tiles in World.Tiles)
                if (tiles.Active && (hits.Mask & 1u << tiles.Layer) != 0 && tiles.Hit(new(x[i], y[i]), new(px, py), out var normal))
                {
                    Bounce(i, ref nvx, ref nvy, normal, age, new(x[i], y[i]), now);
                    (px, py) = (x[i], y[i]);
                    break;
                }
        x[i] = px; y[i] = py; vx[i] = nvx; vy[i] = nvy;
    }

    // Colliders mode: up to Max queries raycasts per tick along this tick's moves, continuing round-robin next tick.
    private void CastColliders()
    {
        var c = fx.Collision!;
        for (int q = 0, n = Math.Min(c.MaxQueries, count); q < n; q++)
        {
            int i = cursor++ % count;
            if (stepped - birth[i] <= 0) continue;
            float dx = x[i] - prevX![i], dy = y[i] - prevY![i];
            if (dx == 0 && dy == 0) continue;
            var from = new Vector2(prevX[i], prevY![i]);
            if (World!.Physics!.Raycast(from, new(dx, dy), MathF.Sqrt(dx * dx + dy * dy), filter) is not { } hit) continue;
            float nvx = vx[i], nvy = vy[i];
            Bounce(i, ref nvx, ref nvy, hit.Normal, (float)(stepped - birth[i]), from, stepped);
            (x[i], y[i], vx[i], vy[i]) = (from.X, from.Y, nvx, nvy);
        }
        if (count > 0) cursor %= count;
    }

    // Keeps Bounce of the speed into the surface, removes Dampen of all speed and Lifetime loss of the lifetime.
    private void Bounce(int i, ref float nvx, ref float nvy, Vector2 normal, float age, Vector2 at, double now)
    {
        var c = fx.Collision!;
        float into = nvx * normal.X + nvy * normal.Y;
        if (into < 0) { nvx -= (1 + c.Bounce) * into * normal.X; nvy -= (1 + c.Bounce) * into * normal.Y; }
        if (c.Dampen > 0) { nvx *= 1 - c.Dampen; nvy *= 1 - c.Dampen; }
        // A particle that loses all its time ends at t = 1 and expires on the next tick.
        if (c.LifetimeLoss > 0) life[i] = MathF.Max(age, life[i] - life[i] * c.LifetimeLoss);
        Collisions++;
        collided++;
        if (subs != null && !SubTarget) Fire(ParticleEvent.Collision, i, at, new(nvx, nvy), age, now);
    }

    private void Move(int from, int to)
    {
        x[to] = x[from]; y[to] = y[from]; vx[to] = vx[from]; vy[to] = vy[from]; life[to] = life[from];
        size[to] = size[from]; cos[to] = cos[from]; sin[to] = sin[from]; birth[to] = birth[from]; color[to] = color[from];
        if (prevX != null) { prevX[to] = prevX[from]; prevY![to] = prevY[from]; }
        if (pick != null) pick[to] = pick[from];
        if (angle != null) angle[to] = angle[from];
    }

    private float Next(ParticleRange range) => random.NextSingle(range.Min, range.Max);

    // MathF.SinCos is not the same bits in JIT (one sincosf call) and Native AOT (Sin and Cos) on macOS arm64; separate
    // Sin and Cos match, as in Brutal.Maths' NextSingleUnit2.
    private static Vector2 Unit(float degrees)
    {
        float radians = float.DegreesToRadians(degrees);
        return new(MathF.Cos(radians), MathF.Sin(radians));
    }

    // A fixed number of draws per particle for each shape, so a stream replays identically.
    private void Spawn(int i, double now, Matrix3x2 matrix, Vector2 origin)
    {
        life[i] = Next(fx.Lifetime);
        float speed = Next(fx.Speed);
        size[i] = Next(fx.Size);
        float rotation = Next(fx.Rotation);
        var spin = Unit(rotation);
        (cos[i], sin[i]) = (spin.X, spin.Y);
        if (angle != null) angle[i] = rotation;
        float mix = random.NextSingle();
        static int Channel(int a, int b, float t) => (int)(a + (b - a) * t + .5f);
        color[i] = System.Drawing.Color.FromArgb(Channel(colorMin.A, colorMax.A, mix), Channel(colorMin.R, colorMax.R, mix),
            Channel(colorMin.G, colorMax.G, mix), Channel(colorMin.B, colorMax.B, mix)).ToArgb();
        var side = new Vector2(-axis.Y, axis.X);
        float turn = random.NextSingle(-.5f, .5f) * fx.Spread;
        Vector2 at = Vector2.Zero;
        switch (fx.Shape)
        {
            case ParticleShape.Circle:
            {
                float radius = MathF.Sqrt(random.NextSingle()) * fx.Radius;
                var unit = random.NextSingleUnit2();
                at = new(unit.X * radius, unit.Y * radius);
                break;
            }
            case ParticleShape.Edge: at = side * random.NextSingle(-.5f * fx.Width, .5f * fx.Width); break;
            case ParticleShape.Box:
            {
                var p = random.NextSinglePointInRectangle(fx.Width, fx.Height);
                at = new(p.X - fx.Width * .5f, p.Y - fx.Height * .5f);
                break;
            }
            case ParticleShape.Cone:
            {
                float t = random.NextSingle(-1, 1);
                at = side * (t * fx.Radius);
                turn = t * fx.Spread * .5f;
                break;
            }
        }
        Vector2 direction;
        if (fx.Shape == ParticleShape.Circle)
            direction = at == Vector2.Zero ? axis : at / at.Length();
        else
        {
            direction = Unit(fx.Direction + turn);
        }
        var point = origin + at;
        if (fx.Space == ParticleSpace.World)
        {
            point = Affine2D.Transform(point, matrix);
            direction = Affine2D.TransformNormal(direction, matrix);
            float length = direction.Length();
            if (length > 0) direction /= length;
        }
        x[i] = point.X; y[i] = point.Y;
        if (prevX != null) { prevX[i] = point.X; prevY![i] = point.Y; }
        vx[i] = direction.X * speed; vy[i] = direction.Y * speed;
        birth[i] = now;
        // Drawn last so effects without a random curve keep their stream.
        if (pick != null) pick[i] = random.NextSingle();
    }

    // Integrated particles are at their last tick's position; others follow the closed form at any age.
    private Vector2 At(int i, float age)
    {
        if (integrate) return new(x[i], y[i]);
        float px = x[i] + vx[i] * age;
        if (accelerationX != 0) px += accelerationX * .5f * age * age;
        return new(px, y[i] + vy[i] * age + accelerationY * .5f * age * age);
    }

    // Lifetime t as a LUT cell k and the fraction u towards the next sample, computed once per particle for every curve.
    private static int Cell(float t, out float u)
    {
        float f = Math.Clamp(t, 0, 1) * (ParticleLut.Size - 1);
        int k = Math.Min((int)f, ParticleLut.Size - 2);
        u = f - k;
        return k;
    }

    // A curve, or each particle's own point between the curve and its Max curve.
    private float Mix(float[] low, float[]? high, int i, int k, float u)
    {
        float a = low[k] + (low[k + 1] - low[k]) * u;
        return high == null ? a : a + (high[k] + (high[k + 1] - high[k]) * u - a) * pick![i];
    }

    private float SizeAt(int i, float t, int k, float u) =>
        sizeLow == null ? size[i] * (1 + (fx.EndSize - 1) * t) : size[i] * Mix(sizeLow, sizeHigh, i, k, u);

    private Color Tint(int i, float t, int k, float u)
    {
        if (tint == null)
        {
            var c = System.Drawing.Color.FromArgb(color[i]);
            return System.Drawing.Color.FromArgb((int)(c.A * (1 + (fx.EndAlpha - 1) * t)), c);
        }
        uint argb = (uint)color[i];
        int o = k * 4;
        float r = tint[o] + (tint[o + 4] - tint[o]) * u, g = tint[o + 1] + (tint[o + 5] - tint[o + 1]) * u,
            b = tint[o + 2] + (tint[o + 6] - tint[o + 2]) * u, a = gradientAlpha ? tint[o + 3] + (tint[o + 7] - tint[o + 3]) * u : 1 + (fx.EndAlpha - 1) * t;
        return System.Drawing.Color.FromArgb((int)((argb >> 24) * a) << 24 | (int)((argb >> 16 & 255) * r) << 16 |
            (int)((argb >> 8 & 255) * g) << 8 | (int)((argb & 255) * b));
    }

    internal int Read(Span<Particle2D> into, Matrix3x2 matrix)
    {
        int n = Math.Min(count, into.Length);
        for (int i = 0; i < n; i++)
        {
            float age = (float)(stepped - birth[i]), t = age / life[i];
            int k = Cell(t, out float u);
            var at = At(i, age);
            if (fx.Space == ParticleSpace.Local) at = Affine2D.Transform(at, matrix);
            into[i] = new(at, SizeAt(i, t, k, u), age, Tint(i, t, k, u));
        }
        return count;
    }

    internal void Draw(ICanvas2D canvas, ParticleTexture builtIn, RectangleF frame, Matrix3x2 matrix, Matrix3x2 view, double now)
    {
        var texture = fx.Resolved ?? builtIn;
        var sheet = fx.Sheet;
        int cells = texture.Cells.Length, n = 0;
        bool local = fx.Space == ParticleSpace.Local;
        // Integrated particles: draw time sits between the previous tick and the last one.
        float blend = integrate && step > 0 ? Math.Clamp(1 - (float)((stepped - now) / step), 0, 1) : 1;
        for (int i = 0; i < count; i++)
        {
            float age = (float)(now - birth[i]);
            if (age < 0 || age >= life[i]) continue;
            float t = age / life[i], u = 0;
            int k = curves ? Cell(t, out u) : 0;
            float scale = SizeAt(i, t, k, u) * .5f, c = cos[i], s = sin[i];
            if (spinLow != null)
            {
                // Draw only: the turned angle is the start rotation plus lifetime × the integral of the speed curve.
                float radians = float.DegreesToRadians(angle![i] + life[i] * Mix(spinLow, spinHigh, i, k, u));
                (c, s) = (MathF.Cos(radians), MathF.Sin(radians));
            }
            var center = integrate ? new Vector2(prevX![i] + (x[i] - prevX[i]) * blend, prevY![i] + (y[i] - prevY[i]) * blend) : At(i, age);
            var right = new Vector2(c * scale, s * scale);
            var down = new Vector2(-s * scale * fx.Aspect, c * scale * fx.Aspect);
            if (local)
            {
                center = Affine2D.Transform(center, matrix);
                right = Affine2D.TransformNormal(right, matrix);
                down = Affine2D.TransformNormal(down, matrix);
            }
            float reach = MathF.Abs(right.X) + MathF.Abs(right.Y) + MathF.Abs(down.X) + MathF.Abs(down.Y);
            if (!frame.IntersectsWith(new(center.X - reach, center.Y - reach, reach * 2, reach * 2))) continue;
            int cell = cells == 1 ? 0 : sheet is { Fps: > 0 } ? (int)(age * sheet.Fps) % cells : Math.Min((int)(t * cells), cells - 1);
            var tint = Tint(i, t, k, u);
            // SDL's multiply adds dst·(1 − srcA) to src·dst, so the colour must be premultiplied or faded particles brighten.
            if (fx.Blend == ParticleBlend.Multiply)
                tint = System.Drawing.Color.FromArgb(tint.A, tint.R * tint.A / 255, tint.G * tint.A / 255, tint.B * tint.A / 255);
            quads[n++] = new(center, right, down, cell, tint);
        }
        if (n != 0) canvas.DrawParticles(texture, quads.AsSpan(0, n), view, fx.Blend);
    }
}

public sealed partial class SceneGame
{
    private Body[] emitters=[], lights=[], particleTileBodies=[];
    private float effectStep;

    // Colliding emitters share the orthogonal tile layers (as dense chunk copies) and the scene's physics queries.
    // Sub-emitters bind to World-space emitters on direct children that use the named asset; a rule without one does nothing.
    private void IndexParticles()
    {
        if (!EngineFeatures.Particles) return;
        foreach (var b in emitters) b.Particles!.SubTarget = false;
        foreach (var b in emitters)
        {
            if (b.Particles!.Effect.SubEmitters is not { } rules) continue;
            var bound = new List<(ParticleSubEmitter, ParticleSystem2D)>();
            foreach (var rule in rules)
                foreach (var child in emitters)
                    if (child.Source.ParentId == b.Id && child.Particles!.Effect.Space == ParticleSpace.World &&
                        child.Source.GetComponent<ParticleEmitter2DComponent>()?.Particles == rule.Particles)
                    {
                        bound.Add((rule, child.Particles));
                        break;
                    }
            b.Particles.BindSubEmitters([.. bound]);
        }
        if (!emitters.Any(b => b.Particles!.Effect.Collision != null)) { particleTileBodies = []; return; }
        particleTileBodies = tileLayers.Where(b => b.Palette != null && b.Map!.Layout == TileGrid.Orthogonal).ToArray();
        var world = new ParticleWorld(Array.ConvertAll(particleTileBodies, b => b.ParticleTiles ??= new(b.Map!, b.Palette!)), this);
        foreach (var b in emitters) if (b.Particles!.Effect.Collision != null) b.Particles.World = world;
    }

    // Editor Scene Play: a saved .particles asset replaces the effect of every emitter that uses it (ParticleSystem2D.Reload
    // decides whether live particles stay), and prefabs spawned later load the new file. A file that does not load or
    // validate throws before any emitter changes.
    internal (int Kept, int Restarted) ReloadParticles(Guid asset)
    {
        var users = byId.Values.Where(b => b.Particles != null &&
            b.Source.GetComponent<ParticleEmitter2DComponent>() is { Legacy: false } e && e.Particles == asset).ToArray();
        if (users.Length == 0 || assets == null || world.ProjectDirectory is not { } root) return default;
        var fx = assets.ReloadParticles(root, asset);
        int kept = 0;
        foreach (var b in users)
            if (b.Particles!.Reload(b.Source, b.Source.GetComponent<ParticleEmitter2DComponent>()!, fx)) kept++;
        IndexParticles();
        AssetsChanged?.Invoke();
        return (kept, users.Length - kept);
    }

    // Plays the emitter's cycle from the start; its time-0 bursts fire on this game tick.
    public bool EmitParticles(Guid id)
    {
        if (!byId.TryGetValue(id,out var b) || !b.Active || b.Particles == null) return false;
        b.Particles.Play();
        return true;
    }

    public int ReadParticles(Guid id, Span<Particle2D> destination) =>
        byId.TryGetValue(id,out var b) && b.Particles != null ? b.Particles.Read(destination,b.MatrixAt(b.Position)) : 0;

    // The last game tick's births, deaths and collisions of one emitter (one aggregate read instead of per-particle callbacks).
    public ParticleEvents ReadParticleEvents(Guid id) =>
        byId.TryGetValue(id,out var b) && b.Particles != null ? b.Particles.Events : default;

    private void StepEffects(float seconds)
    {
        effectStep=seconds;
        if (!EngineFeatures.Particles) return;
        foreach (var b in emitters) { b.Particles!.BeginTick(); b.Particles.Live = b.Active; }
        foreach (var b in particleTileBodies)
        {
            b.ParticleTiles!.Active = b.Active;
            if (b.Active) b.ParticleTiles.Matrix = b.MatrixAt(b.Position);
        }
        foreach (var b in emitters)
            if (b.Active) b.Particles!.Step(seconds,Time.GameSeconds,b.MatrixAt(b.Position),b.MotionOwner is { Support:null });
    }
}
