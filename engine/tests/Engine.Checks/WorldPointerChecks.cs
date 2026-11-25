using System.Numerics;
using Engine.Core;

internal static class WorldPointerChecks
{
    [Check(5300, "world-pointer")] static void Registered(CheckRun c) => Run(c.Project);
    private sealed class Listener : GameBehaviour, IWorldPointerHandler
    {
        public readonly List<WorldPointerEvent> Events = [];
        public void OnWorldPointer(WorldPointerEvent pointer) => Events.Add(pointer);
    }

    public static void Run(string project)
    {
        const string left = "Mouse.Left";
        // The KeyRoom showcase: both crates follow a mouse drag (DragCrate), the Bag button sits over Crate 2's lower edge,
        // and F8's Detail Camera draws an inset viewport over the chest (camera at zoom 1.5, inset at 2.5).
        var room = SceneDocument.Load(Path.Combine(project, "Scenes", "Showcase.scene"));
        using (var game = new SceneGame(room))
        {
            var input = new GameInputRouter(game.Actions, () => game.Ui, game.LogicalSize, () => game);
            Guid Object(string name) => room.Objects.Single(o => o.Name == name).Id;
            var dragged = Object("Crate 2");
            var other = Object("Crate");
            var camera = Object("Main Camera");
            void Step(float pan = 0)
            {
                if (pan != 0) game.TryMoveStep(camera, new(pan / 60, 0));   // the showcase camera is fixed; pan it here
                game.FixedUpdate(input.Take(), 1f / 60);
            }
            Vector2 Position(Guid id) => game.TryGetPosition(id, out var p) ? p : throw new Exception("Missing crate");
            var f = Position(dragged);
            var b = Position(other);
            Step();
            // Press on the Bag button above Crate 2 and leave it before releasing: no click, and the world never sees it.
            input.Pointer(new(660, 408), left, true);
            input.Pointer(new(660, 380));
            input.Pointer(new(660, 380), left, false);
            Step();
            Require(Position(dragged) == f && Position(other) == b && game.Ui.ActiveInputMode == null, "UI gesture reached world");
            Vector2 start = new(670, 380), end = new(430, 335);
            input.Pointer(start, left, true);
            input.Pointer(end);
            for (int i = 0; i < 60; i++) Step(100);
            Require(Vector2.Distance(Position(dragged), f + (end - start) / 1.5f + new Vector2(100, 0)) < .01f && Position(other) == b,
                "Crate 2 must keep the same grab point while the camera moves: " + Position(dragged));
            input.Pointer(end, left, false);
            var released = Position(dragged);
            var detail = Object("Detail Camera");
            game.SetActive(detail, true);
            Step();
            Require(game.TryGetView(detail, out var inset), "detail camera missing");
            start = inset.WorldToScreen(released + new Vector2(8, 7));
            end = start + new Vector2(100, -20);   // ends outside the inset, over the main view
            input.Pointer(start, left, true);
            input.Pointer(end);
            input.Pointer(end, left, false);
            Require(Vector2.Distance(Position(dragged), released + (end - start) / inset.Zoom) < .01f && Position(other) == b,
                "drag crossing viewports must keep the original camera: " + Position(dragged));
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
