using VYaml.Annotations;
using System.Numerics;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Engine.Core;

[YamlObject]
public sealed partial record PrefabLink(Guid AssetId, Dictionary<Guid, Guid> Instances, string Revision)
{
    // Values use this instance's object IDs. The keys use IDs from the source asset.
    public Dictionary<Guid,JsonElement>? Baseline { get; set; }
}

public sealed partial class SceneDocument
{
    public void ResolveAssets(ProjectAssets assets)
    {
        foreach (var sprite in Objects.Select(o => o.GetComponent<SpriteRendererComponent>()).OfType<SpriteRendererComponent>())
        {
            if (sprite.AssetId is Guid id && assets.PathFor(id) is { } path) sprite.Asset = path;
            else if (sprite.Asset != null) sprite.AssetId ??= assets.IdFor(sprite.Asset);
        }
    }

    public void ExportPrefab(SceneObject root, string path, bool update = false)
    {
        var project = ProjectDirectory ?? throw new InvalidOperationException("Save the project first.");
        if (!update && File.Exists(path)) throw new IOException("A prefab with that name already exists.");
        if (!Objects.Contains(root)) throw new InvalidOperationException("Select an object in this scene.");
        var assets = ProjectAssets.Scan(project);
        if(update && (root.Prefab==null || assets.PathFor(root.Prefab.AssetId)!=Path.GetRelativePath(project,path).Replace(Path.DirectorySeparatorChar,'/')))
            throw new InvalidOperationException("Apply must target the instance's source.");
        var prior = update ? ReadAsset(path).Objects.Single(o => o.Parent == null).Prefab : null;
        WriteTemplate(root,path,update ? root.Prefab : null,prior,assets);
    }

    public void CreatePrefabVariant(SceneObject root,string path)
    {
        if (root.Prefab == null) throw new InvalidOperationException("Select a linked prefab root.");
        if (File.Exists(path)) throw new IOException("A prefab with that name already exists.");
        WriteTemplate(root,path,null,null,ProjectAssets.Scan(ProjectDirectory!),variant:true);
    }

    private void WriteTemplate(SceneObject root,string path,PrefabLink? old,PrefabLink? baseLink,ProjectAssets assets,bool variant=false)
    {
        var project = ProjectDirectory ?? throw new InvalidOperationException("Save the project first.");
        NormalizeScriptFields();
        var localIds = old?.Instances.ToDictionary(p => p.Value,p => p.Key) ?? [];
        var originals = ParentFirst(root).ToArray();
        var copies = originals.ToDictionary(o => o.Id,o => NewCopy(o,localIds.GetValueOrDefault(o.Id,Guid.NewGuid())));
        var library = ScriptLibrary.Open(project);
        foreach (var obj in originals)
        {
            var copy = copies[obj.Id];
            copy.SetParent(obj == root ? null : copies[obj.Parent!.Id]);
            CopyFields(obj,copy,copies,library,exporting:true);
            if (obj.Prefab != null && (obj != root || variant)) copy.Prefab=RemapLink(obj.Prefab,copies,library);
        }
        var template = new SceneDocument { ProjectDirectory=project, Objects=originals.Select(o => copies[o.Id]).ToList() };
        var first = template.Objects[0];
        if (baseLink != null) first.Prefab=baseLink;
        first.X=first.Y=0;
        template.Validate();
        var active = new HashSet<Guid>();
        if (old != null) active.Add(old.AssetId);
        foreach (var dep in template.Objects.Where(o=>o.Prefab!=null))
            ResolveTemplate(project,assets,dep.Prefab!.AssetId,active);
        ProjectAssets.Write(path,template);
        assets=ProjectAssets.Scan(project);
        var relative=Path.GetRelativePath(project,path).Replace(Path.DirectorySeparatorChar,'/');
        var id=assets.IdFor(relative)!.Value;
        var resolved=ResolveTemplate(project,assets,id,[]);
        var map=copies.ToDictionary(p=>p.Value.Id,p=>p.Key);
        var (pristine,_) = MakeCopies(resolved.Document,root,map,root.Id,library);
        root.Prefab=Link(id,resolved.Revision,pristine);
    }

    private static PrefabLink RemapLink(PrefabLink link,Dictionary<Guid,SceneObject> copies,ScriptLibrary? library)
    {
        // Keep mappings for removed inherited members: absence is a structural override.
        var ids=link.Instances.ToDictionary(p=>p.Key,p=>copies.TryGetValue(p.Value,out var copy)?copy.Id:p.Value);
        Dictionary<Guid,JsonElement>? baseline=null;
        if(link.Baseline!=null)
        {
            baseline=[];
            foreach(var (source,value) in link.Baseline)
            {
                var previous=ProjectYaml.Decode<SceneObject>(ProjectYaml.Encode(value));
                var mapped=previous.CopyTransform(ids[source]);
                mapped.ParentId=previous.ParentId is Guid p && copies.TryGetValue(p,out var parent)?parent.Id:previous.ParentId;
                foreach(var component in previous.Components)
                {
                    mapped.AddComponent(component switch {
                        Tilemap2DComponent {StreamTarget: Guid anchor} map when copies.TryGetValue(anchor,out var focus) => map with { StreamTarget=focus.Id },
                        Joint2DComponent {Connected: Guid linkId} joint when copies.TryGetValue(linkId, out var other) => joint with { Connected = other.Id },
                        ScriptComponent script => script.Duplicate(copies,library),
                        Camera2DComponent camera=>camera.Relink(id=>copies.TryGetValue(id,out var to)?to.Id:null),
                        _=>component });
                }
                baseline[source]=PrefabMerge.Snapshot(mapped);
            }
        }
        return link with { Instances=ids,Baseline=baseline };
    }

    private static SceneObject NewCopy(SceneObject o, Guid id) => o.CopyTransform(id);

    private static void CopyFields(SceneObject source, SceneObject dest, Dictionary<Guid, SceneObject> copies, ScriptLibrary library,
        bool exporting = false, SceneObject? previous = null)
    {
        foreach (var component in source.Components)
        {
            if (component is Tilemap2DComponent map)
            {
                Guid? target = map.StreamTarget is Guid id && copies.TryGetValue(id, out var other) ? other.Id :
                    exporting ? null : previous?.GetComponent<Tilemap2DComponent>()?.StreamTarget;
                dest.AddComponent(map with { StreamTarget = target }); continue;
            }
            if (component is Joint2DComponent joint)
            {
                Guid? target = joint.Connected is Guid id && copies.TryGetValue(id, out var other) ? other.Id :
                    exporting ? null : (previous?.Components.FirstOrDefault(c => c.GetType() == joint.GetType()) as Joint2DComponent)?.Connected;
                dest.AddComponent(joint with { Connected = target }); continue;
            }
            if (component is Camera2DComponent cam)
            {
                Guid? Link(Guid? own,Guid? old) => own is Guid id && copies.TryGetValue(id,out var linked) ? linked.Id : exporting ? null : old;
                var before=previous?.GetComponent<Camera2DComponent>();
                dest.AddComponent(cam with { Follow=Link(cam.Follow,before?.Follow), SplitWith=Link(cam.SplitWith,before?.SplitWith) }); continue;
            }
            if (component is not ScriptComponent script) { dest.AddComponent(component with { }); continue; }
            var clone = script.Duplicate(copies, library);
            if (exporting && script.EventLinks != null)
            {
                var local = script.EventLinks.Where(l => copies.ContainsKey(script.InstanceId(l.Target)))
                    .Select(l => l with { Target = copies[script.InstanceId(l.Target)].Id }).ToArray();
                clone.EventLinks = local.Length == 0 ? null : local;
            }
            if (exporting)
                foreach (var field in library.Get(script.Script).Fields.Where(f => f.IsObjectList && script.Values.ContainsKey(f.Name)))
                {
                    var sourceIds = script.ReadReferences(field.Name, true);
                    clone.WriteReferences(field.Name, sourceIds.Select(id =>
                        copies.TryGetValue(id, out var local) ? local.Id : Guid.Empty).ToArray());
                }
            foreach (var field in library.Get(script.Script).Fields.Where(f => f.Type == typeof(Guid)))
            {
                var id = (Guid?)field.Value(script);
                if (id == Guid.Empty || (id != null && copies.ContainsKey(id.Value))) continue;
                if (exporting) clone.Values.Remove(field.Name);
                else if (previous?.Components.OfType<ScriptComponent>().FirstOrDefault(s => s.Script == script.Script) is { } old &&
                         old.Values.TryGetValue(field.Name, out var value)) clone.Values[field.Name] = value.Clone();
            }
            dest.AddComponent(clone);
        }
    }

    public SceneObject PlacePrefab(string relative, Vector2 position)
    {
        var assets = ProjectAssets.Scan(ProjectDirectory!);
        var id = assets.IdFor(relative) ?? throw new InvalidDataException($"Missing prefab {relative}.");
        var source = ResolveTemplate(ProjectDirectory!,assets,id,[]);
        return Materialize(id,source,position,null,false);
    }

    private sealed record ResolvedPrefab(SceneDocument Document,string Revision);

    internal static SceneDocument ReadResolvedPrefab(string project,ProjectAssets assets,Guid id) =>
        ResolveTemplate(project,assets,id,[]).Document;

    private static ResolvedPrefab ResolveTemplate(string project,ProjectAssets assets,Guid id,HashSet<Guid> active)
    {
        var entry=assets.Entries.FirstOrDefault(e=>e.Id==id && e.Path.EndsWith(ProjectAssets.Prefab))
            ?? throw new InvalidDataException($"Missing prefab {id}.");
        if(!active.Add(id))throw new InvalidDataException($"Prefab cycle at {entry.Path}.");
        try
        {
            var doc=ReadAsset(Path.Combine(project,entry.Path));
            doc.ProjectDirectory=project;
            if(doc.Objects.Count(o=>o.Parent==null)!=1)throw new InvalidDataException($"{entry.Path} must have one root.");
            var dependencies=new StringBuilder();
            doc.RefreshLinks(assets,active,dependencies);
            string revision=dependencies.Length==0?entry.Hash:Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.Hash+dependencies)));
            return new(doc,revision);
        }
        finally {active.Remove(id);}
    }

    public bool RefreshPrefabs(ProjectAssets assets) => RefreshLinks(assets,[],null);

    private bool RefreshLinks(ProjectAssets assets,HashSet<Guid> active,StringBuilder? dependencies)
    {
        NormalizeScriptFields(); // Migrate all three sides before merging source revisions.
        bool changed=false;
        var resolved=new Dictionary<Guid,ResolvedPrefab>();
        // An outer template owns its inherited nested roots. Locally added prefab
        // children still have their own independent source.
        foreach(var root in ParentFirst().Where(o=>o.Prefab!=null).ToArray())
        {
            if(!Objects.Contains(root))continue;
            bool owned=false;
            for(var parent=root.Parent;parent!=null;parent=parent.Parent)
                if(parent.Prefab?.Instances.ContainsValue(root.Id)==true){owned=true;break;}
            if(owned)continue;
            var link=root.Prefab!;
            if(!resolved.TryGetValue(link.AssetId,out var source))
                resolved[link.AssetId]=source=ResolveTemplate(ProjectDirectory!,assets,link.AssetId,active);
            dependencies?.Append(source.Revision);
            if(link.Revision==source.Revision && link.Baseline!=null)continue;
            Materialize(link.AssetId,source,new(root.X,root.Y),root,false);
            changed=true;
        }
        return changed;
    }

    public IEnumerable<string> PrefabOverrides(SceneObject root) =>
        PropertyOverrides(root).Select(p => p.ToString()).Concat(StructuralOverrides(root).Select(s => s.ToString()));

    public SceneObject RevertPrefab(SceneObject root)
    {
        var link=root.Prefab ?? throw new InvalidOperationException("Select a prefab instance.");
        var source=ResolveTemplate(ProjectDirectory!,ProjectAssets.Scan(ProjectDirectory!),link.AssetId,[]);
        return Materialize(link.AssetId,source,new(root.X,root.Y),root,true);
    }

    private static Guid InheritedId(Guid root,Guid source)
    {
        Span<byte> pair=stackalloc byte[32];
        root.TryWriteBytes(pair);source.TryWriteBytes(pair[16..]);
        return new Guid(SHA256.HashData(pair).AsSpan(0,16));
    }

    private static (Dictionary<Guid,SceneObject> Copies,SceneObject Root) MakeCopies(SceneDocument template,SceneObject? previous,
        Dictionary<Guid,Guid> ids,Guid rootId,ScriptLibrary library)
    {
        var root=template.Objects.Single(o=>o.Parent==null);
        var copies=template.Objects.ToDictionary(o=>o.Id,o=>NewCopy(o,o==root?rootId:ids.GetValueOrDefault(o.Id,InheritedId(rootId,o.Id))));
        foreach(var obj in template.Objects)
        {
            var copy=copies[obj.Id];
            copy.SetParent(obj==root?previous?.Parent:copies[obj.Parent!.Id]);
            CopyFields(obj,copy,copies,library);
            if(obj!=root && obj.Prefab!=null)copy.Prefab=RemapLink(obj.Prefab,copies,library);
        }
        return (copies,copies[root.Id]);
    }

    private static PrefabLink Link(Guid id,string revision,Dictionary<Guid,SceneObject> copies) =>
        new(id,copies.ToDictionary(p=>p.Key,p=>p.Value.Id),revision)
        {Baseline=copies.ToDictionary(p=>p.Key,p=>PrefabMerge.Snapshot(p.Value))};

    private SceneObject Materialize(Guid id,ResolvedPrefab source,Vector2 position,SceneObject? previous,bool revert)
    {
        var prior=previous?.Prefab;
        var ids=prior?.Instances??[];
        var library=ScriptLibrary.Open(ProjectDirectory!);
        var (copies,first)=MakeCopies(source.Document,previous,ids,previous?.Id??Guid.NewGuid(),library);
        var link=Link(id,source.Revision,copies);
        var existing=Objects.ToDictionary(o=>o.Id);
        var inherited=new List<SceneObject>();
        foreach(var (key,copy) in copies)
        {
            if(!revert && ids.TryGetValue(key,out var oldId))
            {
                if(!existing.TryGetValue(oldId,out var edited))continue;
                if(prior!.Baseline?.GetValueOrDefault(key) is { ValueKind:JsonValueKind.Object } before)
                {
                    var merged=PrefabMerge.Restore(PrefabMerge.Merge(before,edited,link.Baseline![key]));
                    merged.Prefab=copy.Prefab;
                    inherited.Add(merged);
                    continue;
                }
                // Legacy instances have no old baseline. If revisions match, their
                // saved content is authoritative, including external object references.
                if(prior.Revision==source.Revision){inherited.Add(edited);continue;}
                foreach(var script in copy.Components.OfType<ScriptComponent>())
                    if(edited.Components.OfType<ScriptComponent>().FirstOrDefault(s=>s.Script==script.Script) is {} oldScript)
                        foreach(var field in library.Get(script.Script).Fields.Where(f=>f.Type==typeof(Guid) || f.IsObjectList))
                            if(!script.Values.ContainsKey(field.Name) && oldScript.Values.TryGetValue(field.Name,out var v))script.Values[field.Name]=v.Clone();
            }
            inherited.Add(copy);
        }
        var instance=inherited.Single(o=>o.Id==first.Id);
        instance.X=position.X;instance.Y=position.Y;instance.SetParent(previous?.Parent);
        instance.Prefab=link;
        var removed=ids.Values.ToHashSet();
        if(revert && previous!=null)removed.UnionWith(ParentFirst(previous).Select(o=>o.Id));
        int index=previous==null?Objects.Count:Objects.TakeWhile(o=>o!=previous).Count(o=>!removed.Contains(o.Id));
        var next=Objects.Where(o=>!removed.Contains(o.Id)).ToList();
        next.InsertRange(index,inherited);
        var present=next.Select(o=>o.Id).ToHashSet();
        removed.UnionWith(link.Instances.Values.Where(mapped=>!present.Contains(mapped)));
        foreach(var obj in next)
            if(obj.ParentId is Guid parent && !present.Contains(parent) && removed.Contains(parent))obj.SetParent(instance);
        var candidate=new SceneDocument {Objects=next};
        candidate.Validate();
        Objects=next;
        return instance;
    }
}
