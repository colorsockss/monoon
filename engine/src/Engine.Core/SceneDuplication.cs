using System.Text;

namespace Engine.Core;

public sealed partial class SceneDocument
{
    // First line of copied objects on the clipboard. The rest is a scene file holding only those objects.
    public const string ObjectClipboardHeader = "# MoNoon objects";

    public bool ContainsGameCamera(SceneObject root) => Cameras.Any(camera => camera.IsWithin(root));

    public SceneObject DuplicateObject(SceneObject root, ScriptLibrary? scripts = null)
    {
        if (!Objects.Contains(root)) throw new ArgumentException("Select an object from this scene.", nameof(root));

        var copies = CopyObjects(ParentFirst(root).ToArray(), root.Parent, scripts);

        // Keep sibling and drawing order even in files where a child appears before its parent.
        var ordered = new List<SceneObject>(Objects.Count + copies.Count);
        foreach (var original in Objects)
        {
            ordered.Add(original);
            if (copies.TryGetValue(original.Id, out var copy)) ordered.Add(copy);
        }
        Objects = ordered;
        return copies[root.Id];
    }

    public static bool IsObjectClipboard(string? text) => text?.StartsWith(ObjectClipboardHeader, StringComparison.Ordinal) == true;

    // References keep their GUIDs here. Paste remaps the ones that point into the copy.
    public string CopyObjects(IEnumerable<SceneObject> selection)
    {
        var picked = selection.Where(Objects.Contains).ToHashSet();
        var tops = picked.Where(obj => !picked.Any(other => other != obj && obj.IsWithin(other))).ToArray();
        if (tops.Length == 0) throw new ArgumentException("Select objects from this scene.", nameof(selection));
        var subset = ProjectYaml.Decode<SceneDocument>(ProjectYaml.Encode(new SceneDocument
        {
            Objects = Objects.Where(obj => tops.Any(obj.IsWithin)).ToList()
        }));
        var ids = subset.Objects.Select(obj => obj.Id).ToHashSet();
        foreach (var obj in subset.Objects)
            if (obj.ParentId is Guid parent && !ids.Contains(parent)) obj.ParentId = null;
        return ObjectClipboardHeader + "\n" + Encoding.UTF8.GetString(ProjectYaml.Encode(subset));
    }

    public IReadOnlyList<SceneObject> PasteObjects(string clipboard, SceneObject? parent, ScriptLibrary? scripts = null)
    {
        if (!IsObjectClipboard(clipboard)) throw new InvalidDataException("The clipboard has no copied objects.");
        if (parent != null && !Objects.Contains(parent)) throw new ArgumentException("Paste into an object of this scene.", nameof(parent));
        SceneDocument source;
        try { source = ProjectYaml.Decode<SceneDocument>(Encoding.UTF8.GetBytes(clipboard)); }
        catch (Exception ex) when (ex is not InvalidDataException) { throw new InvalidDataException("The copied objects cannot be read: " + ex.Message, ex); }
        source.Validate();
        if (source.Objects.Count == 0) throw new InvalidDataException("The clipboard has no copied objects.");
        var copies = CopyObjects(source.ParentFirst().ToArray(), parent, scripts);
        foreach (var original in source.Objects) Objects.Add(copies[original.Id]);
        return source.Objects.Where(obj => obj.Parent == null).Select(obj => copies[obj.Id]).ToArray();
    }

    // Shared by Duplicate and Paste: new IDs, and references into the copied set follow the copies.
    // References that leave the set keep their target, which is a missing reference in another scene.
    private Dictionary<Guid, SceneObject> CopyObjects(SceneObject[] originals, SceneObject? parent, ScriptLibrary? scripts)
    {
        var copies = originals.ToDictionary(obj => obj.Id, obj => obj.CopyTransform(Guid.NewGuid()));
        var library = scripts == null && ProjectDirectory == null ? null : ResolveScripts(scripts);
        var placed = new List<SceneObject>();
        foreach (var original in originals)
        {
            var copy = copies[original.Id];
            if (original.Prefab is { } prefab)
                copy.Prefab = RemapLink(prefab,copies,library);
            if (original.Parent is { } owner && copies.TryGetValue(owner.Id, out var copiedOwner)) copy.SetParent(copiedOwner);
            else
            {
                copy.SetParent(parent);
                copy.Name = UniqueName(original.Name, parent, placed);
                placed.Add(copy);
            }
            foreach (var component in original.Components)
                copy.AddComponent(component is ScriptComponent script ? script.Duplicate(copies, library) :
                    component is Tilemap2DComponent { StreamTarget: Guid anchor } map && copies.TryGetValue(anchor,out var focus) ? map with { StreamTarget=focus.Id } :
                    component is Joint2DComponent { Connected: Guid link } joint && copies.TryGetValue(link, out var other) ? joint with { Connected = other.Id } :
                    component is Camera2DComponent { Follow: Guid id } cam && copies.TryGetValue(id,out var followed) ? cam with { Follow=followed.Id } : component with { });
        }
        return copies;
    }

    private string UniqueName(string name, SceneObject? parent, IEnumerable<SceneObject> pending)
    {
        var names = Objects.Concat(pending).Where(obj => obj.Parent == parent).Select(obj => obj.Name).ToHashSet(StringComparer.Ordinal);
        if (!names.Contains(name)) return name;
        string stem = name;
        int suffix = stem.LastIndexOf(" (", StringComparison.Ordinal);
        if (suffix > 0 && stem.EndsWith(')') && int.TryParse(stem.AsSpan(suffix + 2, stem.Length - suffix - 3), out int number) && number > 0)
            stem = stem[..suffix];
        for (int i = 1; ; i++)
        {
            string candidate = $"{stem} ({i})";
            if (!names.Contains(candidate)) return candidate;
        }
    }
}
