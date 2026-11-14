using System.Drawing;
using System.Numerics;
using Engine.Core;
using Brutal.SdlApi;
using Engine.Desktop;

internal static class BatchingCheck
{
    public static unsafe void Run(SdlRenderer renderer, (RectangleF Bounds, Color Tint)[] sprites)
    {
        using var original = new SdlCanvas(renderer);
        using var batched = new SdlCanvas(renderer, batchSprites: true);
        foreach (int count in new[] { 100, 1_000, 10_000 })
        {
            Compare($"{count} sprites", canvas =>
            {
                canvas.Clear(Color.Black);
                foreach (var sprite in sprites.AsSpan(0, count)) canvas.DrawSquareSprite(sprite.Bounds, sprite.Tint);
            });
        }

        // Six adjacent 2×2 cells. Noninteger magnification exposes sampling across cell edges.
        string sheet = Path.Combine(AppContext.BaseDirectory, "Checks", "Cells.png");
        var colors = new[] { Color.FromArgb(250,12,40), Color.FromArgb(5,201,30), Color.FromArgb(70,44,229),
            Color.FromArgb(247,155,11), Color.FromArgb(48,188,210), Color.FromArgb(190,13,232) };
        foreach (var canvas in new[]{original,batched})
        {
            canvas.Clear(Color.Black);
            for (int i = 0; i < colors.Length; i++)
                if (!canvas.DrawSprite(sheet,new RectangleF(i*13,0,13,17),new Engine.Core.SpriteRegion(i%3*2,i/3*2,2,2)))
                    throw new InvalidDataException("Could not draw test cell.");
            canvas.FlushSprites();
            var pixels = ReadFrame(renderer);
            renderer.GetRenderOutputSize(out int width, out _);
            for (int y = 0; y < 17; y++)
                for (int x = 0; x < 78; x++)
                {
                    int offset = 4*(y*width+x);
                    var color = colors[x/13];
                    if (pixels[offset] != color.R || pixels[offset+1] != color.G || pixels[offset+2] != color.B)
                        throw new InvalidOperationException($"Sheet edge sampled the wrong texel at ({x}, {y}).");
                }
        }
        Console.WriteLine("Pixel match: all six sheet cells including interior and outer edges, immediate and batched SDL.");
        foreach (var canvas in new[] { original, batched })
        {
            string[] layouts = [Engine.Core.TileGrid.Isometric,Engine.Core.TileGrid.HexPointy,Engine.Core.TileGrid.HexFlat];
            canvas.Clear(Color.Black);
            for (int i = 0; i < layouts.Length; i++)
                canvas.DrawTile(sheet,new(i*64,0,64,64),new Engine.Core.SpriteRegion(2*i,0,2,2),layouts[i]);
            for (int i = 0; i < layouts.Length; i++)
                canvas.DrawTile(sheet,new(i*64,64,64,64),new Engine.Core.SpriteRegion(0,0,6,4),layouts[i]);
            canvas.FlushSprites();
            var pixels = ReadFrame(renderer);
            renderer.GetRenderOutputSize(out int width,out _);
            for (int i = 0; i < layouts.Length; i++)
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        double dx = Math.Abs(x+.5-32), dy = Math.Abs(y+.5-32);
                        double limit = i == 0 ? dx+dy : i == 1 ? dy+dx/2 : dx+dy/2;
                        if (Math.Abs(limit-32) < 1) continue; // Rasterization conventions differ on the outline.
                        var expected = limit < 32 ? colors[i] : Color.Black;
                        int at = 4*(y*width+64*i+x);
                        if (pixels[at] != expected.R || pixels[at+1] != expected.G || pixels[at+2] != expected.B)
                            throw new InvalidOperationException($"{layouts[i]} sampled the wrong pixel at ({x},{y}).");
                        var patterned = limit < 32 ? colors[3*(y/32) + 3*(2*x+1)/128] : Color.Black;
                        at += 64*width*4;
                        if (pixels[at] != patterned.R || pixels[at+1] != patterned.G || pixels[at+2] != patterned.B)
                            throw new InvalidOperationException($"{layouts[i]} distorted the tile pattern at ({x},{y}).");
                    }
        }
        Console.WriteLine("Pixel match: diamond and both hexagon orientations clip opaque atlas cells.");
        // Exercise the bulk API across two batch boundaries and keep overlapping alpha order.
        var instances = sprites.Take(2500).Select(s => new SpriteInstance2D(s.Bounds, Color.FromArgb(160, s.Tint))).ToArray();
        var appearance = new SceneAppearance(sheet, Color.White, new SpriteRegion(2, 0, 2, 2), new SpriteCanvas(4, 4, 1, 1));
        byte[] Pooled(bool group)
        {
            batched.Clear(Color.Black);
            var view = Matrix3x2.CreateTranslation(3, 5);
            long start = batched.DrawSubmissions;
            batched.DrawSpriteInstances(appearance, instances, view, group);
            batched.FlushSprites();
            if (batched.DrawSubmissions - start != (group ? 3 : instances.Length))
                throw new InvalidOperationException("Unexpected submission count for pooled sprites.");
            return ReadFrame(renderer);
        }
        if (!Pooled(true).AsSpan().SequenceEqual(Pooled(false)))
            throw new InvalidOperationException("Pooled sprite modes produced different pixels.");
        Console.WriteLine("Pixel match: 2,500 pooled, tinted atlas sprites; 3 vs 2,500 SDL submissions.");

        string image = Path.Combine(AppContext.BaseDirectory, "Checks", "Key.png");
        Compare("textures, alpha, cache invalidation, overlays and clipping", canvas =>
        {
            canvas.DrawSquareSprite(new RectangleF(200, 0, 80, 80), Color.Red);
            canvas.Clear(Color.Black);
            var clip = new SdlRect { x = 10, y = 10, w = 120, h = 90 };
            renderer.SetRenderClipRect(&clip);
            canvas.DrawSquareSprite(new RectangleF(0, 0, 140, 110), Color.DarkBlue);
            if (!canvas.DrawSprite(image, new RectangleF(2.5f, 4.5f, 70, 65))) throw new InvalidDataException(image);
            canvas.InvalidateSprite(image);
            canvas.DrawSquareSprite(new RectangleF(60, 20, 45, 40), Color.FromArgb(40, 201, 80, 35));
            canvas.FillRectangle(new RectangleF(63, 22, 12, 11), Color.Pink);
            if (!canvas.DrawSprite(image, new RectangleF(85, 25, 35, 50))) throw new InvalidDataException(image);
            canvas.DrawText("Sprite", 85, 25, 18, Color.White);
            canvas.DrawSquareSprite(new RectangleF(0, 70, 160, 40), Color.SeaGreen);
            canvas.FlushSprites();
            renderer.SetRenderClipRect(null);
            canvas.DrawSquareSprite(new RectangleF(140, 70, 23, 15), Color.Yellow);
            canvas.ClearSprites();
        });

        void Compare(string name, Action<SdlCanvas> draw)
        {
            byte[] Capture(SdlCanvas canvas)
            {
                draw(canvas);
                canvas.FlushSprites();
                return ReadFrame(renderer);
            }
            var expected = Capture(original);
            var actual = Capture(batched);
            if (!actual.AsSpan().SequenceEqual(expected))
                throw new InvalidOperationException($"Batched pixels differ from unbatched pixels for {name}.");
            Console.WriteLine($"Pixel match: {name}");
        }
    }

    private static unsafe byte[] ReadFrame(SdlRenderer renderer)
    {
        var surface = Sdl.PInvoke.RenderReadPixels(renderer, null);
        if (surface == null) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        try
        {
            var rgba = Sdl.PInvoke.ConvertSurface(surface, SdlPixelFormat.Rgba32);
            if (rgba == null) throw new InvalidOperationException(Sdl.PInvoke.GetError());
            try
            {
                int rowBytes = rgba->w * 4;
                var bytes = new byte[rowBytes * rgba->h];
                for (int y = 0; y < rgba->h; y++)
                    new ReadOnlySpan<byte>((byte*)rgba->pixels + y * rgba->pitch, rowBytes).CopyTo(bytes.AsSpan(y * rowBytes));
                return bytes;
            }
            finally { Sdl.PInvoke.DestroySurface(rgba); }
        }
        finally { Sdl.PInvoke.DestroySurface(surface); }
    }
}
