using System.Drawing;

namespace Engine.Core;

/// <summary>A perfect rectangular maze: one connected path between every pair of rooms.
/// Coordinates are tile coordinates, including a one-cell closed border.</summary>
public sealed class MazeLayout
{
    public const int Version = 1;
    public int Width { get; }
    public int Height { get; }
    public Point Entrance => new(1, 1);
    public Point Exit { get; }
    private readonly bool[] passages;

    public bool IsPassage(int x, int y) => (uint)x < Width && (uint)y < Height && passages[y * Width + x];

    public static MazeLayout Generate(int columns, int rows, ulong seed) => new(columns, rows, new SeededRandom(seed));

    private MazeLayout(int columns, int rows, SeededRandom random)
    {
        if (columns is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows is < 1 or > 256 || rows * columns < 2) throw new ArgumentOutOfRangeException(nameof(rows));
        Width = 2 * columns + 1;
        Height = 2 * rows + 1;
        passages = new bool[Width * Height];
        int start = Width + 1, farthest = start, maxDepth = 1;
        // Iterative backtracking avoids a call-stack limit. Candidate order and tie-breaking
        // are part of layout v1; never depend on collection iteration or floating point.
        int[] path = new int[columns * rows];
        Span<int> moves = stackalloc int[4];
        ReadOnlySpan<int> offsets = [-Width, 1, Width, -1];
        path[0] = start;
        passages[start] = true;
        int depth = 1;
        while (depth != 0)
        {
            int at = path[depth - 1], count = 0;
            foreach (int step in offsets)
            {
                int door = at + step;
                if (door < Width || door >= passages.Length - Width || door % Width == 0 || door % Width == Width - 1)
                    continue;
                if (!passages[door + step]) moves[count++] = step;
            }
            if (count == 0) { depth--; continue; }
            int delta = moves[random.NextInt(count)], next = at + 2 * delta;
            passages[at + delta] = passages[next] = true;
            path[depth++] = next;
            if (depth > maxDepth || depth == maxDepth && next < farthest)
                (maxDepth, farthest) = (depth, next);
        }
        Exit = new(farthest % Width, farthest / Width);
    }
}
