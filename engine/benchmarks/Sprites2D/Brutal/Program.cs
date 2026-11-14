using System.Drawing;
using Brutal.SdlApi;
using Engine.Desktop;

if (args.Length > 2 || (args.Length == 2 && args[1] is not ("--unbatched" or "--verify-batching")))
    throw new ArgumentException("Usage: Brutal [SDL renderer name [--unbatched | --verify-batching]]");
bool batchSprites = args.ElementAtOrDefault(1) != "--unbatched";
var sprites = SpriteBenchmark.CreateSprites((x, y, r, g, b) =>
    (Bounds: new RectangleF(x, y, SpriteBenchmark.SpriteSize, SpriteBenchmark.SpriteSize), Tint: Color.FromArgb(r, g, b)));
var benchmark = new SpriteBenchmark();
Sdl.Init.Initialize(SdlInitFlags.Video);
try
{
    using var window = Sdl.Video.CreateWindow("BRUTAL sprite benchmark", SpriteBenchmark.Width, SpriteBenchmark.Height, 0);
    using var renderer = Sdl.Render.CreateRenderer(window, args.FirstOrDefault()!);
    if (!renderer.SetRenderVSync(0) || !renderer.GetRenderVSync(out int vsync) || vsync != 0)
        throw new InvalidOperationException("Could not disable SDL VSync.");
    if (!renderer.GetRenderOutputSize(out int width, out int height))
        throw new InvalidOperationException(Sdl.PInvoke.GetError());
    if (args.ElementAtOrDefault(1) == "--verify-batching")
    {
        BatchingCheck.Run(renderer, sprites);
        return 0;
    }
    string mode = batchSprites ? "geometry" : "unbatched";
    SpriteBenchmark.ReportConfiguration($"BRUTAL {Sdl.Render.GetRendererName(renderer)} {mode}", width, height);
    using var canvas = new SdlCanvas(renderer, batchSprites);
    while (!benchmark.Finished)
    {
        while (Sdl.Events.PollEvent(out var ev))
            if (ev.type is SdlEventType.Quit or SdlEventType.WindowCloseRequested) return 1;

        benchmark.BeginDraw();
        canvas.Clear(Color.Black);
        foreach (var sprite in sprites.AsSpan(0, benchmark.Count))
            canvas.DrawSquareSprite(sprite.Bounds, sprite.Tint);
        canvas.FlushSprites();
        benchmark.EndDraw();
        if (benchmark.FirstWarmup)
        {
            var last = sprites[benchmark.Count - 1];
            VerifyPixel(renderer, (int)(last.Bounds.X + last.Bounds.Width / 2), (int)(last.Bounds.Y + last.Bounds.Height / 2), last.Tint);
        }
        if (!renderer.RenderPresent()) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        benchmark.EndPresent();
    }
    return 0;
}
finally { Sdl.Init.Quit(); }

static unsafe void VerifyPixel(SdlRenderer renderer, int x, int y, Color expected)
{
    var rect = new SdlRect { x = x, y = y, w = 1, h = 1 };
    var surface = Sdl.PInvoke.RenderReadPixels(renderer, &rect);
    if (surface == null) throw new InvalidOperationException(Sdl.PInvoke.GetError());
    try
    {
        byte r, g, b, a;
        if (Sdl.PInvoke.ReadSurfacePixel(surface, 0, 0, &r, &g, &b, &a) == 0 ||
            r != expected.R || g != expected.G || b != expected.B)
            throw new InvalidOperationException($"Incorrect rendered pixel at ({x},{y}), expected {expected}, got ({r},{g},{b}).");
    }
    finally { Sdl.PInvoke.DestroySurface(surface); }
}
