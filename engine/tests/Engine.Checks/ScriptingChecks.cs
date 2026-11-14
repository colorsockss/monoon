using System.Numerics;
using System.Text.Json;
using Engine.Core;

internal static class ScriptingChecks
{
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
        parent.AddComponent(pc); child.AddComponent(cc); child.AddComponent(new BoxCollider2DComponent { IsTrigger=true });
        scene.Objects.AddRange([parent,child]); scene.Reparent(child,parent);
        var colliderField = library.Get("Probe").Fields.Single(f => f.Name=="Collider");
        Check(colliderField.Accepts(child) && !colliderField.Accepts(parent),"filter by required target component");
        var duplicate = scene.DuplicateObject(parent,library);
        var other = scene.Objects.Single(o => o.ParentId==duplicate.Id);
        var dupScript = duplicate.GetComponent<ScriptComponent>()!;
        Check(dupScript.ReadOverride("Peer",Guid.Empty)==other.Id && dupScript.ReadOverride("Collider",Guid.Empty)==other.Id,"duplicate remaps typed refs");
        scene.RemoveObject(other); scene.RemoveObject(duplicate);
        var assetDir = Directory.CreateDirectory(Path.Combine(root,"Assets"));
        scene.ExportPrefab(parent,Path.Combine(assetDir.FullName,"Pair.prefab"));
        var asset = new PrefabReference(ProjectAssets.Scan(root).IdFor("Assets/Pair.prefab")!.Value);
        var game = new GameSession(scene,library); var world=game.Current;
        var p = world.GetComponent<GameBehaviour>(parent.Id)!; var c = world.GetComponent<GameBehaviour>(child.Id)!;
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
