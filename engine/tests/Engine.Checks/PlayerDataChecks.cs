using System.Numerics;
using Engine.Core;

internal static class PlayerDataChecks
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Console.WriteLine("PASS player data: " + message);
    }

    private static void Reject(Action read)
    {
        try { read(); } catch (InvalidDataException) { return; }
        throw new Exception("Expected invalid data");
    }

    internal static void Run(string scratch, SceneDocument scene)
    {
        string root = Path.Combine(scratch, "profile");
        var store = new PlayerStorage(root);
        var data = GameData.Parse("version: 1\nvalues:\n  list: [1, false]\n  nested: {name: test}\n");
        data.Set("name", "007");
        data.Set("sound", true);
        data.Set("volume", 0.75);
        store.SaveSlot(3, data);
        var loaded = store.LoadSlot(3, 1)!;
        Check(loaded.GetText("name") == "007" && loaded.GetBool("sound") && loaded.GetNumber("volume") == .75 &&
            !loaded.Values["list"][1].GetBoolean() && loaded.Values["nested"].GetProperty("name"u8).GetString() == "test", "nested YAML and typed values round trip");
        Reject(() => GameData.Parse("values: {}"));
        Reject(() => GameData.Parse("version: 1"));
        Reject(() => store.LoadSlot(3, 2));
        var upgraded = store.LoadSlot(3, 3, (from, item) => item.Set("step" + from, true))!;
        Check(upgraded.Version == 3 && upgraded.GetBool("step1") && upgraded.GetBool("step2") && store.LoadSlot(3, 1)!.Version == 1, "migration is sequential, explicit, and leaves the source intact");
        store.SaveSlot(3, upgraded);
        Reject(() => store.SaveSlot(3, data));
        using (var held = new FileStream(Path.Combine(root,".writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            try { store.SaveSlot(3, upgraded); throw new Exception("Expected lock contention"); }
            catch (IOException) { }
        }
        Check(store.LoadSlot(3,3)!.GetBool("step2"), "concurrent writer cannot replace a locked profile");
        store.SaveSettings(data);
        File.WriteAllText(Path.Combine(root, "slot-004.save"), "version: 0\nvalues: {}");
        Reject(() => store.SaveSlot(4, data));
        Check(File.ReadAllText(Path.Combine(root,"slot-004.save")).Contains("version: 0") && store.ListSlots().SequenceEqual([3,4]), "damaged and newer saves are preserved");
        store.DeleteSlot(4);
        Check(store.LoadSlot(4,1) == null && store.LoadSettings(1)!.GetText("name") == "007", "slot deletion leaves preferences intact");
        var rules = new DataReference(ProjectAssets.Scan(scratch).IdFor("Assets/KeyRoomRules.data")!.Value);
        var original = Path.Combine(scratch, "Assets", "KeyRoomRules.data");
        var renamed = Path.Combine(scratch, "Assets", "Rules2.data");
        File.Move(original, renamed);
        File.Move(original + ".meta", renamed + ".meta");
        using (var game = new GameSession(scene, null, null, store))
        {
            var copy = game.Current.ReadData(rules);
            copy.Set("chestReach", 0);
            Check(game.Current.ReadData(rules).GetNumber("chestReach") == 72, "data reference survives rename and cached values stay isolated");
        }
        File.Move(renamed, original);
        File.Move(renamed + ".meta", original + ".meta");
        using var session = new GameSession(scene, null, null, store);
        var loop = new GameLoop(session, session.Actions);
        void Tap(string key) { session.Actions.Press(key); session.Actions.Release(key); loop.Advance(GameTime.FixedStep); }
        Tap("E");
        Tap("N");
        var pending = session.Current.Objects.Single(o => o.Name == "Coin");
        var chest = scene.Objects.Single(o => o.Name == "Chest").Id;
        Check(pending.ParentId == chest && session.Current.TryGetPosition(pending.Id, out var moved) &&
            moved == new Vector2(400,126), "N moves the spawned coin 16 pixels under Chest");
        Tap("F5");
        Check(store.LoadSlot(1,2)!.GetBool("chest") && !store.LoadSlot(1,2)!.GetBool("coin"), "slot one saves an opened chest with pending loot");
        Tap("Right");
        for (int i = 0; i < 33; i++) session.FixedUpdate(Vector2.UnitX, 1f/60);
        Check(session.State.Has("coin"), "collected the pending coin");
        for (int i = 0; i < 58; i++) session.FixedUpdate(-Vector2.UnitX, 1f/60);
        for (int i = 0; i < 65; i++) session.FixedUpdate(Vector2.Zero, 1f/60);
        for (int i = 0; i < 120; i++) session.FixedUpdate(Vector2.UnitX, 1f/60);
        SceneStreamingChecks.Wait(session);
        Tap("2"); Tap("F5"); Tap("H");
        var second = ProjectAssets.ReadManifest(scene.ProjectDirectory!).IdFor("Scenes/SecondRoom.scene")!.Value;
        Check(session.SceneId == second && store.LoadSlot(2,2)!.GetText("room") == second.ToString() &&
            store.LoadSlot(2,2)!.GetBool("key") && store.LoadSettings(1)!.GetBool("showControls"), "second room and preferences use independent files");
        using var restart = new GameSession(scene, null, null, store);
        var next = new GameLoop(restart,restart.Actions);
        void Press(string key) { restart.Actions.Press(key); restart.Actions.Release(key); next.Advance(GameTime.FixedStep); next.Advance(GameTime.FixedStep); }
        Press("F9");
        Check(restart.State.Has("controls") && !restart.State.Has("coin") && restart.Current.Objects.Count(o => o.Name=="Coin")==1, "new session restores settings and exactly one uncollected coin");
        var restored = restart.Current.Objects.Single(o => o.Name == "Coin");
        Check(restored.Id == pending.Id && restored.ParentId == chest && restart.Current.TryGetPosition(restored.Id, out var pos) &&
            pos == new Vector2(400,126), "F9 restores the saved coin ID, parent and edited position after restart");
        Press("F9");
        Check(restart.Current.Objects.Count(o => o.Name=="Coin")==1, "repeated load cannot duplicate loot");
        for (int i = 0; i < 33; i++) restart.FixedUpdate(Vector2.UnitX, 1f/60);
        Press("F5"); Press("F9"); Press("F9"); Press("E");
        Check(restart.State.Has("coin") && !restart.Current.Objects.Any(o => o.Name == "Coin") &&
            !restart.Current.IsActive(scene.Objects.Single(o => o.Name == "Chest Lid").Id),
            "collected checkpoint keeps the chest open and the instance absent after repeated F9 and E");
        Press("2"); Press("F9");
        Check(restart.SceneId==second && restart.State.Has("coin") && restart.State.Has("key"), "second slot restores the second room and its progression");
        var legacyPending = new GameData { Version = 2 };
        legacyPending.Set("room", scene.AssetId!.Value.ToString());
        legacyPending.Set("key", false); legacyPending.Set("chest", true); legacyPending.Set("coin", false);
        store.SaveSlot(1, legacyPending);
        Press("1"); Press("F9");
        var migrated = restart.Current.Objects.Single(o => o.Name == "Coin");
        Check(migrated.ParentId == chest && restart.Current.TryGetPosition(migrated.Id, out var initial) &&
            initial == new Vector2(384,126), "v2 pending loot restores at its original location");
        Press("N"); Press("F5"); Press("F9");
        Check(restart.Current.TryGetPosition(migrated.Id, out var upgradedPosition) && upgradedPosition == new Vector2(400,126),
            "next explicit save adopts stable instance identity for legacy loot");
        var legacy = new GameData();
        legacy.Set("room", scene.AssetId!.Value.ToString());
        legacy.Set("hasKey", true);
        store.DeleteSlot(1);
        store.SaveSlot(1,legacy);
        Press("1"); Press("F9");
        var door = scene.Objects.Single(o => o.Name == "Door").Id;
        Check(restart.SceneId==scene.AssetId && restart.State.Has("key") && !restart.State.Has("chest") &&
            restart.Current.IsActive(door) && !restart.Current.IsColliderEnabled(door) &&
            restart.Current.GetComponent<SpriteRendererComponent>(door)?.Asset == "Assets/DoorOpen3.png", "legacy checkpoint migrates and reopens the unlocked door");
    }
}
