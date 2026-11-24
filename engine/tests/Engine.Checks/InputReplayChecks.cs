using System.Diagnostics;
using System.Security.Cryptography;
using Engine.Core;

internal static class InputReplayChecks
{
    [Check(7300)] static void Registered(CheckRun c) => CheckRecording(c.Scratch);
    internal static async Task RunInventory(string repository, string builds, string scratch)
    {
        var fixture = Path.Combine(repository, "engine", "tests", "fixtures", "InventoryLab");
        var recording = Path.Combine(fixture, "Herbalist.inputreplay");
        var expected = File.ReadAllBytes(Path.Combine(fixture, "Herbalist.save"));
        var release = await Replay("Release", false);
        var native = await Replay("Native AOT", true);
        if (!release.SequenceEqual(native) || !release.SequenceEqual(expected))
            throw new InvalidDataException($"Recorded save {Hash(expected)}, Release {Hash(release)}, Native AOT {Hash(native)}.");
        Console.WriteLine($"PASS InventoryLab input replay: live recording, headless Release and headless Native AOT produced identical saves ({expected.Length} bytes, SHA-256 {Hash(expected)}).");

        async Task<byte[]> Replay(string mode, bool aot)
        {
            string player = InventoryBuildChecks.PlayerPath(builds, aot);
            var profile = Directory.CreateDirectory(Path.Combine(scratch, mode)).FullName;
            await InventoryBuildChecks.RunPlayer(player, profile, mode, ["--replay-input", recording, profile]);
            var saved = File.ReadAllBytes(Path.Combine(profile, "slot-001.save"));
            Console.WriteLine($"{mode} replay save: {saved.Length} bytes, SHA-256 {Hash(saved)}.");
            return saved;
        }
    }

    internal static void CheckRecording(string scratch)
    {
        string path = Path.Combine(scratch, "roundtrip.inputreplay");
        var recorder = new InputRecorder(Guid.NewGuid(), Guid.NewGuid());
        long timestamp = Stopwatch.GetTimestamp();
        var press = new ReplayInput(ReplayInputKind.Key, Control: "Left", Down: true);
        var release = press with { Down = false };
        recorder.Observe(press, timestamp);
        recorder.EndFrame(TimeSpan.FromTicks(100000));
        recorder.EndFrame(TimeSpan.FromTicks(200000));
        recorder.Observe(release, timestamp + 1);
        recorder.Observe(new(ReplayInputKind.Reset), timestamp + 2);
        recorder.EndFrame(TimeSpan.Zero);
        recorder.Observe(press, timestamp + 3); // Window closed before an update consumed this event.
        var original = recorder.Save(path);
        var loaded = InputReplay.Load(path);
        if (loaded.Project != original.Project || loaded.StartScene != original.StartScene ||
            !loaded.FrameTicks.SequenceEqual(new long[] { 100000, 200000, 0 }) ||
            !loaded.Events.SequenceEqual(original.Events) ||
            !loaded.Events.Select(e => e.Frame).SequenceEqual(new[] { 0, 2, 2 }) ||
            loaded.Events[0].Input != press || loaded.Events[1].Input != release)
            throw new InvalidDataException("Input recording lost an edge, order, or frame boundary.");
        try { recorder.Save(path); throw new Exception("Replaced an existing recording."); }
        catch (IOException) when (File.Exists(path)) { }
        var badEvent = loaded.Events[0] with { Input = new(ReplayInputKind.Pad, Control: "Pad.South", Value: 2) };
        foreach (var broken in new[] { loaded with { Version = -1 }, loaded with { FrameTicks = [-1] },
            loaded with { Events = loaded.Events.Reverse().ToArray() }, loaded with { Events = [badEvent] } })
        {
            try { broken.Validate(); }
            catch (InvalidDataException) { continue; }
            throw new Exception("Invalid replay was accepted.");
        }
        Console.WriteLine("PASS input recording preserves event order, held frames and elapsed ticks, discards unconsumed input and rejects invalid recordings.");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
