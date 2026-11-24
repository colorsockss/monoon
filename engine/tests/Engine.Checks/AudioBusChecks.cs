using System.Numerics;
using System.Text;
using Engine.Core;

// 16.10: Voice/UI/Ambience buses, snapshots and ducking in AudioLab (2.5/7.8 method: offline 512-frame blocks, sample by sample).
internal static class AudioBusChecks
{
    [Check(4300, "audio-buses")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private static void Check(bool result, string why) { if (!result) throw new Exception("Audio buses: " + why); }
    private static float[] Render(AudioMixer m, int frames) { var buffer = new float[frames * 2]; m.Render(buffer); return buffer; }
    private static readonly float Duck = MathF.Pow(10, -8 / 20f);

    public static void Run(string repo, string scratch)
    {
        var path = Path.Combine(repo, "samples", "AudioLab", "Scenes", "AudioLab.scene");
        SceneDocument Lab(bool talk)
        {
            var scene = SceneDocument.Load(path);
            scene.Objects.Single(o => o.Name == "Player").RemoveComponent<ScriptComponent>();
            scene.Objects.Single(o => o.Name == "Line").GetComponent<AudioSourceComponent>()!.PlayOnStart = talk;
            return scene;
        }
        // Music alone, and music with the line on a muted Voice bus: ducking follows bus activity, not the signal.
        float[] Play(bool talk, int frames)
        {
            using var game = new SceneGame(Lab(talk));
            game.Levels = game.Levels with { Voice = 0 };
            game.FixedUpdate(Vector2.Zero, 1 / 60f);
            return Render(game.Mixer, frames);
        }
        const int Frames = 2 * 57600, Attack = 2400, Release = 19200, LineEnd = 43201;
        var plain = Play(false, Frames);
        var ducked = Play(true, Frames);
        double worst = 0;
        for (int i = 2 * Attack; i < 2 * LineEnd; i++) worst = Math.Max(worst, Math.Abs(ducked[i] - plain[i] * Duck));
        Check(worst <= 2e-7, $"music under the line is -8 dB on every sample after the 0.05 s attack (max error {worst:E2})");
        double ramp = 0;
        for (int f = 0; f < Attack; f++)
            for (int c = 0; c < 2; c++)
                if (Math.Abs(plain[2 * f + c]) > 1e-3)
                    ramp = Math.Max(ramp, Math.Abs(ducked[2 * f + c] / plain[2 * f + c] - (1 + (Duck - 1) * (f + 1) / Attack)));
        Check(ramp < 1e-5, $"linear attack ramp ({ramp:E2})");
        // The line leaves on the block after its last frame; the 0.4 s release then ends exactly at unity.
        int restored = Enumerable.Range(0, Frames).Last(f => ducked[2 * f] != plain[2 * f] || ducked[2 * f + 1] != plain[2 * f + 1]) + 1;
        Check(restored > LineEnd + Release - 1 && restored <= LineEnd + 512 + Release, $"release ends at frame {restored}");
        Check(ducked.AsSpan(2 * restored).SequenceEqual(plain.AsSpan(2 * restored)), "music returns to the same samples after the release");

        // Unused rules, snapshots and the three extra groups leave the PCM unchanged: the project's settings vs none at all.
        float[] Bare(bool configured)
        {
            var scene = Lab(false);
            if (!configured) scene = scene.WithSettings(scene.Settings with { AudioDucking = null, AudioSnapshots = null });
            using var game = new SceneGame(scene);
            game.FixedUpdate(Vector2.Zero, 1 / 60f);
            return Render(game.Mixer, 57600);
        }
        Check(Bare(true).SequenceEqual(Bare(false)), "unused buses, snapshot and ducking rule leave AudioLab's PCM byte-identical");

        // Night (music x0.5) over 1 s: halfway at ~75 %, then the same samples as Music at half its level (x0.5 is exact).
        var night = Play(false, 2 * 48000);
        float[] Night()
        {
            using var game = new SceneGame(Lab(false));
            game.Levels = game.Levels with { Voice = 0 };
            game.FixedUpdate(Vector2.Zero, 1 / 60f);
            var head = Render(game.Mixer, 512);
            Check(game.TransitionTo("Night", 1) && !game.TransitionTo("Missing", 1), "known and unknown snapshot names");
            return [.. head, .. Render(game.Mixer, 2 * 48000 - 512)];
        }
        var blended = Night();
        double mid = 0; int midCount = 0;
        for (int f = 512 + 23900; f < 512 + 24100; f++)
            if (Math.Abs(night[2 * f]) > 1e-3) { mid += blended[2 * f] / night[2 * f]; midCount++; }
        Check(midCount > 0 && Math.Abs(mid / midCount - .75) < .002, $"snapshot halfway gain {mid / midCount:F4}");
        float[] HalfMusic()
        {
            using var game = new SceneGame(Lab(false));
            game.Levels = game.Levels with { Voice = 0 };
            game.FixedUpdate(Vector2.Zero, 1 / 60f);
            var head = Render(game.Mixer, 512);
            game.Levels = game.Levels with { Music = game.Levels.Music * .5f };
            return [.. head, .. Render(game.Mixer, 2 * 48000 - 512)];
        }
        Check(blended.AsSpan(2 * (512 + 48000)).SequenceEqual(HalfMusic().AsSpan(2 * (512 + 48000))), "snapshot ends on the exact bus gain");

        // Steady state with a ducking voice and a running snapshot blend: no managed allocation per block.
        using (var game = new SceneGame(Lab(true)))
        {
            game.FixedUpdate(Vector2.Zero, 1 / 60f);
            var block = new float[1024];
            for (int i = 0; i < 8; i++) game.Mixer.Render(block);
            game.TransitionTo("Night", 2);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++) game.Mixer.Render(block);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bytes == 0, $"mixing with ducking and a snapshot blend allocated {bytes} B");
        }

        // Settings: old files keep their bytes, new keys round-trip, bad rules are rejected.
        var old = Encoding.UTF8.GetString(ProjectYaml.Encode(new GameSettings { Mixer = new(.8f, .8f, .75f) }));
        Check(!old.Contains("voice") && !old.Contains("audioSnapshots") && !old.Contains("audioDucking"), "settings without the new buses write no new keys");
        var lab = GameSettings.Load(Path.Combine(repo, "samples", "AudioLab"));
        Check(lab.AudioDucking is [{ Trigger: AudioBus.Voice, Target: AudioBus.Music, Decibels: -8 }] &&
              lab.AudioSnapshots is [{ Name: "Night", Levels: { Music: .5f, Effects: .6f, Voice: null } }], "AudioLab bus settings");
        var copy = Path.Combine(scratch, "audio-bus-settings"); Directory.CreateDirectory(copy);
        lab.Save(copy);
        var again = GameSettings.Load(copy);
        Check(again.AudioDucking!.SequenceEqual(lab.AudioDucking) && again.AudioSnapshots!.SequenceEqual(lab.AudioSnapshots), "bus settings round trip");
        foreach (var bad in new[] { lab with { AudioDucking = [new(AudioBus.Music, AudioBus.Music)] }, lab with { AudioDucking = [new(Decibels: 3)] },
                     lab with { AudioSnapshots = [new("A"), new("A")] }, lab with { AudioSnapshots = [new("B", new(Music: 2))] }, lab with { Mixer = new(Voice: -1) } })
        {
            try { bad.Validate(); throw new Exception("Audio buses: accepted invalid settings"); }
            catch (InvalidDataException) { }
        }

        // player.settings: the AudioLab voice level is saved and restored (7.8 method, through the sample's script).
        var storage = Path.Combine(scratch, "audio-bus-profile");
        var scene = SceneDocument.Load(path);
        var voiceList = scene.Objects.Single(o => o.Name == "VoiceList").Id;
        using (var session = new GameSession(scene, null, null, new PlayerStorage(storage)))
        {
            session.FixedUpdate(session.Actions.Take(), 1 / 60f);
            Check(session.Current.Levels.Level(AudioBus.Voice) == 1, "voice level starts at Full");
            session.Current.Ui.SetSelectedIndex(voiceList, 0);
            session.FixedUpdate(session.Actions.Take(), 1 / 60f);
            Check(session.Current.Levels.Voice == .375f, "voice level applied");
        }
        using (var session = new GameSession(SceneDocument.Load(path), null, null, new PlayerStorage(storage)))
        {
            session.FixedUpdate(session.Actions.Take(), 1 / 60f);
            Check(session.Current.Levels.Voice == .375f && session.Current.Ui.SelectedIndex(voiceList) == 0, "voice level restored from player.settings");
        }
        Console.WriteLine($"PASS audio buses: line ducks music -8 dB on every sample (max error {worst:E1}), linear attack, release to identical samples at frame {restored}; " +
                          "unused rules byte-identical; Night snapshot blend and exact end; 0 B per block; settings bytes, round trip, validation; voice level restored");
    }
}
