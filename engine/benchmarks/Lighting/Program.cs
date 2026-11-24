using System.Diagnostics;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

// Roadmap 16.7: draw cost of PlatformerLab's Cave per frame. Modes: "off" deactivates Global Light 2D (all 8 point lights
// add to the screen as before 16.7, no shadows); a number N keeps lighting on with the first N shadow lights active, "Nn"
// the same lights without shadows.
// Renderers: "sw" is the player's SdlCanvas on an 800×450 software renderer (the headless replay path; CPU rasterises
// everything), "metal" the Metal renderer of a hidden window drawing into an 800×450 target, synchronised every frame by a 1×1 read-back.
// Usage: Lighting <Cave.scene> <sw|metal> <off|N> [frames]. Times only Draw + submit; the tick runs outside the timer.
static unsafe class P
{
    static void Main(string[] args)
    {
        var scene = SceneDocument.Load(Path.GetFullPath(args[0]));
        bool metal = args[1] == "metal";
        string mode = args[2];
        int frames = args.Length > 3 ? int.Parse(args[3]) : 600;
        SdlWindow window = default;
        SdlSurface* surface = null;
        SdlRenderer renderer;
        if (metal)
        {
            Sdl.Init.InitSubSystem(SdlInitFlags.Video);
            window = Sdl.Video.CreateWindow("Lighting bench", 800, 450, SdlWindowFlags.Hidden);
            renderer = Sdl.Render.CreateRenderer(window, "metal");
            // An offscreen target: reading back from the window's drawable waits for the compositor (about 8 ms).
            var target = renderer.CreateTexture(SdlPixelFormat.Rgba32, SdlTextureAccess.Target, 800, 450);
            if (!renderer.SetRenderTarget(target)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        }
        else
        {
            surface = Sdl.PInvoke.CreateSurface(800, 450, SdlPixelFormat.Rgba32);
            renderer = Sdl.Render.CreateSoftwareRenderer(surface);
        }
        // "Nn": N lights without shadows.
        if (mode.EndsWith('n')) { foreach (var o in scene.Objects) if (o.GetComponent<PointLight2DComponent>() is {} l) l.Shadows = false; mode = mode[..^1]; }
        using var canvas = new SdlCanvas(renderer, batchSprites: true, previewShader: ShaderPixels.Preview);
        using var game = new GameSession(scene);
        var world = game.Current;
        var lights = scene.Objects.Where(o => o.GetComponent<PointLight2DComponent>() != null).ToArray();
        if (mode == "off") world.SetActive(scene.Objects.Single(o => o.GetComponent<GlobalLight2DComponent>() != null).Id, false);
        else for (int i = int.Parse(mode); i < lights.Length; i++) world.SetActive(lights[i].Id, false);
        var one = new SdlRect { w = 1, h = 1 };
        var times = new double[frames];
        long bytes = 0, calls = 0;
        for (int f = -360; f < frames; f++)
        {
            game.FixedUpdate(game.Actions.Take(), 1f / 60);
            long submitted = canvas.DrawSubmissions, allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            game.Draw(canvas, 1);
            canvas.FlushSprites();
            if (metal) Sdl.PInvoke.DestroySurface(Sdl.PInvoke.RenderReadPixels(renderer, &one));
            long end = Stopwatch.GetTimestamp();
            if (f < 0) continue;
            times[f] = Stopwatch.GetElapsedTime(start, end).TotalMilliseconds;
            bytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
            calls += canvas.DrawSubmissions - submitted;
        }
        Array.Sort(times);
        Console.WriteLine($"{args[1]}\t{args[2]}\tmedian_ms={times[frames / 2]:F3}\tp95_ms={times[frames * 95 / 100]:F3}\tmean_ms={times.Average():F3}" +
            $"\tdraws={calls / (double)frames:F1}\tB/frame={bytes / (double)frames:F1}\trebuilds/frame={world.ShadowRebuilds / (double)(frames + 360):F2}\tcasters/edges/quads={world.ShadowTotals()}");
        canvas.Dispose();
        renderer.Dispose();
        if (metal) window.Dispose();
        else Sdl.PInvoke.DestroySurface(surface);
    }
}
