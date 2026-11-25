using Engine.Core;
using System.Numerics;

internal static class WorldCheckpointChecks
{
    [Check(8200)] static void Registered(CheckRun c) => Run(c.Scene);
    internal static void Run(SceneDocument scene)
    {
        var room = scene.AssetId!.Value;
        var root = scene.ProjectDirectory!;
        var chest = scene.Objects.Single(o => o.Name == "Chest");
        var player = scene.Objects.Single(o => o.Name == "Player");
        var tree = scene.Objects.Single(o => o.Name == "Pot" && o.Prefab != null);   // an authored prefab instance
        var doc = SceneDocument.Load(Path.Combine(root, "Scenes/Showcase.scene"));
        var path = Path.Combine(root, "Assets", "SavedChest.prefab");
        doc.ExportPrefab(doc.Objects.Single(o => o.Id == chest.Id), path);
        var manifest = ProjectAssets.Scan(root);
        var box = new PrefabReference(manifest.IdFor("Assets/SavedChest.prefab")!.Value);
        var loot = new PrefabReference(manifest.IdFor("Assets/Showcase/Coin.prefab")!.Value);
        using var session = new GameSession(scene);
        void Step() => session.FixedUpdate(Vector2.Zero, 1f / 60);
        var game = session.Current;
        var outer = game.Instantiate(box, new(700,320), player.Id);
        var lid = game.Objects.Single(o => o.ParentId == outer && o.Name == "Chest Lid");
        var inner = game.Instantiate(loot, new(725,346), outer);
        var other = game.Instantiate(loot, new(850,320));
        game.TrackForSave(inner);
        game.TrackForSave(outer);
        game.TrackForSave(tree.Id, WorldSaveFields.Existence);
        Step();
        game.Destroy(tree.Id);
        game.Destroy(lid.Id);
        game.SetColliderEnabled(inner, false);
        game.SetActive(inner, false);
        Require(game.TryMoveStep(player.Id, new(4,0)), "move the authored ancestor");
        Step();
        Require(game.TryGetPosition(inner, out var at), "nested instance position");
        var slot = new GameData();
        session.State.SaveWorld(slot);
        slot = GameData.Parse(slot.ToYaml());
        session.State.LoadSavedScene(room, slot);
        Step(); Step();
        game = session.Current;
        Require(game.HasRestoredWorld && game.Objects.Single(o => o.Id == inner).ParentId == outer &&
            game.Objects.Single(o => o.Id == outer).ParentId == player.Id &&
            game.TryGetPosition(inner, out var restored) && Vector2.Distance(restored, at) < .001f,
            "stable identity, nested instances and moving authored parent");
        Require(!game.IsActive(inner) && !game.IsColliderEnabled(inner) &&
            !game.Objects.Any(o => o.Id == tree.Id || o.Id == lid.Id || o.Id == other),
            "authored and member removals, selected properties and untracked transient");
        Require(game.Objects.Single(o => o.Id == outer).GetComponent<ScriptComponent>()!.ReadOverride("Lid", Guid.Empty) == lid.Id,
            "remapped script field keeps its instance ID");
        var broken = GameData.Parse(slot.ToYaml().Replace(box.Id.ToString(), Guid.NewGuid().ToString()));
        bool entered = false;
        string? failure = null;
        session.LoadFailed += message => failure = message;
        session.State.LoadSavedScene(room, broken, _ => entered = true);
        Step();
        Require(!entered && failure != null && session.Current == game && !game.IsColliderEnabled(inner),
            "invalid prefab leaves the playing scene and its state intact");
        var second = manifest.IdFor("Scenes/ShowcaseRoom2.scene")!.Value;
        session.State.LoadScene(second); Step(); Step();
        var anotherRoom = new GameData();
        session.State.SaveWorld(anotherRoom);
        using var restart = new GameSession(scene);
        restart.State.LoadSavedScene(second, GameData.Parse(anotherRoom.ToYaml()));
        restart.FixedUpdate(Vector2.Zero, .016f);
        restart.State.LoadScene(room);
        restart.FixedUpdate(Vector2.Zero, .016f);
        Require(restart.Current.HasRestoredWorld && restart.Current.TryGetPosition(inner, out var fromOther) &&
            Vector2.Distance(fromOther, at) < .001f, "another room's slot retains the visited room across restart");
        session.State.LoadSavedScene(room, new GameData()); Step(); Step();
        Require(!session.Current.HasRestoredWorld && session.Current.IsActive(tree.Id) &&
            !session.Current.Objects.Any(o => o.Id == inner), "legacy slot clears all remembered worlds");

        static void Require(bool ok, string message)
        {
            if (!ok) throw new Exception("World checkpoint: " + message);
            Console.WriteLine("PASS world checkpoint: " + message);
        }
    }
}
