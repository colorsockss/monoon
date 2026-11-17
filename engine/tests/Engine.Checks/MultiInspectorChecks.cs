using Engine.Core;
using Engine.Editor;

internal static class MultiInspectorChecks
{
    internal static void Run(string project)
    {
        var scene = SceneDocument.OpenForEditing(Path.Combine(project, "Scenes", "WallWorkshop.scene"));
        SceneObject[] Walls() => scene.Objects.Where(o => o.Name.StartsWith("Wall ", StringComparison.Ordinal)).ToArray();
        Collider2DComponent[] Colliders() => Walls().Select(o => o.GetComponent<BoxCollider2DComponent>()!).ToArray();
        var ids = Walls().Select(o => o.Id).ToArray();
        var original = scene.CaptureObjects();
        var overrides = Walls().Select(o => scene.PropertyOverrides(o).ToArray()).ToArray();
        var prefab = Path.Combine(project, "Assets", "WallSegment.prefab");
        var source = File.ReadAllBytes(prefab);
        uint[] masks = Colliders().Select(c => c.Mask).ToArray();
        void Require(bool ok, string message)
        {
            if (!ok) throw new Exception("Inspector multi-edit: " + message);
        }
        Require(ids.Length == 10 && overrides.Any(fields => fields.Any(f => f.Property.Field == "mask")) &&
            overrides.Any(fields => fields.All(f => f.Property.Field != "mask")), "linked walls include inherited and overridden masks");
        var history = new SceneHistory();
        history.Reset(scene);
        var setHigh = CollisionMaskSelection.ToggleBit(masks, 1u << 31);
        Require(CollisionMaskSelection.Apply(Colliders(), setHigh), "change mixed high bit");
        var enabled = Colliders().Select(c => c.Mask).ToArray();
        Require(enabled.SequenceEqual(masks.Select(mask => mask | (1u << 31))), "unrelated bits preserved");
        Require(!CollisionMaskSelection.Apply(Colliders(), setHigh), "no-op skipped");
        var clearHigh = CollisionMaskSelection.ToggleBit(enabled, 1u << 31);
        Require(enabled.Select(clearHigh).SequenceEqual(masks.Select(mask => mask & ~(1u << 31))), "clear shared high bit");

        history.Record(scene, ids[^1], ids[^1], null, multiSelection: ids);
        var edited = scene.CaptureObjects();
        Require(history.MultiSelection(false)!.SequenceEqual(ids), "selection stored with mask edit");
        history.Restore(scene, false);
        Require(!history.CanUndo && original == scene.CaptureObjects(), "one Undo restores the entire scene");
        Require(Walls().Select((o, i) => scene.PropertyOverrides(o).SequenceEqual(overrides[i])).All(v => v), "each override restored");
        Require(history.MultiSelection(true)!.SequenceEqual(ids), "redo selection preserved");
        history.Restore(scene, true);
        Require(edited == scene.CaptureObjects() && File.ReadAllBytes(prefab).SequenceEqual(source), "redo and unchanged prefab source");
        Console.WriteLine("PASS Inspector multi-edit: mixed masks, high bits, no-op, one Undo, per-instance overrides and Redo.");
    }
}
