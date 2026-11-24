using Engine.Core;

internal static class ScriptRoutineChecks
{
    [Check(5900)] static void Registered(CheckRun c) => Run();
    internal static void Run()
    {
        var scene = SceneDocument.Empty(new());
        var a = new SceneObject { Name = "A" };
        var b = new SceneObject { Name = "B" };
        var c = new SceneObject { Name = "C" };
        scene.Objects.AddRange([a, b, c]);
        var game = new SceneGame(scene);
        var loop = new GameLoop(game, game.Actions);
        var time = game.Time;
        var log = new List<string>();
        int frame = 0;
        void Frame(int count = 1) { for (int i = 0; i < count; i++, frame++) loop.Advance(GameTime.FixedStep); }
        IEnumerable<ScriptWait> Note(string name, ScriptWait wait)
        {
            log.Add($"{name} start {time.TickCount}");
            yield return wait;
            log.Add($"{name} done {time.TickCount} frame {frame}");
        }
        Frame(); // Start the scene at tick 1.

        var seconds = game.StartRoutine(a.Id, Note("seconds", ScriptWait.Seconds(.5)));
        var frames = game.StartRoutine(b.Id, Note("frames", ScriptWait.Frames(3)));
        bool go = false;
        var until = game.StartRoutine(a.Id, Note("until", ScriptWait.Until(() => go)));
        Check(log.SequenceEqual(["seconds start 1", "frames start 1", "until start 1"]) && seconds.IsRunning,
            "a routine runs to its first wait when started");
        Frame(3);
        Check(log.Contains("frames done 4 frame 3") && !frames.IsRunning, "Frames(3) resumes on the third tick");
        go = true;
        Frame();
        Check(log.Contains("until done 5 frame 4"), "Until resumes on the first tick after its condition holds");
        Frame(5);
        time.IsPaused = true;
        Frame();
        var ui = game.StartRoutine(b.Id, Note("ui", ScriptWait.UiSeconds(.25)));
        Frame(60);
        Check(time.TickCount == 10 && seconds.IsRunning && log.Any(l => l.StartsWith("ui done 10 frame")),
            "pause holds game waits while UiSeconds keeps counting");
        for (int i = 0; i < 3; i++) { time.StepOnce(); Frame(); }
        Check(time.TickCount == 13 && seconds.IsRunning, "each Step advances game waits by one tick");
        time.IsPaused = false;
        Frame(25);
        Check(log.Contains("seconds done 31 frame 92") && !seconds.IsRunning && !ui.IsRunning,
            "Seconds(0.5) resumes exactly 30 game ticks after it started, paused frames excluded");

        var order = new List<int>();
        IEnumerable<ScriptWait> Ordered(int n) { yield return ScriptWait.Frames(1); order.Add(n); }
        game.StartRoutine(c.Id, Ordered(1));
        game.StartRoutine(a.Id, Ordered(2));
        game.StartRoutine(c.Id, Ordered(3));
        Frame();
        Check(order.SequenceEqual([1, 2, 3]), "routines resuming on the same tick keep their start order");

        bool cleaned = false;
        IEnumerable<ScriptWait> Forever()
        {
            try { while (true) yield return ScriptWait.Frames(1); }
            finally { cleaned = true; }
        }
        var disabled = game.StartRoutine(c.Id, Forever());
        game.SetActive(c.Id, false);
        Check(!disabled.IsRunning && cleaned, "disabling the owner stops its routine and runs finally blocks");
        game.SetActive(c.Id, true);
        Frame(2);
        Check(!disabled.IsRunning, "re-enabling the owner does not resume a cancelled routine");
        try { game.SetActive(c.Id, false); game.StartRoutine(c.Id, Forever()); throw new Exception("Expected an inactive owner error."); }
        catch (InvalidOperationException) { Console.WriteLine("PASS routines: an inactive owner cannot start a routine"); }
        game.SetActive(c.Id, true);

        IEnumerable<ScriptWait> SelfDisable() { yield return ScriptWait.Frames(1); game.SetActive(c.Id, false); yield return ScriptWait.Frames(1); log.Add("after self disable"); }
        var self = game.StartRoutine(c.Id, SelfDisable());
        Frame(3);
        Check(!self.IsRunning && !log.Contains("after self disable"), "a routine that disables its own owner stops after that step");
        game.SetActive(c.Id, true);

        var removed = game.StartRoutine(b.Id, Forever());
        game.Destroy(b.Id);
        Check(!removed.IsRunning, "removing the owner stops its routine");

        IEnumerable<ScriptWait> Throws() { yield return ScriptWait.Frames(1); throw new InvalidDataException("boom"); }
        var failing = game.StartRoutine(c.Id, Throws());
        try { Frame(); throw new Exception("Expected the routine failure."); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Throws") && ex.Message.EndsWith("boom"))
        {
            Check(!failing.IsRunning, "a failing routine reports its method and stops");
        }

        var survivor = game.StartRoutine(a.Id, Forever());
        game.Dispose();
        Check(!survivor.IsRunning, "ending the scene stops every routine");
    }

    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("Routines: " + label);
        Console.WriteLine("PASS routines: " + label);
    }
}
