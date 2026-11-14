using System.Drawing;
using System.Numerics;
using Brutal.SdlApi;
using Engine.Core;

namespace Engine.Desktop;

internal sealed class SdlSpriteBatch(SdlRenderer renderer)
{
    private readonly SdlVertex[] vertices = new SdlVertex[4096];
    private readonly int[] indices = new int[12288];
    private SdlTexture texture;
    private int vertexCount, indexCount;

    public void Add(SdlTexture nextTexture, RectangleF bounds, Color tint, SpriteRegion? source, Size size, string layout, Matrix3x2? transform = null)
    {
        var shape = TileGrid.Outline(layout);
        int first = Reserve(nextTexture, shape.Length);
        var color = new SdlFColor { r = tint.R / 255f, g = tint.G / 255f, b = tint.B / 255f, a = tint.A / 255f };
        foreach (var uv in shape)
        {
            float u = uv.X, v = uv.Y;
            if (source != null)
            {
                u = (source.X + u * source.Width) / size.Width;
                v = (source.Y + v * source.Height) / size.Height;
            }
            var point = TileGrid.Vertex(bounds, uv);
            vertices[first++] = Vertex(transform is {} m ? Vector2.Transform(point,m) : point, u, v, color);
        }
    }

    public void Line(Vector2 start, Vector2 end, float width, Color tint)
    {
        var delta = end - start;
        if (delta == Vector2.Zero) return;
        var side = Vector2.Normalize(new(-delta.Y, delta.X)) * (width * .5f);
        var color = new SdlFColor { r = tint.R / 255f, g = tint.G / 255f, b = tint.B / 255f, a = tint.A / 255f };
        int first = Reserve(default, 4);
        vertices[first] = Vertex(start - side, 0, 0, color);
        vertices[first + 1] = Vertex(end - side, 0, 0, color);
        vertices[first + 2] = Vertex(end + side, 0, 0, color);
        vertices[first + 3] = Vertex(start + side, 0, 0, color);
    }

    private int Reserve(SdlTexture nextTexture, int count)
    {
        if (vertexCount + count > vertices.Length || texture.Pointer != nextTexture.Pointer) Flush();
        texture = nextTexture;
        int first = vertexCount;
        vertexCount += count;
        for (int i = 2; i < count; i++)
        {
            indices[indexCount++] = first;
            indices[indexCount++] = first + i - 1;
            indices[indexCount++] = first + i;
        }
        return first;
    }

    public long Submissions {get;private set;}
    public unsafe void Flush()
    {
        if (vertexCount == 0) return;
        Submissions++;
        int usedVertices = vertexCount, usedIndices = indexCount;
        vertexCount = indexCount = 0;
        fixed (SdlVertex* vertexData = vertices)
        fixed (int* indexData = indices)
            if (!renderer.RenderGeometry(texture, vertexData, usedVertices, indexData, usedIndices))
                throw new InvalidOperationException($"Could not draw sprite batch: {Sdl.PInvoke.GetError()}");
    }

    private static SdlVertex Vertex(Vector2 p, float u, float v, SdlFColor color) => new()
    {
        position = new SdlFPoint { x = p.X, y = p.Y },
        tex_coord = new SdlFPoint { x = u, y = v },
        color = color
    };
}
