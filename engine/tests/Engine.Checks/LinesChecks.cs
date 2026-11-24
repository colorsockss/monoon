using System.Drawing;
using System.Numerics;
using Engine.Core;

internal static class LinesChecks
{
    [Check(4800, "lines")]
    static void Registered(CheckRun c) { Run(c.Scratch); RunPaths(c.Repository, c.Scratch); }
    private const float Step = 1f / 60;
    private static void Check(bool value, string why)
    {
        if (!value) throw new Exception("Lines and paths: " + why);
        Console.WriteLine("PASS lines and paths: " + why);
    }

    private sealed class Strips : ICanvas2D
    {
        public readonly List<(Vector2[] Edges, Color[] Colors)> Drawn = [];
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF rectangle, Color color) { }
        public bool DrawSprite(string path, RectangleF destination) => true;
        public bool DrawAffine(string? path, RectangleF r, Matrix3x2 matrix, Color tint, SpriteRegion? source = null, string layout = TileGrid.Orthogonal) => true;
        public void FillStrip(ReadOnlySpan<Vector2> edges, ReadOnlySpan<Color> colors, Matrix3x2 matrix)
        {
            var world = new Vector2[edges.Length];
            for (int i = 0; i < world.Length; i++) world[i] = Vector2.Transform(edges[i], matrix);
            Drawn.Add((world, colors.ToArray()));
        }
    }

    internal static void Run(string scratch)
    {
        // YAML: optional tangent handles, defaults and the schema version that marks the new components.
        var scene = SceneDocument.Empty(new());
        var beam = new SceneObject { Name = "Beam", X = 10, Y = 20, Width = 4, Height = 4 };
        beam.AddComponent(new Line2DComponent { Points = [new(0, 0), new(60, 0), new(60, 80, 0, -20, 0, 20)], Width = 8, StartColor = "#FF0000", EndColor = "#0000FF" });
        var comet = new SceneObject { Name = "Comet", X = 0, Y = 200, Width = 10, Height = 10 };
        comet.AddComponent(new Trail2DComponent { Time = .25f, MaxPoints = 8, MinStep = 1, StartColor = "#FFFFFF", EndColor = "#00FF00" });
        comet.AddComponent(new PathFollower2DComponent { Points = [new(0, 0), new(300, 0)], Speed = 240, Repeat = PathRepeat.PingPong });
        var lift = new SceneObject { Name = "Lift", X = 100, Y = 300, Width = 60, Height = 10 };
        lift.AddComponent(new BoxCollider2DComponent());
        lift.AddComponent(new Rigidbody2DComponent { BodyType = BodyType2D.Kinematic });
        // A quarter circle followed by a straight run: constant speed must hold on the curve and across the join.
        lift.AddComponent(new PathFollower2DComponent { Points = [new(0, 0, 0, 0, 0, -55.23f), new(100, -100, -55.23f, 0, 55.23f, 0), new(220, -100)], Speed = 90, Repeat = PathRepeat.PingPong });
        var rider = new SceneObject { Name = "Rider", X = 120, Y = 270, Width = 16, Height = 30 };
        rider.AddComponent(new BoxCollider2DComponent());
        rider.AddComponent(new Rigidbody2DComponent { FreezeRotation = true });
        rider.AddComponent(new CharacterMotor2DComponent());
        scene.Objects.AddRange([beam, comet, lift, rider]);
        var file = Path.Combine(Directory.CreateDirectory(Path.Combine(scratch, "LinesPaths")).FullName, "Lines.scene");
        scene.Save(file);
        var text = File.ReadAllText(file);
        var reopened = SceneDocument.Load(file);
        Check(reopened.SchemaVersion == 30 && text.Contains("type: line2D") && text.Contains("type: pathFollower2D") &&
              reopened.Objects.Single(o => o.Id == beam.Id).GetComponent<Line2DComponent>()!.Points.SequenceEqual(beam.GetComponent<Line2DComponent>()!.Points) &&
              reopened.Objects.Single(o => o.Id == comet.Id).GetComponent<Trail2DComponent>() == comet.GetComponent<Trail2DComponent>(),
            "line, trail and path follower round-trip through YAML as scene version 30");
        var invalid = SceneDocument.Load(file);
        invalid.Objects.Single(o => o.Id == lift.Id).AddComponent(new PositionTween2DComponent());
        Check(Throws(() => invalid.Save(file + ".bad.scene")), "a path follower refuses a second position driver (tween)");

        using var game = new SceneGame(reopened);
        game.FixedUpdate(Vector2.Zero, Step);

        // Line: width across the ribbon, colour from start to end along the flattened length.
        var canvas = new Strips();
        game.Draw(canvas, 1);
        var line = canvas.Drawn.Single(s => s.Colors[0] == Color.FromArgb(255, 0, 0));
        Check(line.Colors[^1] == Color.FromArgb(0, 0, 255) && line.Edges.Length == line.Colors.Length * 2 && line.Colors.Length == 2 + 24 &&
              Math.Abs(Vector2.Distance(line.Edges[0], line.Edges[1]) - 8) < 1e-3f && line.Edges[0] == new Vector2(10, 24) && line.Edges[1] == new Vector2(10, 16),
            "a curved line is a mitred ribbon of its flattened points with a start-to-end gradient");
        var points = new List<Vector2>();
        Check(game.SetLinePoints(beam.Id, [new(10, 20), new(110, 20), new(110, 120)]) && game.TryGetLinePoints(beam.Id, points) &&
              points.SequenceEqual([new Vector2(10, 20), new(110, 20), new(110, 120)]), "scripts replace a line's points in world space and read them back");

        // Path follower: equal arc length per tick (chord within 0.1 %), so speed is constant on curves too.
        var deck = new List<Vector2>();
        var carried = new List<float>();
        for (int i = 0; i < 260; i++)
        {
            game.FixedUpdate(Vector2.Zero, Step);
            game.TryGetPosition(lift.Id, out var at);
            game.TryGetPosition(rider.Id, out var on);
            deck.Add(at);
            if (i > 20) carried.Add(Vector2.Distance(on - at, new Vector2(20, -30)));
        }
        var steps = deck.Zip(deck.Skip(1), Vector2.Distance).Take(150).ToArray();
        Check(steps.All(s => Math.Abs(s - 1.5f) < 1.5f * .001f), $"constant speed along the curve: {steps.Min():F5}–{steps.Max():F5} px per tick (1.5 expected)");
        Check(carried.Max() < .05f, $"the kinematic path platform carries a character motor (largest slip {carried.Max():F4} px)");

        // Trail: game-time samples, capped, cleared while inactive, identical in two runs.
        var trail = new List<Vector2>();
        game.TryGetTrailPoints(comet.Id, trail);
        Check(trail.Count is > 2 and <= 8 && trail.Zip(trail.Skip(1)).All(p => p.Second.X > p.First.X || p.Second.X < p.First.X),
            $"the trail keeps at most its point budget ({trail.Count} of 8) inside its lifetime");
        canvas.Drawn.Clear();
        game.Draw(canvas, 1);
        var ribbon = canvas.Drawn.Single(s => s.Colors[^1].A == 255 && s.Colors[^1].G == 255 && s.Colors[^1].R == 255);
        Check(ribbon.Colors[0].A < 255 && ribbon.Colors[0].A < ribbon.Colors[^1].A, "the trail fades from its head to its tail");
        using (var again = new SceneGame(SceneDocument.Load(file)))
        {
            for (int i = 0; i < 261; i++) again.FixedUpdate(Vector2.Zero, Step);
            var second = new List<Vector2>();
            again.TryGetTrailPoints(comet.Id, second);
            Check(second.SequenceEqual(trail), "the same ticks leave the same trail points");
            again.SetActive(comet.Id, false);
            again.FixedUpdate(Vector2.Zero, Step);
            again.TryGetTrailPoints(comet.Id, second);
            Check(second.Count == 0, "a deactivated object drops its trail");
        }
    }

    // PlatformerLab Paths: walk onto the lift, stand while it follows its curve over the pillar, walk off at the goal.
    internal const int Walk = 10, Stand = 86, Leave = 346, Ticks = 400;
    internal static void RunPaths(string repository, string scratch)
    {
        var scene = SceneDocument.Load(Path.Combine(repository, "samples", "PlatformerLab", "Scenes", "Paths.scene"));
        var runner = scene.Objects.Single(o => o.Name == "Runner").Id;
        var lift = scene.Objects.Single(o => o.Name == "Path lift").Id;
        var storage = new PlayerStorage(Path.Combine(scratch, "paths-profile"));
        using var session = new GameSession(scene, null, null, storage);
        var trace = new List<string>();
        for (int i = 0; i < Ticks; i++)
        {
            if (i == Walk || i == Leave) session.Actions.Press("Right");
            if (i == Stand) session.Actions.Release("Right");
            session.FixedUpdate(session.Actions.Take(), Step);
            session.Current.TryGetPosition(runner, out var r);
            session.Current.TryGetPosition(lift, out var l);
            session.Current.TryGetMotion(runner, out var m);
            trace.Add($"{i} runner {r} lift {l} support {(m.Support == lift ? "lift" : m.Support == null ? "-" : "other")}");
        }
        if (Environment.GetEnvironmentVariable("MONOON_PATHS_TRACE") == "1") trace.ForEach(Console.WriteLine);
        var data = storage.LoadSlot(1, 1);
        Check(data?.GetBool("rode") == true, $"PlatformerLab Paths: the runner rides the path lift over the pillar to the goal ({data?.GetNumber("ticks")} ticks)");
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (InvalidDataException) { return true; }
    }
}
