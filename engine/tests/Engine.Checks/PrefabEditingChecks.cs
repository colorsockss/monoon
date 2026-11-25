using System.Numerics;
using Engine.Core;

internal static class PrefabEditingChecks
{
    [Check(9700)] static void Registered(CheckRun c) => Run(c.Scratch);
    internal static void Run(string project)
    {
        string Asset(string name)=>Path.Combine(project,"Assets",name+".prefab");
        var scene=SceneDocument.Load(Path.Combine(project,"Scenes","Showcase.scene"));
        var chest=scene.Objects.Single(o=>o.Name=="Chest");
        // The showcase chest body has no picture (its lid and open sprite are children); give it a renderer to carry the
        // per-property overrides of one component below.
        chest.AddComponent(new SpriteRendererComponent());
        var lid=scene.Objects.Single(o=>o.Name=="Chest Lid");
        var nestedId=lid.Id;
        scene.ExportPrefab(lid,Asset("CheckLid"));
        scene.ExportPrefab(chest,Asset("CheckChest"));
        var placed=scene.PlacePrefab("Assets/CheckChest.prefab",new(80,64));
        var placedLid=scene.Objects.Single(o=>o.Parent==placed && o.Name=="Chest Lid");
        Check(placedLid.Prefab?.AssetId==lid.Prefab!.AssetId,"placing a nested prefab preserves its link");
        placed.Width=56;
        placed.GetComponent<SpriteRendererComponent>()!.Order=7;
        placedLid.Rotation=15;
        scene.CreatePrefabVariant(placed,Asset("CheckVariant"));
        var variantId=placed.Prefab!.AssetId;
        var second=scene.PlacePrefab("Assets/CheckVariant.prefab",new(144,100));
        var secondId=second.Id;
        var secondLid=scene.Objects.Single(o=>o.Parent==second && o.Name=="Chest Lid").Id;
        second.GetComponent<SpriteRendererComponent>()!.Order=9;
        var copy=scene.DuplicateObject(second);
        var copyId=copy.Id;
        var saved=Path.Combine(project,"Scenes","CheckOverrides.scene");
        scene.Save(saved);
        // Change both levels of the source independently with instances already saved.
        lid.Height=14;
        scene.ExportPrefab(lid,Asset("CheckLid"),true);
        chest.GetComponent<SpriteRendererComponent>()!.SquareColor="#224488";
        scene.ExportPrefab(chest,Asset("CheckChest"),true);
        var reopened=SceneDocument.Load(saved);
        SceneObject Find(Guid id)=>reopened.Objects.Single(o=>o.Id==id);
        Check(Find(secondId).Width==56 && Find(secondId).GetComponent<SpriteRendererComponent>() is {Order:9,SquareColor:"#224488"} &&
              Find(secondId).WorldPosition==new Vector2(144,100),"variant and instance override separate properties of the same component");
        Check(Find(secondLid).Height==14 && Find(secondLid).Rotation==15 && Find(secondLid).Parent==Find(secondId),
            "nested source update propagates through a variant with stable child IDs");
        Check(Find(copyId).GetComponent<SpriteRendererComponent>() is {Order:9,SquareColor:"#224488"},"duplicated link keeps independent overrides");
        var clean=reopened.RevertPrefab(Find(secondId));
        Check(clean.GetComponent<SpriteRendererComponent>()!.Order==7 && clean.Width==56 && clean.X==144,"revert restores variant defaults and keeps scene placement");
        using(var game=new SceneGame(reopened))
        {
            var spawned=game.Instantiate(new(variantId),new(90,50));
            Check(game.Objects.Single(o=>o.Id==spawned).Width==56 && game.Objects.Single(o=>o.Parent?.Id==spawned && o.Name=="Chest Lid").Height==14,
                "runtime instantiation resolves the nested variant");
        }
        var sourceFile=Asset("CheckChest");
        var original=File.ReadAllBytes(sourceFile);
        // Adding an instance of the same source beneath its root would introduce a cycle.
        var recursive=reopened.PlacePrefab("Assets/CheckChest.prefab",new(0,0));
        reopened.Reparent(recursive,Find(chest.Id));
        bool rejected=false;
        try {reopened.ExportPrefab(Find(chest.Id),sourceFile,true);}
        catch(InvalidDataException){rejected=true;}
        Check(rejected && File.ReadAllBytes(sourceFile).SequenceEqual(original),"cyclic prefab update leaves the source intact");
        CheckPropertyEditing(reopened, Find(secondId), Find(copyId), "Assets/CheckVariant.prefab");
        var transform=new SelectionTransform([chest,lid]);
        var before=lid.WorldPosition;
        transform.Apply(Matrix3x2.CreateTranslation(16,32));
        Check(lid.WorldPosition==before+new Vector2(16,32) && lid.Id==nestedId,"parent and child receive a group transform exactly once");
        transform.Apply(Matrix3x2.Identity);
        Check(lid.WorldPosition==before,"cancel restores the original affine transform");
    }
    private static void CheckPropertyEditing(SceneDocument scene, SceneObject first, SceneObject sibling, string relative)
    {
        var project = scene.ProjectDirectory!;
        var stage = SceneDocument.OpenPrefabSource(project, relative);
        var root = stage.Document.Objects.Single(o => o.Id == stage.RootId);
        var childId = stage.Document.Objects.Single(o => o.Parent == root && o.Name == "Chest Lid").Id;
        root.Height = 38;
        SceneDocument.SavePrefabSource(stage);
        scene.RefreshPrefabs(ProjectAssets.Scan(project));
        SceneObject Current(Guid id) => scene.Objects.Single(o => o.Id == id);
        var local = Current(first.Id);
        var child = Current(local.Prefab!.Instances[childId]);
        Check(local.Height == 38 && Current(sibling.Id).Height == 38 && child.Parent == local,
            "isolated variant edits propagate and preserve child identity");
        child.Width = 45;
        child.Height = 32;
        PropertyOverride Field(SceneObject owner, string name) => scene.PropertyOverrides(Current(first.Id))
            .Single(p => p.SourceId == childId && p.Property.Field == name && p.Owner == owner.Name);
        scene.EditPrefabProperty(local, Field(child, "width"), true);
        local = Current(first.Id);
        child = Current(child.Id);
        var other = Current(Current(sibling.Id).Prefab!.Instances[childId]);
        Check(other.Width == 45 && other.Height != 32 && child.Height == 32 && child.Width == 45,
            "applying a child property updates siblings and preserves unrelated overrides");
        scene.EditPrefabProperty(local, Field(child, "height"), false);
        Check(Current(child.Id).Height == other.Height && Current(child.Id).Width == 45,
            "reverting one property preserves applied values and placement");
        var saved = Path.Combine(project, "Scenes", "IndividualOverrides.scene");
        scene.Save(saved);
        var reloaded = SceneDocument.Load(saved);
        Check(reloaded.Objects.Single(o => o.Id == child.Id) is { Width:45 } loaded && loaded.Parent?.Id == first.Id &&
              loaded.Height == other.Height && SceneDocument.ReadAsset(Path.Combine(project,relative)).Objects.Single(o=>o.Id==childId).Width==45,
            "scene and prefab reopen with matching selected properties");
        local = Current(first.Id);
        var script = local.GetComponent<ScriptComponent>()!;
        PropertyOverride LidField() => scene.PropertyOverrides(Current(first.Id)).Single(p => p.Property.Field == "Lid");
        script.Values["Lid"] = System.Text.Json.JsonSerializer.SerializeToElement(local.Id);
        scene.EditPrefabProperty(local, LidField(), true);
        Check(Current(sibling.Id).GetComponent<ScriptComponent>()!.Values["Lid"].GetGuid() == sibling.Id,
            "applying a script reference translates the instance ID through the source to each sibling");
        var sourceBytes = File.ReadAllBytes(Path.Combine(project,relative));
        local = Current(first.Id);
        local.GetComponent<ScriptComponent>()!.Values["Lid"] = System.Text.Json.JsonSerializer.SerializeToElement(sibling.Id);
        bool external = false;
        try { scene.EditPrefabProperty(local, LidField(), true); } catch (InvalidOperationException) { external = true; }
        Check(external && sourceBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(project,relative))),
            "a scene-only object reference cannot leak into a prefab source");
        bool stale = false;
        try { SceneDocument.SavePrefabSource(stage); } catch (IOException) { stale = true; }
        Check(stale && sourceBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(project,relative))),
            "a stale open source cannot overwrite a newer applied property");
    }

    private static void Check(bool passed,string message){if(!passed)throw new Exception(message);Console.WriteLine("PASS "+message);}
}
