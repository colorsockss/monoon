using System.Drawing;

namespace Engine.Core;

public sealed partial class SceneGame
{
    internal sealed class ColliderGrid
    {
        private readonly Body[] bodies;
        private readonly SpatialGrid grid;

        internal ColliderGrid(Body[] colliders)
        {
            bodies = colliders;
            grid = new(colliders.Length);
            for (int i = 0; i < bodies.Length; i++) grid.Add(i, Bounds(i));
        }

        private RectangleF Bounds(int index) => bodies[index].ShapeAt(bodies[index].Position).Bounds;
        internal void Update(int index) => grid.Update(index, Bounds(index));

        internal List<int>? Find(RectangleF bounds)
        {
            var result = new List<int>();
            if (!grid.Collect(bounds, result)) return null;
            result.Sort();
            return result;
        }
    }
}
