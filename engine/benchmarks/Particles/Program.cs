using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

// Step + draw cost of ParticleSystem2D at 1,024 and 10,240 live particles (2 and 20 emitters of 512, as in the P.0
// bench). "null": the canvas does SdlSpriteBatch.Quad's vertex math without the SDL submit (same scope as P.0).
// "sdl": the player's SdlCanvas on a 64×64 software renderer, submit and rasterisation included.
sealed class NullCanvas : ICanvas2D
{
    readonly Vector4[] verts = new Vector4[4096]; readonly Vector4[] cols = new Vector4[4096]; int vi;
    public void Clear(Color c) { }
    public void FillRectangle(RectangleF r, Color c) { }
    public void DrawText(string text, float x, float y, float size, Color c) { }
    public bool DrawSprite(string p, RectangleF r) => true;
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
    void Put(Vector2 p, float u, float v, Vector4 color) { verts[vi] = new(p, u, v); cols[vi] = color; vi = (vi + 1) & 4095; }
}

static unsafe class P
{
    static void Main(string[] args)
    {
        bool sdl = args.Contains("sdl");
        var surface = Sdl.PInvoke.CreateSurface(64, 64, SdlPixelFormat.Rgba32);
        var renderer = Sdl.Render.CreateSoftwareRenderer(surface);
        using var sdlCanvas = new SdlCanvas(renderer, batchSprites: true, previewShader: ShaderPixels.Preview);
        ICanvas2D canvas = sdl ? sdlCanvas : new NullCanvas();
        for (int pass = 0; pass < 2; pass++)
            foreach (int emitters in new[] { 2, 20 })
                Run(canvas, sdlCanvas, emitters, pass == 1, sdl);
    }

    static void Run(ICanvas2D canvas, SdlCanvas flush, int emitterCount, bool print, bool sdl)
    {
        var fx = new ParticleEffect { Rate = 256, Lifetime = new(2, 2), MaxParticles = 512, Speed = new(0, 20), Spread = 360, Size = new(4, 8) };
        // "curves": a smooth size curve and a colour + alpha gradient (P.4 LUTs).
        if (Environment.GetCommandLineArgs().Contains("curves"))
            fx = fx with { SizeOverLifetime = new([new(0, 0, 0, 6), new(.3f, 1.4f), new(1, 0)], Smooth: true),
                ColorOverLifetime = new([new(0, "#FFF3B0"), new(.35f, "#FF9A3C"), new(1, "#7A1A10")], [new(0, 0), new(.1f, 1), new(1, 0)]) };
        // "noise": integrated motion with a value-noise force (P.8). "tiles": tile collision against a solid band 16 px
        // under the emitter centre (every particle looks up its cell each tick; many bounce).
        if (Environment.GetCommandLineArgs().Contains("noise")) fx = fx with { Noise = new(60, .05f, .5f) };
        ParticleWorld? world = null;
        if (Environment.GetCommandLineArgs().Contains("tiles"))
        {
            fx = fx with { Gravity = 60, Collision = new(Bounce: .5f) };
            var region = new SpriteRegion(0, 0, 16, 16);
            var map = new Tilemap2DComponent { CellSize = new(16, 16) };
            for (int column = -8; column < 12; column++) for (int row = 3; row < 6; row++) map.Set(new(column, row), 1);
            world = new([new ParticleTiles(map, new("tiles.png", new Dictionary<int, TileDefinition> { [1] = new(1, region, true) })) { Matrix = Matrix3x2.Identity }], null);
        }
        var systems = Enumerable.Range(0, emitterCount).Select(i => new ParticleSystem2D(
            new SceneObject { Name = "E", Width = 64, Height = 64 }, new ParticleEmitter2DComponent { Trigger = ParticleTrigger.Start, Seed = (uint)i + 1 }, fx) { World = world }).ToArray();
        var frame = new RectangleF(-100000, -100000, 200000, 200000);
        var puff = new ParticleTexture(null, ShaderClip.Puff, [ShaderClip.Puff.Region(0)]);
        double now = 0; const float dt = 1 / 60f;
        void Tick()
        {
            now += dt;
            foreach (var s in systems) { s.Step(dt, now, Matrix3x2.Identity, false); s.Draw(canvas, puff, frame, Matrix3x2.Identity, Matrix3x2.Identity, now); }
            if (sdl) flush.FlushSprites();
        }
        for (int i = 0; i < 600; i++) Tick(); // 512 alive per emitter
        const int N = 2000;
        long a0 = GC.GetAllocatedBytesForCurrentThread(); var sw = Stopwatch.StartNew();
        for (int i = 0; i < N; i++) Tick();
        sw.Stop(); long a1 = GC.GetAllocatedBytesForCurrentThread();
        int alive = systems.Sum(s => s.Count);
        double us = sw.Elapsed.TotalMicroseconds / N;
        if (print) Console.WriteLine($"{(sdl ? "sdl" : "null")}{string.Concat(Environment.GetCommandLineArgs().Skip(1).Where(a => a != "sdl").Select(a => "+" + a))}\talive={alive}\tus/frame={us:F1}\tns/particle={us * 1000 / alive:F1}\tB/frame={(a1 - a0) / (double)N:F1}");
    }
}
