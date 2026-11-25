using System.Numerics;
using Engine.Core;

internal static class PlayerDataChecks
{
    [Check(8100)] static void Registered(CheckRun c) => Run(c.Scratch, c.Scene);
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
        Recovery(Path.Combine(scratch, "slot-recovery"));
        string root = Path.Combine(scratch, "profile");
        var store = new PlayerStorage(root);
        var logicalSize = new System.Drawing.Size(800, 450);
        var preferred = new DesktopPreferences(new(720, 540), true, false);
        Check(store.LoadDisplay(logicalSize) == new DesktopPreferences(logicalSize, false, true),
            "new desktop profile uses the logical resolution, windowed mode and VSync");
        store.SaveDisplay(preferred);
        Check(store.LoadDisplay(logicalSize) == preferred && store.LoadSettings(1) == null,
            "desktop options round trip independently of game settings");
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
        var hero = scene.Objects.Single(o => o.Name == "Player").Id;
        var chest = scene.Objects.Single(o => o.Name == "Chest").Id;
        Vector2 At(GameSession game) => game.Current.TryGetPosition(hero, out var p) ? p : throw new Exception("Missing player");
        // Walks one way until the condition holds; the showcase hero starts 88 px from the chest and 109 px from the key.
        void Walk(GameSession game, Vector2 direction, Func<bool> done, int limit = 200)
        {
            for (int i = 0; i < limit && !done(); i++) game.FixedUpdate(direction, 1f/60);
            if (!done()) throw new Exception("Player data: the walk did not arrive");
        }
        bool InReach(GameSession game) => game.Current.TryGetPosition(chest, out var c) && Vector2.Distance(At(game), c) <= 68;
        // The spawned coin (y 167-189) is above the chest's base: step north 16 px, then east into it.
        void Collect(GameSession game)
        {
            for (int i = 0; i < 4; i++) game.FixedUpdate(-Vector2.UnitY, 1f/60);
            Walk(game, Vector2.UnitX, () => game.State.Has("coin"));
        }
        Walk(session, Vector2.UnitX, () => InReach(session));
        Tap("E");
        Tap("N");
        var pending = session.Current.Objects.Single(o => o.Name == "Coin");
        Check(pending.ParentId == chest && session.Current.TryGetPosition(pending.Id, out var moved) &&
            moved == new Vector2(432,167), "N moves the spawned coin 16 pixels under Chest");
        Tap("F5");
        Check(store.LoadSlot(1,2)!.GetBool("chest") && !store.LoadSlot(1,2)!.GetBool("coin"), "slot one saves an opened chest with pending loot");
        Collect(session);
        Check(session.State.Has("coin"), "collected the pending coin");
        Walk(session, -Vector2.UnitX, () => session.State.Has("key"));
        for (int i = 0; i < 90; i++) session.FixedUpdate(Vector2.Zero, 1f/60);   // the door shot (1.4 s) returns to the room camera
        Walk(session, Vector2.UnitX, () => At(session).X >= 277);                 // under the north doorway, then through it
        Walk(session, -Vector2.UnitY, () => session.State.PendingScene != null || session.SceneId != scene.AssetId);
        SceneStreamingChecks.Wait(session);
        Tap("2"); Tap("F5"); Tap("H");
        var second = ProjectAssets.ReadManifest(scene.ProjectDirectory!).IdFor("Scenes/ShowcaseRoom2.scene")!.Value;
        Check(session.SceneId == second && store.LoadSlot(2,2)!.GetText("room") == second.ToString() &&
            store.LoadSlot(2,2)!.GetBool("key") && store.LoadSettings(1)!.GetBool("showControls"), "second room and preferences use independent files");
        using var restart = new GameSession(scene, null, null, store);
        var next = new GameLoop(restart,restart.Actions);
        void Press(string key) { restart.Actions.Press(key); restart.Actions.Release(key); next.Advance(GameTime.FixedStep); next.Advance(GameTime.FixedStep); }
        Press("F9");
        Check(restart.State.Has("controls") && !restart.State.Has("coin") && restart.Current.Objects.Count(o => o.Name=="Coin")==1, "new session restores settings and exactly one uncollected coin");
        var restored = restart.Current.Objects.Single(o => o.Name == "Coin");
        Check(restored.Id == pending.Id && restored.ParentId == chest && restart.Current.TryGetPosition(restored.Id, out var pos) &&
            pos == new Vector2(432,167), "F9 restores the saved coin ID, parent and edited position after restart");
        Press("F9");
        Check(restart.Current.Objects.Count(o => o.Name=="Coin")==1, "repeated load cannot duplicate loot");
        Collect(restart);
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
            initial == new Vector2(416,167), "v2 pending loot restores at its original location");
        Press("N"); Press("F5"); Press("F9");
        Check(restart.Current.TryGetPosition(migrated.Id, out var upgradedPosition) && upgradedPosition == new Vector2(432,167),
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
            restart.Current.GetComponent<SpriteRendererComponent>(door)?.Asset == "Assets/Showcase/DoorOpen.png", "legacy checkpoint migrates and reopens the unlocked door");
    }
    private static void Recovery(string root)
    {
        Directory.CreateDirectory(root);
        var store = new PlayerStorage(root);
        var path = Path.Combine(root, "slot-017.save");
        var backup = path + ".bak";
        var old = new GameData();
        old.Set("herbs", 3);
        var next = new GameData();
        next.Set("herbs", 2);

        // A pre-recovery save may include a BOM, comments and platform-specific line endings.
        File.WriteAllText(path, "# old save\r\nversion: 1\r\nvalues: {herbs: 3}\r\n", new System.Text.UTF8Encoding(true));
        byte[] original = File.ReadAllBytes(path);
        store.SaveSlot(17, next);
        byte[] second = File.ReadAllBytes(path);
        Check(File.ReadAllBytes(backup).SequenceEqual(original), "legacy slot is backed up byte-for-byte");
        File.WriteAllText(path, next.ToYaml().Replace("herbs: 2", "herbs: 9"));
        Check(store.LoadSlot(17, 1, out bool recovered)!.GetNumber("herbs") == 3 && recovered &&
            File.ReadAllBytes(path).SequenceEqual(original), "valid YAML with modified values restores the previous slot");
        store.LoadSlot(17, 1, out recovered);
        Check(!recovered, "the next load reads the repaired primary");

        File.Delete(path);
        Check(store.ListSlots().SequenceEqual([17]) && store.LoadSlot(17, 1, out recovered) != null && recovered,
            "a missing primary remains discoverable and can be restored");
        File.WriteAllText(path, "broken: [");
        store.SaveSlot(17, next);
        Check(File.ReadAllBytes(backup).SequenceEqual(original) && File.ReadAllBytes(path).SequenceEqual(second),
            "saving directly over a damaged primary preserves the good backup");

        var upgraded = next.UpgradeTo(2, (_, _) => { });
        store.SaveSlot(17, upgraded);
        byte[] newer = File.ReadAllBytes(path);
        Reject(() => store.LoadSlot(17, 1));
        Reject(() => store.SaveSlot(17, old));
        Check(File.ReadAllBytes(path).SequenceEqual(newer) && File.ReadAllBytes(backup).SequenceEqual(second),
            "valid newer primary never falls back or downgrades");

        // A process stopped after publishing the next checksums but before File.Replace.
        File.WriteAllBytes(path, second);
        File.WriteAllBytes(backup, original);
        Check(store.LoadSlot(17, 1, out recovered)!.GetNumber("herbs") == 2 && !recovered,
            "interrupted rotation still reads the previously committed primary");
        File.WriteAllBytes(path, []);
        Check(store.LoadSlot(17, 1)!.GetNumber("herbs") == 3, "interrupted rotation still verifies its old backup");

        File.WriteAllText(path, "broken primary");
        File.WriteAllText(backup, "broken backup");
        Reject(() => store.LoadSlot(17, 1));
        Reject(() => store.SaveSlot(17, old));
        Check(File.ReadAllText(path) == "broken primary" && File.ReadAllText(backup) == "broken backup",
            "two damaged copies are reported and preserved");
        store.DeleteSlot(17);
        Check(!Directory.EnumerateFiles(root, "slot-*").Any() && store.LoadSlot(17, 1) == null &&
            store.ListSlots().Length == 0, "deleting a slot removes the backup and checksums");
    }

}
