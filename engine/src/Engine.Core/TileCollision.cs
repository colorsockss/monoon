using System.Drawing;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

/// <summary>Normalized cell coordinates, from the top left. A directed face from left to right blocks from above.</summary>
[YamlObject]
public sealed partial record TileCollision(ColliderVertex[] Vertices, bool OneWay = false)
{
    public void Validate(string source, int tile)
    {
        if (Vertices == null || Vertices.Any(p => !(p.X is >= 0 and <= 1 && p.Y is >= 0 and <= 1)))
            throw new InvalidDataException($"{source}: tile #{tile} needs vertices within the cell (0 to 1).");
        if (OneWay ? Vertices.Length != 2 || Vertices[0] == Vertices[1] :
            !CollisionGeometry.Convex(Vertices.Select(p => new Vector2(p.X,p.Y)).ToArray()))
            throw new InvalidDataException($"{source}: tile #{tile} needs " + (OneWay ?
                "two different endpoints for its one-way face." : "3–32 vertices in convex boundary order."));
    }

    internal static Shape2D? WorldShape(TileDefinition tile, RectangleF bounds, string layout, Matrix3x2 matrix)
    {
        if (tile.Collision is not {} custom) return tile.Solid ? CollisionGeometry.Tile(bounds,layout,matrix) : null;
        Vector2 Vertex(ColliderVertex p) => Vector2.Transform(TileGrid.Vertex(bounds,new(p.X,p.Y)),matrix);
        if (!custom.OneWay) return Shape2D.Hull(Array.ConvertAll(custom.Vertices, Vertex));
        var a = Vertex(custom.Vertices[0]);
        var b = Vertex(custom.Vertices[1]);
        var edge = b - a;
        var normal = Vector2.Normalize(new Vector2(edge.Y,-edge.X));
        return Shape2D.Face(a,b,matrix.GetDeterminant() < 0 ? -normal : normal);
    }
}

internal static partial class CollisionGeometry
{
    internal static double FaceGap(Shape2D shape, Shape2D face)
    {
        var normal = face.FaceNormal;
        var origin = face.Vertices![0] + face.Offset;
        var offset = shape.Offset - origin;
        if (shape.Vertices == null) return Dot(offset,normal) - shape.Radius;
        double gap = double.PositiveInfinity;
        foreach (var p in shape.Vertices) gap = Math.Min(gap, Dot(p + offset,normal));
        return gap;
    }

    private static Contact? CastFace(Shape2D shape, Vector2 travel, Shape2D face, bool query)
    {
        // Test the supporting feature at plane crossing, then clip it to the finite face.
        // Edges have no back wall or end caps, including while moving almost parallel to the plane.
        var n = face.FaceNormal;
        var gap = FaceGap(shape,face);
        var speed = Dot(travel,n);
        if (gap < -Eps || speed >= -1e-6 * travel.Length() && !(query && travel == default && gap <= Eps)) return null;
        double time = gap <= Eps ? 0 : -gap / speed;
        if (time < 0 || time > 1) return null;
        var edge = face.Vertices![1] - face.Vertices[0];
        var tangent = Vector2.Normalize(edge);
        var origin = face.Offset + face.Vertices[0];
        var offset = shape.Offset - origin;
        double shift = time * Dot(travel,tangent);
        double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
        if (shape.Vertices == null) lo = hi = Dot(offset - shape.Radius * n,tangent) + shift;
        else foreach (var p in shape.Vertices)
        {
            if (Dot(p + offset,n) > gap + Eps) continue;
            double along = Dot(p + offset,tangent) + shift;
            lo = Math.Min(lo,along); hi = Math.Max(hi,along);
        }
        return hi >= -Eps && lo <= edge.Length() + Eps ? new((float)time,n) : null;
    }
}
