using System.Numerics;
using System.Text.Json;
using Engine.Core;

internal static class RuntimePrefabChecks
{
    const float Step = 1f / 60;
    static void Require(bool condition, string label)
    {
        if (!condition) throw new Exception("Runtime prefab: " + label);
    }

    internal static void Chest(SceneDocument scene)
    {
        using var game = new SceneGame(scene);
        var before = scene.CaptureObjects();
        IEnumerable<SceneObject> Coins() => game.Objects.Where(o => o.Name == "Coin");
        var collectibles = game.FindByGroup("수집품");
        var tagged = game.FindByTag("Coin");
        Require(!Coins().Any() && collectibles.Count == 0 && tagged.Count == 0, "no preplaced coin");
        for (int i = 0; i < 5; i++) game.FixedUpdate(Vector2.UnitX, Step);   // into the chest's reach
        game.FixedUpdate(new GameInput(Vector2.Zero, true), Step);
        var coin = Coins().Single();
        Require(collectibles.Single() == coin.Id && tagged.Single() == coin.Id &&
            game.FindByGroup("Coin").Count == 0 && game.FindByTag("coin").Count == 0, "live prefab group and exact tag");
        game.SetActive(coin.Id, false);
        Require(collectibles.Single() == coin.Id && !game.IsActive(coin.Id), "inactive members retain their classification");
        game.SetActive(coin.Id, true);
        Require(coin.GetComponent<BoxCollider2DComponent>()?.IsTrigger == true &&
            game.TryGetPosition(coin.Id, out var at) && at == new Vector2(416,167), "spawned trigger transform");
        // The chest rules (one coin, reach, pickup) are project tests: samples/KeyRoom/Tests/ChestTests.cs.
        for (int i = 0; i < 4; i++) game.FixedUpdate(-Vector2.UnitY, Step);   // level with the coin, above the chest's base
        for (int i = 0; i < 35; i++) game.FixedUpdate(Vector2.UnitX, Step);
        Require(!game.Destroy(coin.Id) && collectibles.Count == 0 && tagged.Count == 0, "held lists update on pickup");
        var reference = new PrefabReference(ProjectAssets.Scan(scene.ProjectDirectory!).IdFor("Assets/Showcase/Coin.prefab")!.Value);
        var first = game.Instantiate(reference, new(700, 230));
        var second = game.Instantiate(reference, new(710, 230));
        Require(collectibles.SequenceEqual(new[] { first, second }), "spawn order before the next tick");
        foreach (var id in collectibles)
            Require(game.Destroy(id) && !tagged.Contains(id), "destroy while iterating removes the tag immediately");
        Require(collectibles.Count == 0, "destroy while iterating removes both group members");
        Require(before == scene.CaptureObjects(), "Play did not modify authored objects");
        Console.WriteLine("PASS KeyRoom chest spawns one trigger; player collects it; scene unchanged");
    }

    internal static void Subtrees(SceneDocument scene, string project)
    {
        var source = scene.Objects.Single(o => o.Name == "Chest");
        var path = Path.Combine(project, "Assets", "TestChest.prefab");
        scene.ExportPrefab(source, path);
        var asset = new PrefabReference(ProjectAssets.Scan(project).IdFor("Assets/TestChest.prefab")!.Value);
        using var game = new SceneGame(scene);
        var player = scene.Objects.Single(o => o.Name == "Player");
        game.FixedUpdate(Vector2.UnitX, Step);
        var a = game.Instantiate(asset, new(120,70), player.Id);
        var b = game.Instantiate(asset, new(210,80));
        game.FixedUpdate(Vector2.Zero, Step);
        var lidA = game.Objects.Single(o => o.ParentId == a && o.Name == "Chest Lid");
        var lidB = game.Objects.Single(o => o.ParentId == b && o.Name == "Chest Lid");
        var script = game.Objects.Single(o => o.Id == a).GetComponent<ScriptComponent>()!;
        Require(game.TryGetPosition(lidA.Id, out var local), "child has a transform");
        Require(a != b && lidA.Id != lidB.Id && script.ReadOverride("Lid", Guid.Empty) == lidA.Id &&
            Vector2.Distance(local, new Vector2(120,78)) < .5f, "fresh IDs, internal references and parent placement");   // LidMotion has rocked it one tick
        game.SetActive(lidA.Id, false);
        game.SetActive(a, false);
        game.FixedUpdate(Vector2.UnitX, Step);
        Require(game.TryGetPosition(lidA.Id, out var carried) && carried == local + new Vector2(4,0),
            "inactive child retains local transform when parent moves");
        game.SetActive(a, true);
        Require(game.IsActive(a) && !game.IsActive(lidA.Id) && game.IsActive(lidB.Id), "independent child active state");
        game.Destroy(a);
        game.FixedUpdate(Vector2.Zero, Step);
        Require(!game.Objects.Any(o => o.Id == a || o.Id == lidA.Id) && game.IsActive(lidB.Id), "destroy removes only requested subtree");
        Console.WriteLine("PASS runtime prefab parent, internal references, active state and subtree lifetime");
    }

    // Instantiate reuses one parsed template per prefab file stamp; instances must still own their state.
    internal static void Cached(SceneDocument scene, string project)
    {
        var path = Path.Combine(project, "Assets", "TestChest.prefab");
        var asset = new PrefabReference(ProjectAssets.Scan(project).IdFor("Assets/TestChest.prefab")!.Value);
        var saved = File.ReadAllBytes(path);
        var stamp = File.GetLastWriteTimeUtc(path);
        try
        {
            using var game = new SceneGame(scene);
            SceneObject Get(Guid id) => game.Objects.Single(o => o.Id == id);
            Guid Lid(Guid id) => game.Objects.Single(o => o.ParentId == id && o.Name == "Chest Lid").Id;
            var a = game.Instantiate(asset, new(100, 70));
            var scriptA = Get(a).GetComponent<ScriptComponent>()!;
            scriptA.Values["Probe"] = JsonSerializer.SerializeToElement(1);
            var b = game.Instantiate(asset, new(200, 70));
            var scriptB = Get(b).GetComponent<ScriptComponent>()!;
            Require(!scriptB.Values.ContainsKey("Probe") && scriptA.ReadOverride("Lid", Guid.Empty) == Lid(a) &&
                scriptB.ReadOverride("Lid", Guid.Empty) == Lid(b), "cached instances keep their own script values and references");
            Require(Get(a).Components.Zip(Get(b).Components).All(p => !ReferenceEquals(p.First, p.Second)) &&
                Get(Lid(a)).Components.Zip(Get(Lid(b)).Components).All(p => !ReferenceEquals(p.First, p.Second)),
                "cached instances never share component records");
            var edited = SceneDocument.ReadAsset(path);
            edited.Objects.Single(o => o.Parent == null).Name = "Edited Chest";
            ProjectYaml.Write(path, edited);
            File.SetLastWriteTimeUtc(path, stamp.AddSeconds(2));
            var c = game.Instantiate(asset, new(300, 70));
            Require(Get(c).Name == "Edited Chest" && Get(b).Name == "Chest", "a changed prefab file is read again");
        }
        finally
        {
            File.WriteAllBytes(path, saved);
            File.SetLastWriteTimeUtc(path, stamp);
        }
        Console.WriteLine("PASS runtime prefab template cache keeps instances independent and rereads a changed file");
    }
}
