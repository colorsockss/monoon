using System.Numerics;
using Engine.Core;

internal static class WorldPointerChecks
{
    [Check(5300)] static void Registered(CheckRun c) => Run(c.Project);
    private sealed class Listener : GameBehaviour, IWorldPointerHandler
    {
        public readonly List<WorldPointerEvent> Events = [];
        public void OnWorldPointer(WorldPointerEvent pointer) => Events.Add(pointer);
    }

    public static void Run(string project)
    {
        const string left = "Mouse.Left";
        var workshop = SceneDocument.Load(Path.Combine(project, "Scenes", "PointerWorkshop.scene"));
        using (var game = new SceneGame(workshop))
        {
            var input = new GameInputRouter(game.Actions, () => game.Ui, game.LogicalSize, () => game);
            void Step() => game.FixedUpdate(input.Take(), 1f / 60);
            Guid Object(string name) => workshop.Objects.Single(o => o.Name == name).Id;
            var front = Object("Front crate");
            var back = Object("Back crate");
            Vector2 Position(Guid id) => game.TryGetPosition(id, out var p) ? p : throw new Exception("Missing crate");
            var f = Position(front);
            var b = Position(back);
            Step();
            input.Pointer(new(358, 220), left, true);
            input.Pointer(new(368, 221));
            input.Pointer(new(368, 221), left, false);
            Step();
            Require(Position(front) == f && Position(back) == b, "UI gesture reached world");
            Vector2 start = new(327, 247), end = new(458, 310);
            input.Pointer(start, left, true);
            input.Pointer(end);
            input.Key("D", true);
            for (int i = 0; i < 60; i++) Step();
            Require(Vector2.Distance(Position(front), f + end - start + new Vector2(100, 0)) < .01f && Position(back) == b,
                "front crate must keep the same grab point while camera moves");
            input.Key("D", false);
            input.Pointer(end, left, false);
            var released = Position(front);
            Require(game.TryGetView(Object("Detail Camera"), out var inset), "detail camera missing");
            start = inset.WorldToScreen(new(330, 232));
            end = new(550, 168);
            input.Pointer(start, left, true);
            input.Pointer(end);
            input.Pointer(end, left, false);
            Require(Vector2.Distance(Position(back), b + (end - start) / inset.Zoom) < .01f && Position(front) == released,
                "drag crossing viewports must keep the original camera");
        }

        var scene = SceneDocument.Empty(new());
        var target = new SceneObject { X = 100, Y = 100, Width = 30, Height = 20, Rotation = 90 };
        target.AddComponent(new ScriptComponent { Script = "Listener" });
        scene.Objects.Add(target);
        var cover = scene.CreateCamera(new(4000, 4000));
        var coverLens = cover.GetComponent<Camera2DComponent>()!;
        coverLens.Output = Camera2DComponent.Extra;
        coverLens.Viewport = new(0, 0, .5f, .5f);
        using var world = new SceneGame(scene, null, null, _ => new Listener());
        var router = new GameInputRouter(world.Actions, () => world.Ui, world.LogicalSize, () => world);
        world.FixedUpdate(Vector2.Zero, 1f / 60);
        var events = world.GetComponent<Listener>(target.Id)!.Events;
        Vector2 local = new(4, 7), point = Vector2.Transform(local, target.WorldMatrix);
        void Press() => router.Pointer(point, left, true);
        void Release() => router.Pointer(point, left, false);
        void Expect(params WorldPointerPhase[] sequence)
        {
            Require(events.Select(e => e.Phase).SequenceEqual(sequence), "unexpected phases: " + string.Join(",", events.Select(e => e.Phase)));
            events.Clear();
        }
        // The inset displays empty space here even though the main view has a target below it.
        Press(); Release(); Expect();
        world.SetActive(cover.Id, false);
        Press(); Release();
        Require(events.Count > 0 && events[0].CameraId == scene.Camera!.Id && Vector2.Distance(events[0].GrabLocalPosition, local) < .001f,
            "rotated object local grab point");
        Expect(WorldPointerPhase.Press, WorldPointerPhase.Release, WorldPointerPhase.Click);
        Press(); router.Pointer(point + new Vector2(12, 0)); Release();
        Expect(WorldPointerPhase.Press, WorldPointerPhase.Drag, WorldPointerPhase.Drag, WorldPointerPhase.Release);
        Press(); router.Reset(); Release();
        Expect(WorldPointerPhase.Press, WorldPointerPhase.Cancel);
        Press(); world.Destroy(target.Id); Release();
        Expect(WorldPointerPhase.Press, WorldPointerPhase.Cancel);
        Console.WriteLine("PASS world pointer picking, UI priority, capture across camera motion/viewports, local grab, click and cancellation");
    }

    private static void Require(bool pass, string why)
    {
        if (!pass) throw new Exception("World pointer: " + why);
    }
}
