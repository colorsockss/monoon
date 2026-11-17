using System.Drawing;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record UiBorders(int Left, int Top, int Right, int Bottom)
{
    internal void Validate(Size size)
    {
        if (Left < 0 || Top < 0 || Right < 0 || Bottom < 0 ||
            (long)Left + Right >= size.Width || (long)Top + Bottom >= size.Height)
            throw new InvalidDataException("9-slice borders must be nonnegative and leave at least one center pixel in the source PNG.");
    }
}

// Source regions are fixed for the node's lifetime. Adjacent pieces share destination edges.
internal sealed class UiNineSlice
{
    private readonly SpriteRegion?[] regions = new SpriteRegion?[9];
    private readonly UiBorders borders;
    public UiNineSlice(UiBorders borders, Size size)
    {
        borders.Validate(size);
        this.borders = borders;
        Span<int> x = [0, borders.Left, size.Width - borders.Right, size.Width];
        Span<int> y = [0, borders.Top, size.Height - borders.Bottom, size.Height];
        for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
                if (x[i+1] > x[i] && y[j+1] > y[j])
                    regions[j*3+i] = new(x[i], y[j], x[i+1]-x[i], y[j+1]-y[j]);
    }

    public bool Draw(ICanvas2D canvas, string path, RectangleF box, float scale)
    {
        Span<float> x = stackalloc float[4], y = stackalloc float[4];
        Edges(x, box.X, box.Width, borders.Left * scale, borders.Right * scale);
        Edges(y, box.Y, box.Height, borders.Top * scale, borders.Bottom * scale);
        for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
                if (regions[j*3+i] is {} src && x[i+1] > x[i] && y[j+1] > y[j] &&
                    !canvas.DrawSprite(path, RectangleF.FromLTRB(x[i], y[j], x[i+1], y[j+1]), src)) return false;
        return true;
    }

    private static void Edges(Span<float> cuts, float start, float length, float before, float after)
    {
        cuts[0] = start;
        cuts[3] = start + length;
        if (before + after > length) cuts[1] = cuts[2] = start + length * before / (before + after);
        else { cuts[1] = start + before; cuts[2] = cuts[3] - after; }
    }
}
