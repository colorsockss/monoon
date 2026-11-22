using System.Text.Json;
using System.Text.Json.Nodes;

namespace Engine.Core;

// A component is identified by discriminator and script name, independent of its position.
internal sealed record PrefabProperty(string? Component, string? Group, string Field)
{
    internal string Label => string.Join(".", new[] { Component?.TrimEnd('/'), Group, Field }.OfType<string>());
    private bool IsSprite => Component == "spriteRenderer/" && Field == "sprite";

    private JsonObject? Container(JsonObject document, bool create = false)
    {
        var owner = Component == null ? document : document["components"]?.AsArray()
            .FirstOrDefault(c => PrefabMerge.ComponentKey(c) == Component)?.AsObject();
        if (owner == null || Group == null) return owner;
        if (create && owner[Group] == null) owner[Group] = new JsonObject();
        return owner[Group]?.AsObject();
    }

    internal void Copy(JsonObject from, JsonObject to)
    {
        var source = Container(from);
        var target = Container(to, create: source != null);
        if (target == null) return;
        foreach (var key in IsSprite ? new[] { "asset", "assetId" } : new[] { Field })
        {
            if (source != null && source.TryGetPropertyValue(key, out var value)) target[key] = value?.DeepClone();
            else target.Remove(key);
        }
    }

    internal string Display(JsonObject document)
    {
        var fields = Container(document);
        var value = IsSprite ? fields?["asset"] ?? fields?["assetId"] : fields?[Field];
        return value?.ToString() ?? "(default)";
    }

    internal static IEnumerable<PrefabProperty> Changed(JsonObject before, JsonObject after, bool root)
    {
        IEnumerable<PrefabProperty> Fields(JsonObject a, JsonObject b, string? component, string? group)
        {
            bool spriteReported = false;
            foreach (var key in a.Select(p => p.Key).Union(b.Select(p => p.Key)))
            {
                if (component == null && (key is "id" or "components" || root && key is ("x" or "y" or "parentId"))) continue;
                if (a.ContainsKey(key) == b.ContainsKey(key) && JsonNode.DeepEquals(a[key], b[key])) continue;
                if (component == "spriteRenderer/" && key is ("asset" or "assetId"))
                {
                    if (!spriteReported) yield return new(component, null, "sprite");
                    spriteReported = true;
                }
                else if (group == null && key == "values" && component?.StartsWith("script/") == true)
                {
                    foreach (var field in Fields(a[key]?.AsObject() ?? new(), b[key]?.AsObject() ?? new(), component, key))
                        yield return field;
                }
                else yield return new(component, group, key);
            }
        }

        foreach (var field in Fields(before, after, null, null)) yield return field;
        var local = after["components"]!.AsArray().ToDictionary(PrefabMerge.ComponentKey);
        foreach (var old in before["components"]!.AsArray())
        {
            var key = PrefabMerge.ComponentKey(old);
            if (local.TryGetValue(key, out var current))
                foreach (var field in Fields(old!.AsObject(), current!.AsObject(), key, null)) yield return field;
        }
    }
}

internal sealed record PropertyOverride(Guid SourceId, string Owner, PrefabProperty Property, string SourceValue, string LocalValue)
{
    public override string ToString() => $"{Owner} · {Property.Label}";
}

public sealed partial class SceneDocument
{
    internal sealed record PrefabSource(Guid Id, Guid RootId, string Revision, SceneDocument Document);

    internal static PrefabSource OpenPrefabSource(string project, string relative)
    {
        var assets = ProjectAssets.Scan(project);
        var id = assets.IdFor(relative) ?? throw new InvalidDataException($"Missing prefab {relative}.");
        var source = ResolveTemplate(project, assets, id, []);
        source.Document.Settings = GameSettings.Load(project);
        source.Document.ResolveAssets(assets);
        return new(id, source.Document.Objects.Single(o => o.Parent == null).Id, source.Revision, source.Document);
    }

    internal static void SavePrefabSource(PrefabSource source)
    {
        var doc = source.Document;
        var project = doc.ProjectDirectory!;
        var assets = ProjectAssets.Scan(project);
        var current = ResolveTemplate(project, assets, source.Id, []);
        if (current.Revision != source.Revision)
            throw new IOException("The prefab or an inherited source changed on disk. Reopen it before saving.");
        doc.NormalizeScriptFields();
        doc.Validate();
        if (doc.Objects.Count(o => o.Parent == null) != 1 || doc.Objects.Single(o => o.Parent == null).Id != source.RootId)
            throw new InvalidDataException("Keep the original prefab root and place all children beneath it.");
        foreach (var link in doc.Objects.Select(o => o.Prefab).OfType<PrefabLink>())
            ResolveTemplate(project, assets, link.AssetId, [source.Id]);
        doc.ResolveAssets(assets);
        ProjectYaml.Write(Path.Combine(project, assets.PathFor(source.Id)!), doc);
        doc.MigratedScriptFields = false;
    }

    private static JsonObject PropertyNode(JsonElement value) => JsonNode.Parse(value.GetRawText())!.AsObject();
    private static JsonObject PropertyNode(SceneObject obj) => PropertyNode(PrefabMerge.Snapshot(obj));

    internal IEnumerable<PropertyOverride> PropertyOverrides(SceneObject root)
    {
        if (root.Prefab?.Baseline is not {} baseline) yield break;
        var objects = Objects.ToDictionary(o => o.Id);
        foreach (var (id, snapshot) in baseline)
        {
            if (!objects.TryGetValue(root.Prefab.Instances[id], out var obj)) continue;
            var source = PropertyNode(snapshot);
            var local = PropertyNode(obj);
            foreach (var field in PrefabProperty.Changed(source, local, obj == root))
                yield return new(id, obj.Name, field, field.Display(source), field.Display(local));
        }
    }

    private void ReplaceProperty(SceneObject target, JsonObject value)
    {
        var replacement = PrefabMerge.Restore(value);
        replacement.Prefab = target.Prefab;
        var next = new SceneDocument { Objects = Objects.Select(o => o == target ? replacement : o).ToList() };
        next.Validate();
        Objects = next.Objects;
    }

    internal void EditPrefabProperty(SceneObject root, PropertyOverride change, bool apply)
    {
        if (!PropertyOverrides(root).Contains(change)) throw new InvalidOperationException("This override has changed. Reopen the list.");
        var link = root.Prefab!;
        var assets = ProjectAssets.Scan(ProjectDirectory!);
        var source = OpenPrefabSource(ProjectDirectory!, assets.PathFor(link.AssetId)!);
        if (source.Revision != link.Revision) throw new IOException("The prefab source changed. Refresh the instance first.");
        var obj = Objects.Single(o => o.Id == link.Instances[change.SourceId]);
        var field = change.Property;
        if (!apply)
        {
            var restored = PropertyNode(obj);
            field.Copy(PropertyNode(link.Baseline![change.SourceId]), restored);
            ReplaceProperty(obj, restored);
            return;
        }

        var doc = source.Document;
        var target = doc.Objects.Single(o => o.Id == change.SourceId);
        // Remap just the selected component. Unrelated scripts may have missing definitions.
        var local = PropertyNode(obj);
        var isolated = obj.CopyTransform(obj.Id);
        isolated.ParentId = obj.ParentId;
        if (field.Component != null)
        {
            var component = local["components"]!.AsArray()
                .Zip(obj.Components).Single(pair => PrefabMerge.ComponentKey(pair.First) == field.Component).Second;
            isolated.AddComponent(component);
        }
        var toSource = doc.Objects.ToDictionary(o => link.Instances[o.Id]);
        var mapped = obj.CopyTransform(target.Id);
        mapped.ParentId = obj.ParentId is Guid parent && toSource.TryGetValue(parent, out var p) ? p.Id : null;
        var library = ScriptLibrary.Open(ProjectDirectory!);
        CopyFields(isolated, mapped, toSource, library, exporting: true);
        // Translate back to detect scene-only references before committing the selected property.
        var toScene = Objects.Where(o => toSource.ContainsKey(o.Id)).ToDictionary(o => toSource[o.Id].Id);
        var check = mapped.CopyTransform(obj.Id);
        check.ParentId = mapped.ParentId is Guid mappedParent ? toScene[mappedParent].Id : null;
        CopyFields(mapped, check, toScene, library, exporting: true);
        var roundTrip = (JsonObject)local.DeepClone();
        field.Copy(PropertyNode(check), roundTrip);
        if (!JsonNode.DeepEquals(local, roundTrip))
            throw new InvalidOperationException("A prefab source can reference only objects inside that prefab.");
        var updated = PropertyNode(target);
        field.Copy(PropertyNode(mapped), updated);
        doc.ReplaceProperty(target, updated);
        SavePrefabSource(source);
        RefreshPrefabs(ProjectAssets.Scan(ProjectDirectory!));
    }
}
