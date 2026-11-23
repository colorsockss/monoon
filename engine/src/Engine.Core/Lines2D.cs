using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using VYaml.Annotations;

namespace Engine.Core;

// One node of a path. In and Out are tangent handles relative to the point; both zero make a sharp corner.
[YamlObject]
public readonly partial record struct PathPoint(float X, float Y, float InX = 0, float InY = 0, float OutX = 0, float OutY = 0);

[DisplayName("Line 2D"), YamlObject]
public sealed partial record Line2DComponent : SceneComponent
{
    private PathPoint[] points = [new(0, 0), new(100, 0)];
    // Local coordinates of the owner. Width is in world units and the colour blends from start to end along the length.
    public PathPoint[] Points { get => (PathPoint[])points.Clone(); set => points = value is null ? [] : (PathPoint[])value.Clone(); }
    public float Width { get; set; } = 4;
    public string StartColor { get; set; } = "#FFFFFF";
    public string EndColor { get; set; } = "#FFFFFF";
    public int SortingLayer { get; set; }
    public int Order { get; set; }

    internal void Validate(string name)
    {
        if (points.Length > LinePaths.MaxPoints || !LinePaths.Finite(points) || Width is not (> 0 and <= 1024) ||
            !SceneObject.TryColor(StartColor, out _) || !SceneObject.TryColor(EndColor, out _))
            throw new InvalidDataException($"{name}: a line needs at most {LinePaths.MaxPoints} finite points, width 0–1024 and #RRGGBB colours.");
    }
}

[DisplayName("Trail 2D"), YamlObject]
public sealed partial record Trail2DComponent : SceneComponent
{
    // A point is recorded at the owner's centre on each game tick it has moved Min step; it fades out over Time.
    public float Time { get; set; } = .4f;
    public float Width { get; set; } = 6;
    public string StartColor { get; set; } = "#FFFFFF";
    public string EndColor { get; set; } = "#FFFFFF";
    public int MaxPoints { get; set; } = 32;
    public float MinStep { get; set; } = 2;
    public int SortingLayer { get; set; }
    public int Order { get; set; }

    internal void Validate(string name)
    {
        if (Time is not (> 0 and <= 10) || Width is not (> 0 and <= 256) || MaxPoints is < 2 or > 256 ||
            MinStep is not (>= 0 and <= 4096) || !SceneObject.TryColor(StartColor, out _) || !SceneObject.TryColor(EndColor, out _))
            throw new InvalidDataException($"{name}: a trail needs time 0–10 s, width 0–256, 2–256 points, min step 0–4096 and #RRGGBB colours.");
    }
}

public enum PathRepeat { Once, PingPong, Loop }

[DisplayName("Path Follower 2D"), YamlObject]
public sealed partial record PathFollower2DComponent : SceneComponent
{
    private PathPoint[] points = [new(0, 0), new(100, 0)];
    // Offsets from the owner's start position in its parent's space. The first point is where the owner starts;
    // Loop closes the path back to it.
    public PathPoint[] Points { get => (PathPoint[])points.Clone(); set => points = value is null ? [] : (PathPoint[])value.Clone(); }
    public float Speed { get; set; } = 60;
    public PathRepeat Repeat { get; set; } = PathRepeat.PingPong;
    public bool PlayOnStart { get; set; } = true;

    internal void Validate(SceneObject owner)
    {
        if (points.Length is < 2 or > LinePaths.MaxPoints || !LinePaths.Finite(points) || Speed is not (> 0 and <= 100000) ||
            !Enum.IsDefined(Repeat) || owner.GetComponent<PositionTween2DComponent>() != null ||
            owner.GetComponent<Rigidbody2DComponent>() is { BodyType: not BodyType2D.Kinematic })
            throw new InvalidDataException($"{owner.Name}: a path follower needs 2–{LinePaths.MaxPoints} finite points, speed 0–100000, " +
                "a transform or kinematic body and no position tween.");
    }
}

internal static class LinePaths
{
    public const int MaxPoints = 1024;
    internal static bool Finite(PathPoint[] points) => points.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y) &&
        float.IsFinite(p.InX) && float.IsFinite(p.InY) && float.IsFinite(p.OutX) && float.IsFinite(p.OutY));

    internal static Color Mix(Color a, Color b, float t, int alpha = 255)
    {
        static int Channel(int x, int y, float t) => (int)MathF.Round(x + (y - x) * t);
        return Color.FromArgb(alpha, Channel(a.R, b.R, t), Channel(a.G, b.G, t), Channel(a.B, b.B, t));
    }
}

// A path flattened into a polyline with cumulative lengths. Positions are looked up by arc length, so a follower that
// travels the same distance each tick moves at constant speed along curves too.
internal sealed class CurvePath
{
    private const int CurveSteps = 24;
    private readonly List<Vector2> points = [];
    private readonly List<float> lengths = [];
    public ReadOnlySpan<Vector2> Points => CollectionsMarshal.AsSpan(points);
    public float Length => lengths.Count == 0 ? 0 : lengths[^1];
    public float LengthAt(int index) => lengths[index];

    public CurvePath Build(ReadOnlySpan<PathPoint> nodes, bool closed)
    {
        points.Clear();
        lengths.Clear();
        if (nodes.Length == 0) return this;
        Add(new(nodes[0].X, nodes[0].Y));
        int segments = closed && nodes.Length > 2 ? nodes.Length : nodes.Length - 1;
        for (int s = 0; s < segments; s++)
        {
            var a = nodes[s];
            var b = nodes[(s + 1) % nodes.Length];
            Vector2 p0 = new(a.X, a.Y), p3 = new(b.X, b.Y), p1 = p0 + new Vector2(a.OutX, a.OutY), p2 = p3 + new Vector2(b.InX, b.InY);
            int steps = p1 == p0 && p2 == p3 ? 1 : CurveSteps;
            for (int k = 1; k <= steps; k++)
            {
                float t = k / (float)steps, u = 1 - t;
                Add(u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3);
            }
        }
        return this;
    }

    private void Add(Vector2 point)
    {
        if (points.Count > 0 && point == points[^1]) return;
        lengths.Add(points.Count == 0 ? 0 : lengths[^1] + Vector2.Distance(points[^1], point));
        points.Add(point);
    }

    public Vector2 At(float distance)
    {
        if (points.Count == 0) return default;
        var l = CollectionsMarshal.AsSpan(lengths);
        if (distance <= 0) return points[0];
        if (distance >= l[^1]) return points[^1];
        int i = l.BinarySearch(distance);
        if (i >= 0) return points[i];
        i = ~i;
        return Affine2D.Lerp(points[i - 1], points[i], (distance - l[i - 1]) / (l[i] - l[i - 1]));
    }
}

// Turns a polyline into the left/right edge pairs of a ribbon. Corners are mitred (at most 4× the half-width).
internal sealed class LineStrip
{
    private Vector2[] edges = new Vector2[64];

    public void Draw(ICanvas2D canvas, ReadOnlySpan<Vector2> points, ReadOnlySpan<float> halfWidths, ReadOnlySpan<Color> colors, Matrix3x2 view)
    {
        int n = points.Length;
        if (n < 2) return;
        if (edges.Length < n * 2) edges = new Vector2[n * 4];
        for (int i = 0; i < n; i++)
        {
            Vector2 before = i > 0 ? Vector2.Normalize(points[i] - points[i - 1]) : default;
            Vector2 after = i < n - 1 ? Vector2.Normalize(points[i + 1] - points[i]) : default;
            var tangent = before + after;
            tangent = tangent.LengthSquared() < 1e-8f ? (i > 0 ? before : after) : Vector2.Normalize(tangent);
            var normal = new Vector2(-tangent.Y, tangent.X);
            float scale = i > 0 && i < n - 1 ? 1 / Math.Max(Vector2.Dot(normal, new Vector2(-after.Y, after.X)), .25f) : 1;
            var offset = normal * (halfWidths[halfWidths.Length == 1 ? 0 : i] * scale);
            edges[i * 2] = points[i] + offset;
            edges[i * 2 + 1] = points[i] - offset;
        }
        canvas.FillStrip(edges.AsSpan(0, n * 2), colors[..n], view);
    }
}

internal sealed class LineRenderer(Line2DComponent config)
{
    public PathPoint[] Points = config.Points;
    private readonly CurvePath path = new();
    private readonly LineStrip strip = new();
    private Vector2[] world = [];
    private Color[] colors = [];

    public void Draw(ICanvas2D canvas, Matrix3x2 matrix, Matrix3x2 view)
    {
        var local = path.Build(Points, false).Points;
        int n = local.Length;
        if (n < 2) return;
        if (world.Length < n) { world = new Vector2[n * 2]; colors = new Color[n * 2]; }
        SceneObject.TryColor(config.StartColor, out var start);
        SceneObject.TryColor(config.EndColor, out var end);
        for (int i = 0; i < n; i++)
        {
            world[i] = Affine2D.Transform(local[i], matrix);
            colors[i] = LinePaths.Mix(start, end, path.LengthAt(i) / path.Length);
        }
        strip.Draw(canvas, world.AsSpan(0, n), [config.Width * .5f], colors.AsSpan(0, n), view);
    }

    public void CopyTo(Matrix3x2 matrix, List<Vector2> points)
    {
        points.Clear();
        foreach (var p in path.Build(Points, false).Points) points.Add(Affine2D.Transform(p, matrix));
    }
}

// World positions sampled on game ticks, so the same replay leaves the same trail at any frame rate.
internal sealed class TrailRenderer(Trail2DComponent config)
{
    private readonly Vector2[] points = new Vector2[config.MaxPoints];
    private readonly double[] times = new double[config.MaxPoints];
    private readonly Vector2[] drawn = new Vector2[config.MaxPoints + 1];
    private readonly float[] widths = new float[config.MaxPoints + 1];
    private readonly Color[] colors = new Color[config.MaxPoints + 1];
    private readonly LineStrip strip = new();
    private int first, count;

    public void Clear() => count = 0;

    public void Step(Vector2 position, double now)
    {
        while (count > 0 && now - times[first] >= config.Time) { first = (first + 1) % points.Length; count--; }
        if (count > 0 && Vector2.Distance(points[(first + count - 1) % points.Length], position) < config.MinStep) return;
        if (count == points.Length) { first = (first + 1) % points.Length; count--; }
        int at = (first + count) % points.Length;
        points[at] = position;
        times[at] = now;
        count++;
    }

    public void CopyTo(List<Vector2> into)
    {
        into.Clear();
        for (int k = 0; k < count; k++) into.Add(points[(first + k) % points.Length]);
    }

    public void Draw(ICanvas2D canvas, Vector2 head, double now, Matrix3x2 view)
    {
        SceneObject.TryColor(config.StartColor, out var start);
        SceneObject.TryColor(config.EndColor, out var end);
        int n = 0;
        void Add(Vector2 p, float life)
        {
            if (n > 0 && p == drawn[n - 1]) return;
            drawn[n] = p;
            widths[n] = config.Width * .5f * life;
            colors[n++] = LinePaths.Mix(end, start, life, (int)MathF.Round(255 * life));
        }
        for (int k = 0; k < count; k++)
        {
            int i = (first + k) % points.Length;
            double age = now - times[i];
            // Between ticks the head is interpolated behind the newest sample; drawing that sample would fold the ribbon back.
            if (age >= 0 && age < config.Time) Add(points[i], (float)(1 - age / config.Time));
        }
        Add(head, 1);
        strip.Draw(canvas, drawn.AsSpan(0, n), widths.AsSpan(0, n), colors.AsSpan(0, n), view);
    }
}

internal sealed class PathMotion(PathFollower2DComponent config)
{
    private readonly CurvePath path = new CurvePath().Build(config.Points, config.Repeat == PathRepeat.Loop);
    private Vector2? origin;
    private double travelled;
    public bool Playing { get; set; } = config.PlayOnStart;

    public Vector2 Advance(Vector2 position, Matrix3x2 parent, float dt)
    {
        if (!Playing || path.Length == 0) return default;
        origin ??= Affine2D.Transform(position, Affine2D.Inverse(parent)) - path.At(0);
        travelled += (double)config.Speed * dt;
        double length = path.Length, distance = config.Repeat switch
        {
            PathRepeat.Loop => travelled % length,
            PathRepeat.PingPong => length - Math.Abs(length - travelled % (2 * length)),
            _ => Math.Min(travelled, length),
        };
        if (config.Repeat == PathRepeat.Once && distance >= length) Playing = false;
        return Affine2D.Transform(origin.Value + path.At((float)distance), parent) - position;
    }
}

public sealed partial class SceneGame
{
    private Body[] trails = [];

    // World points replace the line's local points; curves are flattened when read back.
    public bool SetLinePoints(Guid id, ReadOnlySpan<Vector2> worldPoints)
    {
        if (removed.Contains(id) || !byId.TryGetValue(id, out var b) || b.Line == null || worldPoints.Length > LinePaths.MaxPoints) return false;
        var inverse = Affine2D.Inverse(b.MatrixAt(b.Position));
        var local = new PathPoint[worldPoints.Length];
        for (int i = 0; i < local.Length; i++)
        {
            var p = Affine2D.Transform(worldPoints[i], inverse);
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y)) return false;
            local[i] = new(p.X, p.Y);
        }
        b.Line.Points = local;
        return true;
    }

    public bool TryGetLinePoints(Guid id, List<Vector2> worldPoints)
    {
        if (removed.Contains(id) || !byId.TryGetValue(id, out var b) || b.Line == null) return false;
        b.Line.CopyTo(b.MatrixAt(b.Position), worldPoints);
        return true;
    }

    public bool TryGetTrailPoints(Guid id, List<Vector2> worldPoints)
    {
        if (removed.Contains(id) || !byId.TryGetValue(id, out var b) || b.Trail == null) return false;
        b.Trail.CopyTo(worldPoints);
        return true;
    }

    private static Vector2 TrailCenter(Body b, Matrix3x2 matrix) => Affine2D.Transform(b.Size * .5f, matrix);

    private void StepTrails()
    {
        foreach (var b in trails)
            if (b.Active) b.Trail!.Step(TrailCenter(b, b.MatrixAt(b.Position)), Time.GameSeconds);
            else b.Trail!.Clear();
    }
}
