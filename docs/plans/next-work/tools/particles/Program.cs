using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Engine.Core;
using Engine.Desktop;

sealed class Tex : IDisposable { public void Dispose() { } }
sealed class NullCanvas(bool lookup) : ICanvas2D
{
    public readonly ShaderSheets<Tex> Sheets = new((_, _, _) => new Tex());
    public int Draws; public float Sink;
    public void Clear(Color c) { }
    public void FillRectangle(RectangleF r, Color c) { }
    public void DrawText(string text, float x, float y, float size, Color c) { }
    public bool DrawSprite(string p, RectangleF r) => true;
    public bool DrawAffine(string? p, RectangleF r, Matrix3x2 m, Color tint, SpriteRegion? source = null, string layout = TileGrid.Orthogonal) => true;
    public void DrawShader(ShaderClip shader, int frame, RectangleF local, Matrix3x2 m, Color tint, bool additive)
    {
        if (lookup) Sheets.Get(shader, Color.White, additive);  // same lookup SdlCanvas.DrawShader does per call
        Draws++;
        // copy of SdlSpriteBatch.Add vertex work (outline lookup, colour convert, 4 transforms) without the SDL submit
        var shape = TileGrid.Outline(TileGrid.Orthogonal);
        var color = new Vector4(tint.R / 255f, tint.G / 255f, tint.B / 255f, tint.A / 255f);
        var src = shader.Region(frame);
        foreach (var uv in shape)
        {
            float u = (src.X + uv.X * src.Width) / 12f, v = (src.Y + uv.Y * src.Height) / 12f;
            var pt = Vector2.Transform(TileGrid.Vertex(local, uv), m);
            verts[vi] = new(pt, u, v); cols[vi] = color; vi = (vi + 1) & 4095;
        }
    }
    readonly Vector4[] verts = new Vector4[4096]; readonly Vector4[] cols = new Vector4[4096]; int vi;
}
static class P
{
    static void Main()
    {
        for (int pass = 0; pass < 2; pass++)
        {
            Console.WriteLine($"-- pass {pass}");
            foreach (int emitters in new[] { 1, 20 })
            foreach (bool lookup in new[] { false, true })
                Run(emitters, lookup);
            Soa(512 * 20);
        }
    }
    static void Run(int emitterCount, bool lookup)
    {
        var cfg = new ParticleEmitter2DComponent { Count = 256, Lifetime = 60 };
        var bursts = Enumerable.Range(0, emitterCount).Select(_ => new ParticleBurst(Guid.NewGuid(), cfg)).ToArray();
        var canvas = new NullCanvas(lookup);
        var view = Matrix3x2.Identity; var frame = new RectangleF(-100000, -100000, 200000, 200000);
        double now = 0; const double dt = 1 / 60.0;
        void Tick(bool emit)
        {
            now += dt;
            foreach (var b in bursts) { b.Step(false, new(0, 0), now); if (emit) b.Emit(new(0, 0), now); b.Draw(canvas, ShaderClip.Puff, frame, view, now); }
        }
        for (int i = 0; i < 600; i++) Tick(true); // warm: 512 alive per emitter (cap)
        // steady state draw+step at cap, no emission
        const int N = 2000;
        long a0 = GC.GetAllocatedBytesForCurrentThread(); var sw = Stopwatch.StartNew();
        for (int i = 0; i < N; i++) Tick(false);
        sw.Stop(); long a1 = GC.GetAllocatedBytesForCurrentThread();
        double perFrameUs = sw.Elapsed.TotalMicroseconds / N; int alive = 512 * emitterCount;
        Console.WriteLine($"step+draw emitters={emitterCount} alive={alive} lookup={lookup}: {perFrameUs:F1} us/frame, {perFrameUs * 1000 / alive:F1} ns/particle, alloc {(a1 - a0) / (double)N:F1} B/frame");
        // emission churn: emit 256 per emitter per frame at cap (RemoveRange + Add)
        a0 = GC.GetAllocatedBytesForCurrentThread(); sw.Restart();
        for (int i = 0; i < N; i++) Tick(true);
        sw.Stop(); a1 = GC.GetAllocatedBytesForCurrentThread();
        Console.WriteLine($"  + emit 256/emitter/frame: {sw.Elapsed.TotalMicroseconds / N:F1} us/frame, alloc {(a1 - a0) / (double)N:F1} B/frame");
    }
    // Reference SoA kernel: integrate + write 4 vertices (pos+uv+color) per particle, no virtual call per particle.
    static void Soa(int n)
    {
        var px = new float[n]; var py = new float[n]; var vx = new float[n]; var vy = new float[n]; var age = new float[n]; var life = new float[n]; var size = new float[n];
        var verts = new Vector4[n * 4]; var cols = new uint[n * 4];
        var r = new SeededRandom(1);
        for (int i = 0; i < n; i++) { vx[i] = r.NextInt(100) - 50; vy[i] = -r.NextInt(100); life[i] = 60; size[i] = 5; }
        float dt = 1 / 60f; float g = 130;
        void Frame()
        {
            for (int i = 0; i < n; i++)
            {
                age[i] += dt; vy[i] += g * dt; px[i] += vx[i] * dt; py[i] += vy[i] * dt;
                float t = 1 - age[i] / life[i]; float s = size[i] * (.55f + .45f * t); uint c = (uint)(t * 255) << 24 | 0xE6BE8C;
                int v = i * 4;
                verts[v] = new(px[i] - s, py[i] - s * .5f, 0, 0); verts[v + 1] = new(px[i] + s, py[i] - s * .5f, 1, 0);
                verts[v + 2] = new(px[i] + s, py[i] + s * .5f, 1, 1); verts[v + 3] = new(px[i] - s, py[i] + s * .5f, 0, 1);
                cols[v] = cols[v + 1] = cols[v + 2] = cols[v + 3] = c;
            }
        }
        for (int i = 0; i < 300; i++) Frame();
        const int N = 2000; long a0 = GC.GetAllocatedBytesForCurrentThread(); var sw = Stopwatch.StartNew();
        for (int i = 0; i < N; i++) Frame();
        sw.Stop(); long a1 = GC.GetAllocatedBytesForCurrentThread();
        Console.WriteLine($"SoA reference n={n}: {sw.Elapsed.TotalMicroseconds / N:F1} us/frame, {sw.Elapsed.TotalMicroseconds * 1000 / N / n:F2} ns/particle, alloc {(a1 - a0) / (double)N:F1} B/frame");
    }
}
