using System.Diagnostics;
using System.Numerics;
using Engine.Core;

// Mixing cost per 512-frame block: 16 Tide.wav loops on Music + 16 spatial beacons on Effects (the audio-mixer benchmark),
// 2,000 blocks after 200 warm-up blocks. Candidate "active": a Voice line loop with Voice->Music ducking and a snapshot ramp.
var repo = args[0]; bool active = args.Length > 1 && args[1] == "active", voice = active || args.Length > 1 && args[1] == "voice";
using var m = new AudioMixer();
#if BUSES
if (active) m.Configure(new GameSettings { AudioDucking = [new()], AudioSnapshots = [new("Night", new(Music: .5f, Effects: .6f))] });
#endif
var tide = Path.Combine(repo, "samples/AudioLab/Assets/Tide.wav"); var beacon = Path.Combine(repo, "samples/AudioLab/Assets/Beacon.wav");
for (int i = 0; i < 16; i++) m.Start(new object(), tide, new AudioSourceComponent { Bus = AudioBus.Music, Loop = true, Volume = .1f }, default, 0);
for (int i = 0; i < 16; i++) m.Start(new object(), beacon, new AudioSourceComponent { Loop = true, Spatial = true, Volume = .1f }, new Vector2(i * 30, 0), 0);
#if BUSES
if (voice) m.Start(new object(), Path.Combine(repo, "samples/AudioLab/Assets/Line.wav"), new AudioSourceComponent { Bus = AudioBus.Voice, Loop = true, Volume = .1f }, default, 0);
#endif
var buffer = new float[1024];
for (int i = 0; i < 200; i++) m.Render(buffer);
#if BUSES
if (active) m.TransitionTo("Night", 20);
#endif
var times = new double[2000]; long bytes = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < times.Length; i++) { long t = Stopwatch.GetTimestamp(); m.Render(buffer); times[i] = Stopwatch.GetElapsedTime(t).TotalMicroseconds; }
bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
Array.Sort(times);
Console.WriteLine($"BLOCK median {times[1000]:F1} us p95 {times[1900]:F1} us, {bytes} B over 2000 blocks");
