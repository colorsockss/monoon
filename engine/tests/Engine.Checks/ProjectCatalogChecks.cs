using Engine.Core;

internal static class ProjectCatalogChecks
{
    [Check(7800)]
    static void Registered(CheckRun c) { c.CopyKeyRoom(); Run(c.Scratch); }
    internal static void Run(string project)
    {
        var test=Directory.CreateTempSubdirectory("monoon-catalog-");
        try
        {
            foreach(var folder in new[]{"Assets","Scenes","Scripts"})
                foreach(var source in Directory.EnumerateFiles(Path.Combine(project,folder),"*",SearchOption.AllDirectories))
                {
                    var dest=Path.Combine(test.FullName,Path.GetRelativePath(project,source));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);File.Copy(source,dest);
                }
            File.Copy(Path.Combine(project,GameSettings.FileName),Path.Combine(test.FullName,GameSettings.FileName));
            Verify(test.FullName);
        }
        finally { test.Delete(true); }
    }
    private static void Verify(string root)
    {
        void Check(bool ok,string msg){if(!ok)throw new Exception(msg);Console.WriteLine("PASS project: "+msg);}
        const string table="Assets/Loot.table",coin="Assets/Coin.prefab",scene="Scenes/KeyRoom.scene",rules="Assets/KeyRoomRules.data";
        var catalog=ProjectCatalog.Inspect(root);
        Check(catalog.UsedBy("Assets/DoorOpen2.png").Any(l => l.Source == scene && l.Field.EndsWith("KeySequence.Frames[1]")),
            "a script asset list reports the source field and index");
        Check(!catalog.Problems.Any(),string.Join("; ",catalog.Problems)+"KeyRoom has no unresolved saved references");
        Check(catalog.Uses(scene).Any(l=>l.Target==rules && l.Field.Contains("OpenChest.Rules")) && catalog.UsedBy(table).Any(l=>l.Source==coin && l.Field.Contains("LootTable")),"scene and prefab expose typed dependencies in both directions");
        Check(ProjectCatalog.Search(ProjectCatalog.Files(root),"t:sprite DOOR").Count()==5 && ProjectCatalog.Search(ProjectCatalog.Files(root),"t:script chest").Single()=="Scripts/OpenChest.cs","typed searches span project folders");
        string path=Path.Combine(root,table);
        var data=GameTable.Load(path);var before=File.ReadAllBytes(path);
        try{(data with{Rows=[data.Rows[0],data.Rows[0]]}).Save(path,true);throw new Exception("duplicate row accepted");}catch(InvalidDataException){}
        Check(before.SequenceEqual(File.ReadAllBytes(path)),"invalid table edits preserve the previous file");
        var id=ProjectAssets.Scan(root).IdFor(table);
        var outside=Directory.CreateTempSubdirectory("monoon-import-");
        try
        {
            var backup=Path.Combine(outside.FullName,"Loot.table");
            File.Move(path,backup);File.Copy(path+".meta",backup+".meta");
            var missing=ProjectCatalog.Inspect(root).Problems.Single(l=>l.Target==table);
            Check(missing.Source==coin && missing.Field.Contains("LootTable"),"deleted targets report source and field despite retained manifest ID");
            var bad=Path.Combine(outside.FullName,"broken.table");File.WriteAllText(bad,"version: -1\n");
            try{ProjectImport.Copy(root,"Assets",[backup,bad]);throw new Exception("invalid import accepted");}catch(InvalidDataException){}
            Check(!File.Exists(path),"failed batch does not publish its valid first asset");
            ProjectImport.Copy(root,"",[backup]);
            Check(ProjectAssets.Scan(root).IdFor(table)==id && !ProjectCatalog.Inspect(root).Problems.Any(),"restore imports reuse sidecars and clear missing links");
            var others=Path.Combine(root,"Assets","Tests");Directory.CreateDirectory(others);
            try{ProjectImport.Copy(root,"Assets/Tests",[backup]);throw new Exception("duplicate identity accepted");}catch(InvalidDataException){}
            Check(!Directory.EnumerateFiles(others).Any(),"conflicting import does not duplicate the source identity");
            Directory.Delete(others);
            var p=Path.Combine(root,coin);
            var content=File.ReadAllText(p);
            File.WriteAllText(p,content.Replace(id!.Value.ToString(),"not-a-guid"));
            Check(ProjectCatalog.Inspect(root).Problems.Any(l=>l.Source==coin && l.Problem=="Malformed reference"),"damaged typed script IDs are not treated as None");
            File.WriteAllText(p,content);
            var sceneFile=Path.Combine(root,scene);
            var doc=SceneDocument.Load(sceneFile);var placed=doc.PlacePrefab(coin,new(200,100));doc.Save(sceneFile);
            File.Move(p,p+".bak");
            var degraded=SceneDocument.OpenForEditing(sceneFile);
            Check(degraded.Objects.Any(o=>o.Id==placed.Id && o.Prefab!=null) && degraded.LoadNotice!=null && ProjectCatalog.Inspect(root).Problems.Any(l=>l.Source==scene && l.Target==coin),
                "editor reopens a scene with a missing prefab and preserves the saved instance for repair");
            File.Move(p+".bak",p);
            var typed=GameTable.Parse("version: 1\ncolumns: [{name: text, type: Text}, {name: cost, type: Number}, {name: enabled, type: Boolean}]\nrows: [{key: first, cells: [\"007\", 2.5, true]}]");
            string round=Path.Combine(outside.FullName,"round.table");typed.Save(round,false);var row=GameTable.Load(round).Row("first");
            Check(row.GetText("text")=="007" && row.GetNumber("cost")==2.5 && row.GetBool("enabled"),"typed keys and values round trip");
        }
        finally{outside.Delete(true);}
    }
}
