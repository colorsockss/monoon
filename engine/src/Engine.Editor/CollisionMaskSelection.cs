using Engine.Core;

namespace Engine.Editor;

internal static class CollisionMaskSelection
{
    // A mixed bit becomes enabled on every target. Other bits stay local to that target.
    internal static Func<uint, uint> ToggleBit(IEnumerable<uint> masks, uint bit) =>
        masks.All(mask => (mask & bit) != 0) ? mask => mask & ~bit : mask => mask | bit;

    internal static bool Apply(IEnumerable<Collider2DComponent> targets, Func<uint, uint> change)
    {
        bool changed = false;
        foreach (var target in targets)
        {
            uint next = change(target.Mask);
            if (next == target.Mask) continue;
            target.Mask = next;
            changed = true;
        }
        return changed;
    }
}
