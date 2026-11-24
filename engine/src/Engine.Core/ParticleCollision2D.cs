using System.Drawing;
using System.Numerics;

namespace Engine.Core;

// What colliding particles query: tile layers by direct cell lookup, and the scene's physics for Colliders mode.
internal sealed class ParticleWorld(ParticleTiles[] tiles, IPhysics2D? physics)
{
    internal ParticleTiles[] Tiles { get; } = tiles;
    internal IPhysics2D? Physics { get; } = physics;
}

// One orthogonal tile layer as seen by particles: a dense copy of each touched 32×32 chunk and each tile's collision
// planes in cell pixels, so a particle costs one floor, one array read and a few dot products. Chunk copies are made
// the first time a particle enters a chunk and again after the map's cells change; steady play never allocates.
internal sealed class ParticleTiles
{
    // Outward planes (nx, ny, d) with n·p ≤ d inside, in pixels from the cell's top left. OneWay has one plane that only
    // stops particles crossing it from the front.
    private readonly record struct Shape(Vector3[]? Planes, bool OneWay);

    private readonly Tilemap2DComponent map;
    private readonly Shape[] shapes;
    private readonly float width, height;
    private Dictionary<Point, int[]?> chunks = [];
    private object? cells;
    private Point last = new(int.MinValue, int.MinValue);
    private int[]? lastCells;
    private Matrix3x2 inverse = Matrix3x2.Identity;

    internal bool Active { get; set; } = true;
    internal int Layer => map.CollisionLayer;

    internal ParticleTiles(Tilemap2DComponent map, TilePalette palette)
    {
        this.map = map;
        (width, height) = (map.CellSize.Width, map.CellSize.Height);
        shapes = new Shape[palette.Tiles.Count == 0 ? 1 : palette.Tiles.Keys.Max() + 1];
        foreach (var (id, tile) in palette.Tiles)
        {
            if (tile.Collision is { } custom)
            {
                var points = Array.ConvertAll(custom.Vertices, p => new Vector2(p.X * width, p.Y * height));
                shapes[id] = custom.OneWay ? new([Plane(points[0], points[1], null)], true) : new(Hull(points), false);
            }
            else if (tile.Solid) shapes[id] = new(Hull([new(0, 0), new(width, 0), new(width, height), new(0, height)]), false);
        }
    }

    private static Vector3[] Hull(Vector2[] points)
    {
        var center = Vector2.Zero;
        foreach (var p in points) center += p;
        center /= points.Length;
        var planes = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++) planes[i] = Plane(points[i], points[(i + 1) % points.Length], center);
        return planes;
    }

    // The edge a→b; its normal points away from center (or, for a one-way face, to the left of a→b as TileCollision does).
    private static Vector3 Plane(Vector2 a, Vector2 b, Vector2? center)
    {
        // Scalar maths: Vector2 dot products may use different instructions in JIT and Native AOT on x64.
        float ex = b.X - a.X, ey = b.Y - a.Y, length = MathF.Sqrt(ex * ex + ey * ey), nx = ey / length, ny = -ex / length;
        if (center is { } c && nx * (c.X - a.X) + ny * (c.Y - a.Y) > 0) (nx, ny) = (-nx, -ny);
        return new(nx, ny, nx * a.X + ny * a.Y);
    }

    internal Matrix3x2 Matrix { set => inverse = Affine2D.Inverse(value); }

    private int Tile(int column, int row)
    {
        if (!ReferenceEquals(cells, map.CellVersion())) { cells = map.CellVersion(); chunks = []; last = new(int.MinValue, int.MinValue); }
        var key = new Point(column >> 5, row >> 5);
        if (key != last)
        {
            last = key;
            if (!chunks.TryGetValue(key, out lastCells)) chunks[key] = lastCells = map.DenseChunk(key);
        }
        return lastCells?[((row & 31) << 5) | (column & 31)] ?? 0;
    }

    // Whether moving from `from` to `to` (world) ends inside a solid tile; the normal is the face the move came through.
    internal bool Hit(Vector2 from, Vector2 to, out Vector2 normal)
    {
        normal = default;
        var p = Affine2D.Transform(to, inverse);
        float cx = MathF.Floor(p.X / width), cy = MathF.Floor(p.Y / height);
        int column = (int)cx, row = (int)cy, id = Tile(column, row);
        if ((uint)id >= (uint)shapes.Length || shapes[id].Planes is not { } planes) return false;
        float ox = cx * width, oy = cy * height, px = p.X - ox, py = p.Y - oy;
        var q = Affine2D.Transform(from, inverse);
        float qx = q.X - ox, qy = q.Y - oy;
        Vector3 face;
        if (shapes[id].OneWay)
        {
            face = planes[0];
            if (face.X * qx + face.Y * qy - face.Z < 0 || face.X * px + face.Y * py - face.Z >= 0) return false;
        }
        else
        {
            face = planes[0];
            float best = float.NegativeInfinity;
            foreach (var plane in planes)
            {
                if (plane.X * px + plane.Y * py - plane.Z > 0) return false;
                float gap = plane.X * qx + plane.Y * qy - plane.Z;
                if (gap > best) (best, face) = (gap, plane);
            }
        }
        // Normals carry the inverse-transpose of the layer pose.
        float nx = inverse.M11 * face.X + inverse.M12 * face.Y, ny = inverse.M21 * face.X + inverse.M22 * face.Y,
            length = MathF.Sqrt(nx * nx + ny * ny);
        normal = length > 0 ? new(nx / length, ny / length) : new(face.X, face.Y);
        return true;
    }
}

// 2D value noise from an integer lattice hash with smoothstep blending (no Sin/Cos, no tables). Both force channels
// come from one hash per lattice corner.
internal static class ParticleNoise2D
{
    private static uint Hash(int x, int y, uint seed)
    {
        uint h = unchecked((uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ seed);
        h ^= h >> 15; h = unchecked(h * 0x2C1B3C6Du);
        h ^= h >> 12; h = unchecked(h * 0x297A2D39u);
        return h ^ h >> 15;
    }

    // Two values in −1…1 at (x, y).
    internal static Vector2 Sample(float x, float y, uint seed)
    {
        float fx = MathF.Floor(x), fy = MathF.Floor(y), u = x - fx, v = y - fy;
        u = u * u * (3 - 2 * u);
        v = v * v * (3 - 2 * v);
        int ix = (int)fx, iy = (int)fy;
        uint a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
        static float Low(uint h) => (h & 0xFFFF) * (2f / 0xFFFF) - 1;
        static float High(uint h) => (h >> 16) * (2f / 0xFFFF) - 1;
        static float Mix(float a, float b, float t) => a + (b - a) * t;
        return new(Mix(Mix(Low(a), Low(b), u), Mix(Low(c), Low(d), u), v), Mix(Mix(High(a), High(b), u), Mix(High(c), High(d), u), v));
    }
}
