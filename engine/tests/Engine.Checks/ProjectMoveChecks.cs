using Engine.Core;
using Engine.Editor;

internal static class ProjectMoveChecks
{
    // Use a disposable KeyRoom clone containing the extension, start scene and Coin prefab.
    internal static void Run(string root, string work)
    {
        string FileAt(string path) => Path.Combine(root, path);
        void Check(bool ok, string description)
        {
            if (!ok) throw new Exception("Project move: " + description);
            Console.WriteLine("PASS project move: " + description);
        }
        var file = FileAt("Scenes/KeyRoom.scene");
        File.AppendAllText(file, "\n# Keep this comment when undo restores the file.\n");
        var original = File.ReadAllBytes(file);
        var scene = SceneDocument.OpenForEditing(file);
        var history = new SceneHistory();
        history.Reset(scene);
        var assetId = ProjectAssets.Scan(root).IdFor("Assets/Door.png");
        var sidecar = File.ReadAllBytes(FileAt("Assets/Door.png.meta"));
        var bad = "Assets/DoorOpen3.png";
        try { ProjectPathEdit.Move(root, "Assets/Door.png", bad); throw new Exception("Replaced an existing PNG"); }
        catch (IOException) { Check(File.ReadAllBytes(file).SequenceEqual(original), "destination conflict leaves references intact"); }
        var png = ProjectPathEdit.Move(root, "Assets/Door.png", "Assets/door.png");
        scene.RestoreObjects(png.RewriteSnapshot(scene.CaptureObjects(), true));
        history.Record(scene, null, null, null, png);
        Check(ProjectAssets.Scan(root).IdFor("Assets/door.png") == assetId &&
            File.ReadAllBytes(FileAt("Assets/door.png.meta")).SequenceEqual(sidecar) && !history.IsDirty, "case-only rename keeps ID and sidecar");
        history.Restore(scene, false);
        Check(File.ReadAllBytes(file).SequenceEqual(original) && !history.CanUndo, "one Undo restores exact scene bytes and casing");
        history.Restore(scene, true);
        var editedDisk = SceneDocument.ReadAsset(file);
        editedDisk.Objects.Single(o => o.Name == "Wall").Name = "Edited Wall";
        ProjectAssets.Write(file, editedDisk);
        history.Restore(scene, false);
        Check(SceneDocument.ReadAsset(file).Objects.Any(o => o.Name == "Edited Wall"), "undo retains later unrelated disk edit");

        var scripts = ProjectPathEdit.Move(root, "Scripts", "Assets/Gameplay");
        var renamed = ProjectPathEdit.Move(root, "Assets/Gameplay/KeySequence.cs", "Assets/Gameplay/UnlockDoor.cs");
        var catalog = ProjectCatalog.Inspect(root);
        Check(!catalog.Problems.Any() && catalog.UsedBy("Assets/Gameplay/UnlockDoor.cs").Any(l => l.Source == "Scenes/KeyRoom.scene") &&
            catalog.UsedBy("Assets/Gameplay/CollectCoin.cs").Any(l => l.Source == "Assets/Coin.prefab"), "scene and prefab resolve the original C# types at the new source paths");
        var archive = Path.Combine(work, "relocated.monotemplate");
        WorkspaceArchives.Export(root, archive, WorkspaceArchives.Template);
        var clone = Directory.CreateDirectory(Path.Combine(work,"Relocated")).FullName;
        WorkspaceArchives.Install(archive, clone, WorkspaceArchives.Template);
        using (var session = new GameSession(SceneDocument.OpenForEditing(Path.Combine(clone, "Scenes/KeyRoom.scene"))))
            Check(!ProjectCatalog.Inspect(clone).Problems.Any(), "relocated script metadata survives template round trip and Play");
        renamed.Restore(false);
        scripts.Restore(false);
        Check(!Directory.EnumerateFiles(FileAt("Scripts"), "*.meta", SearchOption.AllDirectories).Any(), "Undo removes only generated script metadata");
    }
}
