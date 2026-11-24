#define MONOON_DEBUG_DRAW
using System.Drawing;
using System.Numerics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Engine.Core;

// 16.14: tick.Debug draws in Scene Play (and development builds); other compilations drop the calls with their arguments.
internal static class ScriptDebugChecks
{
    [Check(6600, "script-debug")] static void Registered(CheckRun c) => Run(c.Scratch);
    private static void Check(bool ok, string why) { if (!ok) throw new Exception("Script debug: " + why); }

    internal static void Run(string scratch)
    {
        string root = Directory.CreateDirectory(Path.Combine(scratch, "Debug Draw Project")).FullName;
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(root, "Scripts")).FullName, "Gizmo.cs"), """
            using System.Drawing;
            using System.Numerics;
            using Engine.Core;
            public sealed class Gizmo : GameBehaviour
            {
                public int Evaluated;
                private int Count() => ++Evaluated;
                public override void FixedUpdate(ScriptTick tick)
                {
                    if (tick.Session.Time.TickCount % 2 == 0) return;
                    tick.Debug.Line(new Vector2(10, 10), new Vector2(110, 10), Color.Red, Count());
                    tick.Debug.Circle(new Vector2(50, 50), 20, Color.Lime);
                    tick.Debug.Text(new Vector2(5, 80), "tick", Color.White);
                }
                public override void UpdateUi(UiTick tick) => tick.Debug.Text(new Vector2(5, 100), "ui", Color.White);
            }
            """);
        var scene = SceneDocument.Empty(new()); scene.ProjectDirectory = root;
        var obj = new SceneObject { Name = "Gizmo" };
        obj.AddComponent(new ScriptComponent { Script = "Gizmo" });
        scene.Objects.Add(obj);

        var play = ProjectScripts.ForScenePlay(root);
        Check(References(play.Current().Bytes, "Line") && References(play.Current().Bytes, "Text"), "Scene Play keeps the calls");
        var shipped = new ProjectScripts(root);
        Check(!References(shipped.Current().Bytes, "get_Debug") && !References(shipped.Current().Bytes, "Circle"),
            "a compilation without " + ScriptDebug.Symbol + " has no ScriptDebug reference");

        using (var session = new GameSession(scene, play))
        {
            var g = session.Current;
            var canvas = new Capture();
            session.FixedUpdate(default(GameInput), 1f / 60);
            g.Draw(canvas, 1);
            Check(canvas.Strips == 2 && canvas.Texts.SequenceEqual(["tick", "ui"]), $"tick 1 draws a line, a circle and two texts ({canvas.Strips}, {string.Join(",", canvas.Texts)})");
            Check(canvas.FirstStrip is [var a, var b, var c, var d] && Near(a, new(10, 10.5f)) && Near(b, new(10, 9.5f)) && Near(c, new(110, 10.5f)) && Near(d, new(110, 9.5f)),
                "line width 1 in screen pixels at the default camera");
            session.FixedUpdate(default(GameInput), 1f / 60);
            canvas = new(); g.Draw(canvas, 1);
            Check(canvas.Strips == 0 && canvas.Texts.SequenceEqual(["ui"]), "game shapes last one tick, UI shapes one UI frame");
            session.FixedUpdate(default(GameInput), 1f / 60);
            g.Time.IsPaused = true;
            session.UpdateUi(default, 1f / 60); session.UpdateUi(default, 1f / 60);
            canvas = new(); g.Draw(canvas, 1);
            Check(canvas.Strips == 2 && canvas.Texts.Count == 2, "paused game keeps the last tick's shapes");
            Check((int)g.GetComponent<GameBehaviour>(obj.Id)!.GetType().GetField("Evaluated")!.GetValue(g.GetComponent<GameBehaviour>(obj.Id))! == 2,
                "Scene Play evaluates the arguments");

            // Steady drawing allocates nothing once the lists have grown.
            var debug = new ScriptDebug(g, true);
            Check(g.TryGetView(g.ActiveCameraId!.Value, out var shot), "camera view");
            g.Time.AdvanceUi(1.0 / 60);
            for (int i = 0; i < 20; i++) debug.Circle(new(i, i), 4, Color.Gold);
            DebugShapes.Draw(g, canvas, shot);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 100; frame++)
            {
                g.Time.AdvanceUi(1.0 / 60);
                for (int i = 0; i < 10; i++) { debug.Circle(new(i, i), 4, Color.Gold); debug.Line(default, new(i, 9), Color.Gold); }
                DebugShapes.Draw(g, canvas, shot);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0, $"no allocation per frame ({allocated} bytes)");
            int drawn = DebugShapes.Count(g);
            debug.Line(default, new(float.NaN, 0), Color.Gold);
            Check(DebugShapes.Count(g) == drawn, "a non-finite point is ignored");
            for (int i = 0; i < DebugShapes.Capacity + 10; i++) debug.Line(default, Vector2.One, Color.Gold);
            Check(DebugShapes.Count(g) == DebugShapes.Capacity + 3, "capacity caps a runaway loop (the tick's three shapes stay)");
        }
        using (var session = new GameSession(scene, shipped))
        {
            var canvas = new Capture();
            session.FixedUpdate(default(GameInput), 1f / 60);
            session.Current.Draw(canvas, 1);
            var live = session.Current.GetComponent<GameBehaviour>(obj.Id)!;
            Check(canvas.Strips == 0 && canvas.Texts.Count == 0 && (int)live.GetType().GetField("Evaluated")!.GetValue(live)! == 0,
                "without the symbol nothing is drawn and the arguments are not evaluated");
        }
        Console.WriteLine("PASS script debug drawing: Scene Play lines/circles/text, one-tick and one-UI-frame lifetime, pause, no allocation, calls compiled out elsewhere");
    }

    private static bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .001f;

    private static bool References(byte[] assembly, string member)
    {
        using var pe = new PEReader(new MemoryStream(assembly));
        var md = pe.GetMetadataReader();
        foreach (var handle in md.MemberReferences)
        {
            var reference = md.GetMemberReference(handle);
            if (md.GetString(reference.Name) != member) continue;
            if (member == "get_Debug" || reference.Parent.Kind == HandleKind.TypeReference &&
                md.GetString(md.GetTypeReference((TypeReferenceHandle)reference.Parent).Name) == nameof(ScriptDebug)) return true;
        }
        return false;
    }

    private sealed class Capture : ICanvas2D
    {
        public int Strips;
        public Vector2[]? FirstStrip;
        public readonly List<string> Texts = new(8);
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF rectangle, Color color) { }
        public bool DrawSprite(string path, RectangleF destination) => true;
        public void FillStrip(ReadOnlySpan<Vector2> edges, ReadOnlySpan<Color> colors, Matrix3x2 matrix) { if (Strips++ == 0 && FirstStrip == null) FirstStrip = edges.ToArray(); }
        public void DrawText(string text, float x, float y, float size, Color color) { if (Texts.Count < 8) Texts.Add(text); }
    }
}
