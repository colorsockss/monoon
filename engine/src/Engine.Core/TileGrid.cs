using System.Drawing;
using System.Numerics;

namespace Engine.Core;

// Cell coordinates stay integral in every layout. Hexagonal maps use axial (q,r) coordinates.
public static class TileGrid
{
    public const string Orthogonal = "Orthogonal", Isometric = "Isometric", HexPointy = "HexPointy", HexFlat = "HexFlat";
    public static bool IsSupported(string layout) => layout is Orthogonal or Isometric or HexPointy or HexFlat;
    public static bool IsHex(string layout) => layout is HexPointy or HexFlat;

    private static readonly Vector2[] square = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
    private static readonly Vector2[] diamond = [new(.5f, 0), new(1, .5f), new(.5f, 1), new(0, .5f)];
    private static readonly Vector2[] pointy = [new(.5f, 0), new(1, .25f), new(1, .75f), new(.5f, 1), new(0, .75f), new(0, .25f)];
    private static readonly Vector2[] flat = [new(.25f, 0), new(.75f, 0), new(1, .5f), new(.75f, 1), new(.25f, 1), new(0, .5f)];
    public static ReadOnlySpan<Vector2> Outline(string layout) => layout switch
    {
        Orthogonal => square,
        Isometric => diamond,
        HexPointy => pointy,
        HexFlat => flat,
        _ => throw new ArgumentOutOfRangeException(nameof(layout))
    };

    public static Vector2 Vertex(RectangleF bounds, Vector2 uv) =>
        new(bounds.X + bounds.Width * uv.X, bounds.Y + bounds.Height * uv.Y);

    internal static (double X, double Y) Project(string layout, double q, double r) => layout switch
    {
        Isometric => ((q - r) * .5, (q + r) * .5),
        HexPointy => (q + r * .5, r * .75),
        HexFlat => (q * .75, r + q * .5),
        _ => (q, r)
    };

    private static (double Q, double R) Inverse(string layout, double x, double y) => layout switch
    {
        Isometric => (x + y, y - x),
        HexPointy => (x - y / 1.5, y / .75),
        HexFlat => (x / .75, y - x / 1.5),
        _ => (x, y)
    };

    internal static Point Pick(string layout, double x, double y)
    {
        if (layout == Orthogonal) return new(int.CreateSaturating(Math.Floor(x)), int.CreateSaturating(Math.Floor(y)));
        var (q, r) = Inverse(layout, x - .5, y - .5);
        return Round(layout, q, r);
    }

    internal static Point Round(string layout, double q, double r)
    {
        double a = Math.Floor(q + .5), b = Math.Floor(r + .5);
        if (IsHex(layout))
        {
            double s = -q - r, c = Math.Floor(s + .5);
            if (Math.Abs(a - q) > Math.Abs(b - r) && Math.Abs(a - q) > Math.Abs(c - s)) a = -b - c;
            else if (Math.Abs(b - r) > Math.Abs(c - s)) b = -a - c;
        }
        return new(int.CreateSaturating(a), int.CreateSaturating(b));
    }

    // Rows intersecting the viewport, then only columns inside its expanded screen-space box.
    public static IEnumerable<Point> VisibleCells(Tilemap2DComponent map, Vector2 origin, RectangleF view)
    {
        var (first, last) = map.CellRange(origin, view);
        var (dx, dy) = Project(map.Layout, 1, 0);
        for (long r = first.Y; r <= last.Y; r++)
        {
            var (x, y) = Project(map.Layout, 0, r);
            long lo = first.X, hi = last.X;
            Clip(x, dx, (view.Left - origin.X) / map.CellSize.Width - 1, (view.Right - origin.X) / map.CellSize.Width, ref lo, ref hi);
            Clip(y, dy, (view.Top - origin.Y) / map.CellSize.Height - 1, (view.Bottom - origin.Y) / map.CellSize.Height, ref lo, ref hi);
            for (long q = lo; q <= hi; q++) yield return new((int)q, (int)r);
        }
    }

    private static void Clip(double origin, double step, double min, double max, ref long lo, ref long hi)
    {
        if (step == 0) { if (origin < min || origin > max) hi = lo - 1; return; }
        lo = Math.Max(lo, long.CreateSaturating(Math.Ceiling((min - origin) / step)));
        hi = Math.Min(hi, long.CreateSaturating(Math.Floor((max - origin) / step)));
    }

    public static IEnumerable<Point> HexLine(Point start, Point end)
    {
        long dq = (long)end.X - start.X, dr = (long)end.Y - start.Y;
        long steps = Math.Max(Math.Abs(dq + dr), Math.Max(Math.Abs(dq), Math.Abs(dr)));
        for (long i = 0; i <= steps; i++)
        {
            var t = steps == 0 ? 0 : (double)i / steps;
            yield return Round(HexPointy, start.X + dq * t, start.Y + dr * t);
        }
    }
}
