using System.Numerics;
using Engine.Core;

internal static class TimeChecks
{
    [Check(8500)] static void Registered(CheckRun c) => Run(c.Scene);
    public static void Run(SceneDocument scene)
    {
        using var session = new GameSession(scene);
        var actions = session.Actions;
        var loop = new GameLoop(session, actions);
        var time = session.Time;
        const double dt = GameTime.FixedStep;
        var player = scene.Objects.Single(o => o.Name == "Player").Id;
        Vector2 Position()
        {
            if (!session.Current.TryGetPosition(player, out var position)) throw new Exception("Missing player.");
            return position;
        }
        void Check(bool passed, string label)
        {
            if (!passed) throw new Exception("Time: " + label);
            Console.WriteLine("PASS time: " + label);
        }
        void Tap(string control) { actions.Press(control); actions.Release(control); }
        // Start within the chest's reach, so a replayed Interact would spawn a coin.
        session.Current.TryMoveStep(player, new Vector2(16, 0));
        var origin = Position();
        var gameTimer = time.StartTimer(4 * dt);
        var uiTimer = time.StartTimer(.2, TimeDomain.Ui, repeat:true);
        time.Scale = .5;
        Tap("Right");
        loop.Advance(dt);
        Check(Position() == origin && time.TickCount == 0, "half speed accumulates before the first fixed tick");
        loop.Advance(dt);
        Check(Position() == origin + new Vector2(4,0) && time.TickCount == 1, "short movement survives UI frames until a game tick");
        Tap("P");
        loop.Advance(dt);
        var paused = Position();
        var before = time.GameSeconds;
        actions.Press("Right");
        Tap("E");
        loop.Advance(1.25);
        Check(time.IsPaused && Position() == paused && time.GameSeconds == before && time.TickCount == 1,
            "pause freezes held movement, game time and tick count");
        Check(uiTimer.Consume() && !uiTimer.Consume() && !gameTimer.Consume() && uiTimer.Remaining > 0,
            "unscaled repeating timer advances through pause while game timer waits");
        Tap("H");
        loop.Advance(dt);
        Check(session.State.Has("controls"), "unscaled keyboard action opens help while paused");
        var button = session.Current.Ui.Bounds(scene.Objects.Single(o => o.Name == "Controls button").Id)!.Value;
        session.Current.Ui.Pointer(true,button.X + 4,button.Y + 4);
        session.Current.Ui.Pointer(false,button.X + 4,button.Y + 4);
        loop.Advance(dt);
        Check(!session.State.Has("controls"), "UI clicks close help while paused");
        time.Scale = 4;
        time.StepOnce();
        loop.Advance(dt);
        loop.Advance(.1);
        Check(Position() == paused + new Vector2(4,0) && time.TickCount == 2 && time.IsPaused &&
            loop.Interpolation == 1f, "one step executes exactly one tick and holds the current pose at 4x");
        Check(!session.Current.Objects.Any(o => o.Name=="Coin"), "paused Interact was not replayed on step");
        actions.Release("Right");
        time.IsPaused = false;
        loop.Advance(dt);
        var resumed = time.TickCount;
        loop.Advance(dt);
        Check(time.TickCount == resumed + 4 && Position() == paused + new Vector2(4,0) &&
            gameTimer.Consume() && !gameTimer.Consume(), "4x resumes with four fixed ticks and one game-timer expiry");
        uiTimer.Cancel();
        Check(!uiTimer.IsRunning && !uiTimer.Consume(), "timer cancellation");
        try { time.Scale = double.NaN; throw new Exception("Invalid speed accepted"); }
        catch (ArgumentOutOfRangeException) { }
        Check(time.Scale == 4, "invalid scale preserves previous speed");
        time.IsPaused = true;
        loop.Advance(.1);
        var ticks = time.TickCount;
        session.State.LoadScene(ProjectAssets.ReadManifest(scene.ProjectDirectory!).IdFor("Scenes/ShowcaseRoom2.scene")!.Value);
        loop.Advance(dt);
        Check(session.SceneId != scene.AssetId && time.IsPaused && session.Time == time && time.TickCount == ticks,
            "UI can switch scenes while paused, retaining both clocks");
        using var restart = new GameSession(scene);
        Check(!restart.Time.IsPaused && restart.Time.Scale == 1 && restart.Time.TickCount == 0 && restart.Time.UiSeconds == 0,
            "a new Play starts at normal speed with fresh clocks");

        var doorLoop = new GameLoop(restart,restart.Actions);
        var playerCamera = restart.Current.ActiveCameraId;
        restart.Actions.Press("Left");
        for (int i = 0; i < 70 && !restart.State.Has("key"); i++) doorLoop.Advance(dt);
        restart.Actions.Release("Left");
        var doorCamera = restart.Current.ActiveCameraId;
        var canvas = new CaptureCanvas();
        string DoorFrame()
        {
            restart.Draw(canvas, doorLoop.Interpolation);
            return Path.GetFileName(canvas.Images.Single(i => Path.GetFileName(i.Path) is "Door.png" or "DoorOpen.png").Path);
        }
        var firstFrame = DoorFrame();
        restart.Time.IsPaused = true;
        doorLoop.Advance(2);
        Check(restart.State.Has("key") && doorCamera != playerCamera && DoorFrame() == firstFrame &&
            restart.Current.ActiveCameraId == doorCamera, "KeyRoom door animation and the door-shot camera timer both stop");
        // The door holds shut for 0.5 s (30 ticks) before it swings open; the door shot lasts 1.4 s (84 ticks).
        for (int i = 0; i < 32; i++) restart.Time.StepOnce();
        doorLoop.Advance(dt);
        Check(DoorFrame() != firstFrame && restart.Current.ActiveCameraId == doorCamera, "32 manual ticks advance the door animation");
        restart.Time.Scale = 2;
        restart.Time.IsPaused = false;
        doorLoop.Advance(dt);
        for (int i = 0; i < 25; i++) doorLoop.Advance(dt);
        Check(restart.Current.ActiveCameraId == doorCamera, "door camera still active after 82 game ticks");
        doorLoop.Advance(dt);
        Check(restart.Current.ActiveCameraId == playerCamera, "camera timer expires on the 84th game tick at 2x");
    }
}
