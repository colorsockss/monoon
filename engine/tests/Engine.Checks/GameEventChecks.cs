using System.Numerics;
using Engine.Core;

internal static class GameEventChecks
{
    [Check(10000)] static void Registered(CheckRun c) => Run(c.Scratch);
    internal static void Run(string scratch)
    {
        var root = Path.Combine(scratch, ".event-checks");
        foreach (var folder in new[] { "Scenes", "Scripts", "Assets" }) Directory.CreateDirectory(Path.Combine(root, folder));
        File.WriteAllText(Path.Combine(root, "Scripts", "Bell.cs"), """
            using System;
            using Engine.Core;
            public sealed class Bell : GameBehaviour
            {
                public event Action Ring;
                public void Fire() => Ring?.Invoke();
            }
            """);
        File.WriteAllText(Path.Combine(root, "Scripts", "Listener.cs"), """
            using Engine.Core;
            public sealed class Listener : GameBehaviour
            {
                [GameEventHandler] public void Hear(GameEventContext e) => e.Session.Log(e.ObjectId.ToString());
                [GameEventHandler] public void Remove(GameEventContext e) => e.Objects.Destroy(e.ObjectId);
            }
            """);
        var scene = SceneDocument.Empty(new());
        scene.ProjectDirectory = root;
        var bell = new SceneObject { Name = "Bell" };
        var child = new SceneObject { Name = "Receiver" };
        child.SetParent(bell);
        var link = new GameEventLink("Ring", child.Id, "Listener", "Hear");
        var script = new ScriptComponent { Script = "Bell", EventLinks = [link] };
        bell.AddComponent(script);
        child.AddComponent(new ScriptComponent { Script = "Listener" });
        scene.Objects.AddRange([bell,child]);
        var copy = scene.DuplicateObject(bell);
        var copiedChild = scene.Objects.Single(o => o.Parent == copy);
        Check(copy.GetComponent<ScriptComponent>()!.EventLinks![0].Target == copiedChild.Id, "duplicated hierarchy remaps the receiver");
        scene.RemoveObject(copiedChild);
        Check(copy.GetComponent<ScriptComponent>()!.EventLinks == null && script.EventLinks!.Length == 1, "deleting an authored target prunes only its own connections");
        scene.ExportPrefab(bell, Path.Combine(root, "Assets", "Bell.prefab"));
        var messages = new List<string>();
        Action Fire(SceneGame g, Guid id)
        {
            var instance = g.LiveScripts(id).Single();
            return instance.GetType().GetMethod("Fire")!.CreateDelegate<Action>(instance);
        }
        using var game = new SceneGame(scene);
        game.State.Logged += e => messages.Add(e.Message);
        void Tick() => game.FixedUpdate(Vector2.Zero, 1f/60);
        Tick();
        var fire = Fire(game,bell.Id);
        fire();
        Check(messages.SequenceEqual([child.Id.ToString()]), "generated sender dispatches once to the configured receiver");
        messages.Clear();
        game.SetActive(child.Id,false);
        fire();
        game.SetActive(child.Id,true);
        game.SetActive(child.Id,true);
        fire();
        Check(messages.SequenceEqual([child.Id.ToString()]), "disable/enable does not duplicate a handler");
        messages.Clear();
        var spawned = game.Instantiate(new(bell.Prefab!.AssetId),default);
        Tick();
        var spawnedChild = game.Objects.Single(o => o.Parent?.Id == spawned);
        var spawnedFire = Fire(game,spawned);
        spawnedFire();
        Check(messages.SequenceEqual([spawnedChild.Id.ToString()]), "spawned sender reaches only its own prefab receiver");
        messages.Clear();
        game.Destroy(child.Id);
        fire();
        game.Destroy(spawned);
        spawnedFire();
        Check(messages.Count == 0, "removed targets and retained delegates on destroyed senders are inert");
        game.Dispose();
        using var fresh = new SceneGame(scene);
        fresh.State.Logged += e => messages.Add(e.Message);
        fresh.FixedUpdate(Vector2.Zero, 1f/60);
        fire();
        Fire(fresh,bell.Id)();
        Check(messages.SequenceEqual([child.Id.ToString()]), "scene replacement reuses authored IDs without reviving old senders");

        messages.Clear();
        var additiveId = Guid.NewGuid();
        // Deliberately collide with the primary scene's authored GUIDs.
        var additive = new SceneDocument { ProjectDirectory = root };
        additive.RestoreObjects(scene.CaptureObjects());
        fresh.ResolveAdditiveIds(additive);
        using var incoming = new SceneGame(additive, null, fresh.State, null, additive: true);
        fresh.Attach(additiveId, incoming);
        fresh.FixedUpdate(Vector2.Zero, 1f/60);
        var additiveBell = additive.Objects.Single(o => o.Name == "Bell");
        var additiveChild = additive.Objects.Single(o => o.Parent == additiveBell);
        var additiveFire = Fire(fresh, additiveBell.Id);
        additiveFire();
        fresh.Detach(additiveId);
        additiveFire();
        Fire(fresh,bell.Id)();
        Check(messages.SequenceEqual([additiveChild.Id.ToString(),child.Id.ToString()]),
            "additive ID collisions and unload isolate primary and incoming event connections");

        var defs = scene.OpenScripts(root).Current().Components;
        script.EventLinks = [link with { Method = "RemovedMethod" }];
        Check(script.EventErrors(defs["Bell"], scene.Objects, defs).Single().Contains("RemovedMethod"),
            "an obsolete handler is reported during content validation");
        messages.Clear();
        script.EventLinks = [link with { Method = "Remove" },link];
        using var chain = new SceneGame(scene);
        chain.State.Logged += e => messages.Add(e.Message);
        chain.FixedUpdate(Vector2.Zero, 1f/60);
        Fire(chain,bell.Id)();
        Check(messages.Count == 0 && !chain.IsActive(child.Id), "destroying a receiver in one handler cancels its remaining callbacks");
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Console.WriteLine($"PASS game events: {message}");
    }
}
