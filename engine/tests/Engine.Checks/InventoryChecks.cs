using System.Numerics;
using Engine.Core;
using Engine.Editor;
using MoNoon.Inventory;

internal static class InventoryChecks
{
    [Check(1900, StopAfter = "inventory")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    internal static void Run(string repo, string scratch)
    {
        var sample = Path.Combine(repo, "samples", "InventoryLab");
        var target = Directory.CreateDirectory(Path.Combine(scratch, "InventoryImport")).FullName;
        var package = Path.Combine(repo, "packages", "Inventory.monopackage");
        var files = WorkspaceArchives.Install(package, target, WorkspaceArchives.Package);
        string[] expected = ["Assets/Inventory/Bag.prefab", "Assets/Inventory/Bag.prefab.meta",
            "Assets/Inventory/Items.table", "Assets/Inventory/Items.table.meta",
            "Scripts/Inventory/InventoryBag.cs", "Scripts/Inventory/InventoryContainer.cs"];
        Check(files.Select(f => Path.GetRelativePath(target, f).Replace(Path.DirectorySeparatorChar, '/')).Order().SequenceEqual(expected)
            && expected.All(f => File.ReadAllBytes(Path.Combine(sample, f)).SequenceEqual(File.ReadAllBytes(Path.Combine(target, f))))
            && WorkspaceArchives.Install(package, target, WorkspaceArchives.Package).Length == 0, "package imports matched sources and is idempotent");
        var sceneDir = Directory.CreateDirectory(Path.Combine(target, "Scenes")).FullName;
        var sceneFile = Path.Combine(sceneDir, "Camp.scene");
        var id = Guid.NewGuid();
        File.WriteAllText(sceneFile, $"schemaVersion: 14\nobjects:\n- id: {id}\n  name: Camera\n  width: 1\n  height: 1\n  components:\n  - type: camera2D\ndefaultCameraId: {id}\n");
        var scene = SceneDocument.OpenForEditing(sceneFile);
        var bagObject = scene.PlacePrefab("Assets/Inventory/Bag.prefab", Vector2.Zero);
        scene.Save(sceneFile);
        var reopened = SceneDocument.Load(sceneFile);
        using (var game = new GameSession(reopened))
        {
            game.FixedUpdate(Vector2.Zero, 1f / 60);
            Check(game.Current.HasScript(bagObject.Id, "Inventory/InventoryContainer") && !ProjectCatalog.Inspect(target).Problems.Any(),
                "imported prefab opens and plays with a resolved item table in an independent project");
        }

        var catalog = GameTable.Load(Path.Combine(target, "Assets", "Inventory", "Items.table"));
        var bag = new InventoryBag(catalog, 3);
        Check(bag.TryAdd("key") && bag.TryAdd("herb", 4) && bag[2] == new ItemStack("herb", 1), "pickup splits over stack limit");
        Check(!bag.TryAdd("herb", 3) && !bag.TryAdd("gem") && bag[2].Count == 1, "full inventory rejects the whole pickup");
        Check(bag.TryTake(0) && !bag.TryTake(0) && bag.TryAdd("herb") && bag[0].Count == 0 && bag[2].Count == 2,
            "partial stacks precede earlier empty slots");
        var saved = GameData.Parse(bag.Capture().ToYaml());
        Check(bag.TryAdd("gem", 2) && !bag.TryTake(0, 3) && bag[0] == new ItemStack("gem", 2), "freed slot accepts another item; insufficient quantity is preserved");
        bag.Restore(saved);
        Check(bag[0] == default && bag[1] == new ItemStack("herb", 3) && bag[2] == new ItemStack("herb", 2), "YAML round trip preserves slot order and holes");
        saved.Set("count2", 3.5);
        try { bag.Restore(saved); throw new Exception("Invalid stack was accepted"); }
        catch (InvalidDataException) { Check(bag[2].Count == 2, "invalid save leaves existing contents intact"); }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("Inventory: " + message);
        Console.WriteLine("PASS inventory: " + message);
    }
}
