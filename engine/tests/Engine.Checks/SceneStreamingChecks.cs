using System.Numerics;
using Engine.Core;

internal static class SceneStreamingChecks
{
    [Check(7900)] static void Registered(CheckRun c) => Run(c.Scratch, c.Scene);
    internal static void Wait(GameSession game)
    {
        long deadline = Environment.TickCount64 + 5000;
        while (game.State.PendingScene is {} op)
        {
            if (Environment.TickCount64 >= deadline) throw new Exception("Scene operation timed out: " + op.Status);
            game.UpdateUi(default, GameTime.FixedStep);
            Thread.Sleep(1);
        }
    }

    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("Scene streaming: " + label);
        Console.WriteLine("PASS scene streaming: " + label);
    }

    internal static void Run(string root, SceneDocument first)
    {
        var player = first.Objects.Single(o => o.Name == "Player").Id;
        // A camera-less physics sector with a colliding authored object ID. Spawning
        // an unparented prefab in OnCreate checks script ownership across unload.
        var sectorPath = Path.Combine(root, "Scenes", "TestSector.scene");
        const float wallX = 330; // east of the hero, level with his feet box (y 196-208) and short of the chest's base at x 366
        var sector = new SceneDocument { Objects = [new() { Id = player, Name = "Sector wall", Tag = "Obstacle", Groups = ["Sector", " Sector "], X = wallX, Y = 180, Width = 30, Height = 40 }] };
        sector.Objects[0].AddComponent(new BoxCollider2DComponent());
        sector.Objects[0].AddComponent(new SpriteRendererComponent());
        sector.Objects[0].AddComponent(new ScriptComponent { Script = "SectorProbe" });
        File.WriteAllText(Path.Combine(root, "Scripts", "SectorProbe.cs"), """
            using System;
            using System.Numerics;
            using Engine.Core;
            public sealed class SectorProbe : GameBehaviour
            {
                public override void OnCreate(ScriptLife life)
                {
                    life.Session.Set("sectorCreated");
                    var asset = new DataReference(new Guid("ce012864-520c-4767-a2df-dd59fbdb5234"));
                    if (life.Objects.ReadData(asset).GetBool("reloaded")) life.Session.Set("sectorDataReloaded");
                    var coins = life.Objects.FindByGroup("수집품");
                    var id = life.Objects.Instantiate(new PrefabReference(new Guid("f1788efe-8407-5800-bcb7-030b9e45baf3")),new Vector2(650,220));
                    if (coins.Count == 1 && coins[0] == id && life.Objects.FindByGroup("Sector").Count == 1 &&
                        life.Objects.FindByTag("Obstacle")[0] == life.ObjectId) life.Session.Set("groupsCreated");
                }
                public override void OnDestroy(ScriptLife life)
                {
                    life.Session.Set("sectorDestroyed");
                    if (life.Objects.FindByGroup("수집품").Count == 0 && life.Objects.FindByTag("Obstacle").Count == 0)
                        life.Session.Set("groupsDetached");
                }
            }
            """);
        string sectorData = Path.Combine(root, "Assets", "SectorProbe.data");
        File.WriteAllText(sectorData, "version: 1\nvalues: {reloaded: false}");
        File.WriteAllText(sectorData + ".meta", "id: ce012864-520c-4767-a2df-dd59fbdb5234");
        sector.Save(sectorPath);
        // The guide's chest row shows the coin prefab's CoinPile.png, which the room itself does not draw until a coin
        // spawns: the one texture only the guide brings (its key icon, panel and buttons are shared with the room's HUD).
        var guidePath = Path.Combine(root, "Scenes", "ShowcaseGuide.scene");
        var guideText = File.ReadAllText(guidePath);
        File.WriteAllText(guidePath, guideText.Replace("sprite: 687b5216-5886-5d01-a874-9089c67ecb7b", "sprite: " + ProjectAssets.Scan(root).IdFor("Assets/Showcase/CoinPile.png")));
        var manifest = ProjectAssets.Scan(root);
        var sectorId = manifest.IdFor("Scenes/TestSector.scene")!.Value;
        var guide = manifest.IdFor("Scenes/ShowcaseGuide.scene")!.Value;
        var room = manifest.IdFor("Scenes/ShowcaseRoom2.scene")!.Value;
        var acquired = new List<string>();
        var released = new List<string>();
        bool rejectCoin = false;
        int thread = Environment.CurrentManagedThreadId;
        void Acquire(string path)
        {
            if (Environment.CurrentManagedThreadId != thread) throw new Exception("Native asset loaded off the game thread");
            string name = Path.GetFileName(path);
            if (name == "CoinPile.png" && rejectCoin) throw new InvalidDataException("Test image upload failure");
            acquired.Add(name);
        }
        void Release(string path) => released.Add(Path.GetFileName(path));
        using (var game = new GameSession(first))
        {
            var state = game.State;
            game.UseResources(new(Acquire,Release,Acquire,Release));
            var original = game.Current;
            var hud = original.Objects.Single(o => o.Name == "Controls button");
            var addition = state.AddSceneAsync(guide);
            var overlapping = state.AddSceneAsync(sectorId);
            Wait(game);
            Check(addition.Status == SceneStatus.Completed && overlapping.Status == SceneStatus.Failed && ReferenceEquals(original,game.Current) &&
                acquired.Count(n => n == "Key.png") == 1 && acquired.Count(n => n == "CoinPile.png") == 1, "additive UI shares the primary world and preloads only new textures");
            int count = original.Objects.Count;
            var duplicate = state.AddSceneAsync(guide);
            Wait(game);
            Check(duplicate.Status == SceneStatus.Completed && original.Objects.Count == count, "adding the same scene twice is idempotent");
            var close = game.Current.Objects.Single(o => o.Name == "Close guide");
            var box = original.Ui.Bounds(close.Id)!.Value;
            original.Ui.Pointer(true, box.X+8,box.Y+8);
            original.Ui.Pointer(false, box.X+8,box.Y+8);
            game.UpdateUi(default,.02);
            Check(!state.IsSceneLoaded(guide) && original.Ui.Bounds(close.Id) == null && original.Ui.Bounds(hud.Id) != null &&
                released.Contains("CoinPile.png") && !released.Contains("Key.png"), "close input removes the guide and releases its last texture reference");

            var physics = state.AddSceneAsync(sectorId); Wait(game);
            game.FixedUpdate(Vector2.Zero,1f/60);
            for (int i=0; i<22; i++) game.FixedUpdate(Vector2.UnitX,1f/60);
            Check(physics.Status == SceneStatus.Completed && original.TryGetPosition(player,out var stopped) && stopped.X == wallX - 22 &&
                original.Objects.Single(o => o.Name == "Sector wall").Id != player && state.Has("sectorCreated") &&
                original.Objects.Any(o => o.Name=="Coin"), "camera-less sector uses the host collision world, remaps duplicate IDs and spawns its own prefab");
            state.UnloadScene(sectorId); Wait(game);
            for (int i=0; i<12; i++) game.FixedUpdate(Vector2.UnitX,1f/60);
            Check(state.Has("groupsCreated") && state.Has("groupsDetached"), "group and tag indices are current inside additive lifecycle callbacks");
            Check(state.Has("sectorDestroyed") && original.TryGetPosition(player,out var continued) && continued.X > wallX &&
                !original.Objects.Any(o=>o.Name is "Coin" or "Sector wall"), "unload removes sector collision and unparented runtime children, preserving player state");

            File.WriteAllText(sectorData, "version: 1\nvalues: {reloaded: true}");
            state.AddSceneAsync(sectorId); Wait(game);
            game.FixedUpdate(Vector2.Zero,1f/60);
            Check(state.Has("sectorDataReloaded"), "unloading the final scene owner drops its cached YAML data");
            state.UnloadScene(sectorId); Wait(game);
            rejectCoin = true;
            try
            {
                original.Instantiate(new(manifest.IdFor("Assets/Showcase/Coin.prefab")!.Value), new(650,220));
                throw new Exception("Expected rejected prefab texture");
            }
            catch (InvalidDataException) { }
            Check(!original.Objects.Any(o => o.Name=="Coin") && original.FindByGroup("수집품").Count == 0,
                "failed runtime prefab preparation leaves no half-spawned object or group member");
            var failed = state.AddSceneAsync(guide); Wait(game);
            Check(failed.Status == SceneStatus.Failed && failed.Error!.Contains("upload") && !state.IsSceneLoaded(guide) &&
                original.Ui.Bounds(close.Id)==null && !released.Contains("Key.png"), "failed native preparation preserves active UI, scene, and shared resources");
            rejectCoin = false;
            var retry = state.AddSceneAsync(guide); Wait(game);
            Check(retry.Status==SceneStatus.Completed, "a failed scene can be retried");

            bool entered = false;
            var cancel = state.LoadSceneAsync(room,"Loading",1,_=>entered=true);
            game.UpdateUi(default,.01);
            long tick = game.Time.TickCount;
            game.Simulate(default,1f/60);
            Check(game.InputUi == null && game.Time.TickCount == tick, "replacement transition blocks simulation and UI input");
            cancel.Cancel(); game.UpdateUi(default,.01);
            Check(cancel.Status==SceneStatus.Canceled && !entered && game.Current==original && state.IsSceneLoaded(guide) && game.InputUi!=null, "cancel keeps the old world and resumes input");
            state.Set("key");
            var replace = state.LoadSceneAsync(room,"Loading",.1,_=>entered=true);
            Wait(game);
            Check(entered && replace.Status==SceneStatus.Completed && replace.Progress==1 && state.SceneId==room &&
                !state.IsSceneLoaded(guide) && state.Has("key") && released.Contains("Unlock.wav") && !released.Contains("Key.png"),
                "async replace commits callbacks, unloads all added scenes and keeps shared key texture");
            var missing = state.LoadSceneAsync(Guid.NewGuid()); Wait(game);
            Check(missing.Status==SceneStatus.Failed && state.SceneId==room, "missing scene preserves the committed room");
            var primary = state.UnloadScene(room); Wait(game);
            Check(primary.Status==SceneStatus.Failed && state.IsSceneLoaded(room), "primary scene cannot be removed without a replacement");
        }
        Check(released.Count(n=>n=="Key.png")==1, "session disposal releases last shared texture once");
        File.Delete(sectorData);
        File.Delete(sectorData + ".meta");
        File.Delete(sectorPath);
        File.Delete(sectorPath + ".meta");
        File.Delete(Path.Combine(root, "Scripts", "SectorProbe.cs"));
        File.WriteAllText(guidePath, guideText);
    }
}
