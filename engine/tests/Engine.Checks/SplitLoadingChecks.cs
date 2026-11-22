using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Engine.Core;

// Roadmap 10.7: async scene loads decode files on worker threads and spend a bounded
// amount of game-thread time per update on object creation and uploads.
internal static class SplitLoadingChecks
{
    private const int Scripted = 600, Files = 12;
    private const double ScriptMs = .5, UploadMs = 3, DecodeMs = 15, Budget = 4;

    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("Split loading: " + label);
        Console.WriteLine("PASS split loading: " + label);
    }

    private static void Spin(double ms)
    {
        long end = Stopwatch.GetTimestamp() + (long)(ms * Stopwatch.Frequency / 1000);
        while (Stopwatch.GetTimestamp() < end) { }
    }

    private sealed class Payload(Action freed) : IDisposable
    {
        private int done;
        public void Dispose() { if (Interlocked.Exchange(ref done, 1) == 0) freed(); }
    }

    // Counts native objects (uploaded files) and decoded buffers like the SDL player's resources.
    private sealed class Probe
    {
        public readonly int Thread = Environment.CurrentManagedThreadId;
        public int Native, Uploads, Decoded;
        public readonly ConcurrentBag<int> DecodeThreads = [];
        public readonly List<string> Loaded = [];
        private void Load(string path)
        {
            if (Environment.CurrentManagedThreadId != Thread) throw new Exception("Native asset uploaded off the game thread");
            Spin(UploadMs);
            Native++; Uploads++; Loaded.Add(Path.GetFileName(path));
        }
        public SceneResources Resources(bool decode) => new(Load, _ => Native--, Load, _ => Native--)
        {
            Decode = decode ? (path, _) =>
            {
                DecodeThreads.Add(Environment.CurrentManagedThreadId);
                System.Threading.Thread.Sleep((int)DecodeMs);
                Interlocked.Increment(ref Decoded);
                return new Payload(() => Interlocked.Decrement(ref Decoded));
            } : null,
            Upload = (path, _, _) => Load(path),
        };
    }

    internal static void Run(string root, SceneDocument first)
    {
        File.WriteAllText(Path.Combine(root, "Scripts", "SplitProbe.cs"), $$"""
            using System.Diagnostics;
            using Engine.Core;
            public sealed class SplitProbe : GameBehaviour
            {
                // A component whose construction takes {{ScriptMs}} ms of game-thread time.
                private readonly long built = Build();
                private static long Build()
                {
                    long end = Stopwatch.GetTimestamp() + Stopwatch.Frequency / {{(int)(1000 / ScriptMs)}};
                    while (Stopwatch.GetTimestamp() < end) { }
                    return end;
                }
                public override void OnCreate(ScriptLife life) => life.Session.Set("created " + life.ObjectId);
            }
            """);
        var key = Path.Combine(root, "Assets", "Key.png");
        var created = new List<string>();
        var images = new List<Guid>();
        for (int i = 0; i < Files; i++)
        {
            var path = Path.Combine(root, "Assets", $"Split{i}.png");
            File.Copy(key, path);
            var id = Guid.NewGuid();
            File.WriteAllText(path + ".meta", $"id: {id}");
            images.Add(id);
            created.Add(path);
        }
        var sound = ProjectAssets.Scan(root).IdFor("Assets/Unlock.wav")!.Value;
        var field = new SceneDocument();
        var camera = new SceneObject { Id = Guid.NewGuid(), Name = "Field camera", X = 400, Y = 225 };
        camera.AddComponent(new Camera2DComponent());
        field.Objects.Add(camera);
        SceneObject? group = null;
        for (int i = 0; i < Scripted; i++)
        {
            var obj = new SceneObject { Id = Guid.NewGuid(), Name = $"Crate {i}", X = 900 + i % 30 * 20, Y = i / 30 * 20, Width = 16, Height = 16 };
            if (i % 10 == 0) group = obj;
            else obj.SetParent(group);
            obj.AddComponent(new BoxCollider2DComponent());
            obj.AddComponent(new SpriteRendererComponent { AssetId = images[i % Files] });
            obj.AddComponent(new ScriptComponent { Script = "SplitProbe" });
            field.Objects.Add(obj);
        }
        var speaker = new SceneObject { Id = Guid.NewGuid(), Name = "Field speaker", X = 900, Y = 0 };
        speaker.AddComponent(new AudioSourceComponent { Sound = sound });
        field.Objects.Add(speaker);
        var scenePath = Path.Combine(root, "Scenes", "SplitField.scene");
        field.Save(scenePath);
        created.Add(scenePath);
        created.Add(Path.Combine(root, "Scripts", "SplitProbe.cs"));
        var fieldId = ProjectAssets.Scan(root).IdFor("Scenes/SplitField.scene")!.Value;
        try
        {
            CompareFinalState(first, fieldId);
            CheckBudget(first, fieldId);
            CheckCancel(first, fieldId);
        }
        finally
        {
            foreach (var path in created) { File.Delete(path); File.Delete(path + ".meta"); }
        }
    }

    private static (int Done, double Progress) Step(GameSession game, Probe probe, bool move = false)
    {
        probe.Uploads = 0;
        if (move) game.FixedUpdate(Vector2.UnitX, 1f / 60);
        else game.UpdateUi(default, 1.0 / 60);
        Thread.Sleep(1); // The rest of a frame, during which worker threads decode.
        return (probe.Uploads, game.State.PendingScene?.Progress ?? 1);
    }

    private static string Snapshot(GameSession game, Probe probe)
    {
        var world = game.Current;
        var rows = world.Objects.Select(o => world.TryGetPosition(o.Id, out var p) ? $"{o.Id} {o.Name} {o.ParentId} {p.X:F2},{p.Y:F2}" : $"{o.Id} {o.Name} -");
        var flags = world.Objects.Where(o => game.State.Has("created " + o.Id)).Count();
        return string.Join("\n", rows) + $"\ncreated {flags}\nloaded {string.Join(",", probe.Loaded.Distinct().Order())}\nscene {game.State.SceneId}";
    }

    // Synchronous LoadScene, the previous async staging (budget 0) and the split load end in the same world.
    private static void CompareFinalState(SceneDocument first, Guid fieldId)
    {
        string Load(int mode, out List<float> progress)
        {
            progress = [];
            var probe = new Probe();
            using var game = new GameSession(first);
            game.UseResources(probe.Resources(decode: mode == 2));
            game.State.SceneLoadBudgetMilliseconds = mode == 2 ? Budget : 0;
            if (mode == 0) game.State.LoadScene(fieldId);
            else game.State.LoadSceneAsync(fieldId);
            for (int i = 0; i < 2000 && (i == 0 || game.State.PendingScene != null); i++) progress.Add((float)Step(game, probe).Progress);
            if (game.State.SceneId != fieldId) throw new Exception("Split loading: the field was not entered");
            for (int i = 0; i < 3; i++) game.FixedUpdate(Vector2.Zero, 1f / 60);
            return Snapshot(game, probe);
        }
        string sync = Load(0, out _), staged = Load(1, out _), split = Load(2, out var steps);
        Check(sync == staged && sync == split && sync.Contains($"created {Scripted}"),
            "synchronous, one-step async and split loads produce the same objects, poses, OnCreate calls and resident files");
        Check(steps.Count > 20 && steps.Zip(steps.Skip(1)).All(p => p.First <= p.Second) && steps[^1] == 1,
            $"split replacement reports monotonic progress over {steps.Count} updates");
    }

    private static void CheckBudget(SceneDocument first, Guid fieldId)
    {
        var probe = new Probe();
        using var game = new GameSession(first);
        game.UseResources(probe.Resources(decode: true));
        game.State.SceneLoadBudgetMilliseconds = Budget;
        var player = first.Objects.Single(o => o.Name == "Player").Id;
        game.Current.TryGetPosition(player, out var before);
        var op = game.State.AddSceneAsync(fieldId);
        long tick = game.Time.TickCount;
        int updates = 0, maxBodies = 0, maxUploads = 0, lastDone = 0;
        bool ticked = true;
        while (!op.IsDone && updates < 5000)
        {
            var (uploads, progress) = Step(game, probe, move: true);
            updates++;
            ticked &= game.Time.TickCount == tick + updates;
            if (progress < .5f)
            {
                int done = (int)Math.Round((progress - .1f) / .4f * (Scripted + 2));
                maxBodies = Math.Max(maxBodies, done - lastDone);
                lastDone = Math.Max(lastDone, done);
            }
            maxUploads = Math.Max(maxUploads, uploads);
        }
        game.Current.TryGetPosition(player, out var after);
        Check(op.Status == SceneStatus.Completed && game.State.IsSceneLoaded(fieldId) && game.Current.Objects.Count(o => o.Name.StartsWith("Crate ")) == Scripted,
            "a split additive load completes");
        // 600 components of 0.5 ms are 300 ms of construction; 4 ms per update allows at most 8 per update.
        Check(maxBodies is > 0 and <= (int)(Budget / ScriptMs) + 1 && updates >= Scripted * ScriptMs / Budget,
            $"object creation stays within {Budget} ms per update (at most {maxBodies} components per update, {updates} updates)");
        Check(maxUploads is > 0 and <= 2, $"uploads of {UploadMs} ms stop once the {Budget} ms budget is spent (at most {maxUploads} per update)");
        Check(ticked && after.X > before.X, $"the running room keeps simulating every update while the field loads ({before.X} → {after.X})");
        for (int i = 0; i < 400 && probe.Decoded != 0; i++) Thread.Sleep(5);
        Check(probe.DecodeThreads.Any(t => t != probe.Thread) && probe.Decoded == 0 && game.State.SceneLoadBudgetMilliseconds == Budget,
            "files decode on worker threads and every decoded buffer is consumed");

        // Budget 0 keeps the previous staging: every component in one update, then one file per update.
        var legacy = new Probe();
        using var old = new GameSession(first);
        old.UseResources(legacy.Resources(decode: false));
        old.State.SceneLoadBudgetMilliseconds = 0;
        var add = old.State.AddSceneAsync(fieldId);
        var steps = new List<(int Uploads, double Progress)>();
        while (!add.IsDone && steps.Count < 5000) steps.Add(Step(old, legacy));
        var staged = steps.FindIndex(s => s.Progress >= .5);
        Check(add.Status == SceneStatus.Completed && staged >= 0 && steps.All(s => s.Uploads <= 1) &&
            steps.Count(s => s.Uploads == 1) >= Files && steps.Take(staged).All(s => s.Progress < .06),
            "budget 0 stages the whole scene in one update and uploads one file per update");
    }

    private static void CheckCancel(SceneDocument first, Guid fieldId)
    {
        var probe = new Probe();
        using var game = new GameSession(first);
        var resources = probe.Resources(decode: true);
        game.UseResources(resources);
        game.State.SceneLoadBudgetMilliseconds = Budget;
        int resident = resources.ResidentCount, native = probe.Native, objects = game.Current.Objects.Count;
        bool Settled()
        {
            for (int i = 0; i < 400 && (resources.DecodedCount != 0 || probe.Decoded != 0); i++) Thread.Sleep(5);
            return resources.DecodedCount == 0 && probe.Decoded == 0 && resources.ResidentCount == resident && probe.Native == native &&
                game.Current.Objects.Count == objects && !game.State.IsSceneLoaded(fieldId) && game.State.PendingScene == null;
        }
        foreach (var (label, stop) in new (string, Func<double, bool>)[]
        {
            ("while creating objects", p => p is > .2 and < .4),
            ("while decoding and uploading files", p => p is > .55 and < .9),
        })
        {
            var op = game.State.AddSceneAsync(fieldId);
            double progress = 0;
            for (int i = 0; i < 5000 && !stop(progress); i++) progress = Step(game, probe, move: true).Progress;
            if (!stop(progress)) throw new Exception("Split loading: did not reach the cancel point " + label);
            int decodedAtCancel = resources.DecodedCount;
            op.Cancel();
            Step(game, probe, move: true);
            Check(op.Status == SceneStatus.Canceled && Settled(),
                $"cancel {label} releases uploads and {decodedAtCancel} decoded buffers back to {resident} resident files");
        }
        var retry = game.State.AddSceneAsync(fieldId);
        for (int i = 0; i < 5000 && !retry.IsDone; i++) Step(game, probe);
        Check(retry.Status == SceneStatus.Completed && game.State.IsSceneLoaded(fieldId), "a canceled split load can be started again");
        game.State.UnloadScene(fieldId);
        Step(game, probe);
        Check(probe.Native == native && resources.ResidentCount == resident, "unloading the field returns to the room's resident files");
    }
}
