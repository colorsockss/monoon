using System.Drawing;
using System.Numerics;
using System.Text.RegularExpressions;
using Engine.Core;

internal static class PrefabPoolChecks
{
    [Check(2100, "prefab-pool", StopAfter = "pool")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("Prefab pool: " + message);
        Console.WriteLine("PASS prefab pool: " + message);
    }

    internal static void Run(string repo, string scratch)
    {
        Reset(scratch);
        Barrage(repo);
    }

    private static void Reset(string scratch)
    {
        var root = Path.Combine(scratch, ".prefab-pool-checks");
        foreach (var folder in new[] { "Scenes", "Scripts", "Assets" }) Directory.CreateDirectory(Path.Combine(root, folder));
        File.WriteAllText(Path.Combine(root, "Scripts", "Shot.cs"), """
            using System;
            using Engine.Core;
            public sealed class Shot : GameBehaviour
            {
                public event Action Ping;
                private int touches, lives;
                public void Fire() => Ping?.Invoke();
                public override void OnCreate(ScriptLife l) => l.Session.Log("create");
                public override void OnEnable(ScriptLife l) => l.Session.Log("enable");
                public override void OnRent(ScriptLife l) { touches = 0; l.Session.Log($"rent {++lives}"); }
                public override void OnReturn(ScriptLife l) => l.Session.Log("return");
                public override void OnDisable(ScriptLife l) => l.Session.Log("disable");
                public override void OnDestroy(ScriptLife l) => l.Session.Log("destroy");
                public override void FixedUpdate(ScriptTick t) => t.Session.Log("tick");
                public override void OnTriggerEnter(ScriptContact c) => c.Session.Log($"shot enter {++touches}");
            }
            """);
        File.WriteAllText(Path.Combine(root, "Scripts", "Ear.cs"), """
            using Engine.Core;
            public sealed class Ear : GameBehaviour
            {
                [GameEventHandler] public void Hear(GameEventContext e) => e.Session.Log("heard");
            }
            """);
        File.WriteAllText(Path.Combine(root, "Scripts", "Wall.cs"), """
            using Engine.Core;
            public sealed class Wall : GameBehaviour
            {
                public override void OnTriggerEnter(ScriptContact c) => c.Session.Log("wall enter");
                public override void OnTriggerStay(ScriptContact c) => c.Session.Log("wall stay");
                public override void OnTriggerExit(ScriptContact c) => c.Session.Log("wall exit");
            }
            """);
        var source = new SceneDocument { Settings = new(), ProjectDirectory = root };
        var shot = new SceneObject { Name = "Shot", Tag = "Shot", Groups = ["탄"], Width = 10, Height = 10 };
        var tail = new SceneObject { Name = "Tail", X = 20, Y = 5, Width = 4, Height = 4 };
        tail.SetParent(shot);
        shot.AddComponent(new BoxCollider2DComponent { IsTrigger = true });
        shot.AddComponent(new ScriptComponent { Script = "Shot", EventLinks = [new GameEventLink("Ping", tail.Id, "Ear", "Hear")] });
        tail.AddComponent(new ScriptComponent { Script = "Ear" });
        source.Objects.AddRange([shot, tail]);
        source.ExportPrefab(shot, Path.Combine(root, "Assets", "Shot.prefab"));
        var prefab = new PrefabReference(shot.Prefab!.AssetId);

        var scene = SceneDocument.Empty(new());
        scene.ProjectDirectory = root;
        var wall = new SceneObject { Name = "Wall", X = 100, Y = 0, Width = 40, Height = 40 };
        wall.AddComponent(new BoxCollider2DComponent { IsTrigger = true });
        wall.AddComponent(new ScriptComponent { Script = "Wall" });
        var holder = new SceneObject { Name = "Holder", X = 300, Y = 300, Width = 1, Height = 1 };
        scene.Objects.AddRange([wall, holder]);
        using var game = new SceneGame(scene);
        var log = new List<string>();
        game.State.Logged += e => log.Add(e.Message);
        void Tick() => game.FixedUpdate(Vector2.Zero, 1f / 60);
        int Count(string message) => log.Count(m => m == message);
        Tick();
        var tagged = game.FindByTag("Shot");
        var grouped = game.FindByGroup("탄");

        var a = game.Rent(prefab, new(105, 5));
        var tailId = game.Objects.Single(o => o.ParentId == a).Id;
        Check(log.SequenceEqual(["create", "enable", "rent 1"]) && game.PoolStats(prefab) == new PrefabPoolStats(1, 0, 1, 0, 0) &&
            tagged.Single() == a && grouped.Single() == a, "an empty pool instantiates once and calls OnRent after OnEnable");
        log.Clear();
        Tick();
        Check(!log.Contains("tick") && !log.Contains("wall enter"), "a newly spawned instance waits one tick like Instantiate");
        Tick();
        Check(Count("tick") == 1 && Count("wall enter") == 1 && Count("shot enter 1") == 1, "rented collider enters the wall");
        log.Clear();
        Tick();
        Check(log.SequenceEqual(["tick", "wall stay"]) || log.SequenceEqual(["wall stay", "tick"]), "contact continues as Stay");

        // Disturb everything Return/Rent must reset.
        game.SetLocalTransform(tailId, new(new(-7, 30), 45, new(2, 2)));
        game.SetActive(tailId, false);
        game.SetColliderEnabled(a, false);
        game.SetColliderEnabled(a, true);
        game.TrackForSave(a);
        log.Clear();
        Check(game.Return(a) && log.SequenceEqual(["return", "disable"]) && !game.IsActive(a) && !game.IsActive(tailId) &&
            tagged.Count == 0 && grouped.Count == 0 && game.PoolStats(prefab) == new PrefabPoolStats(0, 1, 1, 0, 1),
            "Return calls OnReturn before OnDisable and leaves tag and group lists");
        Check(!game.Return(a) && !game.Return(wall.Id), "Return rejects an already returned or non-pooled object");
        log.Clear();
        Fire(game, a)();
        Check(log.Count == 0 && game.Physics.Overlap(Shape2D.Box(new(100, 0, 40, 40))).All(t => t.ObjectId != a),
            "a pooled instance neither sends events nor appears in physics queries");
        Tick();
        Check(log.SequenceEqual(["wall exit"]), "the remaining collider receives Exit after Return");
        Check(game.CaptureWorld()?.Prefabs.Length is null or 0, "a returned instance is not saved in a world checkpoint");

        log.Clear();
        var b = game.Rent(prefab, new(110, 10), holder.Id);
        Check(b == a && log.SequenceEqual(["enable", "rent 2"]) && game.PoolStats(prefab) == new PrefabPoolStats(1, 0, 1, 1, 1),
            "Rent reuses the returned instance without OnCreate");
        Check(game.TryGetParent(b, out var parent) && parent == holder.Id && game.TryGetPosition(b, out var at) && at == new Vector2(110, 10) &&
            game.TryGetLocalTransform(tailId, out var local) && local == new LocalTransform2D(new(20, 5), 0, Vector2.One) &&
            game.IsActive(tailId) && game.IsColliderEnabled(b) && tagged.Single() == b && grouped.Single() == b,
            "parent, transform, child active state, collider and tag/group membership are reset");
        log.Clear();
        Fire(game, b)();
        Check(log.SequenceEqual(["heard"]), "the reused instance publishes to its own prefab receiver once");
        log.Clear();
        Tick();
        Check(!log.Contains("wall enter") && !log.Contains("wall stay") && !log.Contains("tick"), "a reused instance also waits one tick");
        Tick();
        Check(Count("wall enter") == 1 && Count("shot enter 1") == 1 && Count("wall exit") == 0 && !log.Contains("wall stay"),
            "the next life starts a new contact and script state was reset in OnRent");

        // Return and rent again before the next contact pass: the old contact must not continue.
        log.Clear();
        Check(game.Return(b), "second return");
        var c = game.Rent(prefab, new(110, 10));
        Tick();
        Check(c == a && Count("wall exit") == 1 && !log.Contains("wall stay") && !log.Contains("wall enter"),
            "same-tick return and rent ends the old contact instead of continuing it");
        log.Clear();
        Tick();
        Check(Count("wall enter") == 1 && Count("shot enter 1") == 1, "the replacement contact begins on the following tick");

        game.Destroy(c);
        Tick();
        var d = game.Rent(prefab, new(400, 400));
        Check(d != a && game.PoolStats(prefab) is { Created: 2, Active: 1 }, "a destroyed rented instance is never handed out again");
        Check(game.Return(d) && game.Destroy(d), "a pooled instance can still be destroyed");
        Tick();
        var e = game.Rent(prefab, new(400, 400));
        Check(e != d && game.PoolStats(prefab) is { Created: 3, Available: 0 }, "a destroyed pooled instance is skipped");
    }

    private static Action Fire(SceneGame game, Guid id)
    {
        var instance = game.LiveScripts(id).Single();
        return instance.GetType().GetMethod("Fire")!.CreateDelegate<Action>(instance);
    }

    private sealed record Score(string Mode, int Ticks, int Fired, int Hits, int Exits, int Active, int Created, int Reused, long Bytes);

    private static Score RunBarrage(string repo, bool pooled)
    {
        var scene = SceneDocument.Load(Path.Combine(repo, "samples/BarrageLab/Scenes/Showcase.scene"));
        using var game = new GameSession(scene);
        string? report = null;
        game.State.Logged += e => report = e.Message;
        void Tick() => game.FixedUpdate(game.Actions.Take(), 1f / 60);
        void Press(string control)
        {
            game.Actions.Press(control);
            Tick();
            game.Actions.Release(control);
        }
        Press(pooled ? "R" : "P"); // On the first tick, before any bullet exists.
        for (int i = 1; i < 600; i++) Tick();
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 300; i++) Tick();
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Press("R");
        var m = Regex.Match(report!, @"mode=(\w+) ticks=(\d+) fired=(\d+) hits=(\d+) exits=(\d+) active=(\d+) created=(\d+) reused=(\d+)");
        int N(int g) => int.Parse(m.Groups[g].Value);
        var score = new Score(m.Groups[1].Value, N(2), N(3), N(4), N(5), N(6), N(7), N(8), bytes);
        Console.WriteLine("BarrageLab prefab bolts: " + score);
        return score;
    }

    private static void Barrage(string repo)
    {
        var pooled = RunBarrage(repo, true);
        var fresh = RunBarrage(repo, false);
        // The showcase ship fires one bolt every 7 ticks at three drifting drones.
        Check(pooled.Mode == "pool" && fresh.Mode == "instantiate" && pooled.Ticks == 900 && pooled.Fired == 128 &&
            pooled.Hits > 10 && pooled.Active > 5, "the BarrageLab ship fires 128 pooled bolts at moving drones");
        Check(pooled with { Mode = "", Created = 0, Reused = 0, Bytes = 0 } == fresh with { Mode = "", Created = 0, Reused = 0, Bytes = 0 } &&
            pooled.Hits == pooled.Exits, "reused bullets record the same hits, exits and live count as Instantiate/Destroy");
        Check(pooled.Reused > pooled.Created && fresh.Created == fresh.Fired && pooled.Created + pooled.Reused == pooled.Fired,
            "pooled bullets are reused after the warm-up");
        Check(pooled.Bytes * 4 < fresh.Bytes, $"steady-state allocation {pooled.Bytes:N0} B pooled vs {fresh.Bytes:N0} B instantiate/destroy");
    }
}
