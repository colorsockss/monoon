using System.Drawing;
using Engine.Core;

namespace Engine.Editor;

internal enum TileTool { Brush, Eraser, Rectangle, Eyedropper }
internal readonly record struct TileChange(Point Cell, int Before, int After);

internal sealed class TileStroke(Tilemap2DComponent map, TileTool tool, int tile, Point start)
{
    private readonly Dictionary<Point, int> original = [];
    private Point previous = start;
    public Point Start { get; } = start;
    public void Move(Point next)
    {
        if (tool == TileTool.Rectangle) return;
        if (TileGrid.IsHex(map.Layout))
        {
            foreach (var cell in TileGrid.HexLine(previous,next)) Paint(cell);
            previous = next;
            return;
        }
        long x = previous.X, y = previous.Y;
        long dx = Math.Abs((long)next.X - x), dy = -Math.Abs((long)next.Y - y);
        int sx = x < next.X ? 1 : -1, sy = y < next.Y ? 1 : -1;
        long error = dx + dy;
        while (true)
        {
            Paint(new((int)x, (int)y));
            if (x == next.X && y == next.Y) break;
            long twice = 2 * error;
            if (twice >= dy) { error += dy; x += sx; }
            if (twice <= dx) { error += dx; y += sy; }
        }
        previous = next;
    }

    private void Paint(Point cell)
    {
        int value = tool == TileTool.Eraser ? 0 : tile;
        int before = map.Get(cell);
        if (before == value) return;
        original.TryAdd(cell, before);
        map.Set(cell, value);
    }

    public TileChange[] Finish(Point end)
    {
        if (tool == TileTool.Rectangle)
        {
            long height = Math.Abs((long)end.Y - Start.Y) + 1;
            long width = Math.Abs((long)end.X - Start.X) + 1;
            if (width > 1_048_576 / height)
                throw new InvalidOperationException("This rectangle exceeds one million cells. Paint a smaller area.");
            for (long y = Math.Min(Start.Y, end.Y); y <= Math.Max(Start.Y, end.Y); y++)
                for (long x = Math.Min(Start.X, end.X); x <= Math.Max(Start.X, end.X); x++)
                    Paint(new((int)x, (int)y));
        }
        else Move(end);
        return original.Select(p => new TileChange(p.Key, p.Value, map.Get(p.Key))).Where(p => p.Before != p.After).ToArray();
    }

    public void Cancel() { foreach (var (cell, value) in original) map.Set(cell, value); }
}
