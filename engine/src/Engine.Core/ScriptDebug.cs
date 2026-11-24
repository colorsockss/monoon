using System.Diagnostics;
using System.Drawing;
using System.Numerics;

namespace Engine.Core;

/// <summary>
/// Script debug drawing (roadmap 16.14): world-space lines, circles and text drawn over every Primary and Extra view.
/// The calls compile only where <see cref="Symbol"/> is defined (Scene Play and development-profile builds). Elsewhere
/// the C# compiler removes each call with its arguments, and distribution publishes set <see cref="Switch"/> to false
/// so trimming also drops the draw path: no code and no per-frame cost in shipped games.
/// </summary>
public readonly struct ScriptDebug
{
    public const string Symbol = "MONOON_DEBUG_DRAW";
    internal const string Switch = "MoNoon.ScriptDebugDraw";
    [System.Diagnostics.CodeAnalysis.FeatureSwitchDefinition(Switch)]
    internal static bool IsSupported => !AppContext.TryGetSwitch(Switch, out bool on) || on;

    private readonly SceneGame? game;
    private readonly bool ui;
    internal ScriptDebug(SceneGame? game, bool ui) => (this.game, this.ui) = (game, ui);

    /// <summary>A line between two world points, <paramref name="width"/> screen pixels wide.</summary>
    [Conditional(Symbol)]
    public void Line(Vector2 from, Vector2 to, Color color, float width = 1) => DebugShapes.Add(game, ui, new(0, from, to, width, color, null));
    /// <summary>A circle outline around a world point.</summary>
    [Conditional(Symbol)]
    public void Circle(Vector2 center, float radius, Color color, float width = 1) =>
        DebugShapes.Add(game, ui, new(1, center, new(radius, 0), width, color, null));
    /// <summary>Text whose top-left corner is at a world point, <paramref name="size"/> screen pixels high.</summary>
    [Conditional(Symbol)]
    public void Text(Vector2 position, string text, Color color, float size = 12) =>
        DebugShapes.Add(game, ui, new(2, position, default, size, color, text));
}

// Shapes from FixedUpdate show until the next game tick, shapes from UpdateUi until the next UI frame. While game time is
// paused the last tick's shapes stay on screen. Lists keep their capacity, so a steady number of calls allocates nothing.
internal static class DebugShapes
{
    internal readonly record struct Shape(byte Kind, Vector2 A, Vector2 B, float Size, Color Color, string? Text);
    internal const int Capacity = 4096;
    private static SceneGame? owner;
    private static readonly List<Shape> game = [], ui = [];
    private static long gameStamp = -1;
    private static double uiStamp = -1;

    internal static void Add(SceneGame? world, bool fromUi, in Shape shape)
    {
        if (world == null || !ScriptDebug.IsSupported) return;
        if (owner != world) { owner = world; game.Clear(); ui.Clear(); gameStamp = -1; uiStamp = -1; }
        var list = fromUi ? ui : game;
        if (fromUi ? uiStamp != world.Time.UiSeconds : gameStamp != world.Time.TickCount)
        {
            list.Clear();
            if (fromUi) uiStamp = world.Time.UiSeconds; else gameStamp = world.Time.TickCount;
        }
        if (list.Count < Capacity && float.IsFinite(shape.A.X + shape.A.Y + shape.B.X + shape.B.Y + shape.Size)) list.Add(shape);
    }

    internal static int Count(SceneGame world) => owner != world ? 0 :
        (gameStamp == world.Time.TickCount ? game.Count : 0) + (uiStamp == world.Time.UiSeconds ? ui.Count : 0);

    internal static void Draw(SceneGame world, ICanvas2D canvas, CameraView shot)
    {
        if (owner != world) return;
        if (gameStamp == world.Time.TickCount) Draw(canvas, shot, game);
        if (uiStamp == world.Time.UiSeconds) Draw(canvas, shot, ui);
    }

    private const int Segments = 32;
    private static readonly Color[] ringColors = new Color[Segments + 1];

    private static void Draw(ICanvas2D canvas, CameraView shot, List<Shape> shapes)
    {
        Span<Vector2> edges = stackalloc Vector2[(Segments + 1) * 2];
        var colors = ringColors; // Color holds a reference (its name), so it cannot live on the stack.
        foreach (var s in shapes)
        {
            var a = shot.WorldToScreen(s.A);
            if (s.Kind == 2) { canvas.DrawText(s.Text!, a.X, a.Y, s.Size, s.Color); continue; }
            float half = Math.Max(s.Size, .5f) * .5f;
            if (s.Kind == 0)
            {
                var b = shot.WorldToScreen(s.B);
                var along = b - a;
                if (along.LengthSquared() < 1e-6f) continue;
                var side = new Vector2(-along.Y, along.X) * (half / along.Length());
                edges[0] = a + side; edges[1] = a - side; edges[2] = b + side; edges[3] = b - side;
                colors[0] = colors[1] = s.Color;
                canvas.FillStrip(edges[..4], colors.AsSpan(0, 2), Matrix3x2.Identity);
                continue;
            }
            float radius = s.B.X * shot.Zoom;
            if (!(radius > 0)) continue;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * (MathF.Tau / Segments);
                var unit = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                edges[i * 2] = a + unit * (radius + half);
                edges[i * 2 + 1] = a + unit * Math.Max(0, radius - half);
                colors[i] = s.Color;
            }
            canvas.FillStrip(edges, colors, Matrix3x2.Identity);
        }
    }
}
