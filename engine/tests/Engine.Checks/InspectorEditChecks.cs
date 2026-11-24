using System.Text.Json.Nodes;
using Engine.Core;
using Engine.Editor;

// Roadmap 13.5 / 13.6 / 13.10 / 13.16 Inspector edit model checks (no UI). Group "inspector-edit".
internal static class InspectorEditChecks
{
    [Check(9500, "inspector-edit")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception("Inspector edit: " + message);
    }

    internal static void Run(string repository, string scratch)
    {
        var root = Path.Combine(scratch, "inspector-edit");
        TileChecks.Prepare(Path.Combine(repository, "samples", "KeyRoom"), root);
        FieldDiscovery();
        MultiEdit(root);
        OverrideRows(root);
        PngImportSettings(root);
        Console.WriteLine("PASS Inspector edit: built-in field keys, ten-wall multi-edit (width, Is Trigger, script number) with one Undo; oak width override reverted alone; PNG import pivot kept by reimport scans.");
    }

    // Default-valued fields have no key in a snapshot; discovery still finds their saved names.
    private static void FieldDiscovery()
    {
        var box = MultiEdits.Fields(typeof(BoxCollider2DComponent)).Select(f => f.Key).ToArray();
        Require(box.Contains("isTrigger") && box.Contains("layer") && !box.Contains("enabled"), "box collider keys " + string.Join(",", box));
        var sprite = MultiEdits.Fields(typeof(SpriteRendererComponent)).Select(f => f.Key).ToArray();
        Require(sprite.Contains("order") && sprite.Contains("flipX") && sprite.Contains("sortByY"), "sprite keys " + string.Join(",", sprite));
        var trigger = MultiEdits.Fields(typeof(BoxCollider2DComponent)).Single(f => f.Key == "isTrigger");
        Require(trigger.Label == "Is Trigger" && trigger.Node(typeof(BoxCollider2DComponent), false)?.GetValue<bool>() == false &&
            trigger.Node(typeof(BoxCollider2DComponent), true)?.GetValue<bool>() == true, "the saved form of each value");
        Require(MultiEdits.Fields(typeof(ScriptComponent)).Length == 0, "scripts use their definitions");
    }

    // WallWorkshop: ten linked walls. Width 76 → 80 (Transform), Is Trigger on (built-in JSON path) and a script number
    // (the "values" group) in one record each; one Undo restores the scene text and every wall's override list.
    private static void MultiEdit(string root)
    {
        var path = Path.Combine(root, "Scenes", "WallWorkshop.scene");
        var scene = SceneDocument.OpenForEditing(path);
        var walls = scene.Objects.Where(o => o.Name.StartsWith("Wall ", StringComparison.Ordinal)).ToArray();
        Require(walls.Length == 10, "ten walls");
        // A script number that differs between walls (the harness adds the same script file; values need no definition).
        for (int i = 0; i < walls.Length; i++)
            walls[i].AddComponent(new ScriptComponent { Script = "QaWall", Values = i % 3 == 0 ? [] : new() { ["Strength"] = System.Text.Json.JsonSerializer.SerializeToElement(i) } });
        var history = new SceneHistory();
        history.Reset(scene);
        var ids = walls.Select(o => o.Id).ToArray();
        var original = scene.CaptureObjects();
        var overrides = walls.Select(o => scene.PropertyOverrides(o).ToArray()).ToArray();
        var mixed = MultiEdits.Values(walls, "script/QaWall", "values", "Strength");
        Require(mixed.Distinct(JsonNodeEquality.Instance).Count() > 1, "script numbers start mixed");

        foreach (var w in walls) scene.Resize(w, 80, w.Height);
        Require(history.Record(scene, ids[^1], ids[^1], null, multiSelection: ids), "width record");
        Require(MultiEdits.Set(walls, "boxCollider2D/", null, "isTrigger", JsonValue.Create(true)), "is trigger");
        Require(!MultiEdits.Set(walls, "boxCollider2D/", null, "isTrigger", JsonValue.Create(true)), "same value again changes nothing");
        Require(history.Record(scene, ids[^1], ids[^1], null, multiSelection: ids), "trigger record");
        Require(MultiEdits.Set(walls, "script/QaWall", "values", "Strength", JsonValue.Create(7)), "script number");
        Require(history.Record(scene, ids[^1], ids[^1], null, multiSelection: ids), "script record");
        var edited = scene.CaptureObjects();
        Require(walls.All(w => w.Width == 80 && w.GetComponent<BoxCollider2DComponent>()!.IsTrigger &&
            w.GetComponent<ScriptComponent>()!.Values["Strength"].GetInt32() == 7), "every wall got the three values");
        Require(walls.All(w => scene.PropertyOverrides(w).Any(c => c.Property.Field == "width") &&
            scene.PropertyOverrides(w).Any(c => c.Property.Field == "isTrigger")), "prefab overrides recorded");

        // Undo the script edit alone, then the other two: each is one record.
        history.Restore(scene, false);
        Require(MultiEdits.Values(scene.Objects.Where(o => ids.Contains(o.Id)).OrderBy(o => Array.IndexOf(ids, o.Id)).ToArray(), "script/QaWall", "values", "Strength")
            .Zip(mixed).All(p => JsonNode.DeepEquals(p.First, p.Second)), "one Undo restores the mixed script numbers");
        history.Restore(scene, false);
        history.Restore(scene, false);
        Require(!history.CanUndo && scene.CaptureObjects() == original, "three Undo steps restore the scene text");
        var restored = ids.Select(id => scene.Objects.Single(o => o.Id == id)).ToArray();
        Require(restored.Select((w, i) => scene.PropertyOverrides(w).SequenceEqual(overrides[i])).All(v => v), "each wall's overrides restored");
        Require(history.MultiSelection(true)!.SequenceEqual(ids), "Redo keeps the selection");
        history.Restore(scene, true); history.Restore(scene, true); history.Restore(scene, true);
        Require(scene.CaptureObjects() == edited, "Redo gives the edited scene");
    }

    // 13.6: KeyRoom's two oaks. The Courtyard oak overrides name, width and height; the other has none. Reverting the
    // width row alone keeps the height override; one Undo brings the width override back.
    private static void OverrideRows(string root)
    {
        var scene = SceneDocument.OpenForEditing(Path.Combine(root, "Scenes", "KeyRoom.scene"));
        var courtyard = scene.Objects.Single(o => o.Name == "Courtyard oak");
        var plain = scene.Objects.Single(o => o.Name == "Oak tree" && o.Prefab != null);
        Require(!scene.PropertyOverrides(plain).Any(), "the plain oak has no overrides");
        var fields = scene.PropertyOverrides(courtyard).Select(c => c.Property.Field).ToArray();
        Require(fields.Contains("width") && fields.Contains("height"), "courtyard oak overrides " + string.Join(",", fields));
        var history = new SceneHistory();
        history.Reset(scene);
        var before = scene.CaptureObjects();
        var width = scene.PropertyOverrides(courtyard).Single(c => c.Property.Field == "width");
        scene.EditPrefabProperty(courtyard, width, apply: false);
        var reverted = scene.Objects.Single(o => o.Id == courtyard.Id);
        Require(reverted.Width == 102 && reverted.Height == 70, $"width reverted alone ({reverted.Width}x{reverted.Height})");
        Require(scene.PropertyOverrides(reverted).Any(c => c.Property.Field == "height") &&
            !scene.PropertyOverrides(reverted).Any(c => c.Property.Field == "width"), "only the width override is gone");
        Require(history.Record(scene, courtyard.Id, courtyard.Id, null), "one record");
        history.Restore(scene, false);
        Require(scene.CaptureObjects() == before && scene.PropertyOverrides(scene.Objects.Single(o => o.Id == courtyard.Id)).Any(c => c.Property.Field == "width"),
            "one Undo restores the width override");
    }

    // 13.16: the import pivot lives in the PNG's .meta, is left out at its default, and survives the reimport scans
    // (8.2 path) and an ID rewrite; a two-file atlas edit restores in reverse order.
    private static void PngImportSettings(string root)
    {
        var door = Path.Combine(root, "Assets", "Door.png");
        var metaFile = door + ".meta";
        var original = File.ReadAllBytes(metaFile);
        var meta = ProjectYaml.Read<AssetMeta>(metaFile);
        Require(meta.PivotX == null && !File.ReadAllText(metaFile).Contains("pivot"), "no pivot key at the default");
        ProjectYaml.Write(metaFile, meta with { PivotX = .5f, PivotY = 1 });
        SpriteSheetImporter.Scan(root, _ => { });
        ProjectAssets.Scan(root);
        var kept = ProjectYaml.Read<AssetMeta>(metaFile);
        Require(kept.PivotX == .5f && kept.PivotY == 1 && kept.Id == meta.Id, "pivot kept through the reimport scans");
        var a = Path.Combine(root, "Assets", "QaA.data");
        var b = Path.Combine(root, "Assets", "QaB.data");
        var beforeA = PrefabFileEdit.Read(a); File.WriteAllText(a, "version: 1\nvalues: {}\n");
        var beforeB = PrefabFileEdit.Read(b); File.WriteAllText(b, "version: 1\nvalues: {}\n");
        var both = new ProjectFileEdits([new PrefabFileEdit(a, beforeA, PrefabFileEdit.Read(a)), new PrefabFileEdit(b, beforeB, PrefabFileEdit.Read(b))]);
        both.Restore(false);
        Require(!File.Exists(a) && !File.Exists(b), "Undo removes both files");
        both.Restore(true);
        Require(File.Exists(a) && File.Exists(b), "Redo writes both again");
        File.WriteAllBytes(metaFile, original);
    }

    private sealed class JsonNodeEquality : IEqualityComparer<JsonNode?>
    {
        public static readonly JsonNodeEquality Instance = new();
        public bool Equals(JsonNode? a, JsonNode? b) => JsonNode.DeepEquals(a, b);
        public int GetHashCode(JsonNode? n) => n?.ToJsonString().GetHashCode() ?? 0;
    }
}
