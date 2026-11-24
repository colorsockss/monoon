using System.Drawing;
using System.Numerics;

namespace Engine.Core;

// Shadow quads of one point light (roadmap 16.7). Add collects the caster edges that face away from the light; Finish joins
// touching collinear horizontal and vertical edges (a row of tiles casts one quad, not one per tile, which matters for
// overdraw on the software renderer), clips each edge to the light's radius and extrudes it away from the light to twice
// the radius, so the far side stays outside the lit circle. A quad is 4 world points: near a, near b, far b, far a.
// Drawn black into the light's own buffer, they cut that light only.
internal sealed class LightShadows
{
    // Plain structs and arrays here (not records, lists or LINQ): each generic or record member adds Native AOT code.
    private struct Segment(int axis, float line, float start, float end, Vector2 a, Vector2 b) // Axis 0: y = Line, 1: x = Line, 2: other
    {
        public int Axis = axis;
        public float Line = line, Start = start, End = end;
        public Vector2 A = a, B = b;
    }
    private Segment[] edges = new Segment[256];
    private int edgeCount;
    private Vector2[] points = new Vector2[256];
    internal int Count { get; private set; }
    internal int Edges => edgeCount;
    internal int Casters { get; private set; }
    internal ReadOnlySpan<Vector2> Quads => points.AsSpan(0, Count);
    internal void Clear() => Count = edgeCount = Casters = 0;

    private const float Diagonal = .70710677f;
    private static readonly Vector2[] Octagon = [new(1, 0), new(Diagonal, Diagonal), new(0, 1), new(-Diagonal, Diagonal),
        new(-1, 0), new(-Diagonal, -Diagonal), new(0, -1), new(Diagonal, -Diagonal)];

    internal void Add(in Shape2D shape, Vector2 light, float radius)
    {
        Casters++;
        var o = shape.Offset;
        if (shape.Parts != null)
        {
            foreach (var part in shape.Parts) Collect(part.Vertices![0] + o, part.Vertices[1] + o);
            return;
        }
        Span<Vector2> ring = stackalloc Vector2[8];
        if (shape.Vertices == null)
        {
            // shortcut: a circle casts as its circumscribed octagon; use more sides if round casters look faceted.
            float r = shape.Radius / MathF.Cos(MathF.PI / 8);
            for (int i = 0; i < 8; i++) ring[i] = o + Octagon[i] * r;
            Polygon(ring, light);
            return;
        }
        var v = shape.Vertices;
        if (v.Length == 2 && shape.FaceNormal != default) { Collect(v[0] + o, v[1] + o); return; }
        if (v.Length == 2)
        {
            // shortcut: a capsule casts as the box around it; its rounded ends are square in the shadow.
            Vector2 a = v[0] + o, b = v[1] + o, axis = b - a;
            float length = axis.Length();
            axis = length > 0 ? axis / length * shape.Radius : new(shape.Radius, 0);
            var side = new Vector2(-axis.Y, axis.X);
            ring[0] = a - axis - side; ring[1] = b + axis - side; ring[2] = b + axis + side; ring[3] = a - axis + side;
            Polygon(ring[..4], light);
            return;
        }
        for (int i = 0; i < v.Length; i++) ring[i] = v[i] + o;
        Polygon(ring[..v.Length], light);
    }

    // A convex outline in either winding. A light inside it (no edge faces the light) casts nothing from it.
    private void Polygon(ReadOnlySpan<Vector2> p, Vector2 light)
    {
        var center = Vector2.Zero;
        foreach (var q in p) center += q;
        center /= p.Length;
        int start = edgeCount, facing = 0;
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 a = p[i], b = p[(i + 1) % p.Length], mid = (a + b) * .5f;
            var normal = new Vector2(b.Y - a.Y, a.X - b.X);
            if (Vector2.Dot(normal, mid - center) < 0) normal = -normal;
            if (Vector2.Dot(normal, light - mid) > 0) { facing++; continue; }
            Collect(a, b);
        }
        if (facing == 0) edgeCount = start;
    }

    private void Collect(Vector2 a, Vector2 b)
    {
        if (edgeCount == edges.Length) Array.Resize(ref edges, edges.Length * 2);
        edges[edgeCount++] = a.Y == b.Y ? new(0, a.Y, Math.Min(a.X, b.X), Math.Max(a.X, b.X), a, b)
            : a.X == b.X ? new(1, a.X, Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y), a, b) : new(2, 0, 0, 0, a, b);
    }

    internal void Finish(Vector2 light, float radius)
    {
        var span = edges.AsSpan(0, edgeCount);
        // Insertion sort: tile cells arrive almost in order, and Span.Sort with a comparison allocates a wrapper per call.
        for (int i = 1; i < span.Length; i++)
        {
            var e = span[i];
            int j = i - 1;
            for (; j >= 0 && Before(e, span[j]); j--) span[j + 1] = span[j];
            span[j + 1] = e;
        }
        static bool Before(in Segment x, in Segment y) => x.Axis != y.Axis ? x.Axis < y.Axis : x.Line != y.Line ? x.Line < y.Line : x.Start < y.Start;
        for (int i = 0; i < span.Length; i++)
        {
            var e = span[i];
            if (e.Axis == 2) { Edge(e.A, e.B, light, radius); continue; }
            float end = e.End;
            while (i + 1 < span.Length && span[i + 1].Axis == e.Axis && span[i + 1].Line == e.Line && span[i + 1].Start <= end)
                end = Math.Max(end, span[++i].End);
            if (e.Axis == 0) Edge(new(e.Start, e.Line), new(end, e.Line), light, radius);
            else Edge(new(e.Line, e.Start), new(e.Line, end), light, radius);
        }
    }

    private void Edge(Vector2 a, Vector2 b, Vector2 light, float radius)
    {
        Vector2 f = a - light, d = b - a;
        float A = Vector2.Dot(d, d), B = Vector2.Dot(f, d), C = Vector2.Dot(f, f) - radius * radius;
        float disc = B * B - A * C;
        if (A < 1e-8f || disc <= 0) return;
        float root = MathF.Sqrt(disc), t0 = Math.Max(0, (-B - root) / A), t1 = Math.Min(1, (-B + root) / A);
        if (t0 >= t1) return;
        float foot = -B / A;
        // Split where the edge is closest to the light so each part spans at most 90°, keeping its far side outside the circle.
        if (foot > t0 && foot < t1 && Vector2.Dot(f + d * t0, f + d * t1) < 0) { Quad(a + d * t0, a + d * foot, light, radius); Quad(a + d * foot, a + d * t1, light, radius); }
        else Quad(a + d * t0, a + d * t1, light, radius);
    }

    private void Quad(Vector2 a, Vector2 b, Vector2 light, float radius)
    {
        Vector2 da = a - light, db = b - light;
        float la = da.Length(), lb = db.Length();
        if (la < 1e-3f || lb < 1e-3f) return; // A light on the edge itself: the edge has no direction to cast along.
        if (Count + 4 > points.Length) Array.Resize(ref points, points.Length * 2);
        points[Count++] = a;
        points[Count++] = b;
        points[Count++] = light + db * (2 * radius / lb);
        points[Count++] = light + da * (2 * radius / la);
    }
}

public sealed partial class SceneGame
{
    // What a light's shadows were built from: a caster's pose and, for tile layers, its cells and streaming state.
    private readonly struct CasterKey(Body owner, object? cells, long loads, long unloads, int edits, int pose, Vector2 position, float angle)
    {
        private readonly Body owner = owner;
        private readonly object? cells = cells;
        private readonly long loads = loads, unloads = unloads;
        private readonly int edits = edits, pose = pose;
        private readonly Vector2 position = position;
        private readonly float angle = angle;
        public bool Same(in CasterKey o) => owner == o.owner && cells == o.cells && loads == o.loads && unloads == o.unloads &&
            edits == o.edits && pose == o.pose && position == o.position && angle == o.angle;
    }
    private sealed class CasterKeys
    {
        public CasterKey[] Items = new CasterKey[32];
        public int Count;
        public void Add(in CasterKey key)
        {
            if (Count == Items.Length) Array.Resize(ref Items, Count * 2);
            Items[Count++] = key;
        }
        public bool Same(CasterKeys other)
        {
            if (Count != other.Count) return false;
            for (int i = 0; i < Count; i++) if (!Items[i].Same(other.Items[i])) return false;
            return true;
        }
    }
    private sealed class LightCache
    {
        public readonly LightShadows Shadows = new();
        public CasterKeys Keys = new();
        public Vector2 Center = new(float.NaN);
        public float Radius;
    }

    private Body[]? lightingFor;
    private Body? globalLight;
    private GlobalLight2DComponent? globalConfig;
    private LightCache?[] lightCaches = [];
    private CasterKeys casterKeys = new();
    internal int ShadowRebuilds { get; private set; }
    // Casters, edges and quads over the cached shadows of every light (diagnostics for checks and benchmarks).
    internal (int Casters, int Edges, int Quads) ShadowTotals()
    {
        (int c, int e, int q) sum = default;
        foreach (var cache in lightCaches)
            if (cache != null) sum = (sum.c + cache.Shadows.Casters, sum.e + cache.Shadows.Edges, sum.q + cache.Shadows.Count / 4);
        return sum;
    }

    private GlobalLight2DComponent? GlobalLight()
    {
        if (!ReferenceEquals(lightingFor, bodies))
        {
            lightingFor = bodies;
            globalLight = null;
            foreach (var b in bodies)
                if (!b.Interface && b.Source.GetComponent<GlobalLight2DComponent>() != null) { globalLight = b; break; }
            globalConfig = globalLight?.Source.GetComponent<GlobalLight2DComponent>();
            lightCaches = new LightCache?[lights.Length];
        }
        return globalLight is { Active: true } ? globalConfig : null;
    }

    private void DrawLighting(ICanvas2D canvas, GlobalLight2DComponent global, RectangleF port, RectangleF frame, Matrix3x2 view,
        float interpolation, double effectTime)
    {
        canvas.BeginLighting(port, global.Ambient());
        for (int i = 0; i < lights.Length; i++)
        {
            var b = lights[i];
            if (!b.Active) continue;
            var light = b.Light!;
            var center = Affine2D.Transform(b.Size * .5f, b.MatrixAt(b.At(interpolation), b.AngleAt(interpolation)));
            if (!light.Shade(center, frame, effectTime, out var area, out var tint)) continue;
            canvas.DrawLight(lightSheet, area, view, tint, light.Shadows ? Shadows(i, b, center, light.Radius) : default, view);
        }
        canvas.EndLighting();
    }

    // Rebuilt only when the light moves or a caster in its square changes (pose, enabled, tiles or streamed pages).
    // shortcut: casters use their end-of-tick pose while sprites interpolate; a fast caster's shadow leads it by under a tick.
    private ReadOnlySpan<Vector2> Shadows(int index, Body owner, Vector2 center, float radius)
    {
        var cache = lightCaches[index] ??= new();
        var bounds = new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2);
        var keys = casterKeys;
        keys.Count = 0;
        foreach (var s in solids)
            if (Casts(s, owner, bounds, out _)) keys.Add(new(s, null, 0, 0, 0, s.PoseRevision, s.Position, s.Angle));
        foreach (var t in tileLayers)
            if (t.Active && t.Palette != null)
                keys.Add(new(t, t.Map!.CellVersion(), t.Stream?.State.Loads ?? -1, t.Stream?.State.Unloads ?? -1, t.Stream?.Edits ?? -1,
                    t.PoseRevision, t.Position, t.Angle));
        if (cache.Center == center && cache.Radius == radius && keys.Same(cache.Keys))
            return cache.Shadows.Quads;
        (cache.Keys, casterKeys) = (keys, cache.Keys);
        (cache.Center, cache.Radius) = (center, radius);
        ShadowRebuilds++;
        var shadows = cache.Shadows;
        shadows.Clear();
        foreach (var s in solids)
            if (Casts(s, owner, bounds, out var shape)) shadows.Add(shape, center, radius);
        foreach (var t in tileLayers)
        {
            if (!t.Active || t.Palette == null) continue;
            var map = t.Map!;
            var m = t.MatrixAt(t.Position);
            var (first, last) = map.CellRange(Vector2.Zero, Affine2D.Bounds(bounds, Affine2D.Inverse(m)));
            // shortcut: a light wider than 512×512 cells skips tile shadows; walk chunks (Tilemap2DComponent.InView) if one needs them.
            if ((long)(last.X - first.X + 1) * (last.Y - first.Y + 1) > 1 << 18) continue;
            for (int y = first.Y; y <= last.Y; y++)
                for (int x = first.X; x <= last.X; x++)
                {
                    var cell = new Point(x, y);
                    int id = map.Get(cell);
                    if (id != 0 && TileShape(t, cell, id, m) is {} tile && BoundsMeet(bounds, tile.Bounds)) shadows.Add(tile, center, radius);
                }
        }
        shadows.Finish(center, radius);
        return shadows.Quads;
    }

    // shortcut: a rotated collider rebuilds its world shape per call (as physics queries do), which allocates.
    private static bool Casts(Body s, Body owner, RectangleF bounds, out Shape2D shape)
    {
        shape = default;
        if (s == owner || !s.Active || s.Collider == null) return false;
        shape = s.ShapeAt(s.Position);
        return BoundsMeet(bounds, shape.Bounds);
    }
}
