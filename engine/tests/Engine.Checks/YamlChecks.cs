using System.Security.Cryptography;
using System.Text.Json;
using Engine.Core;

internal static class YamlChecks
{
    [Check(7400)] static void Registered(CheckRun c) => Run(c.Scratch);
    internal static void Run(string scratch)
    {
        CheckAssetNames(Path.Combine(scratch, "RenameTest"));
        var root = Directory.CreateDirectory(Path.Combine(scratch, "ImportTest")).FullName;
        var scenes = Directory.CreateDirectory(Path.Combine(root, "Scenes")).FullName;
        var fixture = Path.Combine(root, "Wall.prefab.json");
        var oldScene = Path.Combine(scenes, "First.scene.json");
        var sceneFile = Path.ChangeExtension(oldScene, null);
        var sceneId = Guid.NewGuid(); var asset = Guid.NewGuid();
        var source = Guid.NewGuid(); var instance = Guid.NewGuid();
        File.WriteAllText(fixture, $$"""
            {"schemaVersion":6,"objects":[{"id":"{{source}}","name":"Original","components":[{"type":"boxCollider2D"}]}]}
            """);
        var revision = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture)));
        File.WriteAllText(fixture + ".meta", asset.ToString());
        File.WriteAllText(oldScene + ".meta", sceneId.ToString());
        File.WriteAllText(oldScene, JsonSerializer.Serialize(new { schemaVersion = 6, objects = new[] { new {
            id = instance, name = "Local edit", components = Array.Empty<object>(),
            prefab = new { assetId = asset, instances = new Dictionary<Guid, Guid> { [source] = instance }, revision }
        } } }));
        File.WriteAllText(Path.Combine(root, "notes.json"), "unrelated");
        File.WriteAllText(sceneFile, "authored YAML");
        try { LegacyProject.Upgrade(root); throw new Exception("Migration overwrote an existing YAML file."); }
        catch (IOException) { }
        Require(File.ReadAllText(sceneFile) == "authored YAML" && File.Exists(oldScene) && File.Exists(fixture)
            && !File.Exists(Path.ChangeExtension(fixture, null)), "migration conflict preserves existing and legacy files");
        File.Delete(sceneFile);
        LegacyProject.Upgrade(root);
        var scene = SceneDocument.Load(sceneFile);
        var entry = ProjectAssets.Scan(root).Entries.Single(e => e.Id == asset);
        var obj = scene.Objects.Single();
        Require(scene.AssetId == sceneId && obj.Name == "Local edit" && obj.Id == instance && obj.Prefab?.Revision == entry.Hash
            && obj.Prefab.Instances[source] == instance, "import preserves asset IDs, prefab revisions and instance edits");
        Require(!File.Exists(oldScene) && !File.Exists(fixture) && File.ReadAllText(Path.Combine(root, "notes.json")) == "unrelated",
            "only migrated project files are replaced");
        var projectId = GameSettings.Load(root).ProjectId;
        var stamp = File.GetLastWriteTimeUtc(sceneFile);
        LegacyProject.Upgrade(root);
        Require(projectId.HasValue && projectId == GameSettings.Load(root).ProjectId, "legacy project gains a stable save identity");
        Require(File.GetLastWriteTimeUtc(sceneFile) == stamp, "opening a YAML project leaves files untouched");

        var component = new ScriptComponent { Script = "Example" };
        using var data = JsonDocument.Parse("""{"text":"00123","literal":"true","empty":"","nullText":"null","unicode":"열쇠: #1","flag":true,"number":1.25,"ref":{"Id":"abc"},"list":[null,false,42]}""");
        foreach (var field in data.RootElement.EnumerateObject()) component.Values.Add(field.Name, field.Value.Clone());
        obj.AddComponent(component);
        scene.Save(sceneFile);
        var restored = SceneDocument.Load(sceneFile).Objects.Single().GetComponent<ScriptComponent>()!;
        Require(component.Values.All(kv => JsonElement.DeepEquals(kv.Value, restored.Values[kv.Key])),
            "script field strings, numbers, lists, null and references retain their types after reopening");
        var camera = Guid.NewGuid();
        File.WriteAllText(sceneFile, $"schemaVersion: 6\nobjects:\n- id: {camera}\n  components: [{{ type: camera2D }}]\n");
        scene = SceneDocument.Load(sceneFile);
        Require(scene.Camera?.Width == 40 && scene.Camera.GetComponent<Camera2DComponent>()!.Zoom == 1, "hand-authored YAML uses engine defaults");
        File.AppendAllText(sceneFile, "---\nschemaVersion: 6\nobjects: []\n");
        try { SceneDocument.Load(sceneFile); throw new Exception("Extra YAML document was silently ignored."); }
        catch (InvalidDataException) { }
        Directory.Delete(root, true);
    }

    private static void CheckAssetNames(string root)
    {
        Directory.CreateDirectory(root);
        string At(string name) => Path.Combine(root, name);
        var clip = Guid.NewGuid(); var missing = Guid.NewGuid();
        const string contents = "# keep my notes\nframes: []\nloop: false\n";
        File.WriteAllText(At("Door.open.clip.yaml"), contents);
        File.WriteAllText(At("Missing.prefab.yaml.meta"), $"# keep my ID\nid: {missing}\n");
        File.WriteAllText(At("notes.yaml"), "Door.open.clip.yaml");
        File.WriteAllText(At("GameSettings.yaml"), "# settings\nwidth: 640\nheight: 360\n");
        ProjectAssets.Write(At(".monoon-assets.yaml"), new AssetEntry[] {
            new(clip, "Door.open.clip.yaml", "old hash"), new(missing, "Missing.prefab.yaml", "missing hash") });
        File.WriteAllText(At("Door.open.clip"), "different asset");
        try { LegacyProject.Upgrade(root); throw new Exception("Migration overwrote another clip."); }
        catch (IOException) { }
        Require(File.ReadAllText(At("Door.open.clip")) == "different asset" && File.Exists(At("Door.open.clip.yaml"))
            && !File.Exists(At("GameSettings")) && !File.Exists(At("Missing.prefab.meta")), "rename conflict leaves project untouched");
        File.Delete(At("Door.open.clip"));
        LegacyProject.Upgrade(root);
        var assets = ProjectAssets.Scan(root);
        Require(assets.PathFor(clip) == "Door.open.clip" && File.ReadAllText(At("Door.open.clip")) == contents
            && assets.PathFor(missing) == "Missing.prefab" && File.ReadAllText(At("Missing.prefab.meta")).StartsWith("# keep my ID")
            && GameSettings.Load(root).Width == 640 && File.ReadAllText(At("notes.yaml")) == "Door.open.clip.yaml"
            && !File.Exists(At(".monoon-assets.yaml")) && !File.Exists(At("Missing.prefab.yaml.meta")),
            "double-extension migration preserves YAML bytes, manifest-only IDs, absent assets and unrelated files");
        Directory.Delete(root, true);
    }

    private static void Require(bool result, string description)
    {
        if (!result) throw new Exception(description);
        Console.WriteLine("PASS " + description);
    }
}
