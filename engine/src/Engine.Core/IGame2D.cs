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
    IDisposable PushClip(RectangleF area) => throw new NotSupportedException("This canvas does not implement camera viewport clipping.");
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
    void DrawText(string text, float x, float y, float size, Color color) => PixelText.Draw(this, text, x, y, size / 8, color);
    bool DrawTile(string path, RectangleF destination, SpriteRegion source, string layout) =>
        layout == TileGrid.Orthogonal ? DrawSprite(path, destination, source) :
        throw new NotSupportedException($"This canvas does not support {layout} tiles.");
    bool DrawSprite(string path, RectangleF destination);
    bool DrawSprite(string path, RectangleF destination, SpriteRegion? source) =>
        source == null && DrawSprite(path, destination);
}
