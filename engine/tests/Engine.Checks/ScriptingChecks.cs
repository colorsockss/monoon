using System.Numerics;
using System.Text.Json;
using Engine.Core;

internal static class ScriptingChecks
{
    [Check(6200)] static void Registered(CheckRun c) => Run(c.Scratch);
    internal static void Run(string scratch)
    {
        var root = Directory.CreateDirectory(Path.Combine(scratch,"Lifecycle")).FullName;
        var dir = Directory.CreateDirectory(Path.Combine(root,"Scripts")).FullName;
        var common = Path.Combine(dir,"Shared.Tools.cs");
        File.WriteAllText(common,"namespace Rules; public static class Defaults { public const int Tick = 17; }");
        File.WriteAllText(Path.Combine(dir,"Probe.cs"), """
            using Engine.Core;
            using Rules;
            namespace Game;
            public sealed class Probe : GameBehaviour
            {
                public string Label = "";
                public int Marker = Defaults.Tick;
                public ComponentReference<Probe> Peer;
                public ComponentReference<BoxCollider2DComponent> Collider;
                public enum Side : short { Left = -2, Right = 19 }
                public Side Direction = Side.Right;
                public System.Numerics.Vector2 Shift = new(-3.5f, 7);
                public System.Numerics.Vector2 Origin = new() { Y = -2 };
                public SpriteReference[] Frames = [];
                public PrefabReference[] Prefabs;
                public TableReference[] Tables;
                public DataReference[] Items;
                public SceneReference[] Rooms;
                public System.Guid[] Targets = [];
                public System.Guid[] OptionalTargets;
                public static string Log = "";
                private string history = "";
                public string History => history;
                private void Record(string s) { history += s; Log += Label + s + " "; }
                public override void OnCreate(ScriptLife life)
                {
                    if (Peer.ObjectId != default && (Peer.Resolve(life.Objects)?.Label != "C" ||
                        Collider.Resolve(life.Objects)?.IsTrigger != true)) throw new System.Exception("Peer was not ready at creation");
                    Record("C");
                    if (Label=="RemoveAtBirth") life.Objects.Destroy(life.ObjectId);
                }
                public override void OnEnable(ScriptLife life) => Record("+");
                public override void OnDisable(ScriptLife life) => Record("-");
                public override void OnDestroy(ScriptLife life) => Record("X");
                public override void FixedUpdate(ScriptTick tick) => Record("T");
            }
            """);
        var library = new ProjectScripts(root);
        void Check(bool result,string what) { if (!result) throw new Exception("Scripting: "+what); }
        Check(library.Components().SequenceEqual(["Probe"]), "shared files are not attachable");
        Check((int?)library.Get("Probe").Fields.Single(f => f.Name=="Marker").Default==17,"shared constant in Inspector");
        File.WriteAllText(common, File.ReadAllText(common).Replace("17","18"));
        Check((int?)library.Get("Probe").Fields.Single(f => f.Name=="Marker").Default==18,"helper edits invalidate project");
        var scene = SceneDocument.Empty(new()); scene.ProjectDirectory = root;
        var parent = new SceneObject { Name="Parent" };
        var child = new SceneObject { Name="Child" };
        var pc = new ScriptComponent { Script="Probe" }; var cc = new ScriptComponent { Script="Probe" };
        pc.Values["Label"]=JsonSerializer.SerializeToElement("P"); cc.Values["Label"]=JsonSerializer.SerializeToElement("C");
        pc.Values["Peer"]=pc.Values["Collider"]=JsonSerializer.SerializeToElement(child.Id);
        pc.WriteReferences("Targets", [child.Id, Guid.Empty, parent.Id, child.Id]);
        parent.AddComponent(pc); child.AddComponent(cc); child.AddComponent(new BoxCollider2DComponent { IsTrigger=true });
        scene.Objects.AddRange([parent,child]); scene.Reparent(child,parent);
        var fields = library.Get("Probe").Fields;
        var direction = fields.Single(f => f.Name == "Direction");
        Check(direction.EnumNames!.SequenceEqual(["Left","Right"]) && Equals(direction.Default,"Right") &&
            Equals(fields.Single(f => f.Name == "Shift").Default,new Vector2(-3.5f,7)) &&
            Equals(fields.Single(f => f.Name == "Origin").Default,new Vector2(0,-2)),
            "Inspector reflects explicit enum values and Vector2 constructor defaults");
        Check(fields.Count(f => f.IsAssetList) == 5, "all five script asset reference types support lists");
        // Even a coincident scene object GUID must remain unchanged in an asset list.
        foreach (var field in fields.Where(f => f.IsAssetList)) pc.WriteReferences(field.Name, [child.Id,parent.Id]);
        pc.Values["Direction"] = JsonSerializer.SerializeToElement("Left");
        pc.Values["Shift"] = GameData.ParseValue("{x: 21.5, y: -9}");
        var colliderField = library.Get("Probe").Fields.Single(f => f.Name=="Collider");
        Check(colliderField.Accepts(child) && !colliderField.Accepts(parent),"filter by required target component");
        var duplicate = scene.DuplicateObject(parent,library);
        var other = scene.Objects.Single(o => o.ParentId==duplicate.Id);
        var dupScript = duplicate.GetComponent<ScriptComponent>()!;
        Check(dupScript.ReadReferences("Targets", true).SequenceEqual([other.Id, Guid.Empty, duplicate.Id, other.Id]),
            "duplicating a hierarchy remaps each object reference in order, including repeats and empty slots");
        Check(dupScript.ReadOverride("Peer",Guid.Empty)==other.Id && dupScript.ReadOverride("Collider",Guid.Empty)==other.Id,"duplicate remaps typed refs");
        Check(dupScript.ReadReferences("Frames",false).SequenceEqual([child.Id,parent.Id]),
            "duplicating a hierarchy preserves ordered asset IDs");
        scene.RemoveObject(other); scene.RemoveObject(duplicate);
        var assetDir = Directory.CreateDirectory(Path.Combine(root,"Assets"));
        scene.ExportPrefab(parent,Path.Combine(assetDir.FullName,"Pair.prefab"));
        var asset = new PrefabReference(ProjectAssets.Scan(root).IdFor("Assets/Pair.prefab")!.Value);
        var game = new GameSession(scene,library); var world=game.Current;
        var p = world.GetComponent<GameBehaviour>(parent.Id)!; var c = world.GetComponent<GameBehaviour>(child.Id)!;
        Check(p.GetType().GetField("OptionalTargets")!.GetValue(p) == null,
            "an unedited public array keeps its C# initializer");
        var type = p.GetType();
        foreach (var field in fields.Where(f => f.IsAssetList))
        {
            var values = (Array)type.GetField(field.Name)!.GetValue(p)!;
            var id = field.Type.GetElementType()!.GetProperty("Id")!;
            Check(values.Cast<object>().Select(v => (Guid)id.GetValue(v)!).SequenceEqual([child.Id,parent.Id]),
                $"generated factory preserves both {field.Name} asset IDs");
        }
        Check(type.GetField("Direction")!.GetValue(p)!.ToString()=="Left" &&
            (Vector2)type.GetField("Shift")!.GetValue(p)! == new Vector2(21.5f,-9),
            "generated factory binds ordered assets, enum name and vector");
        string History(GameBehaviour b) => (string)b.GetType().GetProperty("History")!.GetValue(b)!;
        string Log() => (string)p.GetType().GetField("Log")!.GetValue(null)!;
        void Step() => game.FixedUpdate(default(GameInput),1f/60);
        try
        {
            Step();
            Check(Log()=="PC CC P+ C+ PT CT ","all creates before enables and first update");
            world.GetComponent<BoxCollider2DComponent>(child.Id)!.IsTrigger=false;
            Check(child.GetComponent<BoxCollider2DComponent>()!.IsTrigger,"reading built-in config cannot edit authored scene");
            world.SetActive(parent.Id,false); world.SetActive(parent.Id,false); Step();
            Check(Log().EndsWith("C- P- ") && History(c)=="C+T-","disable children first, no inactive ticks");
            var pair = world.Instantiate(asset,new(20,20),parent.Id);
            var pc2 = world.GetComponent<GameBehaviour>(pair)!;
            var child2=world.Objects.Single(o=>o.ParentId==pair).Id;
            var target = pc2.GetType().GetField("Peer")!.GetValue(pc2)!;
            Check(((Guid[])pc2.GetType().GetField("Targets")!.GetValue(pc2)!).SequenceEqual([child2, Guid.Empty, pair, child2]),
                "generated script factory resolves ordered prefab references to runtime instance IDs");
            Check((Guid)target.GetType().GetProperty("ObjectId")!.GetValue(target)! == child2 &&
                History(pc2)=="C","spawn inactive, runtime prefab remaps typed target");
            world.SetActive(parent.Id,true); world.SetActive(parent.Id,true); Step();
            Check(History(p)=="C+T-+T" && History(pc2)=="C+","enable only once and resume updates");
            Step(); // Runtime spawns enter the simulation after the first structural commit.
            Check(world.Destroy(pair) && !world.Destroy(pair) && History(pc2)=="C+T-X" &&
                world.GetComponent<GameBehaviour>(child2)==null,"destroy once and invalidate descendants");
            world.SetActive(parent.Id,false);
        }
        finally { game.Dispose(); }
        var log=Log(); game.Dispose();
        Check(log==Log() && log.EndsWith("CX PX ") && History(c).EndsWith("-X"),"stop destroys disabled children before parent once");
        using var fresh = new GameSession(scene,library); fresh.FixedUpdate(default(GameInput),1f/60);
        Check(History(fresh.Current.GetComponent<GameBehaviour>(parent.Id)!)=="C+T" && Log()==log,"new Play isolates scripts and static state");
        var cancel = SceneDocument.Empty(new()); cancel.ProjectDirectory = root;
        var transient = new SceneObject { Name="Remove at creation" };
        var probe = new ScriptComponent { Script="Probe" };
        probe.Values["Label"]=JsonSerializer.SerializeToElement("RemoveAtBirth");
        transient.AddComponent(probe); cancel.Objects.Add(transient);
        using (var nested = new GameSession(cancel,library))
        {
            var reference = nested.Current.GetComponent<GameBehaviour>(transient.Id)!;
            nested.FixedUpdate(default(GameInput),1f/60);
            Check(History(reference)=="CX" && !nested.Current.IsActive(transient.Id),"destroy in OnCreate skips enable, update and disable");
        }
        File.WriteAllText(common, "namespace Rules; public static class Defaults { public const int Tick = ; }");
        try { library.Get("Probe"); throw new Exception("bad shared code accepted"); }
        catch (InvalidDataException ex) { Check(ex.Message.Contains("Shared.Tools.cs(1,"),"shared file diagnostic location"); }
        Console.WriteLine("PASS shared project compilation and invalidation; typed Inspector, duplicate and prefab references; lifecycle order, activation, destruction and fresh Play");
    }
}
