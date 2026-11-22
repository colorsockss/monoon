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
    public void PrepareShader(ShaderClip shader) => ShaderTexture(shader,shader.Additive);
    public void ReleaseShader(ShaderClip shader) => shaderSheets?.Release(shader);

    private SpriteTexture ShaderTexture(ShaderClip shader,bool additive)
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
        return shaderSheets.Get(shader,Color.White,additive);
    }

    public void DrawShader(ShaderClip shader,int frame,RectangleF local,Matrix3x2 matrix,Color tint,bool additive)
    {
        var sheet=ShaderTexture(shader,additive);
        batch.Add(sheet.Handle,local,tint,shader.Region(frame),sheet.Size,TileGrid.Orthogonal,matrix);
        if (!batchSprites) batch.Flush();
    }

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

    private readonly SdlText labels = new(textRasterizer ?? new SdlFontRasterizer());
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

    public void ClearSprites() { FlushSprites(); labels.Dispose(); sprites.Clear(); variants.Dispose(); shaderSheets?.Dispose(); }
    public void InvalidateSprite(string path) { FlushSprites(); sprites.Invalidate(path); variants.Invalidate(path); }
    public void Dispose()
    {
        try { FlushSprites(); }
        finally { labels.Dispose(); sprites.Dispose(); variants.Dispose(); shaderSheets?.Dispose(); }
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
        renderer.SetRenderDrawColor(color.R, color.G, color.B, color.A);
        directDraws++;
        renderer.RenderFillRect(&bounds);
    }
}
