using System.Text.Json;
using Engine.Core;

// Copy objects from one scene and paste them into the same scene and into another one.
internal static class ObjectClipboardChecks
{
    [Check(9900)] static void Registered(CheckRun c) => Run(c.Scratch);
    private static void Check(bool ok, string why) { if (!ok) throw new Exception("Object clipboard: " + why); Console.WriteLine("PASS " + why); }
    private static JsonElement Ref(Guid id) => JsonDocument.Parse($"\"{id}\"").RootElement.Clone();
    private static Guid Field(SceneObject obj, string script, string name) => obj.Components.OfType<ScriptComponent>().Single(s => s.Script == script).ReadOverride(name, Guid.Empty);

    internal static void Run(string project)
    {
        var library = ScriptLibrary.Open(project);
        var source = SceneDocument.Load(Path.Combine(project, "Scenes", "PrefabWorkshop.scene"));
        var target = SceneDocument.Load(Path.Combine(project, "Scenes", "SecondRoom.scene"));
        var chest = source.Objects.Single(o => o.Name == "Chest 1");
        var floor = source.Objects.Single(o => o.Name == "Floor");
        var rig = new SceneObject { Name = "Rig", X = 40, Y = 40 };
        var arm = new SceneObject { Name = "Rig Arm", X = 8 };
        source.Objects.Add(rig);
        source.Objects.Add(arm);
        source.Reparent(arm, rig, keepWorld: false);
        rig.AddComponent(new ScriptComponent { Script = "Pickup", Values = new() { ["Collector"] = Ref(chest.Id), ["Opens"] = Ref(floor.Id) } });
        var before = source.Objects.Select(o => o.Id).ToHashSet();

        var text = source.CopyObjects([arm, rig, chest]);
        Check(SceneDocument.IsObjectClipboard(text) && !SceneDocument.IsObjectClipboard("objects: []"), "clipboard text is marked as copied objects");

        var same = source.PasteObjects(text, null, library);
        var rigCopy = same.Single(o => o.Name == "Rig (1)");
        var chestCopy = same.Single(o => o.Name == "Chest 1 (1)");
        var copied = source.Objects.Where(o => same.Any(o.IsWithin)).ToArray();
        Check(same.Count == 2 && copied.Length == 4 && copied.All(o => !before.Contains(o.Id)) &&
              source.Objects.Select(o => o.Id).Distinct().Count() == source.Objects.Count, "a nested selection pastes each subtree once with new IDs");
        Check(source.Objects.Single(o => o.Parent == rigCopy).Name == "Rig Arm", "children keep their names under the pasted parent");
        Check(Field(rigCopy, "Pickup", "Collector") == chestCopy.Id && Field(rigCopy, "Pickup", "Opens") == floor.Id,
            "a reference into the copy follows the copy and one to the same scene stays");
        var lidCopy = source.Objects.Single(o => o.Parent == chestCopy);
        Check(chestCopy.Prefab?.AssetId == chest.Prefab!.AssetId && chestCopy.Prefab.Instances.Values.ToHashSet().SetEquals([chestCopy.Id, lidCopy.Id]), "the prefab link maps to the pasted instance and its child");

        var other = target.PasteObjects(text, null, library);
        var moved = other.Single(o => o.Name == "Rig");
        Check(Field(moved, "Pickup", "Collector") == other.Single(o => o.Name == "Chest 1").Id &&
              Field(moved, "Pickup", "Opens") == floor.Id && target.Objects.All(o => o.Id != floor.Id),
            "in another scene a reference to the source scene stays as a missing reference");
        var saved = Path.Combine(project, "Scenes", "ClipboardCheck.scene");
        target.Save(saved);
        var reopened = SceneDocument.Load(saved);
        var reloaded = reopened.Objects.Single(o => o.Name == "Rig");
        Check(Field(reloaded, "Pickup", "Opens") == floor.Id && reopened.Objects.Single(o => o.Name == "Chest 1").Prefab?.AssetId == chest.Prefab.AssetId,
            "the saved scene keeps the missing reference and the prefab link");
        using (var game = new SceneGame(reopened, library))
            for (int i = 0; i < 5; i++) game.FixedUpdate(default(GameInput), 1f / 60);
        Check(true, "a scene with a missing pasted reference runs");
        File.Delete(saved);

        foreach (var bad in new[] { "hello", SceneDocument.ObjectClipboardHeader + "\n: [", SceneDocument.ObjectClipboardHeader + "\nschemaVersion: 29\nobjects: []\n" })
        {
            int count = target.Objects.Count;
            bool rejected = false;
            try { target.PasteObjects(bad, null, library); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected && target.Objects.Count == count, "invalid clipboard text is rejected without changing the scene");
        }
    }
}
