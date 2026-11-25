using System.Drawing;
using System.Numerics;
using Brutal.SdlApi;
using Engine.Core;

namespace Engine.Desktop;

public sealed class SdlCanvas(SdlRenderer renderer, Func<ShaderClip, RgbaImage>? previewShader = null, IGameTextRasterizer? textRasterizer = null) : ICanvas2D, IDisposable
{
    private sealed class SpriteTexture(SdlTexture handle, Size size, SdlScaleMode original) : IDisposable
    {
        public SdlTexture Handle { get; } = handle;
        public Size Size { get; } = size;
        public SdlScaleMode Original { get; } = original;
        public SdlScaleMode Current = original;
        public SoftwareTileTextures? SoftwareTiles;
        public void Dispose() { SoftwareTiles?.Dispose(); Handle.Dispose(); }
    }

    private readonly SpriteCache<SpriteTexture> sprites = new(path => LoadTexture(renderer, path));
    private readonly SpriteVariants<SpriteTexture> variants = new((filter, _) => LoadFiltered(renderer,filter));
    private ShaderSheets<SpriteTexture>? shaderSheets;
    public void PrepareShader(ShaderClip shader) => ShaderTexture(shader,shader.Additive,0,out _);
    public void ReleaseShader(ShaderClip shader) => shaderSheets?.Release(shader);

    private SpriteTexture ShaderTexture(ShaderClip shader,bool additive,int frame,out SpriteRegion cell)
    {
        shaderSheets ??= new((clip,_,add) =>
        {
            SpriteTexture texture;
            if (clip.SheetPath is {} path)
            {
                texture = LoadTexture(renderer, path);
                var last = clip.Region(clip.Frames - 1);
                if (texture.Size != new Size(clip.Columns * clip.Width, last.Y + last.Height) ||
                    !SetScale(texture, SdlScaleMode.Nearest))
                {
                    texture.Dispose();
                    throw new InvalidDataException($"Invalid shader sheet dimensions or sampling mode: {path}");
                }
            }
            else
            {
                var pixels = previewShader?.Invoke(clip) ?? throw new InvalidDataException("The exported game is missing a baked shader sheet.");
                texture = Upload(renderer, pixels);
            }
            const uint additiveBlend=2; // SDL_BLENDMODE_ADD
            if (!add || Sdl.Render.SetTextureBlendMode(texture.Handle,additiveBlend)) return texture;
            texture.Dispose();
            throw new InvalidDataException(Sdl.PInvoke.GetError());
        },FlushSprites);
        return shaderSheets.Get(shader,Color.White,additive,frame,out cell);
    }

    public void DrawShader(ShaderClip shader,int frame,RectangleF local,Matrix3x2 matrix,Color tint,bool additive)
    {
        var sheet=ShaderTexture(shader,additive,frame,out var cell);
        batch.Add(sheet.Handle,local,tint,cell,sheet.Size,TileGrid.Orthogonal,matrix);
        if (!batchSprites) batch.Flush();
    }

    // One emitter: its quads go into the sprite batch with no per-particle lookup. Additive sets the texture's SDL blend
    // mode for this run of quads only, so sprites sharing the PNG keep their blending. SDL's multiply expects premultiplied
    // colour, so multiply draws from a premultiplied copy made once per texture.
    public unsafe void DrawParticles(ParticleTexture texture, ReadOnlySpan<ParticleQuad> quads, Matrix3x2 view, ParticleBlend blend)
    {
        SpriteTexture? sheet;
        if (blend == ParticleBlend.Multiply) sheet = Multiplied(texture);
        else if (texture.Shader is {} shader) sheet = ShaderTexture(shader,false,0,out _);
        else if (texture.Path == null || !sprites.TryGet(texture.Path,out sheet)) return;
        if (sheet == null) return;
        if (!SetScale(sheet,SdlScaleMode.Nearest)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        bool additive = blend == ParticleBlend.Additive;
        uint before = 0;
        if (additive)
        {
            FlushSprites();
            if (!sheet.Handle.GetBlendMode(&before) || !sheet.Handle.SetBlendMode(2)) throw new InvalidOperationException(Sdl.PInvoke.GetError()); // SDL_BLENDMODE_ADD
        }
        var cells = texture.Cells;
        foreach (ref readonly var quad in quads) batch.Quad(sheet.Handle,quad,cells[quad.Cell],sheet.Size,view);
        if (additive || !batchSprites) FlushSprites();
        if (additive && !sheet.Handle.SetBlendMode(before)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
    }

    private readonly Dictionary<ParticleTexture, SpriteTexture?> multiplied = [];
    private unsafe SpriteTexture? Multiplied(ParticleTexture texture)
    {
        if (multiplied.TryGetValue(texture, out var done)) return done;
        RgbaImage? image;
        if (texture.Shader is { SheetPath: null } clip)
            image = previewShader?.Invoke(clip) ?? throw new InvalidDataException("The exported game is missing a baked shader sheet.");
        else
        {
            var loaded = Sdl.PInvoke.LoadPNG(texture.Shader?.SheetPath ?? texture.Path!);
            var surface = loaded == null ? null : Sdl.PInvoke.ConvertSurface(loaded, SdlPixelFormat.Rgba32);
            if (loaded != null) Sdl.PInvoke.DestroySurface(loaded);
            if (surface == null) return multiplied[texture] = null;
            var bytes = new byte[surface->w * surface->h * 4];
            for (int y = 0; y < surface->h; y++)
                new ReadOnlySpan<byte>((byte*)surface->pixels + y * surface->pitch, surface->w * 4).CopyTo(bytes.AsSpan(y * surface->w * 4));
            image = new(surface->w, surface->h, bytes);
            Sdl.PInvoke.DestroySurface(surface);
        }
        var pixels = (byte[])image.Pixels.Clone();
        for (int i = 0; i < pixels.Length; i += 4)
            for (int c = 0; c < 3; c++) pixels[i + c] = (byte)(pixels[i + c] * pixels[i + 3] / 255);
        var result = Upload(renderer, new RgbaImage(image.Width, image.Height, pixels));
        if (!result.Handle.SetBlendMode(8)) throw new InvalidOperationException(Sdl.PInvoke.GetError()); // SDL_BLENDMODE_MUL
        return multiplied[texture] = result;
    }
    private void ReleaseMultiplied() { foreach (var t in multiplied.Values) t?.Dispose(); multiplied.Clear(); }

    private static SpriteTexture LoadFiltered(SdlRenderer renderer,SpriteFilter filter)
    {
        var bitmap = SdlSpriteMask.Filter(filter,Color.White);
        return Upload(renderer,bitmap);
    }

    private static SpriteTexture Upload(SdlRenderer renderer, RgbaImage bitmap)
    {
        var texture = bitmap.CreateTexture(renderer);
        if (texture.SetScaleMode(SdlScaleMode.Nearest)) return new(texture, new(bitmap.Width, bitmap.Height), SdlScaleMode.Nearest);
        texture.Dispose();
        throw new InvalidDataException(Sdl.PInvoke.GetError());
    }

    public bool DrawFiltered(SpriteFilter filter,RectangleF local,Matrix3x2 matrix,Color tint)
    {
        var tex = variants.Get(filter,Color.White);
        batch.Add(tex.Handle,local,tint,null,tex.Size,TileGrid.Orthogonal,matrix);
        if (!batchSprites) batch.Flush();
        return true;
    }
    private readonly SdlSpriteBatch batch = new(renderer);
    private readonly bool batchSprites;
    private long directDraws;
    // Count renderer draw submissions, including batched geometry and text; exclude clear/present/state.
    public long DrawSubmissions => directDraws+batch.Submissions+labels.Submissions;
    private readonly bool software = Sdl.Render.GetRendererName(renderer) == "software";
    internal Size LogicalSize { get; init; }

    // A Native AOT game built without the text feature (roadmap 15.12) has no SDL_ttf code: text fails as a missing library does.
    private readonly SdlText labels = new(textRasterizer ?? (EngineFeatures.Text ? new TtfTextRasterizer() : new ExcludedTextRasterizer()));
    public SdlCanvas(SdlRenderer renderer, bool batchSprites, Func<ShaderClip, RgbaImage>? previewShader = null, IGameTextRasterizer? textRasterizer = null) : this(renderer, previewShader, textRasterizer)
    {
        this.batchSprites = batchSprites;
    }

    // Submit sprites before using the SDL renderer directly (present, readback or clip changes).
    public void FlushSprites() => batch.Flush();

    public unsafe IDisposable PushClip(RectangleF area)
    {
        FlushSprites();
        var prior=new SdlRect();
        bool enabled=renderer.RenderClipEnabled();
        if (enabled && !renderer.GetRenderClipRect(&prior)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        static int Edge(float n) => (int)MathF.Round(n);
        var rect=Rectangle.FromLTRB(Edge(area.Left),Edge(area.Top),Edge(area.Right),Edge(area.Bottom));
        if (enabled) rect=Rectangle.Intersect(rect,new(prior.x,prior.y,prior.w,prior.h));
        var next=new SdlRect { x=rect.X,y=rect.Y,w=rect.Width,h=rect.Height };
        if (!renderer.SetRenderClipRect(&next)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        return new ClipScope(this,renderer,prior,enabled);
    }
    private sealed class ClipScope(SdlCanvas canvas,SdlRenderer renderer,SdlRect saved,bool enabled) : IDisposable
    {
        public unsafe void Dispose()
        {
            var prior=saved;
            try { canvas.FlushSprites(); }
            finally { renderer.SetRenderClipRect(enabled?&prior:null); }
        }
    }

    public void UseFonts(IReadOnlyList<string> paths) => labels.UseFonts(paths);
    public float MeasureText(string text,float size)=>labels.Measure(text,size);
    public void MeasureTextEnds(string text,float size,Span<float> ends)=>labels.MeasureEnds(text,size,ends);
    public void DrawText(string text, float x, float y, float size, Color color)
    {
        FlushSprites();
        labels.Draw(renderer, text, x, y, size, color);
    }
    /// <summary>Sprite textures alive in the renderer.</summary>
    public int SpriteTextures => sprites.Count;
    public void Prepare(string path) { if (!sprites.TryGet(path, out _)) throw new InvalidDataException($"Could not load sprite {path}."); }

    /// <summary>Reads a PNG into a CPU surface. Safe on worker threads; <see cref="Upload"/> turns it into a texture.</summary>
    public static unsafe IDisposable Decode(string path)
    {
        var surface = Sdl.PInvoke.LoadPNG(path);
        if (surface == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
        return new DecodedSurface(surface);
    }

    private sealed unsafe class DecodedSurface(SdlSurface* surface) : IDisposable
    {
        public SdlSurface* Surface { get; private set; } = surface;
        public void Dispose() { if (Surface != null) Sdl.PInvoke.DestroySurface(Surface); Surface = null; }
    }

    /// <summary>Creates the texture of a <see cref="Decode"/> result. The caller still disposes the surface.</summary>
    public unsafe void Upload(string path, IDisposable decoded)
    {
        if (sprites.Contains(path)) return;
        try { sprites.Add(path, FromSurface(renderer, ((DecodedSurface)decoded).Surface, path)); }
        catch (InvalidDataException ex) { throw new InvalidDataException($"Could not load sprite {path}.", ex); }
    }

    public static unsafe Size ReadPngSize(string path)
    {
        var surface = Sdl.PInvoke.LoadPNG(path);
        if (surface == null) throw new InvalidDataException($"Cannot read PNG '{Path.GetFileName(path)}': {Sdl.PInvoke.GetError()}");
        try { return new Size(surface->w, surface->h); }
        finally { Sdl.PInvoke.DestroySurface(surface); }
    }

    private static unsafe SpriteTexture LoadTexture(SdlRenderer renderer, string path)
    {
        var surface = Sdl.PInvoke.LoadPNG(path);
        if (surface == null) throw new InvalidDataException(Sdl.PInvoke.GetError());
        try { return FromSurface(renderer, surface, path); }
        finally { Sdl.PInvoke.DestroySurface(surface); }
    }

    private static unsafe SpriteTexture FromSurface(SdlRenderer renderer, SdlSurface* surface, string path)
    {
        try
        {
            var texture = renderer.CreateTextureFromSurface(surface);
            if (texture.GetScaleMode(out var mode)) return new(texture, new Size(surface->w, surface->h), mode);
            texture.Dispose();
            throw new InvalidDataException(Sdl.PInvoke.GetError());
        }
        catch (SdlException ex) { throw new InvalidDataException($"Could not create texture for '{path}'.", ex); }
    }

    public void DrawSpriteInstances(SceneAppearance sprite, ReadOnlySpan<SpriteInstance2D> instances, Matrix3x2 view, bool batched)
    {
        if (sprite.Image == null || sprite.Mask != null || sprite.FlipX || sprite.FlipY || sprite.PixelSnap ||
            !sprites.TryGet(sprite.Image, out var texture) || sprite.Source is {} source && !source.Fits(texture.Size.Width, texture.Size.Height))
        {
            ICanvas2D.DrawInstancesIndividually(this, sprite, instances, view, batched);
            return;
        }
        if (!SetScale(texture, sprite.Source == null ? texture.Original : SdlScaleMode.Nearest))
            throw new InvalidOperationException(Sdl.PInvoke.GetError());
        bool separate = !batchSprites || !batched;
        if (separate) FlushSprites();
        foreach (ref readonly var instance in instances)
        {
            var bounds = sprite.Canvas is {} trim && sprite.Source is {} cell ? trim.Place(instance.Bounds, cell) : instance.Bounds;
            batch.Add(texture.Handle, bounds, instance.Tint, sprite.Source, texture.Size, TileGrid.Orthogonal, view);
            if (separate) FlushSprites();
        }
    }

    private bool SetScale(SpriteTexture sprite, SdlScaleMode filter)
    {
        if (filter == sprite.Current) return true;
        FlushSprites();
        if (!sprite.Handle.SetScaleMode(filter)) return false;
        sprite.Current = filter;
        return true;
    }

    public void DrawSquareSprite(RectangleF destination, Color color)
    {
        if (!DrawTexture(SpriteAssets.SquarePath, destination, color)) FillRectangle(destination, Color.Magenta);
    }

    public bool DrawSprite(string path, RectangleF destination) => DrawSprite(path, destination, null);
    public bool DrawSprite(string path, RectangleF destination, SpriteRegion? source) =>
        DrawTexture(path, destination, Color.White, source);

    public bool DrawTile(string path, RectangleF destination, SpriteRegion source, string layout) =>
        DrawTexture(path,destination,Color.White,source,layout);
    public void DrawLine(System.Numerics.Vector2 start, System.Numerics.Vector2 end, float width, Color color) =>
        batch.Line(start,end,width,color);

    public void FillStrip(ReadOnlySpan<System.Numerics.Vector2> edges, ReadOnlySpan<Color> colors, Matrix3x2 matrix)
    {
        batch.Strip(edges, colors, matrix);
        if (!batchSprites) batch.Flush();
    }

    public bool DrawAffine(string? path, RectangleF local, Matrix3x2 matrix, Color tint, SpriteRegion? source = null,
        string layout = TileGrid.Orthogonal)
    {
        if (matrix.M12 == 0 && matrix.M21 == 0 && matrix.M11 > 0 && matrix.M22 > 0)
        {
            var dest = new RectangleF(local.X*matrix.M11+matrix.M31, local.Y*matrix.M22+matrix.M32,local.Width*matrix.M11,local.Height*matrix.M22);
            if (path != null) return DrawTexture(path,dest,tint,source,layout);
            FillRectangle(dest,tint); return true;
        }
        if (path != null) return DrawTexture(path,local,tint,source,layout,matrix);
        batch.Add(default,local,tint,null,default,layout,matrix);
        if (!batchSprites) batch.Flush();
        return true;
    }

    public Size SpriteSize(string path) => sprites.TryGet(path, out var sprite) ? sprite.Size : Size.Empty;

    // Axis-aligned Tiled and Sliced sprites go to SDL's own tiling (BRUTAL Brutal.Sdl), one draw per sprite; rotated,
    // flipped or unevenly scaled ones use the shared per-piece path through the sprite batch.
    public unsafe bool DrawGrid(string path, RectangleF local, Matrix3x2 matrix, Color tint, SpriteRegion? source, Size size, UiBorders? borders, bool tiled)
    {
        if (matrix.M12 != 0 || matrix.M21 != 0 || matrix.M11 <= 0 || matrix.M11 != matrix.M22 || !tiled && borders == null)
            return SpriteGrid.Draw(this, path, local, matrix, tint, source, size, borders, tiled);
        if (!sprites.TryGet(path, out var sprite) || source != null && !source.Fits(sprite.Size.Width, sprite.Size.Height)) return false;
        if (!SetScale(sprite, source == null ? sprite.Original : SdlScaleMode.Nearest)) return false;
        FlushSprites();
        var texture = sprite.Handle;
        float scale = matrix.M11;
        var target = new SdlFRect { x = local.X * scale + matrix.M31, y = local.Y * scale + matrix.M32, w = local.Width * scale, h = local.Height * scale };
        var crop = source == null ? default : new SdlFRect { x = source.X, y = source.Y, w = source.Width, h = source.Height };
        var cut = source == null ? null : &crop;
        Sdl.Render.SetTextureColorMod(texture, tint.R, tint.G, tint.B);
        Sdl.Render.SetTextureAlphaMod(texture, tint.A);
        directDraws++;
        bool drawn = borders == null ? renderer.RenderTextureTiled(texture, cut, scale, &target)
            : tiled ? renderer.RenderTexture9GridTiled(texture, cut, borders.Left, borders.Right, borders.Top, borders.Bottom, scale, &target, scale)
            : renderer.RenderTexture9Grid(texture, cut, borders.Left, borders.Right, borders.Top, borders.Bottom, scale, &target);
        // The sprite batch colours vertices and leaves texture modulation at white.
        Sdl.Render.SetTextureColorMod(texture, 255, 255, 255);
        Sdl.Render.SetTextureAlphaMod(texture, 255);
        return drawn;
    }

    private unsafe bool DrawTexture(string path, RectangleF destination, Color color, SpriteRegion? source = null, string layout = TileGrid.Orthogonal, Matrix3x2? transform = null)
    {
        if (!sprites.TryGet(path, out var sprite)) return false;
        var texture = sprite.Handle;
        if (source != null && !source.Fits(sprite.Size.Width, sprite.Size.Height)) return false;
        if (software && layout != TileGrid.Orthogonal && source != null)
        {
            var (scaleX, scaleY) = SoftwareTileTextures.Scale(source, layout);
            if (scaleX != 1 || scaleY != 1)
            {
                var cells = sprite.SoftwareTiles ??= new(path);
                var expanded = cells.Get(renderer, source, scaleX, scaleY);
                batch.Add(expanded, destination, color, null, default, layout,transform);
                if (!batchSprites) batch.Flush();
                return true;
            }
        }
        var filter = source == null ? sprite.Original : SdlScaleMode.Nearest;
        if (!SetScale(sprite, filter)) return false;
        if (batchSprites || layout != TileGrid.Orthogonal || transform.HasValue)
        {
            batch.Add(texture, destination, color, source, sprite.Size, layout,transform);
            if (!batchSprites) batch.Flush();
            return true;
        }
        FlushSprites();
        Sdl.Render.SetTextureColorMod(texture, color.R, color.G, color.B);
        Sdl.Render.SetTextureAlphaMod(texture, color.A);
        var target = new SdlFRect { x = destination.X, y = destination.Y, w = destination.Width, h = destination.Height };
        var crop = source == null ? default : new SdlFRect { x = source.X, y = source.Y, w = source.Width, h = source.Height };
        directDraws++;
        return renderer.RenderTexture(texture, source == null ? null : &crop, &target);
    }

    // Screen colour adjustment. The world draws into a target texture of the window's pixel size with the window's
    // presentation, viewport and clip; the tint and lift apply to that layer only, which is then blended over the window.
    private SdlTexture layer;
    private Size layerSize;
    private SdlRendererLogicalPresentation layerMode;
    private int layerWidth, layerHeight;

    public unsafe void BeginColorAdjust()
    {
        FlushSprites();
        if (!renderer.GetRenderOutputSize(out int w, out int h) ||
            !renderer.GetRenderLogicalPresentation(out layerWidth, out layerHeight, out layerMode))
            throw new InvalidOperationException(Sdl.PInvoke.GetError());
        if (layerSize != new Size(w, h))
        {
            if (!layerSize.IsEmpty) layer.Dispose();
            layerSize = default;
            layer = renderer.CreateTexture(SdlPixelFormat.Rgba32, SdlTextureAccess.Target, w, h);
            layerSize = new(w, h);
            const uint blend = 1; // SDL_BLENDMODE_BLEND: undrawn (transparent) pixels keep the window underneath.
            if (!layer.SetBlendMode(blend)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        }
        SdlRect viewport = default, clip = default;
        bool hasViewport = renderer.RenderViewportSet() && renderer.GetRenderViewport(&viewport);
        bool hasClip = renderer.RenderClipEnabled() && renderer.GetRenderClipRect(&clip);
        if (!renderer.SetRenderTarget(layer)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        renderer.SetRenderDrawColor(0, 0, 0, 0);
        renderer.RenderClear(); // Ignores viewport and clip.
        renderer.SetRenderLogicalPresentation(layerWidth, layerHeight, layerMode);
        renderer.SetRenderViewport(hasViewport ? &viewport : null);
        renderer.SetRenderClipRect(hasClip ? &clip : null);
    }

    // One camera viewport in canvas units, straight on the current target: its pixels are opaque, so MOD and ADD need no layer.
    public unsafe void AdjustRectangle(RectangleF area, Color multiply, byte lift)
    {
        FlushSprites();
        var bounds = new SdlFRect { x = area.X, y = area.Y, w = area.Width, h = area.Height };
        renderer.SetRenderDrawBlendMode(4);
        renderer.SetRenderDrawColor(multiply.R, multiply.G, multiply.B, 255);
        renderer.RenderFillRect(&bounds);
        directDraws++;
        if (lift > 0)
        {
            renderer.SetRenderDrawBlendMode(2);
            renderer.SetRenderDrawColor(lift, lift, lift, 255);
            renderer.RenderFillRect(&bounds);
            directDraws++;
        }
        renderer.SetRenderDrawBlendMode(1);
    }

    public unsafe void EndColorAdjust(Color multiply, byte lift)
    {
        FlushSprites();
        var all = new SdlFRect { w = layerSize.Width, h = layerSize.Height };
        void FullPixels()
        {
            var full = new SdlRect { w = layerSize.Width, h = layerSize.Height };
            renderer.SetRenderLogicalPresentation(0, 0, SdlRendererLogicalPresentation.Disabled);
            renderer.SetRenderClipRect(null);
            renderer.SetRenderViewport(&full);
        }
        FullPixels();
        // MOD multiplies colour and keeps alpha; ADD lifts colour weighted by its own alpha, so empty pixels stay empty.
        renderer.SetRenderDrawBlendMode(4);
        renderer.SetRenderDrawColor(multiply.R, multiply.G, multiply.B, 255);
        renderer.RenderFillRect(&all);
        if (lift > 0)
        {
            renderer.SetRenderDrawBlendMode(2);
            renderer.SetRenderDrawColor(lift, lift, lift, 255);
            renderer.RenderFillRect(&all);
        }
        renderer.SetRenderDrawBlendMode(1);
        if (!renderer.SetRenderTarget(default)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        SdlRect viewport = default, clip = default;
        bool hasViewport = renderer.RenderViewportSet() && renderer.GetRenderViewport(&viewport);
        bool hasClip = renderer.RenderClipEnabled() && renderer.GetRenderClipRect(&clip);
        FullPixels();
        directDraws++;
        renderer.RenderTexture(layer, null, &all);
        renderer.SetRenderLogicalPresentation(layerWidth, layerHeight, layerMode);
        renderer.SetRenderViewport(hasViewport ? &viewport : null);
        renderer.SetRenderClipRect(hasClip ? &clip : null);
    }

    // 2D lighting (roadmap 16.7, decision 14: no shaders). The lightmap is a target texture of the lit area in canvas units,
    // cleared to the ambient colour. Lights without shadows add their falloff sprite straight into it, batched together. A
    // light with shadows draws into a scratch target cleared to black, gets its shadow quads filled black, and is then added
    // to the lightmap (3 draws). EndLighting multiplies the target underneath by the lightmap (SDL_BLENDMODE_MOD).
    private SdlTexture lightmap, lightScratch, lightUnder;
    private Size lightmapSize, scratchSize;
    private RectangleF lightArea;
    private SdlRendererLogicalPresentation underMode;
    private int underWidth, underHeight;
    private SdlRect underViewport, underClip;
    private bool underHasViewport, underHasClip;

    private SdlTexture LightTarget(ref SdlTexture texture, ref Size size, int w, int h, uint blend, SdlScaleMode scale)
    {
        if (size.Width >= w && size.Height >= h) return texture;
        w = Math.Max(w, size.Width); h = Math.Max(h, size.Height);
        if (!size.IsEmpty) texture.Dispose();
        size = default;
        texture = renderer.CreateTexture(SdlPixelFormat.Rgba32, SdlTextureAccess.Target, w, h);
        size = new(w, h);
        if (!texture.SetBlendMode(blend) || !texture.SetScaleMode(scale)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        return texture;
    }

    private unsafe void DrawInto(SdlTexture target, Color clear)
    {
        if (!renderer.SetRenderTarget(target)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        renderer.SetRenderLogicalPresentation(0, 0, SdlRendererLogicalPresentation.Disabled);
        renderer.SetRenderViewport(null);
        renderer.SetRenderClipRect(null);
        renderer.SetRenderDrawColor(clear.R, clear.G, clear.B, 255);
        renderer.RenderClear();
    }

    public unsafe void BeginLighting(RectangleF area, Color ambient)
    {
        FlushSprites();
        lightArea = area;
        const uint mod = 4; // SDL_BLENDMODE_MOD
        var map = LightTarget(ref lightmap, ref lightmapSize, Math.Max(1, (int)MathF.Ceiling(area.Width)),
            Math.Max(1, (int)MathF.Ceiling(area.Height)), mod, SdlScaleMode.Linear);
        lightUnder = renderer.GetRenderTarget();
        if (!renderer.GetRenderLogicalPresentation(out underWidth, out underHeight, out underMode)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        fixed (SdlRect* viewport = &underViewport, clip = &underClip)
        {
            underHasViewport = renderer.RenderViewportSet() && renderer.GetRenderViewport(viewport);
            underHasClip = renderer.RenderClipEnabled() && renderer.GetRenderClipRect(clip);
        }
        DrawInto(map, ambient);
    }

    public unsafe void DrawLight(ShaderClip sheet, RectangleF local, Matrix3x2 matrix, Color tint, ReadOnlySpan<Vector2> shadowQuads, Matrix3x2 view)
    {
        var toMap = Matrix3x2.CreateTranslation(-lightArea.X, -lightArea.Y);
        if (shadowQuads.IsEmpty) { DrawShader(sheet, 0, local, matrix * toMap, tint, true); return; }
        var box = Affine2D.Bounds(local, matrix * toMap);
        int x0 = Math.Max(0, (int)MathF.Floor(box.Left)), y0 = Math.Max(0, (int)MathF.Floor(box.Top));
        int x1 = Math.Min((int)MathF.Ceiling(lightArea.Width), (int)MathF.Ceiling(box.Right));
        int y1 = Math.Min((int)MathF.Ceiling(lightArea.Height), (int)MathF.Ceiling(box.Bottom));
        if (x1 <= x0 || y1 <= y0) return;
        FlushSprites();
        const uint add = 2; // SDL_BLENDMODE_ADD
        DrawInto(LightTarget(ref lightScratch, ref scratchSize, x1 - x0, y1 - y0, add, SdlScaleMode.Nearest), Color.Black);
        var toScratch = Matrix3x2.CreateTranslation(-lightArea.X - x0, -lightArea.Y - y0);
        DrawShader(sheet, 0, local, matrix * toScratch, tint, true);
        batch.Quads(shadowQuads, Color.Black, view * toScratch);
        FlushSprites();
        if (!renderer.SetRenderTarget(lightmap)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        var source = new SdlFRect { w = x1 - x0, h = y1 - y0 };
        var target = new SdlFRect { x = x0, y = y0, w = x1 - x0, h = y1 - y0 };
        directDraws++;
        renderer.RenderTexture(lightScratch, &source, &target);
    }

    public unsafe void EndLighting()
    {
        FlushSprites();
        if (!renderer.SetRenderTarget(lightUnder)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        renderer.SetRenderLogicalPresentation(underWidth, underHeight, underMode);
        fixed (SdlRect* viewport = &underViewport, clip = &underClip)
        {
            renderer.SetRenderViewport(underHasViewport ? viewport : null);
            renderer.SetRenderClipRect(underHasClip ? clip : null);
        }
        var source = new SdlFRect { w = lightArea.Width, h = lightArea.Height };
        var target = new SdlFRect { x = lightArea.X, y = lightArea.Y, w = lightArea.Width, h = lightArea.Height };
        directDraws++;
        renderer.RenderTexture(lightmap, &source, &target);
    }

    public void ClearSprites() { FlushSprites(); labels.Dispose(); sprites.Clear(); variants.Dispose(); shaderSheets?.Dispose(); ReleaseMultiplied(); }
    public void InvalidateSprite(string path) { FlushSprites(); sprites.Invalidate(path); variants.Invalidate(path); }
    public void Dispose()
    {
        try { FlushSprites(); }
        finally { labels.Dispose(); sprites.Dispose(); variants.Dispose(); shaderSheets?.Dispose(); ReleaseMultiplied(); if (!layerSize.IsEmpty) layer.Dispose();
            if (!lightmapSize.IsEmpty) lightmap.Dispose(); if (!scratchSize.IsEmpty) lightScratch.Dispose(); }
    }

    public void Clear(Color color)
    {
        FlushSprites();
        if (!LogicalSize.IsEmpty)
        {
            // SDL_RenderClear covers the entire window, including the letterbox bars.
            renderer.SetRenderDrawColor(0, 0, 0, 255);
            renderer.RenderClear();
            FillRectangle(new RectangleF(PointF.Empty, LogicalSize), color);
            return;
        }
        renderer.SetRenderDrawColor(color.R, color.G, color.B, color.A);
        renderer.RenderClear();
    }

    public unsafe void FillRectangle(RectangleF rectangle, Color color)
    {
        FlushSprites();
        var bounds = new SdlFRect { x = rectangle.X, y = rectangle.Y, w = rectangle.Width, h = rectangle.Height };
        renderer.SetRenderDrawBlendMode(1); // SDL_BLENDMODE_BLEND: translucent fills (fades, flashes) blend like the Avalonia preview.
        renderer.SetRenderDrawColor(color.R, color.G, color.B, color.A);
        directDraws++;
        renderer.RenderFillRect(&bounds);
    }
}
