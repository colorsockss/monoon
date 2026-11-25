using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public interface IGame2D
{
    GameTime? Time => null;
    void UpdateUi(GameInput input, double elapsed) { }
    void Simulate(GameInput input, float seconds) => FixedUpdate(input,seconds);
    InputBindings Bindings => new();
    void FixedUpdate(GameInput input, float seconds) => FixedUpdate(input.Move, seconds);
    Size LogicalSize { get; }
    void FixedUpdate(Vector2 moveDirection, float seconds);
    void Draw(ICanvas2D canvas, float interpolation);
}

public interface ICanvas2D
{
    void UseFonts(IReadOnlyList<string> paths) { }
    void FlushSprites() { }

    void DrawSpriteInstances(SceneAppearance sprite, ReadOnlySpan<SpriteInstance2D> instances, Matrix3x2 view, bool batched)
        => DrawInstancesIndividually(this, sprite, instances, view, batched);

    static void DrawInstancesIndividually(ICanvas2D canvas, SceneAppearance sprite, ReadOnlySpan<SpriteInstance2D> instances,
        Matrix3x2 view, bool batched)
    {
        if (!batched) canvas.FlushSprites();
        foreach (ref readonly var instance in instances)
        {
            var bounds = instance.Bounds;
            var matrix = Matrix3x2.CreateTranslation(bounds.X, bounds.Y) * view;
            (sprite with { Color = instance.Tint }).Draw(canvas, new(0, 0, bounds.Width, bounds.Height), matrix);
            if (!batched) canvas.FlushSprites();
        }
    }

    void DrawShader(ShaderClip shader, int frame, RectangleF local, Matrix3x2 matrix, Color tint, bool additive) =>
        throw new NotSupportedException("This canvas does not implement procedural sprites.");
    // One emitter's particles in one call. This fallback draws each quad on its own and blends PNGs as alpha only.
    void DrawParticles(ParticleTexture texture, ReadOnlySpan<ParticleQuad> quads, Matrix3x2 view, ParticleBlend blend)
    {
        var unit = new RectangleF(0, 0, 1, 1);
        foreach (ref readonly var q in quads)
        {
            var corner = q.Center - q.Right - q.Down;
            var matrix = new Matrix3x2(q.Right.X * 2, q.Right.Y * 2, q.Down.X * 2, q.Down.Y * 2, corner.X, corner.Y) * view;
            if (texture.Shader != null) DrawShader(texture.Shader, q.Cell, unit, matrix, q.Tint, blend == ParticleBlend.Additive);
            else DrawAffine(texture.Path, unit, matrix, q.Tint, texture.Cells[q.Cell]);
        }
    }
    // A ribbon of quads: edges holds a left/right vertex pair per colour; colours blend between neighbouring pairs.
    void FillStrip(ReadOnlySpan<Vector2> edges, ReadOnlySpan<Color> colors, Matrix3x2 matrix) =>
        throw new NotSupportedException("This canvas does not implement line strips.");
    // Screen colour adjustment: the world drawn between these calls is multiplied by a colour, then lifted toward white.
    void BeginColorAdjust() => throw new NotSupportedException("This canvas does not implement screen colour adjustment.");
    void EndColorAdjust(Color multiply, byte lift) { }
    // One camera's colour adjustment: the same arithmetic over area of what is already drawn there (opaque camera pixels).
    void AdjustRectangle(RectangleF area, Color multiply, byte lift) =>
        throw new NotSupportedException("This canvas does not implement camera colour adjustment.");
    // 2D lighting (roadmap 16.7). BeginLighting starts a lightmap over area (canvas units) cleared to ambient; DrawLight adds
    // one falloff sprite to it with shadow quads (4 world points each, drawn with view) cut out of that light only;
    // EndLighting multiplies what is already drawn under area by the lightmap. This fallback, for canvases without render
    // targets (the Win/Linux editor preview), darkens to the ambient brightness and adds the lights on top without shadows.
    void BeginLighting(RectangleF area, Color ambient) =>
        FillRectangle(area, Color.FromArgb(255 - (ambient.R + ambient.G + ambient.B) / 3, 0, 0, 0));
    void DrawLight(ShaderClip sheet, RectangleF local, Matrix3x2 matrix, Color tint, ReadOnlySpan<Vector2> shadowQuads, Matrix3x2 view) =>
        DrawShader(sheet, 0, local, matrix, tint, true);
    void EndLighting() { }
    IDisposable PushClip(RectangleF area) => throw new NotSupportedException("This canvas does not implement camera viewport clipping.");
    // Pixel size of a loaded sprite image; empty when this canvas cannot tell (Tiled and Sliced sprites then draw stretched).
    Size SpriteSize(string path) => Size.Empty;
    // Tiled and Sliced sprites (SpriteDrawMode). size is the image or atlas cell; borders cut it into a 9-slice.
    bool DrawGrid(string path, RectangleF local, Matrix3x2 matrix, Color tint, SpriteRegion? source, Size size, UiBorders? borders, bool tiled) =>
        SpriteGrid.Draw(this, path, local, matrix, tint, source, size, borders, tiled);
    bool DrawFiltered(SpriteFilter filter, RectangleF local, Matrix3x2 matrix, Color tint) =>
        throw new NotSupportedException("This canvas does not support sprite masks.");
    // Local rectangle plus affine basis; keeps trimmed atlas frames in object space.
    bool DrawAffine(string? path, RectangleF r, Matrix3x2 matrix, Color tint, SpriteRegion? source = null,
        string layout = TileGrid.Orthogonal)
    {
        if (matrix.M12 != 0 || matrix.M21 != 0 || matrix.M11 <= 0 || matrix.M22 <= 0)
            throw new NotSupportedException("This canvas does not implement affine sprites.");
        var bounds = new RectangleF(r.X * matrix.M11 + matrix.M31,r.Y * matrix.M22 + matrix.M32,r.Width * matrix.M11,r.Height * matrix.M22);
        if (path == null) FillRectangle(bounds,tint);
        else if (path == SpriteAssets.SquarePath) DrawSquareSprite(bounds,tint);
        else if (source != null && layout != TileGrid.Orthogonal) return DrawTile(path,bounds,source,layout);
        else return DrawSprite(path,bounds,source);
        return true;
    }
    void Clear(Color color);
    void FillRectangle(RectangleF rectangle, Color color);
    void DrawSquareSprite(RectangleF destination, Color color) => FillRectangle(destination, color);
    float MeasureText(string text,float size)=>System.Globalization.StringInfo.ParseCombiningCharacters(text).Length*size*.7f;
    /// <summary>
    /// Writes, for each UTF-16 char of <paramref name="text"/>, the right edge of the glyph cluster holding it when the whole
    /// string is drawn at <paramref name="size"/>. Desktop canvases read SDL_ttf's shaped positions; this default adds up text elements.
    /// </summary>
    void MeasureTextEnds(string text, float size, Span<float> ends)
    {
        float x = 0;
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            x += MeasureText(element, size);
            ends.Slice(elements.ElementIndex, element.Length).Fill(x);
        }
    }
    void DrawText(string text, float x, float y, float size, Color color) => PixelText.Draw(this, text, x, y, size / 8, color);
    bool DrawTile(string path, RectangleF destination, SpriteRegion source, string layout) =>
        layout == TileGrid.Orthogonal ? DrawSprite(path, destination, source) :
        throw new NotSupportedException($"This canvas does not support {layout} tiles.");
    bool DrawSprite(string path, RectangleF destination);
    bool DrawSprite(string path, RectangleF destination, SpriteRegion? source) =>
        source == null && DrawSprite(path, destination);
}
