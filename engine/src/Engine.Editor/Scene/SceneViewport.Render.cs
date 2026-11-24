using System.Diagnostics;
using System.Runtime.InteropServices;
using RectangleF = System.Drawing.RectangleF;
using Matrix3x2 = System.Numerics.Matrix3x2;
using Vector2 = System.Numerics.Vector2;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Engine.Core;
using Engine.Desktop;
using GameColor = System.Drawing.Color;

namespace Engine.Editor;

public sealed partial class SceneViewport
{
    public override void Render(DrawingContext context)
    {
        if (native != null) return;
        context.FillRectangle(Surround, new Rect(Bounds.Size));
        using var transform = context.PushTransform(Matrix.CreateScale(Scale, Scale) * Matrix.CreateTranslation(Origin.X, Origin.Y));
        var area = CanvasArea;
        using var clip = context.PushClip(area);
        var started=Stopwatch.GetTimestamp();
        var canvas=new PreviewCanvas(context, bitmaps, variants, shaderSheets, gameText, area);
        using (FrameProfiler.Sample(FrameProfiler.Render)) DrawWorld(canvas);
        if(IsPlaying)Profile.Complete(started,canvas.Submissions);
    }

    internal bool IsSpritePreview => spritePreview;

    internal void DrawWorld(ISceneCanvas canvas)
    {
        if (spritePreview)
        {
            canvas.Clear(SceneGame.Background);
            if (Particles is { } isolated)
            {
                float w = (float)Bounds.Width, h = (float)Bounds.Height;
                isolated.DrawAll(canvas, new RectangleF(-w / 2, -h / 2, w, h), Matrix3x2.CreateTranslation(w / 2, h / 2));
                return;
            }
            if (previewFrame is not { Image: {} path } frame) return;
            var size = frame.Canvas is {} trim ? new System.Drawing.Size(trim.Width,trim.Height) :
                frame.Source is {} cell ? new System.Drawing.Size(cell.Width,cell.Height) :
                previewSizes.TryGetValue(path,out var cached) ? cached : previewSizes[path] = SpriteRegion.ReadPngSize(path);
            float scale = (float)Math.Min(Bounds.Width/size.Width,Bounds.Height/size.Height);
            float width=size.Width*scale, height=size.Height*scale;
            var pivot = new Vector2((float)Bounds.Width/2,(float)Bounds.Height/2);
            var transform = Matrix3x2.CreateScale(previewPose.ScaleX ?? 1,previewPose.ScaleY ?? 1,pivot)
                * Matrix3x2.CreateRotation(float.DegreesToRadians(previewPose.Rotation ?? 0),pivot);
            (frame with { Color = previewPose.Paint(frame.Color) }).Draw(canvas,
                new((float)(Bounds.Width-width)/2,(float)(Bounds.Height-height)/2,width,height),transform);
            return;
        }
        if (game != null)
        {
            PhysicsDebug.Selection=Selected?.Id;
            game.Draw(canvas, loop?.Interpolation ?? 1f);
            return;
        }
        canvas.Clear(SceneGame.Background);
        var area = CanvasArea;
        float unit = (float)(1 / Scale);
        if (EditMap == null)
        {
            double step = 40 * Math.Pow(2, Math.Ceiling(Math.Log2(0.6 / Scale)));
            double left = Math.Floor(area.Left / step) * step, top = Math.Floor(area.Top / step) * step;
            for (int i = 0; i <= Math.Ceiling(area.Width / step); i++)
                canvas.FillRectangle(new RectangleF((float)(left + i * step), (float)area.Top, unit, (float)area.Height), GridColor);
            for (int i = 0; i <= Math.Ceiling(area.Height / step); i++)
                canvas.FillRectangle(new RectangleF((float)area.Left, (float)(top + i * step), (float)area.Width, unit), GridColor);
        }
        var visible = new RectangleF((float)area.X, (float)area.Y, (float)area.Width, (float)area.Height);
        var global = Scene.Objects.FirstOrDefault(o => !IsHidden(o) && o.GetComponent<GlobalLight2DComponent>() != null)?.GetComponent<GlobalLight2DComponent>();
        bool unlit = global == null;
        foreach (var (obj,map,layer,_) in EditItems())
        {
            if (!unlit && layer >= global!.UnlitLayer) { DrawLighting(canvas,global,visible); unlit = true; }
            if (map != null)
            {
                if (Palette(map.Tileset) is { } palette) map.Draw(canvas,palette,obj.WorldMatrix,visible,System.Numerics.Matrix3x2.Identity);
                continue;
            }
            DrawSceneSprite(canvas,obj,visible);
            Particles?.Draw(canvas,obj,visible);
            if (obj.GetComponent<Line2DComponent>() is {} line) new LineRenderer(line).Draw(canvas,obj.WorldMatrix,System.Numerics.Matrix3x2.Identity);
            if (obj.GetComponent<Text2DComponent>() is {} text)
            {
                var label = new WorldTextLabel(text);
                label.Draw(canvas,label.Text(PreviewUi),new(obj.Width,obj.Height),obj.WorldMatrix,System.Numerics.Matrix3x2.Identity,visible);
            }
        }
        if (!unlit) DrawLighting(canvas,global!,visible);
        if (global == null) foreach (var obj in Scene.Objects)
            if (obj.GetComponent<PointLight2DComponent>() is {} light && !IsHidden(obj))
                light.Draw(canvas,ShaderClip.LightFalloff,Vector2.Transform(new(obj.Width*.5f,obj.Height*.5f),obj.WorldMatrix),visible,System.Numerics.Matrix3x2.Identity,0);
        foreach (var camera in Scene.Cameras.Where(camera => !IsHidden(camera)))
        {
            var center = camera.WorldPosition;
            var lens=camera.GetComponent<Camera2DComponent>()!;
            var frame=lens.View(LayoutSettings.Resolution,center).World;
            if (camera==Selected && lens.Bounds is {} box)
                Outline(canvas,new RectangleF(box.X,box.Y,box.Width,box.Height),unit,FrameColor);
            var color = camera == Selected ? SelectionColor : camera == Scene.Camera ? GameColor.FromArgb(135, 150, 158) : FrameColor;
            canvas.DrawText( (camera == Scene.Camera ? "Start / " : "") + camera.Name, frame.Left + 3 * unit, frame.Top + 3 * unit, 11 * unit, color);
            Outline(canvas, frame, 1.3f * unit, color);
            canvas.FillRectangle(new RectangleF(center.X - 6 * unit, center.Y - unit, 12 * unit, 2 * unit), color);
            canvas.FillRectangle(new RectangleF(center.X - unit, center.Y - 6 * unit, 2 * unit, 12 * unit), color);
        }
        DrawTileGuide(canvas,visible,unit);
        var (uiFrame, zoom) = UiFrame();
        using (Preview == null ? null : canvas.PushClip(uiFrame))
            PreviewUi.Draw(canvas, uiFrame.X, uiFrame.Y, 1 / zoom);
        if (Preview != null) Outline(canvas, uiFrame, unit, FrameColor);
        DrawSelection(canvas,unit);
    }

    // The player's lighting path (roadmap 16.7) at effect time 0, with shadows from the scene's colliders and solid tiles.
    private readonly LightShadows editShadows = new();
    private void DrawLighting(ISceneCanvas canvas, GlobalLight2DComponent global, RectangleF visible)
    {
        canvas.BeginLighting(visible, global.Ambient());
        foreach (var obj in Scene.Objects)
        {
            if (obj.GetComponent<PointLight2DComponent>() is not {} light || IsHidden(obj)) continue;
            var center = Vector2.Transform(new(obj.Width*.5f,obj.Height*.5f),obj.WorldMatrix);
            if (!light.Shade(center,visible,0,out var lit,out var tint)) continue;
            editShadows.Clear();
            if (light.Shadows)
                foreach (var other in Scene.Objects)
                {
                    if (other == obj || IsHidden(other)) continue;
                    if (other.GetComponent<Collider2DComponent>() is { IsTrigger: false }) editShadows.Add(CollisionGeometry.ForObject(other),center,light.Radius);
                    if (other.GetComponent<Tilemap2DComponent>() is not {} map || Palette(map.Tileset) is not {} palette) continue;
                    foreach (var (cell,id) in map.InView(Vector2.Zero,Affine2D.Bounds(lit,Affine2D.Inverse(other.WorldMatrix))))
                        if (palette.Tiles.TryGetValue(id,out var tile) && TileCollision.WorldShape(tile,map.Bounds(Vector2.Zero,cell),map.Layout,other.WorldMatrix) is {} shape)
                            editShadows.Add(shape,center,light.Radius);
                }
            editShadows.Finish(center,light.Radius);
            canvas.DrawLight(ShaderClip.LightFalloff,lit,Matrix3x2.Identity,tint,editShadows.Quads,Matrix3x2.Identity);
        }
        canvas.EndLighting();
    }

    private static void Outline(ICanvas2D canvas, RectangleF box, float line, GameColor color)
    {
        canvas.FillRectangle(new RectangleF(box.Left, box.Top, box.Width, line), color);
        canvas.FillRectangle(new RectangleF(box.Left, box.Bottom - line, box.Width, line), color);
        canvas.FillRectangle(new RectangleF(box.Left, box.Top, line, box.Height), color);
        canvas.FillRectangle(new RectangleF(box.Right - line, box.Top, line, box.Height), color);
    }

    // Game text uses the player's SDL_ttf rasterizer; the label image becomes an Avalonia opacity mask.
    private sealed class PreviewText : IDisposable
    {
        public readonly TtfTextRasterizer Renderer = new();
        private readonly Dictionary<string,Bitmap> cache = [];
        public void UseFonts(IReadOnlyList<string> paths)
        {
            if (Renderer.UseFonts(paths)) Clear();
        }
        public unsafe Bitmap Get(string text)
        {
            if (cache.TryGetValue(text, out var existing)) return existing;
            if (cache.Count >= 256) Clear();
            var pixels = Renderer.Rasterize(text);
            fixed (byte* data = pixels.Pixels)
                return cache[text] = new Bitmap(Avalonia.Platform.PixelFormat.Rgba8888, Avalonia.Platform.AlphaFormat.Unpremul, (nint)data,
                    new PixelSize(pixels.Width, pixels.Height), new Vector(96, 96), pixels.Width * 4);
        }
        private void Clear()
        {
            foreach (var bitmap in cache.Values) bitmap.Dispose();
            cache.Clear();
        }
        public void Dispose() { Clear(); Renderer.Dispose(); }
    }

    private sealed class PreviewCanvas(DrawingContext context, SpriteCache<Bitmap> images, SpriteVariants<Bitmap> variants, ShaderSheets<Bitmap> shaders, PreviewText fonts, Rect area) : ISceneCanvas
    {
        public long Submissions;
        public void DrawShader(ShaderClip shader,int frame,RectangleF r,System.Numerics.Matrix3x2 m,GameColor tint,bool additive)
        {
            var bitmap=shaders.Get(shader,GameColor.FromArgb(255,tint),additive,frame,out var cell);
            using var transform=context.PushTransform(new Matrix(m.M11,m.M12,m.M21,m.M22,m.M31,m.M32));
            using var opacity=context.PushOpacity(tint.A/255.0);
            using var blend=context.PushRenderOptions(new RenderOptions {BitmapBlendingMode=additive?BitmapBlendingMode.Plus:BitmapBlendingMode.SourceOver,
                BitmapInterpolationMode=BitmapInterpolationMode.None});
            context.DrawImage(bitmap,new Rect(cell.X,cell.Y,cell.Width,cell.Height),new Rect(r.X,r.Y,r.Width,r.Height));
            Submissions++;
        }
        public System.Drawing.Size SpriteSize(string path) => images.TryGet(path, out var image) ? new(image.PixelSize.Width, image.PixelSize.Height) : default;
        public IDisposable PushClip(RectangleF r) => context.PushClip(new Rect(r.X,r.Y,r.Width,r.Height));
        public void BeginColorAdjust() { }
        // Same arithmetic as the SDL layer (colour × multiply + lift) with blended fills over the game area.
        public void EndColorAdjust(GameColor multiply, byte lift)
        {
            void Fill(GameColor c, BitmapBlendingMode mode)
            {
                using var pixel = new WriteableBitmap(new PixelSize(1,1), new Vector(96,96), Avalonia.Platform.PixelFormat.Rgba8888, Avalonia.Platform.AlphaFormat.Unpremul);
                using (var target = pixel.Lock()) Marshal.Copy(new byte[] { c.R, c.G, c.B, 255 }, 0, target.Address, 4);
                using var blend = context.PushRenderOptions(new RenderOptions { BitmapBlendingMode = mode, BitmapInterpolationMode = BitmapInterpolationMode.None });
                context.DrawImage(pixel, new Rect(0,0,1,1), area);
                Submissions++;
            }
            Fill(multiply, BitmapBlendingMode.Multiply);
            if (lift > 0) Fill(GameColor.FromArgb(lift,lift,lift), BitmapBlendingMode.Plus);
        }
        public bool DrawFiltered(SpriteFilter filter, RectangleF r,System.Numerics.Matrix3x2 m,GameColor tint)
        {
            using var transform = context.PushTransform(new Matrix(m.M11,m.M12,m.M21,m.M22,m.M31,m.M32));
            var image = variants.Get(filter,GameColor.FromArgb(255,tint));
            using var opacity = context.PushOpacity(tint.A/255.0);
            Submissions++;SpriteCellImage.DrawPixels(context,image,new Rect(image.PixelSize.ToSize(1)),new Rect(r.X,r.Y,r.Width,r.Height));
            return true;
        }
        public bool DrawAffine(string? path, RectangleF r, System.Numerics.Matrix3x2 m, GameColor tint, SpriteRegion? source = null, string layout = TileGrid.Orthogonal)
        {
            if (path != null && path != SpriteAssets.SquarePath && tint.ToArgb() != GameColor.White.ToArgb())
                return DrawFiltered(new(path,source,null,null),r,m,tint);
            using var transform = context.PushTransform(new Matrix(m.M11,m.M12,m.M21,m.M22,m.M31,m.M32));
            if (path == null) FillRectangle(r,tint);
            else if (path == SpriteAssets.SquarePath) DrawSquareSprite(r,tint);
            else if (source != null && layout != TileGrid.Orthogonal) return DrawTile(path,r,source,layout);
            else return DrawSprite(path,r,source);
            return true;
        }
        private static SolidColorBrush BrushOf(GameColor c) => new(Color.FromArgb(c.A, c.R, c.G, c.B));
        // Avalonia has no per-vertex colours: each quad gets a linear gradient from its first to its second edge pair.
        public void FillStrip(ReadOnlySpan<Vector2> edges, ReadOnlySpan<GameColor> colors, System.Numerics.Matrix3x2 m)
        {
            using var transform = context.PushTransform(new Matrix(m.M11,m.M12,m.M21,m.M22,m.M31,m.M32));
            for (int i = 0; i + 1 < colors.Length; i++)
            {
                Point P(Vector2 v) => new(v.X, v.Y);
                var (a, b, c, d) = (edges[i*2], edges[i*2+2], edges[i*2+3], edges[i*2+1]);
                var shape = new StreamGeometry();
                using (var g = shape.Open()) { g.BeginFigure(P(a), true); g.LineTo(P(b)); g.LineTo(P(c)); g.LineTo(P(d)); g.EndFigure(true); }
                var brush = new LinearGradientBrush { StartPoint = new RelativePoint(P((a + d) / 2), RelativeUnit.Absolute),
                    EndPoint = new RelativePoint(P((b + c) / 2), RelativeUnit.Absolute),
                    GradientStops = { new GradientStop(BrushOf(colors[i]).Color, 0), new GradientStop(BrushOf(colors[i+1]).Color, 1) } };
                Submissions++; context.DrawGeometry(brush, null, shape);
            }
        }
        public void Clear(GameColor color) => context.FillRectangle(BrushOf(color), area);
        public void FillRectangle(System.Drawing.RectangleF r, GameColor color) {
            Submissions++;context.FillRectangle(BrushOf(color),new Rect(r.X,r.Y,r.Width,r.Height));
        }
        public void DrawSquareSprite(RectangleF r, GameColor color)
        {
            if (!images.TryGet(SpriteAssets.SquarePath, out var square)) { FillRectangle(r, GameColor.Magenta); return; }
            var bounds = new Rect(r.X, r.Y, r.Width, r.Height);
            using var mask = context.PushOpacityMask(new ImageBrush(square) { Stretch = Stretch.Fill }, bounds);
            Submissions++;context.FillRectangle(BrushOf(color), bounds);
        }

        public void UseFonts(IReadOnlyList<string> paths) => fonts.UseFonts(paths);
        public float MeasureText(string text,float size)=>fonts.Renderer.Measure(text,size);
        public void MeasureTextEnds(string text,float size,Span<float> ends)=>fonts.Renderer.MeasureEnds(text,size,ends);
        public void DrawText(string text, float left, float top, float size, GameColor color)
        {
            if (text.Length == 0 || size <= 0) return;
            var glyphs = fonts.Get(text);
            var scale = size / TtfTextRasterizer.FontSize;
            var bounds = new Rect(left, top, glyphs.PixelSize.Width * scale, glyphs.PixelSize.Height * scale);
            using var mask = context.PushOpacityMask(new ImageBrush(glyphs) { Stretch = Stretch.Fill }, bounds);
            Submissions++;context.FillRectangle(BrushOf(color), bounds);
        }
        public void DrawLine(Vector2 start, Vector2 end, float width, GameColor color) {
            Submissions++;context.DrawLine(new Pen(BrushOf(color),width),new Point(start.X,start.Y),new Point(end.X,end.Y));
        }
        public bool DrawTile(string path, RectangleF r, SpriteRegion source, string layout)
        {
            if (layout == TileGrid.Orthogonal) return DrawSprite(path,r,source);
            var geometry = new StreamGeometry();
            using (var shape = geometry.Open())
            {
                var outline = TileGrid.Outline(layout);
                Point VertexAt(int i)
                {
                    var v = TileGrid.Vertex(r,TileGrid.Outline(layout)[i]);
                    return new(v.X,v.Y);
                }
                shape.BeginFigure(VertexAt(0),true);
                for (int i = 1; i < outline.Length; i++) shape.LineTo(VertexAt(i));
                shape.EndFigure(true);
            }
            using var clip = context.PushGeometryClip(geometry);
            return DrawSprite(path,r,source);
        }
        public bool DrawSprite(string path, RectangleF r) => DrawSprite(path, r, null);
        public bool DrawSprite(string path, RectangleF r, SpriteRegion? source)
        {
            if (!images.TryGet(path, out var image)) return false;
            var destination = new Rect(r.X, r.Y, r.Width, r.Height);
            if (source == null) context.DrawImage(image, destination);
            else if (source.Fits(image.PixelSize.Width, image.PixelSize.Height))
                SpriteCellImage.DrawPixels(context, image, new Rect(source.X, source.Y, source.Width, source.Height), destination);
            else return false;
            Submissions++;return true;
        }
    }
}
