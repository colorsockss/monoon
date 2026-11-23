using System;
using System.Collections.Generic;
using Engine.Core;

// KeyRoom door rules. Run: ./monoon test project samples/KeyRoom
public sealed class DoorTests
{
    private const string Room = "Scenes/KeyRoom.scene";

    private static IEnumerable<ScriptWait> TakeKey(GameTestRun run)
    {
        run.Press("Left");
        yield return ScriptWait.Until(() => run.Session.Has("key"));
        run.Release("Left");
    }

    [GameTest(Room)]
    public IEnumerable<ScriptWait> ClosedDoorStopsPlayer(GameTestRun run)
    {
        run.Press("Right");
        yield return ScriptWait.Frames(60);
        run.Assert(MathF.Abs(run.PositionOf("Player").X - 440) < .01f, "the closed door stops the player's leading edge at x 440");
        run.Assert(!run.Session.Has("key") && run.Objects.IsColliderEnabled(run.Find("Door")), "the door stays closed without the key");
    }

    [GameTest(Room)]
    public IEnumerable<ScriptWait> KeyOpensDoorThenHintAfterHalfSecond(GameTestRun run)
    {
        foreach (var wait in TakeKey(run)) yield return wait;
        var door = run.Find("Door");
        long opened = run.Time.TickCount;
        run.Assert(run.Objects.IsActive(door) && !run.Objects.IsColliderEnabled(door), "the key turns the door's collider off");
        run.AreEqual("Assets/DoorOpen3.png", run.Objects.GetComponent<SpriteRendererComponent>(door)?.Asset, "open door sprite");
        run.AreEqual(run.Find("Door Camera"), run.World.ActiveCameraId ?? Guid.Empty, "camera after the pickup");
        run.Assert(!run.Session.Has("doorHint"), "no hint the moment the door opens");
        yield return ScriptWait.Until(() => run.Session.Has("doorHint"));
        run.AreEqual(30L, run.Time.TickCount - opened, "ticks from opening to the hint");
        yield return ScriptWait.Seconds(0.6);
        run.AreEqual(run.Find("Main Camera"), run.World.ActiveCameraId ?? Guid.Empty, "camera one second after the pickup");
    }

    [GameTest(Room)]
    public IEnumerable<ScriptWait> PauseAndStepDelayTheHint(GameTestRun run)
    {
        foreach (var wait in TakeKey(run)) yield return wait;
        long opened = run.Time.TickCount;
        yield return ScriptWait.Frames(10);
        run.Tap("P");
        yield return ScriptWait.UiSeconds(1);
        run.Assert(run.Time.IsPaused && !run.Session.Has("doorHint"), "a second of pause does not count toward the wait");
        run.AreEqual(opened + 10, run.Time.TickCount, "ticks while paused");
        for (int i = 0; i < 5; i++)
        {
            run.Tap("Period");
            yield return ScriptWait.UiSeconds(0.1);
        }
        run.AreEqual(opened + 15, run.Time.TickCount, "five steps run five ticks");
        run.Tap("P");
        yield return ScriptWait.Until(() => run.Session.Has("doorHint"));
        run.AreEqual(opened + 30, run.Time.TickCount, "the hint still comes 30 game ticks after opening");
    }

    [GameTest(Room)]
    public IEnumerable<ScriptWait> RemovingTheDoorCancelsTheHint(GameTestRun run)
    {
        foreach (var wait in TakeKey(run)) yield return wait;
        var door = run.Find("Door");
        run.Tap("F6");
        yield return ScriptWait.Seconds(1);
        run.Assert(!run.Objects.IsActive(door) && !run.Session.Has("doorHint"), "the removed door's wait never finishes");
    }

    [GameTest(Room)]
    public IEnumerable<ScriptWait> RecordedWalkTakesTheKey(GameTestRun run)
    {
        yield return run.Replay("Tests/TakeKey.inputreplay");
        run.AreEqual(30, run.Frame, "frames after the 30-frame recording");
        run.Assert(run.Session.Has("key"), "the recorded walk takes the key");
    }

    [GameTest(Room)]
    public IEnumerable<ScriptWait> OpenDoorLeadsToSecondRoom(GameTestRun run)
    {
        var start = run.SceneId;
        foreach (var wait in TakeKey(run)) yield return wait;
        run.Press("Right");
        yield return ScriptWait.Until(() => run.SceneId != start);
        run.Assert(run.Session.Has("key"), "the key carries into the second room");
    }
}
