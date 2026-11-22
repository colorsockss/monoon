using System.Drawing;

namespace Engine.Core;

public sealed partial class SceneGame
{
    internal sealed class RenderGrid
    {
        private readonly Body[] bodies;
        private readonly SpatialGrid grid;
        private readonly bool[] queued;
        private readonly List<int> moving = [], candidates = [], ranks = [];

        internal RenderGrid(Body[] bodies)
        {
            this.bodies = bodies;
            grid = new(bodies.Length);
            queued = new bool[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                grid.Add(i, Bounds(bodies[i], 1));
                if (bodies[i].Moved) Mark(i);
            }
        }

        private static RectangleF? Bounds(Body body, float fraction)
        {
            // These drawables can extend beyond their owner's rectangle.
            if (body.Map != null || body.Skeleton != null || body.SpritePool != null) return null;
            return Affine2D.Bounds(new(0, 0, body.Size.X, body.Size.Y),
                body.MatrixAt(body.At(fraction), body.AngleAt(fraction)));
        }

        internal void Mark(int index)
        {
            if (queued[index]) return;
            queued[index] = true;
            moving.Add(index);
        }

        internal void Sample(float fraction)
        {
            int keep = 0;
            for (int i = 0; i < moving.Count; i++)
            {
                int index = moving[i];
                var body = bodies[index];
                grid.Update(index, Bounds(body, fraction));
                // The same simulation tick may be drawn at another interpolation fraction.
                if (body.Moved) moving[keep++] = index;
                else queued[index] = false;
            }
            moving.RemoveRange(keep, moving.Count - keep);
        }

        internal List<int>? Ranks(RectangleF frame, WorldDrawOrder order)
        {
            if (!grid.Collect(frame, candidates) || candidates.Count > bodies.Length / 2) return null;
            ranks.Clear();
            foreach (int body in candidates) order.AddRanks(body, ranks);
            ranks.Sort();
            return ranks;
        }
    }
}
