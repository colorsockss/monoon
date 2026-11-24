using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Engine.Core;

// 14.11 frame-cost baseline: ParticleLab's Stress effect (Assets/Storm.particles, 10,240 live particles) steps and draws with
// 0 B allocated per frame and a median Step + Draw within the budget. The canvas does SdlSpriteBatch.Quad's vertex math but
// no SDL submit (the scope of engine/benchmarks/Particles "null"). Wall-clock timing on a shared machine: the median of 600
// frames after a 0.5 s warm-up (JIT tiering) keeps other processes' load out of the verdict. MONOON_PARTICLE_BUDGET_CSV
// names a file for the per-frame microseconds and allocated bytes.
internal static class ParticleBudgetChecks
{
    [Check(3000, "particle-budget")] static void Registered(CheckRun c) => Run(c.Repository);
    internal const double BudgetMicroseconds = 500;
    private const float Dt = 1f / 60;

    private sealed class VertexCanvas : ICanvas2D
    {
        private readonly Vector4[] vertices = new Vector4[4096], colors = new Vector4[4096];
        private int next;
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF rectangle, Color color) { }
        public void DrawText(string text, float x, float y, float size, Color color) { }
        public bool DrawSprite(string path, RectangleF destination) => true;
        public void DrawParticles(ParticleTexture texture, ReadOnlySpan<ParticleQuad> quads, Matrix3x2 view, ParticleBlend blend)
        {
            foreach (ref readonly var q in quads)
            {
                var cell = texture.Cells[q.Cell];
                var color = new Vector4(q.Tint.R / 255f, q.Tint.G / 255f, q.Tint.B / 255f, q.Tint.A / 255f);
                float u0 = cell.X / 12f, v0 = cell.Y / 12f, u1 = (cell.X + cell.Width) / 12f, v1 = (cell.Y + cell.Height) / 12f;
                Put(Affine2D.Transform(q.Center - q.Right - q.Down, view), u0, v0, color);
                Put(Affine2D.Transform(q.Center + q.Right - q.Down, view), u1, v0, color);
                Put(Affine2D.Transform(q.Center + q.Right + q.Down, view), u1, v1, color);
                Put(Affine2D.Transform(q.Center - q.Right + q.Down, view), u0, v1, color);
            }
        }
        private void Put(Vector2 p, float u, float v, Vector4 color) { vertices[next] = new(p, u, v); colors[next] = color; next = (next + 1) & 4095; }
    }

    internal static void Run(string repository)
    {
        var fx = ParticleEffect.Load(Path.Combine(repository, "samples", "ParticleLab", "Assets", "Storm.particles"));
        var system = new ParticleSystem2D(new SceneObject { Name = "Storm", Width = 1, Height = 1 },
            new ParticleEmitter2DComponent { Trigger = ParticleTrigger.Start, Seed = 1 }, fx);
        var canvas = new VertexCanvas();
        var frame = new RectangleF(-100000, -100000, 200000, 200000);
        var puff = new ParticleTexture(null, ShaderClip.Puff, [ShaderClip.Puff.Region(0)]);
        double now = 0;
        void Frame()
        {
            now += Dt;
            system.Step(Dt, now, Matrix3x2.Identity, false);
            system.Draw(canvas, puff, frame, Matrix3x2.Identity, Matrix3x2.Identity, now);
        }
        var warm = Stopwatch.StartNew();
        for (int i = 0; i < 600 || warm.ElapsedMilliseconds < 500; i++) Frame();
        var times = new double[600];
        var bytes = new long[times.Length];
        for (int i = 0; i < times.Length; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            Frame();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
            bytes[i] = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        long allocated = bytes.Sum();
        if (Environment.GetEnvironmentVariable("MONOON_PARTICLE_BUDGET_CSV") is { Length: > 0 } csv)
            File.WriteAllLines(csv, ["frame,step_draw_us,allocated_bytes", .. times.Select((t, i) => FormattableString.Invariant($"{i},{t:F2},{bytes[i]}"))]);
        Array.Sort(times);
        double median = times[times.Length / 2], p95 = times[times.Length * 95 / 100];
        string line = $"{system.Count:N0} particles (Storm.particles): Step + Draw median {median:F1} µs, p95 {p95:F1} µs, " +
            $"{allocated} B over {times.Length} frames (budget {BudgetMicroseconds} µs, SDL submit excluded)";
        // MONOON_QA_SLOW marks an emulated host (QEMU TCG x64 guest, ~10-30x slower): the time is reported, not judged there.
        bool emulated = Environment.GetEnvironmentVariable("MONOON_QA_SLOW") is { Length: > 0 } slow && slow != "1";
        if (system.Count != 10240 || allocated != 0 || (!emulated && median > BudgetMicroseconds)) throw new Exception(line);
        if (emulated) line += " — time not judged (MONOON_QA_SLOW)";
        Console.WriteLine("PASS particle-budget: " + line);
    }
}
