using System.Numerics;
using Engine.Core;

internal static class CutsceneChecks
{
    [Check(2500, StopAfter = "cutscene")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    public static void Run(string repository, string scratch)
    {
        string project = Path.Combine(repository, "samples/PlatformerLab");
        var scene = SceneDocument.Load(Path.Combine(project, "Scenes/Platform.scene"));
        Guid Id(string name) => scene.Objects.Single(o => o.Name == name).Id;
        Guid shot = Id("Far landing shot"), runner = Id("Runner"), ferry = Id("Shuttle");
        var sequenceId = scene.Objects.Single(o => o.Id == shot).GetComponent<CutsceneDirectorComponent>()!.Sequence!.Value;
        var assets = ProjectAssets.Scan(project);
        var sequence = assets.LoadSequence(project, sequenceId);
        Check(sequence.Lines.Length == 3 && sequence.Lines[1].Speaker == "Runner", "sample sequence resolves by GUID and retains speaker order");
        var original = new SceneObject {Name = "Alternate primary", X = 418, Y = 228, Width = 1, Height = 1};
        original.AddComponent(new Camera2DComponent());
        scene.Objects.Add(original);
        foreach (var reason in new[] {CutsceneExit.Completed, CutsceneExit.Skipped, CutsceneExit.Interrupted})
        {
            using var session = new GameSession(scene, null, null, new PlayerStorage(Path.Combine(scratch, reason.ToString())));
            var game = session.Current;
            var input = session.Actions;
            void Step() => session.FixedUpdate(input.Take(), 1f / 60);
            Vector2 Pos(Guid id) => game.TryGetPosition(id, out var value) ? value : throw new Exception("Missing position");
            game.TrySetActive(original.Id); Step();
            input.Press("Right");
            for (int i = 0; i < 220 && !game.Cutscene.HasValue; i++) Step();
            Check(game.Cutscene is {Line: 0, Count: 3} && game.ActiveCameraId == shot && game.TryGetView(shot, out var view) &&
                view.World.Contains(760, 210) && !view.World.Contains(400, 225) && session.InputUi == null, $"{reason}: boarding while holding Right takes the far shot");
            var deckStart = Pos(ferry);
            var offset = Pos(runner) - deckStart;
            input.Press("Space");
            for (int i = 0; i < 60; i++) Step();
            Check(Vector2.Distance(Pos(runner) - Pos(ferry),offset) < .06f && Pos(ferry).X - deckStart.X > 60 &&
                game.Cutscene?.Line == 0, $"{reason}: the world advances while walking and jumping are blocked");
            input.Release("Space"); input.Release("Right");
            Check(!game.PlayCutscene(shot) && !game.TrySetActive(scene.DefaultCameraId!.Value), "a second director or camera request cannot steal the shot");
            if (reason == CutsceneExit.Completed)
            {
                input.Press("Return"); Step(); Step(); Step();
                Check(game.Cutscene?.Line == 1, "a held advance stops at the second line");
                input.Release("Return"); Step();
                input.Press("Return"); Step(); input.Release("Return"); Step();
                Check(game.Cutscene?.Line == 2, "a new press displays the last line");
                input.Press("Pad.South"); Step(); input.Release("Pad.South");
            }
            else if (reason == CutsceneExit.Skipped)
            {
                input.Press("Return"); input.Press("X"); Step();
                input.Release("Return"); input.Release("X");
            }
            else { game.SetActive(shot, false); Step(); }
            Check(game.Cutscene == null && game.LastCutsceneExit == reason && game.ActiveCameraId == original.Id &&
                session.InputUi != null && game.TryGetMotion(runner, out var grounded) && grounded.Support == ferry,
                $"{reason}: previous non-default camera and UI return; dismissal does not jump");
            offset = Pos(runner) - Pos(ferry);
            input.Press("Right"); for (int i=0;i<8;i++) Step(); input.Release("Right");
            input.Press("Space"); Step(); input.Release("Space");
            Check((Pos(runner) - Pos(ferry) - offset).X > 15 && game.TryGetMotion(runner, out var jump) && jump.Velocity.Y < -250 &&
                !game.Cutscene.HasValue, $"{reason}: movement relative to shuttle and a fresh jump work without retriggering");
        }
        using (var session = new GameSession(scene))
        {
            var input = session.Actions;
            void Step() => session.FixedUpdate(input.Take(), 1f / 60);
            Step(); input.Press("R"); Step(); input.Release("R");
            for (int i = 0; i < 220 && session.Current.Cutscene == null; i++) Step();
            Check(session.Current.Cutscene.HasValue, "the guided approach also triggers on contact");
            input.Press("X"); Step(); input.Release("X");
            input.Press("Right"); Step(); input.Release("Right");
            Check(session.Current.TryGetMotion(runner, out var motion) && motion.Velocity.X > 140,
                "skipping immediately during the guided route releases the motor on the next tick");
        }
        var invalid = Path.Combine(scratch, "empty.cutscene");
        File.WriteAllText(invalid, "lines: []");
        try { CutsceneSequence.Load(invalid); throw new Exception("An empty script was accepted"); }
        catch (InvalidDataException) { Check(true, "an empty sequence is rejected before playback"); }
    }

    private static void Check(bool yes, string label)
    {
        if (!yes) throw new Exception("Cutscene: " + label);
        Console.WriteLine("PASS cutscene: " + label);
    }
}
