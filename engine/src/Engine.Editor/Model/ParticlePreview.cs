using System.Drawing;
using System.Numerics;
using Engine.Core;

namespace Engine.Editor;

// Edit-time particle playback for the Particle panel and the Scene view. It steps the player's own ParticleSystem2D at the
// fixed game tick from time 0 with the emitter's seed and world pose, so the state at tick t is the state Play reaches after
// t ticks (scrubbing simulates again from 0). It never writes the scene or the asset. Collision needs the running game's tile
// and physics world and is skipped here.
internal sealed class ParticlePreview
{
    internal const float Step = (float)GameTime.FixedStep;
    private static readonly ParticleTexture BuiltIn = new(null, ShaderClip.Puff, [ShaderClip.Puff.Region(0)]);
    private sealed record Emitter(SceneObject Owner, ParticleEmitter2DComponent Source, ParticleSystem2D System);

    private Emitter[] emitters = [];
    private IReadOnlyList<SceneObject> owners = [];
    private Func<ParticleEmitter2DComponent, ParticleEffect> effects = _ => new();
    private uint seed;
    private Particle2D[] buffer = [];

    public int Tick { get; private set; }
    public double Now { get; private set; }
    // Fraction of the next tick already elapsed; draws sit between the last two ticks as in the game loop.
    public float Interpolation { get; set; } = 1;
    public double StepMicroseconds { get; private set; }
    public IReadOnlyList<SceneObject> Owners => owners;
    public bool IsEmpty => emitters.Length == 0;
    public int Count { get { int n = 0; foreach (var e in emitters) n += e.System.Count; return n; } }
    // Seconds the preview plays before it starts again: the longest emitter cycle plus its longest lifetime.
    public float Span { get { float s = .1f; foreach (var e in emitters) s = MathF.Max(s, e.System.Effect.Duration + e.System.Effect.Lifetime.Max); return MathF.Min(s, 120); } }

    // effect resolves an emitter's .particles asset (the Particle panel passes its unsaved copy for the asset it edits).
    // seed 0 keeps each emitter's own seed.
    public void Build(IReadOnlyList<SceneObject> objects, Func<ParticleEmitter2DComponent, ParticleEffect> effect, uint seedOverride = 0)
    {
        (owners, effects, seed) = (objects, effect, seedOverride);
        Restart();
    }

    public void Clear() => Build([], _ => new());

    public void Restart()
    {
        (Tick, Now) = (0, 0);
        var list = new List<Emitter>();
        foreach (var owner in owners)
            if (owner.GetComponent<ParticleEmitter2DComponent>() is { } source)
                list.Add(new(owner, source, new ParticleSystem2D(owner, seed == 0 ? source : source with { Seed = seed }, effects(source))));
        emitters = [.. list];
        // Sub-emitters bind as in the game: World-space emitters on direct children that use the rule's asset.
        foreach (var e in emitters)
        {
            if (e.System.Effect.SubEmitters is not { } rules) continue;
            var bound = new List<(ParticleSubEmitter, ParticleSystem2D)>();
            foreach (var rule in rules)
                if (Array.Find(emitters, c => c.Owner.ParentId == e.Owner.Id && c.System.Effect.Space == ParticleSpace.World && c.Source.Particles == rule.Particles) is { } child)
                    bound.Add((rule, child.System));
            e.System.BindSubEmitters([.. bound]);
        }
        // Landing and Manual emitters play from time 0 too; a sub-emitter target only emits for its parent.
        foreach (var e in emitters) if (e.Source.Trigger != ParticleTrigger.Start && !e.System.SubTarget) e.System.Play();
        int most = 0;
        foreach (var e in emitters) most = Math.Max(most, e.System.Effect.MaxParticles);
        if (buffer.Length < most) buffer = new Particle2D[most];
    }

    // One game tick, in the order SceneGame uses: the clock advances, then every emitter steps at the new time.
    public void Advance()
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        Now += Step;
        foreach (var e in emitters) { e.System.BeginTick(); e.System.Live = true; }
        foreach (var e in emitters) e.System.Step(Step, Now, Pose(e.Owner), false);
        Tick++;
        StepMicroseconds = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMicroseconds;
    }

    public void Seek(int tick)
    {
        Restart();
        for (int i = 0; i < tick; i++) Advance();
    }

    // The body pose SceneGame steps emitters with: the object's world matrix (MatrixAt(Position)).
    private static Matrix3x2 Pose(SceneObject owner) => owner.WorldMatrix;

    // FNV-1a over every emitter's particles (position, size and age bits, colour) as ParticleLab's ParticleTrace records them.
    public ulong Hash()
    {
        static ulong Mix(ulong hash, uint value) => (hash ^ value) * 1099511628211UL;
        ulong hash = 14695981039346656037UL;
        foreach (var e in emitters)
        {
            int n = e.System.Read(buffer, Pose(e.Owner));
            hash = Mix(hash, (uint)n);
            for (int i = 0; i < Math.Min(n, buffer.Length); i++)
            {
                var p = buffer[i];
                hash = Mix(Mix(Mix(Mix(Mix(hash, BitConverter.SingleToUInt32Bits(p.Position.X)), BitConverter.SingleToUInt32Bits(p.Position.Y)),
                    BitConverter.SingleToUInt32Bits(p.Size)), BitConverter.SingleToUInt32Bits(p.Age)), (uint)p.Color.ToArgb());
            }
        }
        return hash;
    }

    private double DrawTime => Math.Max(0, Now - (1 - Interpolation) * Step);

    // The owner's particles in the Scene view's draw order (world coordinates).
    public void Draw(ICanvas2D canvas, SceneObject owner, RectangleF visible)
    {
        foreach (var e in emitters)
            if (e.Owner == owner) e.System.Draw(canvas, BuiltIn, visible, Pose(owner), Matrix3x2.Identity, DrawTime);
    }

    public void DrawAll(ICanvas2D canvas, RectangleF visible, Matrix3x2 view)
    {
        foreach (var e in emitters) e.System.Draw(canvas, BuiltIn, visible, Pose(e.Owner), view, DrawTime);
    }
}
