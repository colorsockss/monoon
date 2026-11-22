using System.Collections;
using System.Collections.Immutable;

namespace Engine.Core;

public sealed partial class SceneGame
{
    private enum RoleKind { Tag, Group }

    // A script may keep the view even if it queried before the first member spawned.
    private sealed class Members : IReadOnlyList<Guid>
    {
        private ImmutableList<Guid> ids = [];
        public int Count => ids.Count;
        public Guid this[int index] => ids[index];
        // An active loop can destroy its current object without invalidating the iterator.
        public IEnumerator<Guid> GetEnumerator() => ids.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        internal void Add(Guid id) => ids = ids.Add(id);
        internal void Remove(Guid id) => ids = ids.Remove(id);
        internal void ClearMembers() => ids = ids.Clear();
    }

    private readonly Dictionary<(RoleKind Kind, string Name), Members> roles = [];

    public IReadOnlyList<Guid> FindByTag(string tag) => FindRole(RoleKind.Tag, tag);
    public IReadOnlyList<Guid> FindByGroup(string group) => FindRole(RoleKind.Group, group);

    private IReadOnlyList<Guid> FindRole(RoleKind kind, string name) =>
        closing || string.IsNullOrWhiteSpace(name) ? Array.Empty<Guid>() : MembersFor(kind, name.Trim());

    private Members MembersFor(RoleKind kind, string name)
    {
        if (!roles.TryGetValue((kind, name), out var members))
            roles.Add((kind, name), members = new Members());
        return members;
    }

    private void RegisterBody(Body body)
    {
        byId.Add(body.Id, body);
        var obj = body.Source;
        if (obj.Tag is {} tag) MembersFor(RoleKind.Tag, tag).Add(obj.Id);
        if (obj.Groups is {} groups)
            foreach (var group in groups) MembersFor(RoleKind.Group, group).Add(obj.Id);
    }

    private bool MarkRemoved(SceneObject obj)
    {
        if (!removed.Add(obj.Id)) return false;
        if (obj.Tag is {} tag) roles[(RoleKind.Tag, tag)].Remove(obj.Id);
        if (obj.Groups is {} groups)
            foreach (var group in groups) roles[(RoleKind.Group, group)].Remove(obj.Id);
        return true;
    }
}
