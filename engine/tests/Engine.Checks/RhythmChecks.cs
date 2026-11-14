using System.Diagnostics;
using Engine.Core;

internal static class RhythmChecks
{
    private static void Check(bool pass, string message)
    {
        if (!pass) throw new Exception("Rhythm: " + message);
        Console.WriteLine("PASS rhythm: " + message);
    }

    internal static void Run(string repository)
    {
        const int rate = AudioMixer.SampleRate;
        const int beat = rate / 2;
        var timing = new RhythmTiming(120, 35, 140);
        var edges = new (int Frame, BeatGrade Grade)[] {
            (-6721, BeatGrade.Outside), (-6720, BeatGrade.Early), (-1681, BeatGrade.Early),
            (-1680, BeatGrade.OnTime), (0, BeatGrade.OnTime), (1680, BeatGrade.OnTime),
            (1681, BeatGrade.Late), (6720, BeatGrade.Late), (6721, BeatGrade.Outside)
        };
        Check(edges.All(e => timing.Judge(beat + e.Frame) == new BeatHit(2, e.Frame, e.Grade)),
            "inclusive timing windows and both neighboring samples");
        var fractional = new RhythmTiming(137.5m, 30, 100, firstBeatFrame: 8000);
        const long distant = 1234567;
        long target = 8000 + (long)decimal.Round((distant - 1) * 60m * rate / 137.5m);
        Check(fractional.Judge(target) == new BeatHit(distant, 0, BeatGrade.OnTime) &&
            fractional.Judge(0).Grade == BeatGrade.Outside, "first-beat offset and fractional tempo without accumulated drift");

        var scene = SceneDocument.Load(Path.Combine(repository, "samples/RhythmLab/Scenes/Practice.scene"));
        using var game = new GameSession(scene);
        var source = scene.Objects.Single(o => o.Name == "Afterbeat").Id;
        var router = new GameInputRouter(game.Actions, () => game.InputUi, game.LogicalSize);
        var notes = new List<string>();
        game.State.Logged += entry => notes.Add(entry.Message);
        void Tick() => game.FixedUpdate(game.Actions.Take(), 1f/60);
        Tick();
        long start = Stopwatch.GetTimestamp() - 6 * Stopwatch.Frequency;
        long At(long frame) => start + (long)Math.Round(frame * (double)Stopwatch.Frequency / rate);
        bool Position(long timestamp, out long frame) => game.Current.TryGetPlaybackFrame(source, timestamp, out frame);

        // Keep 80 ms queued at all times. Feed the clock just as the SDL sink would,
        // and batch two different beat presses before one game tick.
        const int packet = 480, queued = 3840;
        game.Mixer.Render(new float[2 * queued]);
        Check(!Position(start, out _), "render-ahead alone does not start the rhythm clock");
        var buffer = new float[2 * packet];
        for (int n=0; n<500; n++)
        {
            game.Mixer.Render(buffer);
            game.Mixer.ObserveOutput(queued, At(n * packet));
            int frame = n * packet;
            if (frame == beat - 3840 || frame == 2*beat || frame == 3*beat + 3840 || frame == 3*beat + 4800)
            {
                router.Key("Space", true, timestamp:At(frame));
                router.Key("Space", true, timestamp:At(frame + 12));
                router.Key("Space", false);
            }
            if (frame == 2*beat + packet || frame == 3*beat + 2*beat/3) Tick();
        }
        Tick();
        Check(notes.SequenceEqual(new[] {"빠름    2박   /   -80.0 ms","정확    3박   /   0.0 ms","늦음    4박   /   +80.0 ms"}),
            "timestamped presses survive delayed ticks, repeats and a second tap on the same beat");
        Check(Position(At(4*rate + 1200), out var afterLoop) && Math.Abs(afterLoop - (4*rate+1200)) <= 1,
            "clock follows device consumption across the eight-beat audio loop");
        Check(Position(At(7*rate), out var stalled) && stalled == 500L * packet,
            "clock stops at the last delivered sample when the output stalls");
        Check(game.Current.Play(source) && !Position(At(7*rate), out _), "restart waits for the new voice to reach the output");
        game.Mixer.Render(new float[2*(queued+packet)]);
        game.Mixer.ObserveOutput(0, At(7*rate));
        Check(Position(At(7*rate + queued + packet), out var restarted) && restarted == packet,
            "restarted source uses its own origin after old queued audio");
    }
}
