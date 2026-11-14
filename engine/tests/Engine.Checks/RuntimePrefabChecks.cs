using System.Numerics;
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
        Require(!Coins().Any(), "no preplaced coin");
        game.FixedUpdate(new GameInput(Vector2.Zero, true), Step);
        var coin = Coins().Single();
        Require(coin.GetComponent<BoxCollider2DComponent>()?.IsTrigger == true &&
            game.TryGetPosition(coin.Id, out var at) && at == new Vector2(384,126), "spawned trigger transform");
        game.FixedUpdate(new GameInput(Vector2.Zero, true), Step);
        Require(Coins().Single().Id == coin.Id && !game.State.Has("coin"), "second press does not spawn or collect");
        for (int i = 0; i < 30; i++) game.FixedUpdate(Vector2.UnitX, Step);
        Require(!Coins().Any() && game.State.Has("coin") && !game.TryGetPosition(coin.Id, out _) &&
            !game.Destroy(coin.Id), "pickup removed runtime object once");
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
        var lidA = game.Objects.Single(o => o.ParentId == a);
        var lidB = game.Objects.Single(o => o.ParentId == b);
        var script = game.Objects.Single(o => o.Id == a).GetComponent<ScriptComponent>()!;
        Require(game.TryGetPosition(lidA.Id, out var local), "child has a transform");
        Require(a != b && lidA.Id != lidB.Id && script.ReadOverride("Lid", Guid.Empty) == lidA.Id &&
            local == new Vector2(120,65), "fresh IDs, internal references and parent placement");
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
}
