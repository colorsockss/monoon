using System.Numerics;
using Engine.Core;

// 16.11: dead zone, look-ahead and object-group framing with zoom.
internal static class CameraFramingChecks
{
    [Check(4600, "camera-framing")] static void Registered(CheckRun c) => Run();
    private static void Check(bool ok, string why) { if (!ok) throw new Exception("Camera framing: " + why); }
    private static void Near(float a, float b, string why) => Check(Math.Abs(a - b) < .002f, $"{why}: {a} vs {b}");
    private const float Tick = 1f / 60;

    private static (SceneDocument Scene, SceneObject Camera, Camera2DComponent Lens, SceneObject Target) Scene()
    {
        var scene = SceneDocument.Empty(new());
        var target = new SceneObject { Name = "Target", X = -5, Y = -5, Width = 10, Height = 10 };
        scene.Objects.Add(target);
        var lens = scene.Camera!.GetComponent<Camera2DComponent>()!;
        lens.Follow = target.Id; scene.Camera.X = 0; scene.Camera.Y = 0;
        return (scene, scene.Camera, lens, target);
    }
    private static CameraView View(SceneGame g, Guid id) => g.TryGetView(id, out var v) ? v : throw new Exception("Camera framing: missing view");
    private static void Step(SceneGame g, Guid id, Vector2 move) { Check(g.TryMoveStep(id, move), "move"); g.FixedUpdate(Vector2.Zero, Tick); }

    internal static void Run()
    {
        var (scene, camera, lens, target) = Scene();
        scene.Validate(); Check(scene.SchemaVersion < 33, "plain follow keeps the older schema");
        lens.DeadZoneWidth = 100; lens.DeadZoneHeight = 60;
        using (var g = new SceneGame(scene))
        {
            g.FixedUpdate(Vector2.Zero, Tick);
            Step(g, target.Id, new(30, 20));
            Near(View(g, camera.Id).Center.X, 0, "inside the dead zone X holds"); Near(View(g, camera.Id).Center.Y, 0, "inside the dead zone Y holds");
            Step(g, target.Id, new(40, 20));
            Near(View(g, camera.Id).Center.X, 20, "past the zone edge the camera keeps the target on it (70 - 50)");
            Near(View(g, camera.Id).Center.Y, 10, "Y edge (40 - 30)");
            Step(g, target.Id, new(-50, 0));
            Near(View(g, camera.Id).Center.X, 20, "walking back inside the zone does not move the camera");
        }

        (scene, camera, lens, target) = Scene();
        lens.LookAhead = .5f;
        using (var g = new SceneGame(scene))
        {
            g.FixedUpdate(Vector2.Zero, Tick);
            for (int i = 0; i < 10; i++) Step(g, target.Id, new(2, 0));
            Near(View(g, camera.Id).Center.X, 20 + 2 * 60 * .5f, "look-ahead leads by velocity × seconds (2 px/tick, 0.5 s)");
            Step(g, target.Id, Vector2.Zero);
            Near(View(g, camera.Id).Center.X, 20, "stopping removes the lead");
            g.SetActive(target.Id, false); g.FixedUpdate(Vector2.Zero, Tick);
            g.SetActive(target.Id, true); Step(g, target.Id, new(400, 0));
            Near(View(g, camera.Id).Center.X, 420, "a target that comes back starts without a velocity spike");
        }

        (scene, camera, lens, target) = Scene();
        lens.Follow = null; lens.FrameGroup = "Party"; lens.FramePadding = 20;
        target.Groups = ["Party"];
        var far = new SceneObject { Name = "Far", X = 995, Y = -5, Width = 10, Height = 10, Groups = ["Party"] };
        scene.Objects.Add(far);
        scene.Validate();
        Check(scene.SchemaVersion == 35, $"framing cameras raise the schema to 35 ({scene.SchemaVersion})");
        var reopened = SceneDocument.Empty(new()); reopened.RestoreObjects(scene.CaptureObjects());
        var copy = reopened.Cameras.Single().GetComponent<Camera2DComponent>()!;
        Check(copy.FrameGroup == "Party" && copy.FramePadding == 20 && copy == lens, "YAML round trip");
        using (var g = new SceneGame(scene))
        {
            g.FixedUpdate(Vector2.Zero, Tick);
            var v = View(g, camera.Id);
            Near(v.Center.X, 500, "frames the centre of both"); Near(v.Center.Y, 0, "frame centre Y");
            Near(v.Zoom, 800f / 1050, "zooms out until 1010 px + 2 × 20 padding fit 800 px");
            Check(v.World.Left <= -5 && v.World.Right >= 1005, "both members inside the view");
            Step(g, far.Id, new(-900, 0));
            Near(View(g, camera.Id).Zoom, 1, "members close together: back to the camera Zoom (never above it)");
            g.SetActive(target.Id, false); g.SetActive(far.Id, false); g.FixedUpdate(Vector2.Zero, Tick);
            Near(View(g, camera.Id).Center.X, 50, "no active member keeps the last view");
            g.SetActive(far.Id, true); g.SetActive(target.Id, true);
            lens.HalfLife = .5f;
            Step(g, far.Id, new(4000, 0));
            var eased = View(g, camera.Id).Zoom;
            Check(eased < 1 && eased > 800f / 4150, $"half-life eases the zoom ({eased})");
            lens.Bounds = new(0, 0, 1600, 900);
            for (int i = 0; i < 120; i++) g.FixedUpdate(Vector2.Zero, Tick);
            Check(View(g, camera.Id).Zoom >= 450f / 900 - .001f, "Bounds still prevents zooming out past the map");
        }

        foreach (var bad in new Camera2DComponent[] { new() { DeadZoneWidth = -1 }, new() { LookAhead = float.NaN }, new() { LookAhead = 11 },
            new() { FrameGroup = "" }, new() { FrameGroup = " Party" }, new() { FramePadding = float.PositiveInfinity } })
        {
            var doc = SceneDocument.Empty(new()); var cam = doc.Camera!; cam.RemoveComponent<Camera2DComponent>(); cam.AddComponent(bad);
            try { reopened.RestoreObjects(doc.CaptureObjects()); throw new Exception("Camera framing: invalid camera accepted"); }
            catch (InvalidDataException) { }
        }
        Console.WriteLine("PASS camera framing: dead zone hold and edge, look-ahead lead and stop, group bounds centre and zoom, padding, Bounds limit, schema 33, YAML, validation");
    }
}
