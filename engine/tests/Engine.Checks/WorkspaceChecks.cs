using System.IO.Compression;
using Engine.Core;
using Engine.Editor;

internal static class WorkspaceChecks
{
    [Check(4100, "workspace")] static void Registered(CheckRun c) => Run(c.Project, c.Scratch);
    internal static void Run(string keyRoom, string temp)
    {
        string workspace = Directory.CreateDirectory(Path.Combine(temp, "WorkspaceTests")).FullName;
        var source = Path.Combine(workspace, "Source");
        foreach (var path in Directory.EnumerateFiles(keyRoom, "*", SearchOption.AllDirectories))
        {
            string dest = Path.Combine(source, Path.GetRelativePath(keyRoom, path));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(path, dest);
        }
        var scene = SceneDocument.OpenForEditing(Path.Combine(source, "Scenes", "Showcase.scene"));
        var baseScene = scene.CaptureObjects();
        var player = scene.Objects.First(o => o.Name == "Player").Id;
        scene.Objects.Single(o => o.Id == player).X += 31;
        var mine = scene.CaptureObjects();
        scene.RestoreObjects(baseScene);
        scene.Objects.Single(o => o.Id == player).Name = "Disk Player";
        var disk = scene.CaptureObjects();
        var merged = SceneMerge.Combine(baseScene, mine, disk, ConflictChoice.Local);
        scene.RestoreObjects(merged.Snapshot);
        Check(merged.Conflicts.Count == 0 && scene.Objects.Single(o => o.Id == player) is { Name: "Disk Player", X: 308 }, "independent edits on the same object merge");
        scene.RestoreObjects(mine);
        scene.Objects.Single(o => o.Id == player).X++;
        var other = scene.CaptureObjects();
        foreach (var choice in Enum.GetValues<ConflictChoice>())
        {
            var conflict = SceneMerge.Combine(baseScene, mine, other, choice);
            scene.RestoreObjects(conflict.Snapshot);
            Check(conflict.Conflicts.Single().EndsWith("/x") && scene.Objects.Single(o => o.Id == player).X == (choice == ConflictChoice.Local ? 308 : 309),
                $"same-field conflict resolves to {choice}");
        }
        var history = new SceneHistory();
        history.Reset(scene);
        var settings = scene.Settings;
        scene.Settings = settings with { Accessibility = settings.Accessibility with { Locale = "ko" } };
        scene.Settings.Save(source);
        history.Record(scene, player, player, null);
        history.Restore(scene, false);
        Check(GameSettings.Load(source).Accessibility.Locale == settings.Accessibility.Locale, "settings undo updates the saved project");
        history.Restore(scene, true);
        var external = GameSettings.Load(source) with { GameTitle = "External" };
        external.Save(source);
        try { history.Restore(scene, false); throw new Exception("Undo overwrote external settings"); }
        catch (IOException) { Check(GameSettings.Load(source).GameTitle == "External" && history.CanUndo, "external settings edits survive Undo"); }
        settings.Save(source);
        using (var recovery = new SceneRecovery(source, "Scenes/Showcase.scene", baseScene))
        {
            recovery.Checkpoint(mine);
            Check(!SceneRecovery.Pending(source).Any(), "open session draft is excluded");
            recovery.Dispose();
            var orphan = SceneRecovery.Pending(source).Single();
            Check(orphan.Data.Baseline == baseScene && orphan.Data.Edited == mine, "orphan retains recovery and merge base");
            SceneRecovery.Remove(orphan.Path);
        }

        string extension = Path.Combine(source, "Editor", "KeyRoomTools.cs");
        var dependencies = WorkspaceArchives.Dependencies(source, [extension, Path.Combine(source, "Assets", "Showcase", "Coin.prefab")]);
        Check(dependencies.Contains("Editor/KeyRoomTools.cs") && !dependencies.Contains("Editor/KeyRoomTools.cs.meta") &&
            dependencies.Contains("Assets/Showcase/Coin.prefab") && dependencies.Contains("Assets/Showcase/CoinPile.png.meta") &&
            dependencies.Contains("Assets/CoinPulse.clip") && dependencies.Contains("Assets/Loot.table") && dependencies.Contains("Scripts/CollectCoin.cs") &&
            !dependencies.Contains("Scenes/Showcase.scene"),
            "package carries the editor extension source with the prefab, sprite, clip, table and scripts");
        var package = Path.Combine(workspace, "Coin.monopackage");
        WorkspaceArchives.Export(source, package, WorkspaceArchives.Package, dependencies);
        var destRoot = Directory.CreateDirectory(Path.Combine(workspace, "PackageTarget")).FullName;
        var added = WorkspaceArchives.Install(package, destRoot, WorkspaceArchives.Package);
        Check(added.Length == dependencies.Count && WorkspaceArchives.Install(package, destRoot, WorkspaceArchives.Package).Length == 0 &&
            File.Exists(Path.Combine(destRoot, "Editor", "KeyRoomTools.cs")) && !new ProjectScripts(destRoot).Locations().Values.Any(p => p.StartsWith("Editor/")),
            "install package and repeat with stable IDs; the extension stays out of the game scripts");
        var collision = Path.Combine(destRoot, "Assets", "Showcase", "Coin.prefab");
        File.AppendAllText(collision, "\n# External change\n");
        try { WorkspaceArchives.Install(package, destRoot, WorkspaceArchives.Package); throw new Exception("Package conflict accepted"); }
        catch (IOException) { Check(File.ReadAllText(collision).Contains("External change"), "conflicting package preserves existing file"); }

        string template = Path.Combine(workspace, "KeyRoom.monotemplate");
        WorkspaceArchives.Export(source, template, WorkspaceArchives.Template);
        var clone = Directory.CreateDirectory(Path.Combine(workspace, "Clone")).FullName;
        WorkspaceArchives.Install(template, clone, WorkspaceArchives.Template);
        var cloneScene = SceneDocument.OpenForEditing(Path.Combine(clone, "Scenes", "Showcase.scene"));
        var original = GameSettings.Load(source);
        var copied = GameSettings.Load(clone);
        using (var session = new GameSession(cloneScene))
        {
            var prior = session.Current.Inspect(player)!.Value.Position;
            session.FixedUpdate(System.Numerics.Vector2.UnitX, 1f / 60);
            Check(original.ProjectId != copied.ProjectId && original.StartScene == copied.StartScene &&
                ProjectCatalog.Inspect(clone).Problems.Count() == 0 && session.Current.Inspect(player)!.Value.Position.X > prior.X,
                "new template project has its own save identity, intact references and working KeyRoom Play");
        }
        var old = Path.Combine(clone, "Assets", "LidMotion.clip.yaml");
        var current = Path.ChangeExtension(old, null);
        File.Move(current, old);
        File.Move(current + ".meta", old + ".meta");
        string? backup = null;
        LegacyProject.Upgrade(clone, paths => backup = MigrationBackup.Create(clone, paths));
        using var zip = ZipFile.OpenRead(backup!);
        Check(zip.GetEntry("Assets/LidMotion.clip.yaml") != null && zip.GetEntry("Assets/LidMotion.clip.yaml.meta") != null &&
            File.Exists(current) && !File.Exists(old), "migration backs up original file and identity before conversion");
        ProjectMoveChecks.Run(clone, workspace);
        Directory.Delete(workspace, true);
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("Workspace: " + message);
        Console.WriteLine("PASS workspace: " + message);
    }
}
