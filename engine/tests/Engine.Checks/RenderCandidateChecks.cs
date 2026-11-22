using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

internal static class RenderCandidateChecks
{
    private const int Warmup = 24, Samples = 160;

    internal static void Run(string project, string output)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Directory.CreateDirectory(output);
        var scene = SceneDocument.Load(Path.Combine(project, "Scenes", "Field10000.scene"));
        using var game = new SceneGame(scene, new ProjectScripts(project));
        Guid Id(string name) => scene.Objects.Single(o => o.Name == name).Id;
        var amber = Id("Amber marker");
        var blue = Id("Blue marker");
        var cameras = new[] { ("primary", scene.Camera!.Id), ("minimap", Id("Field minimap")) };
        Require(scene.Objects.Count(o => o.GetComponent<Collider2DComponent>() != null) == 10000, "collider count");
        Sdl.Init.Initialize(SdlInitFlags.Video);
        try
        {
            using var window = Sdl.Video.CreateWindow("CollisionLab", game.LogicalSize.Width, game.LogicalSize.Height, 0);
            using var renderer = Sdl.Render.CreateRenderer(window, "software");
            using var canvas = new SdlCanvas(renderer, batchSprites: true) { LogicalSize = game.LogicalSize };
            if (!renderer.SetRenderVSync(0)) throw new IOException(Sdl.PInvoke.GetError());
            var checks = new List<object>();
            var measurements = new List<object>();
            var rows = new List<string> { "camera,path,sample,candidates,cpu_render_ms,shared_prepare_ms" };

            void Pose(int x, int y) => Require(game.SetLocalTransform(amber, new(new(x, y), 0, Vector2.One)), "marker pose");
            void Flush()
            {
                canvas.FlushSprites();
                if (!Sdl.Render.FlushRenderer(renderer)) throw new IOException(Sdl.PInvoke.GetError());
            }

            foreach (var (pose, x, y) in new[] { ("behind",414,300), ("tie",414,308), ("front",414,316),
                                                 ("minimap-only",1008,316), ("outside",-3000,316) })
            {
                Pose(x, y);
                foreach (var (name, id) in cameras)
                {
                    Require(game.TryGetView(id, out var view), "camera");
                    byte[]? original = null;
                    var originalOrder = new List<Guid>();
                    for (int mode = 0; mode < 2; mode++)
                    {
                        string path = mode == 0 ? "scan" : "grid";
                        game.UseRenderGrid = mode == 1;
                        game.PrepareCameraDraw(1);
                        canvas.Clear(SceneGame.Background);
                        var order = new List<Guid>();
                        int candidates = game.DrawCamera(canvas, id, 1, order);
                        Flush();
                        string? screenshot = pose is "behind" or "front" ? Path.Combine(output, $"{name}-{pose}-{path}.png") : null;
                        var pixels = ReadPixels(renderer, view.Viewport, screenshot);
                        if (original == null) { original = pixels; originalOrder = order; }
                        else
                        {
                            Require(original.AsSpan().SequenceEqual(pixels), $"{name} {pose} pixel mismatch");
                            Require(originalOrder.SequenceEqual(order), $"{name} {pose} sprite order mismatch");
                        }
                        int a = order.IndexOf(amber), b = order.IndexOf(blue);
                        Require(b >= 0 && a == order.LastIndexOf(amber) && b == order.LastIndexOf(blue), "marker membership");
                        if (pose is "outside" or "minimap-only")
                            Require((a >= 0) == (pose == "minimap-only" && name == "minimap"), "marker crossing camera boundary");
                        else
                        {
                            Require(a >= 0 && (a > b) == (pose == "front"), "Y order including stable ties");
                            var point = view.WorldToScreen(new(440, 330));
                            int offset = 4 * (((int)point.Y - view.Viewport.Top) * view.Viewport.Width + (int)point.X - view.Viewport.Left);
                            byte[] expected = pose == "front" ? [237,171,68,255] : [66,111,204,255];
                            Require(pixels.AsSpan(offset, 4).SequenceEqual(expected), $"{name} {pose} overlap color");
                        }
                        checks.Add(new { camera = name, pose, path, candidates, visible = order.Count,
                            rgba = Convert.ToHexString(SHA256.HashData(pixels)), order = Convert.ToHexString(
                                SHA256.HashData(Encoding.ASCII.GetBytes(string.Join(",", order)))) });
                    }
                }
            }

            Pose(414, 300);
            byte[]? reference = null;
            foreach (bool indexed in new[] { false, true })
            {
                game.UseRenderGrid = indexed;
                game.Draw(canvas, 1);
                Flush();
                var pixels = ReadPixels(renderer, new(Point.Empty, game.LogicalSize),
                    Path.Combine(output, indexed ? "composite-grid.png" : "composite-scan.png"));
                if (reference == null) reference = pixels;
                else Require(reference.AsSpan().SequenceEqual(pixels), "composited frame including HUD");
            }

            foreach (var (name, id) in cameras)
            {
                var times = new[] { new List<double>(), new List<double>() };
                var prep = new[] { new List<double>(), new List<double>() };
                var counts = new int[2];
                for (int i = -Warmup; i < Samples; i++)
                    for (int half = 0; half < 2; half++)
                    {
                        int mode = (i + half) & 1;
                        game.UseRenderGrid = mode == 1;
                        canvas.Clear(SceneGame.Background);
                        Flush();
                        long start = Stopwatch.GetTimestamp();
                        game.PrepareCameraDraw(1);
                        long ready = Stopwatch.GetTimestamp();
                        int candidates = game.DrawCamera(canvas, id, 1);
                        Flush();
                        long end = Stopwatch.GetTimestamp();
                        if (i < 0) continue;
                        double ms = Stopwatch.GetElapsedTime(ready, end).TotalMilliseconds;
                        double setup = Stopwatch.GetElapsedTime(start, ready).TotalMilliseconds;
                        times[mode].Add(ms);
                        prep[mode].Add(setup);
                        counts[mode] = candidates;
                        rows.Add(FormattableString.Invariant($"{name},{(mode == 0 ? "scan" : "grid")},{i},{candidates},{ms:F6},{setup:F6}"));
                    }
                Require(counts[1] > 0 && counts[1] < counts[0], "camera index did not reduce candidates");
                double P95(List<double> values) => values.Order().ElementAt((int)Math.Ceiling(.95 * values.Count) - 1);
                for (int mode = 0; mode < 2; mode++)
                {
                    string path = mode == 0 ? "scan" : "grid";
                    measurements.Add(new { camera = name, path, candidates = counts[mode],
                        renderP95Ms = P95(times[mode]), sharedPrepareP95Ms = P95(prep[mode]) });
                    Console.WriteLine($"{name} {path}: {counts[mode]} candidates, render p95 {P95(times[mode]):F4} ms");
                }
            }
            File.WriteAllLines(Path.Combine(output, "timings.csv"), rows);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new {
                rid = RuntimeInformation.RuntimeIdentifier, os = RuntimeInformation.OSDescription,
                renderer = Sdl.Render.GetRendererName(renderer), totalObjects = scene.Objects.Count, warmup = Warmup,
                samples = Samples, compositeRgba = Convert.ToHexString(SHA256.HashData(reference!)), checks, measurements
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { Sdl.Init.Quit(); }
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidDataException(message);
    }

    private static unsafe byte[] ReadPixels(SdlRenderer renderer, Rectangle rect, string? path)
    {
        var area = new SdlRect { x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height };
        var surface = Sdl.PInvoke.RenderReadPixels(renderer, &area);
        if (surface == null) throw new IOException(Sdl.PInvoke.GetError());
        try
        {
            if (path != null && Sdl.PInvoke.SavePNG(surface, path) == 0) throw new IOException(Sdl.PInvoke.GetError());
            var rgba = Sdl.PInvoke.ConvertSurface(surface, SdlPixelFormat.Rgba32);
            if (rgba == null) throw new IOException(Sdl.PInvoke.GetError());
            try
            {
                int stride = rgba->w * 4;
                var pixels = new byte[stride * rgba->h];
                for (int y = 0; y < rgba->h; y++)
                    Marshal.Copy((nint)((byte*)rgba->pixels + y * rgba->pitch), pixels, y * stride, stride);
                return pixels;
            }
            finally { Sdl.PInvoke.DestroySurface(rgba); }
        }
        finally { Sdl.PInvoke.DestroySurface(surface); }
    }
}
