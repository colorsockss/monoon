namespace Engine.Core;

public sealed partial class SceneDocument
{
    public bool ContainsGameCamera(SceneObject root) => Cameras.Any(camera => camera.IsWithin(root));

    public SceneObject DuplicateObject(SceneObject root, ProjectScripts? scripts = null)
    {
        if (!Objects.Contains(root)) throw new ArgumentException("Select an object from this scene.", nameof(root));


        var originals = ParentFirst(root).ToArray();
        var copies = originals.ToDictionary(obj => obj.Id, obj => obj.CopyTransform(Guid.NewGuid()));
        var result = copies[root.Id];
        result.Name = DuplicateName(root);
        var library = scripts == null && ProjectDirectory == null ? null : ResolveScripts(scripts);
        foreach (var original in originals)
        {
            var copy = copies[original.Id];
            if (original.Prefab is { } prefab)
                copy.Prefab = RemapLink(prefab,copies,library);
            copy.SetParent(original == root ? root.Parent : copies[original.Parent!.Id]);
            foreach (var component in original.Components)
                copy.AddComponent(component is ScriptComponent script ? script.Duplicate(copies, library) :
                    component is Tilemap2DComponent { StreamTarget: Guid anchor } map && copies.TryGetValue(anchor,out var focus) ? map with { StreamTarget=focus.Id } :
                    component is DistanceJoint2DComponent { Connected: Guid link } joint && copies.TryGetValue(link, out var other) ? joint with { Connected = other.Id } :
                    component is Camera2DComponent { Follow: Guid id } cam && copies.TryGetValue(id,out var followed) ? cam with { Follow=followed.Id } : component with { });
        }

        // Keep sibling and drawing order even in files where a child appears before its parent.
        var ordered = new List<SceneObject>(Objects.Count + copies.Count);
        foreach (var original in Objects)
        {
            ordered.Add(original);
            if (copies.TryGetValue(original.Id, out var copy)) ordered.Add(copy);
        }
        Objects = ordered;
        return result;
    }

    private string DuplicateName(SceneObject root)
    {
        string stem = root.Name;
        int suffix = stem.LastIndexOf(" (", StringComparison.Ordinal);
        if (suffix > 0 && stem.EndsWith(')') && int.TryParse(stem.AsSpan(suffix + 2, stem.Length - suffix - 3), out int number) && number > 0)
            stem = stem[..suffix];
        var names = Objects.Where(obj => obj.Parent == root.Parent).Select(obj => obj.Name).ToHashSet(StringComparer.Ordinal);
        for (int i = 1; ; i++)
        {
            string candidate = $"{stem} ({i})";
            if (!names.Contains(candidate)) return candidate;
        }
    }
}
