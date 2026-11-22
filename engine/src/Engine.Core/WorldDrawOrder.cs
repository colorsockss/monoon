using System.Numerics;
using System.ComponentModel;
using VYaml.Annotations;

namespace Engine.Core;

[DisplayName("Sorting Group 2D"), YamlObject]
public sealed partial record SortingGroup2DComponent : SceneComponent
{
    public int SortingLayer { get; set; }
    public int Order { get; set; }
    public bool SortByY { get; set; }
    public float FootOffset { get; set; }
}

// A group occupies one slot in its parent's draw order. Its root renderer also
// has a slot inside the group, so equipment can be placed on either side of it.
internal sealed class WorldDrawOrder
{
    internal readonly record struct Item(int Index, Tilemap2DComponent? Map, int Layer, int Order);

    private sealed class Entry(Item item, int ordinal, bool sortByY, Vector2 foot, List<Entry>? children) : IComparable<Entry>
    {
        public readonly Item Item = item;
        public readonly int Ordinal = ordinal;
        public readonly bool SortByY = sortByY;
        public readonly Vector2 Foot = foot;
        public readonly List<Entry>? Children = children;
        public float WorldY;

        public int CompareTo(Entry? other)
        {
            if (other == null) return 1;
            int c = Item.Layer.CompareTo(other.Item.Layer);
            if (c == 0) c = Item.Order.CompareTo(other.Item.Order);
            if (c == 0) c = SortByY.CompareTo(other.SortByY);
            if (c == 0) c = WorldY.CompareTo(other.WorldY);
            return c != 0 ? c : Ordinal.CompareTo(other.Ordinal);
        }
    }

    private readonly List<Entry> roots = [];
    private readonly List<Entry>[] movingScopes;
    private readonly Entry[] movingEntries;
    private readonly Func<int, float, Matrix3x2> matrixAt;
    private readonly int[] spriteRanks, tileRanks;
    internal void AddRanks(int body, List<int> ranks)
    {
        ranks.Add(spriteRanks[body]);
        if (tileRanks[body] >= 0) ranks.Add(tileRanks[body]);
    }

    public Item[] Items { get; }

    public WorldDrawOrder(IReadOnlyList<SceneObject> objects, Func<int, float, Matrix3x2> matrixAt)
    {
        this.matrixAt = matrixAt;
        var groups = new Dictionary<SceneObject, List<Entry>>();
        foreach (var obj in objects)
            if (obj.GetComponent<SortingGroup2DComponent>() != null) groups.Add(obj, []);

        var moving = new List<Entry>();
        var scopes = new HashSet<List<Entry>>();
        int itemCount = 0, ordinal = 0;
        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            var scope = roots;
            for (var parent = obj.Parent; groups.Count > 0 && parent != null; parent = parent.Parent)
                if (groups.TryGetValue(parent, out var group)) { scope = group; break; }

            void Add(int layer, int order, bool byY = false, float offset = 0, Tilemap2DComponent? map = null, List<Entry>? children = null)
            {
                var entry = new Entry(new(i, map, layer, order), ordinal++, byY, new(obj.Width * .5f, obj.Height + offset), children);
                scope.Add(entry);
                if (byY) { moving.Add(entry); scopes.Add(scope); }
                if (children == null) itemCount++;
            }

            if (obj.GetComponent<SortingGroup2DComponent>() is {} groupConfig)
            {
                var members = groups[obj];
                Add(groupConfig.SortingLayer, groupConfig.Order, groupConfig.SortByY, groupConfig.FootOffset, children: members);
                scope = members;
            }
            if (obj.GetComponent<Tilemap2DComponent>() is {} map) Add(map.SortingLayer, map.Order, map: map);
            var sprite = obj.GetComponent<SpriteRendererComponent>();
            var skeleton = obj.GetComponent<SkeletonAnimator2DComponent>();
            Add(sprite?.SortingLayer ?? skeleton?.SortingLayer ?? 0, sprite?.Order ?? skeleton?.Order ?? 0,
                sprite?.SortByY == true, sprite?.FootOffset ?? 0);
        }

        movingEntries = moving.ToArray();
        movingScopes = scopes.ToArray();
        Items = new Item[itemCount];
        spriteRanks = new int[objects.Count];
        tileRanks = new int[objects.Count];
        Array.Fill(tileRanks, -1);
        roots.Sort();
        foreach (var members in groups.Values) members.Sort();
        Flatten();
    }

    public void Sample(float interpolation)
    {
        foreach (var entry in movingEntries)
            entry.WorldY = Vector2.Transform(entry.Foot, matrixAt(entry.Item.Index, interpolation)).Y;

        bool changed = false;
        foreach (var scope in movingScopes)
        {
            for (int i = 1; i < scope.Count; i++)
            {
                if (scope[i - 1].CompareTo(scope[i]) <= 0) continue;
                scope.Sort();
                changed = true;
                break;
            }
        }
        if (changed) Flatten();
    }

    private void Flatten()
    {
        int index = 0;
        void Write(List<Entry> scope)
        {
            foreach (var entry in scope)
                if (entry.Children is {} nested) Write(nested);
                else
                {
                    var item = entry.Item;
                    (item.Map == null ? spriteRanks : tileRanks)[item.Index] = index;
                    Items[index++] = item;
                }
        }
        Write(roots);
    }
}
