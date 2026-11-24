using Engine.Core;

// Roadmap 10.4: structural overrides are applied to, or reverted from, the source one at a time.
internal static class PrefabStructureChecks
{
    [Check(9800)] static void Registered(CheckRun c) => Run(c.Scratch);
    internal static void Run(string project)
    {
        var path = Path.Combine(project, "Scenes", "PrefabWorkshop.scene");
        var prefab = Path.Combine(project, "Assets", "Chest.prefab");
        var scene = SceneDocument.OpenForEditing(path);
        SceneObject Named(SceneDocument doc, string name) => doc.Objects.Single(o => o.Name == name);
        StructuralOverride[] Changes(SceneDocument doc, string chest) => doc.StructuralOverrides(Named(doc, chest)).ToArray();
        SceneObject[] Locks(SceneDocument doc) => doc.Objects.Where(o => o.Name == "Lock").ToArray();
        void Edit(SceneDocument doc, string chest, StructureChange kind, bool apply) =>
            doc.EditPrefabStructure(Named(doc, chest), Changes(doc, chest).Single(c => c.Kind == kind), apply);

        var chest = Named(scene, "Chest 2");
        var lockObject = new SceneObject { Name = "Lock", X = 24, Y = 12, Width = 12, Height = 14 };
        lockObject.AddComponent(new SpriteRendererComponent { Order = 3, SquareColor = "#E8C547" });
        lockObject.SetParent(chest);
        scene.Objects.Add(lockObject);
        var sound = ProjectAssets.Scan(project).IdFor("Assets/Unlock.wav");
        chest.AddComponent(new AudioSourceComponent { Sound = sound });
        Check(Changes(scene, "Chest 2").Select(c => c.Kind).Order().SequenceEqual([StructureChange.AddedObject, StructureChange.AddedComponent]) &&
              Changes(scene, "Chest 1").Length == 0, "an added child and component are listed as two structural overrides");

        var sourceBefore = File.ReadAllBytes(prefab);
        var sceneBefore = scene.CaptureObjects();
        Edit(scene, "Chest 2", StructureChange.AddedObject, true);
        void Applied(SceneDocument doc, string step)
        {
            var locks = Locks(doc);
            Check(locks.Length == 3 && locks.Select(l => l.Parent!.Name).Order().SequenceEqual(["Chest 1", "Chest 2", "Chest 3"]) &&
                  locks.Any(l => l.Id == lockObject.Id) &&
                  doc.Objects.Where(o => o.GetComponent<AudioSourceComponent>() != null).Select(o => o.Name).SequenceEqual(["Chest 2"]) &&
                  Changes(doc, "Chest 2") is [{ Kind: StructureChange.AddedComponent, Component: "audioSource/" }] &&
                  Changes(doc, "Chest 1").Length == 0 && Changes(doc, "Chest 3").Length == 0,
                $"{step}: the lock reaches every chest while the sound stays on Chest 2");
        }
        Applied(scene, "apply one added child");
        Check(SceneDocument.ReadAsset(prefab).Objects.Count(o => o.Name == "Lock") == 1 &&
              SceneDocument.ReadAsset(prefab).Objects.All(o => o.GetComponent<AudioSourceComponent>() == null),
            "the source receives only the selected child");

        // The editor's Undo step restores the source file and the scene snapshot together.
        var sourceApplied = File.ReadAllBytes(prefab);
        var sceneApplied = scene.CaptureObjects();
        File.WriteAllBytes(prefab, sourceBefore);
        ProjectAssets.Scan(project);
        scene.RestoreObjects(sceneBefore);
        Check(Locks(scene).Select(l => l.Id).SequenceEqual([lockObject.Id]) && Changes(scene, "Chest 2").Length == 2,
            "undo restores the source and both instance overrides");
        File.WriteAllBytes(prefab, sourceApplied);
        ProjectAssets.Scan(project);
        scene.RestoreObjects(sceneApplied);
        scene.Save(path);
        var reopened = SceneDocument.OpenForEditing(path);
        Applied(reopened, "save and reopen");

        Edit(reopened, "Chest 2", StructureChange.AddedComponent, false);
        Check(reopened.Objects.All(o => o.GetComponent<AudioSourceComponent>() == null) && Changes(reopened, "Chest 2").Length == 0,
            "reverting the added component removes only the sound");
        var opener = new ScriptComponent { Script = "OpenChest" };
        opener.Values["Player"] = System.Text.Json.JsonSerializer.SerializeToElement(Named(reopened, "Floor").Id);
        Named(reopened, "Chest 2").AddComponent(opener);
        var appliedBytes = File.ReadAllBytes(prefab);
        bool leaked = false;
        try { Edit(reopened, "Chest 2", StructureChange.AddedComponent, true); } catch (InvalidOperationException) { leaked = true; }
        Check(leaked && appliedBytes.SequenceEqual(File.ReadAllBytes(prefab)), "an added component with a scene-only reference cannot reach the source");
        Edit(reopened, "Chest 2", StructureChange.AddedComponent, false);

        var thirdLock = Named(reopened, "Chest 3").Prefab!.Instances.Values.Select(id => reopened.Objects.Single(o => o.Id == id)).Single(o => o.Name == "Lock");
        reopened.RemoveObject(thirdLock);
        Check(Changes(reopened, "Chest 3") is [{ Kind: StructureChange.RemovedObject, Label: "Lock (removed object)" }], "a removed child is listed");
        Edit(reopened, "Chest 3", StructureChange.RemovedObject, false);
        Check(reopened.Objects.Single(o => o.Id == thirdLock.Id) is { Name: "Lock", Width: 12 } restored && restored.Parent!.Name == "Chest 3" &&
              Changes(reopened, "Chest 3").Length == 0, "reverting a removed child restores the same object under its parent");

        SceneObject Lid(string name) => reopened.Objects.Single(o => o.Name == "Chest Lid" && o.Parent!.Name == name);
        Lid("Chest 3").RemoveComponent<SpriteRendererComponent>();
        Check(Changes(reopened, "Chest 3") is [{ Kind: StructureChange.RemovedComponent, Label: "Chest Lid · spriteRenderer (removed)" }],
            "a removed component is listed");
        Edit(reopened, "Chest 3", StructureChange.RemovedComponent, false);
        Check(Lid("Chest 3").GetComponent<SpriteRendererComponent>() is { SquareColor: "#B07A3E", Order: 2 } && Changes(reopened, "Chest 3").Length == 0,
            "reverting a removed component restores its saved values");

        Lid("Chest 3").RemoveComponent<SpriteRendererComponent>();
        Edit(reopened, "Chest 3", StructureChange.RemovedComponent, true);
        Check(reopened.Objects.Where(o => o.Name == "Chest Lid").All(o => o.GetComponent<SpriteRendererComponent>() == null) &&
              SceneDocument.ReadAsset(prefab).Objects.Single(o => o.Name == "Chest Lid").Components.Any() == false,
            "applying a removed component updates the source and every chest");

        reopened.RemoveObject(reopened.Objects.Single(o => o.Name == "Lock" && o.Parent!.Name == "Chest 1"));
        Edit(reopened, "Chest 1", StructureChange.RemovedObject, true);
        Check(Locks(reopened).Length == 0 && SceneDocument.ReadAsset(prefab).Objects.All(o => o.Name != "Lock") &&
              new[] { "Chest 1", "Chest 2", "Chest 3" }.All(n => Changes(reopened, n).Length == 0),
            "applying a removed child deletes it from the source and every chest");

        var stray = new SceneObject { Name = "Note" };
        stray.SetParent(Named(reopened, "Chest 1"));
        reopened.Objects.Add(stray);
        Edit(reopened, "Chest 1", StructureChange.AddedObject, false);
        Check(reopened.Objects.All(o => o.Name != "Note"), "reverting an added child deletes it from the instance");
        reopened.Save(path);
        Check(SceneDocument.OpenForEditing(path) is var last && Locks(last).Length == 0 &&
              new[] { "Chest 1", "Chest 2", "Chest 3" }.All(n => Changes(last, n).Length == 0), "the final scene reopens without stale overrides");
    }

    private static void Check(bool passed, string message) { if (!passed) throw new Exception(message); Console.WriteLine("PASS " + message); }
}
