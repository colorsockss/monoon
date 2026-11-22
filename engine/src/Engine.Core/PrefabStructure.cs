using System.Text.Json;
using System.Text.Json.Nodes;

namespace Engine.Core;

internal enum StructureChange { AddedObject, RemovedObject, AddedComponent, RemovedComponent }

// Object is the scene ID of an added object and the source ID otherwise. A whole added or
// removed subtree is one override, listed at its topmost object.
internal sealed record StructuralOverride(StructureChange Kind, Guid Object, string? Component, string Label)
{
    public override string ToString() => Label;
}

public sealed partial class SceneDocument
{
    internal IEnumerable<StructuralOverride> StructuralOverrides(SceneObject root)
    {
        if (root.Prefab?.Baseline is not {} baseline) yield break;
        var map = root.Prefab.Instances;
        var byId = Objects.ToDictionary(o => o.Id);
        foreach (var (key, value) in baseline)
        {
            if (!byId.TryGetValue(map[key], out var obj))
            {
                var parent = value.TryGetProperty("parentId", out var p) && p.ValueKind == JsonValueKind.String ? p.GetGuid() : (Guid?)null;
                if (parent == null || byId.ContainsKey(parent.Value) || !map.ContainsValue(parent.Value))
                    yield return new(StructureChange.RemovedObject, key, null, $"{value.GetProperty("name").GetString()} (removed object)");
                continue;
            }
            var before = ComponentKeys(PropertyNode(value));
            var current = ComponentKeys(PropertyNode(obj));
            foreach (var removed in before.Except(current))
                yield return new(StructureChange.RemovedComponent, key, removed, $"{obj.Name} · {removed.TrimEnd('/')} (removed)");
            foreach (var added in current.Except(before))
                yield return new(StructureChange.AddedComponent, key, added, $"{obj.Name} · {added.TrimEnd('/')} (added)");
        }
        var inherited = map.Values.ToHashSet();
        foreach (var obj in ParentFirst(root).Where(o => !inherited.Contains(o.Id) && inherited.Contains(o.ParentId!.Value)))
            yield return new(StructureChange.AddedObject, obj.Id, null, $"{obj.Name} (added object)");
    }

    private static List<string> ComponentKeys(JsonObject node) => node["components"]!.AsArray().Select(PrefabMerge.ComponentKey).ToList();

    private static JsonNode? ComponentNode(JsonObject node, string key) =>
        node["components"]!.AsArray().FirstOrDefault(c => PrefabMerge.ComponentKey(c) == key);

    internal void EditPrefabStructure(SceneObject root, StructuralOverride change, bool apply)
    {
        if (!StructuralOverrides(root).Contains(change)) throw new InvalidOperationException("This override has changed. Reopen the list.");
        var link = root.Prefab!;
        var project = ProjectDirectory!;
        var assets = ProjectAssets.Scan(project);
        var source = OpenPrefabSource(project, assets.PathFor(link.AssetId)!);
        if (source.Revision != link.Revision) throw new IOException("The prefab source changed. Refresh the instance first.");
        var library = ScriptLibrary.Open(project);
        var byId = Objects.ToDictionary(o => o.Id);
        var doc = source.Document;
        if (!apply)
        {
            RevertStructure(root, change, assets, library);
            return;
        }

        var toSource = doc.Objects.ToDictionary(o => link.Instances[o.Id]);
        var instances = new Dictionary<Guid, Guid>(link.Instances);
        var baseline = new Dictionary<Guid, JsonElement>(link.Baseline!);
        switch (change.Kind)
        {
            case StructureChange.AddedComponent:
            {
                var obj = byId[link.Instances[change.Object]];
                var isolated = obj.CopyTransform(obj.Id);
                isolated.ParentId = obj.ParentId;
                isolated.AddComponent(ComponentFor(obj, change.Component!));
                var mapped = MapToSource(isolated, change.Object, toSource, library);
                var target = doc.Objects.Single(o => o.Id == change.Object);
                var updated = PropertyNode(target);
                updated["components"]!.AsArray().Add(PropertyNode(mapped)["components"]![0]!.DeepClone());
                doc.ReplaceProperty(target, updated);
                var known = PropertyNode(baseline[change.Object]);
                known["components"]!.AsArray().Add(ComponentNode(PropertyNode(isolated), change.Component!)!.DeepClone());
                baseline[change.Object] = JsonSerializer.SerializeToElement(known, GameJson.Default.JsonNode);
                break;
            }
            case StructureChange.RemovedComponent:
            {
                var target = doc.Objects.Single(o => o.Id == change.Object);
                var updated = PropertyNode(target);
                updated["components"]!.AsArray().Remove(ComponentNode(updated, change.Component!));
                doc.ReplaceProperty(target, updated);
                var known = PropertyNode(baseline[change.Object]);
                known["components"]!.AsArray().Remove(ComponentNode(known, change.Component!));
                baseline[change.Object] = JsonSerializer.SerializeToElement(known, GameJson.Default.JsonNode);
                break;
            }
            case StructureChange.AddedObject:
            {
                var subtree = AddedSubtree(root, byId[change.Object]);
                var ids = subtree.ToDictionary(o => o.Id, _ => Guid.NewGuid());
                foreach (var obj in subtree) toSource[obj.Id] = obj.CopyTransform(ids[obj.Id]);
                foreach (var obj in subtree)
                {
                    var mapped = MapToSource(obj, ids[obj.Id], toSource, library);
                    if (obj.Prefab != null) mapped.Prefab = RemapLink(obj.Prefab, toSource, library);
                    doc.Objects.Add(mapped);
                    instances[mapped.Id] = obj.Id;
                    baseline[mapped.Id] = PrefabMerge.Snapshot(obj);
                }
                break;
            }
            case StructureChange.RemovedObject:
            {
                var gone = doc.ParentFirst(doc.Objects.Single(o => o.Id == change.Object)).Select(o => o.Id).ToHashSet();
                if (gone.Any(id => byId.ContainsKey(link.Instances[id])))
                    throw new InvalidOperationException("Apply or revert the parent of the remaining children first.");
                doc.Objects = doc.Objects.Where(o => !gone.Contains(o.Id)).ToList();
                foreach (var id in gone) { instances.Remove(id); baseline.Remove(id); }
                break;
            }
        }
        SavePrefabSource(source);
        // The applied change now belongs to the baseline, so the refresh merge keeps it as inherited.
        root.Prefab = link with { Instances = instances, Baseline = baseline };
        RefreshPrefabs(ProjectAssets.Scan(project));
    }

    private void RevertStructure(SceneObject root, StructuralOverride change, ProjectAssets assets, ScriptLibrary library)
    {
        var link = root.Prefab!;
        var obj = Objects.FirstOrDefault(o => o.Id == (change.Kind == StructureChange.AddedObject ? change.Object : link.Instances[change.Object]));
        switch (change.Kind)
        {
            case StructureChange.AddedComponent:
            {
                var node = PropertyNode(obj!);
                node["components"]!.AsArray().Remove(ComponentNode(node, change.Component!));
                ReplaceProperty(obj!, node);
                break;
            }
            case StructureChange.RemovedComponent:
            {
                var known = PropertyNode(link.Baseline![change.Object]);
                var component = ComponentNode(known, change.Component!)!;
                var node = PropertyNode(obj!);
                var list = node["components"]!.AsArray();
                list.Insert(Math.Min(known["components"]!.AsArray().IndexOf(component), list.Count), component.DeepClone());
                ReplaceProperty(obj!, node);
                break;
            }
            case StructureChange.AddedObject:
            {
                var gone = AddedSubtree(root, obj!).Select(o => o.Id).ToHashSet();
                var next = new SceneDocument { Objects = Objects.Where(o => !gone.Contains(o.Id)).ToList() };
                next.Validate();
                Objects = next.Objects;
                break;
            }
            case StructureChange.RemovedObject:
            {
                // The source revision matches the link, so fresh copies equal the baseline.
                var template = ResolveTemplate(ProjectDirectory!, assets, link.AssetId, []).Document;
                var (copies, _) = MakeCopies(template, root, link.Instances, root.Id, library);
                var present = Objects.Select(o => o.Id).ToHashSet();
                var top = template.Objects.Single(o => o.Id == change.Object);
                var restored = template.ParentFirst(top).Select(o => copies[o.Id]).Where(o => !present.Contains(o.Id)).ToList();
                var order = template.Objects.TakeWhile(o => o != top).Reverse()
                    .Select(o => link.Instances.GetValueOrDefault(o.Id)).FirstOrDefault(present.Contains);
                var index = order == Guid.Empty ? Objects.Count : Objects.FindIndex(o => o.Id == order) + 1;
                var next = new SceneDocument { Objects = [.. Objects] };
                next.Objects.InsertRange(index, restored);
                next.Validate();
                Objects = next.Objects;
                break;
            }
        }
    }

    private List<SceneObject> AddedSubtree(SceneObject root, SceneObject top)
    {
        var subtree = ParentFirst(top).ToList();
        if (subtree.Any(o => root.Prefab!.Instances.ContainsValue(o.Id)))
            throw new InvalidOperationException("Move inherited objects out of the added object first.");
        return subtree;
    }

    private static SceneComponent ComponentFor(SceneObject obj, string key) =>
        PropertyNode(obj)["components"]!.AsArray().Zip(obj.Components).Single(p => PrefabMerge.ComponentKey(p.First) == key).Second;

    // Translate back to detect scene-only references before anything reaches the source.
    private SceneObject MapToSource(SceneObject obj, Guid sourceId, Dictionary<Guid, SceneObject> toSource, ScriptLibrary library)
    {
        var mapped = obj.CopyTransform(sourceId);
        mapped.ParentId = obj.ParentId is Guid parent && toSource.TryGetValue(parent, out var p) ? p.Id : null;
        CopyFields(obj, mapped, toSource, library, exporting: true);
        var toScene = toSource.ToDictionary(pair => pair.Value.Id, pair => obj.Id == pair.Key ? obj : new SceneObject { Id = pair.Key });
        var check = mapped.CopyTransform(obj.Id);
        check.ParentId = obj.ParentId;
        CopyFields(mapped, check, toScene, library, exporting: true);
        if (!JsonNode.DeepEquals(PropertyNode(obj), PropertyNode(check)))
            throw new InvalidOperationException("A prefab source can reference only objects inside that prefab.");
        return mapped;
    }
}
