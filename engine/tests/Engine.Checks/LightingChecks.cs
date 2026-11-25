using System.Drawing;
using System.Numerics;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

// Roadmap 16.7: shader-free 2D lighting. Shadow quads, the SDL lightmap (software renderer), the PlatformerLab ShowcaseCave scene's
// shadow cache and per-frame allocation, and the scene schema.
internal static class LightingChecks
{
    [Check(2800, "lighting")] static void Registered(CheckRun c) => Run(c.Repository);
    internal static void Run(string repository)
    {
        Geometry();
        Lightmap();
        Cave(Path.Combine(repository, "samples", "PlatformerLab"));
    }

    private static bool Inside(ReadOnlySpan<Vector2> quads, Vector2 p)
    {
        for (int q = 0; q + 3 < quads.Length; q += 4)
        {
            int sign = 0;
            bool inside = true;
            for (int k = 0; k < 4 && inside; k++)
            {
                Vector2 a = quads[q + k], b = quads[q + (k + 1) % 4];
                float cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
                int s = Math.Sign(cross);
                if (s != 0 && sign != 0 && s != sign) inside = false;
                if (s != 0) sign = s;
            }
            if (inside) return true;
        }
        return false;
    }

    private static void Geometry()
    {
        var shadows = new LightShadows();
        var box = Shape2D.Box(new RectangleF(10, -5, 10, 10));
        shadows.Add(box, Vector2.Zero, 60);
        shadows.Finish(Vector2.Zero, 60);
        var q = shadows.Quads;
        Check(q.Length > 0 && q.Length % 4 == 0 && Inside(q, new(30, 0)) && Inside(q, new(55, 2)) && !Inside(q, new(5, 0)) &&
            !Inside(q, new(15, -30)) && !Inside(q, new(-20, 0)), $"a box casts a shadow behind it and nowhere else ({q.Length / 4} quads)");
        Check(q.ToArray().All(p => p.Length() <= 120.01f), "shadow quads end at twice the light radius");
        shadows.Clear();
        shadows.Add(box, new(15, 0), 60);
        shadows.Finish(new(15, 0), 60);
        Check(shadows.Count == 0, "a light inside a caster is not blocked by it");
        shadows.Clear();
        // A wall 1 px from the light spans almost 180°; the split at its closest point keeps the far side outside the radius.
        shadows.Add(Shape2D.Box(new RectangleF(-200, 1, 400, 4)), Vector2.Zero, 50);
        shadows.Finish(Vector2.Zero, 50);
        Check(Inside(shadows.Quads, new(0, 40)) && Inside(shadows.Quads, new(45, 15)) && !Inside(shadows.Quads, new(30, -10)),
            "a wall right next to the light shadows the whole half plane behind it");
        shadows.Clear();
        shadows.Add(Shape2D.Box(new RectangleF(300, 300, 10, 10)), Vector2.Zero, 50);
        shadows.Finish(Vector2.Zero, 50);
        Check(shadows.Count == 0, "casters outside the radius add no quads");
        shadows.Clear();
        // A row of ten tiles under the light: their ten bottom edges join (side edges stay, so only the near faces are lit).
        for (int x = -5; x < 5; x++) shadows.Add(Shape2D.Box(new RectangleF(x * 8, 20, 8, 8)), Vector2.Zero, 80);
        shadows.Finish(Vector2.Zero, 80);
        var row = shadows.Quads;
        Check(shadows.Edges == 22 && row.Length / 4 <= 13 && Inside(row, new(0, 60)) && Inside(row, new(-38, 40)) && !Inside(row, new(3, 10)),
            $"touching collinear tile edges join ({shadows.Edges} edges, {row.Length / 4} quads)");
    }

    // A 64×32 white surface, ambient 64, one light at the left with a box in front: the shadow keeps exactly the ambient
    // colour, the lit side is brighter, and the lightmap leaves pixels outside its area alone.
    private static unsafe void Lightmap()
    {
        var surface = Sdl.PInvoke.CreateSurface(64, 32, SdlPixelFormat.Rgba32);
        try
        {
            using var renderer = Sdl.Render.CreateSoftwareRenderer(surface);
            using var canvas = new SdlCanvas(renderer, batchSprites: true, previewShader: ShaderPixels.Preview);
            var shadows = new LightShadows();
            shadows.Add(Shape2D.Box(new RectangleF(20, 12, 4, 8)), new(8, 16), 40);
            shadows.Finish(new(8, 16), 40);
            for (int pass = 0; pass < 2; pass++)
            {
                canvas.Clear(Color.White);
                canvas.BeginLighting(new RectangleF(0, 0, 56, 32), Color.FromArgb(64, 64, 64));
                canvas.DrawLight(ShaderClip.LightFalloff, new RectangleF(-32, -24, 80, 80), Matrix3x2.Identity, Color.White, shadows.Quads, Matrix3x2.Identity);
                canvas.EndLighting();
                canvas.FlushSprites();
            }
            var pixels = Sdl.PInvoke.RenderReadPixels(renderer, null);
            byte At(int x, int y) => ((byte*)pixels->pixels)[y * pixels->pitch + x * 4];
            Check(At(30, 16) == 64 && At(12, 16) > 150 && At(12, 2) > 64 && At(60, 16) == 255,
                $"lightmap: shadow {At(30, 16)}, lit {At(12, 16)}, beside {At(12, 2)}, outside the area {At(60, 16)}");
            Sdl.PInvoke.DestroySurface(pixels);
        }
        finally { Sdl.PInvoke.DestroySurface(surface); }
    }

    private static unsafe void Cave(string project)
    {
        var scene = SceneDocument.Load(Path.Combine(project, "Scenes", "ShowcaseCave.scene"));
        Check(scene.SchemaVersion == 42, $"Global Light 2D and shadows raise the scene schema to 42 ({scene.SchemaVersion})");
        var saved = ProjectYaml.Decode<SceneDocument>(ProjectYaml.Encode(scene));
        var global = scene.Objects.Single(o => o.Name == "Night");
        Check(saved.SchemaVersion == 42 && saved.Objects.Single(o => o.Id == global.Id).GetComponent<GlobalLight2DComponent>() ==
            global.GetComponent<GlobalLight2DComponent>() && saved.Objects.Count(o => o.GetComponent<PointLight2DComponent>() is { Shadows: true }) == 4,
            "lighting settings round-trip through scene YAML");
        // The main showcase has Point Light 2D torches without shadows: only its parallax layers decide the schema.
        var plain = SceneDocument.Load(Path.Combine(project, "Scenes", "Showcase.scene"));
        Check(plain.SchemaVersion == 33, $"point lights without shadows do not raise the schema to 42 ({plain.SchemaVersion})");

        var surface = Sdl.PInvoke.CreateSurface(800, 450, SdlPixelFormat.Rgba32);
        try
        {
            using var renderer = Sdl.Render.CreateSoftwareRenderer(surface);
            using var canvas = new SdlCanvas(renderer, batchSprites: true, previewShader: ShaderPixels.Preview);
            using var game = new GameSession(scene);
            var world = game.Current;
            const float dt = 1f / 60;
            long Frames(int n)
            {
                long bytes = 0;
                for (int i = 0; i < n; i++)
                {
                    game.FixedUpdate(game.Actions.Take(), dt);
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    game.Draw(canvas, 1);
                    canvas.FlushSprites();
                    bytes += GC.GetAllocatedBytesForCurrentThread() - before;
                }
                return bytes;
            }
            Frames(1000); // A full walk lights all three torches and brings the lantern back, growing the shadow buffers once.
            long lit = Frames(120);
            world.SetActive(global.Id, false);
            Frames(30);
            long unlit = Frames(120);
            world.SetActive(global.Id, true);
            Frames(30);
            Check(lit == unlit, $"lighting with 4 shadow lights adds no per-frame allocation ({lit / 120.0:F1} B/frame lit, {unlit / 120.0:F1} unlit)");

            var lantern = scene.Objects.Single(o => o.Name == "Lantern light").Id;
            var crate = scene.Objects.Single(o => o.Name == "Sliding crate").Id;
            int start = world.ShadowRebuilds;
            Frames(60);
            int moving = world.ShadowRebuilds - start;
            world.SetActive(lantern, false); world.SetActive(crate, false);
            Frames(2);
            start = world.ShadowRebuilds;
            Frames(60);
            int still = world.ShadowRebuilds - start;
            Check(moving >= 60 && moving <= 4 * 60 && still == 0,
                $"shadows rebuild only for lights whose casters or position change ({moving} rebuilds in 60 moving frames, {still} when still)");
            // The cave has no tilemap: switching off a static caster stands in for a world edit.
            var box = scene.Objects.Single(o => o.Name == "Crate 1").Id;
            Check(world.SetColliderEnabled(box, false), "a caster's collider can be switched off in the cave");
            start = world.ShadowRebuilds;
            Frames(1);
            int edited = world.ShadowRebuilds - start;
            start = world.ShadowRebuilds;
            Frames(30);
            Check(edited == EditRebuilds && world.ShadowRebuilds == start,
                $"a caster change rebuilds each shadow light in its reach once ({edited}, then {world.ShadowRebuilds - start})");
        }
        finally { Sdl.PInvoke.DestroySurface(surface); }
    }

    // Crate 1 lies within the 200 px radius of Torch lights 1 and 2 (about 81 and 194 px away); Torch light 3 is too far.
    const int EditRebuilds = 2;
    private static void Check(bool pass, string label)
    {
        if (!pass) throw new Exception("Lighting: " + label);
        Console.WriteLine("PASS " + label);
    }
}
