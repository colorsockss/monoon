using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public readonly record struct SpriteReference(Guid Id);

public static class SpriteAssets
{
    public const string Folder = "Assets";
    public static readonly string SquarePath = Path.Combine(AppContext.BaseDirectory, "EngineAssets", "Square.png");
    public static bool IsPng(string path) => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase);

    public static bool IsReference(string path) =>
        !Path.IsPathRooted(path) && IsPng(path) &&
        path.Split('/').All(part => part.Length > 0 && part != "." && part != ".." &&
            !part.Any(c => char.IsControl(c) || "<>:\"\\|?*".Contains(c)));
}

// The editor and the running game use the same draw command and fallback.
public readonly record struct SceneAppearance(string? Image, Color Color, SpriteRegion? Source = null, SpriteCanvas? Canvas = null,
    bool FlipX = false, bool FlipY = false, bool PixelSnap = false, string? Mask = null,
    SpriteDrawMode Mode = SpriteDrawMode.Simple, UiBorders? Slice = null)
{
    // The tile of a Tiled or Sliced sprite in pixels: its atlas cell or whole PNG. Empty until the canvas has the image.
    public Size TileSize(ICanvas2D canvas) => Source is {} cell ? new(cell.Width, cell.Height) : Image == null ? Size.Empty : canvas.SpriteSize(Image);

    public static SceneAppearance From(SceneObject obj, string? project, ProjectAssets? assets = null)
    {
        return obj.GetComponent<SpriteRendererComponent>() is {} renderer
            ? From(renderer, project, assets) : default;
    }

    internal static SceneAppearance From(SpriteRendererComponent r, string? project, ProjectAssets? assets)
    {
        SceneObject.TryColor(r.Tint ?? (r.Asset == null && r.AssetId == null ? r.SquareColor : "#FFFFFF"), out var color);
        var packed = project != null && r.AssetId.HasValue ? assets?.Sprites(project).Find(r.AssetId) : null;
        var asset = packed?.Sheet ?? r.Asset;
        var image = asset == null ? SpriteAssets.SquarePath : project == null ? asset : Path.Combine(project,asset);
        string? mask = null;
        if (r.Mask is Guid id)
        {
            var name = assets?.PathFor(id);
            if (project == null || name == null || !SpriteAssets.IsReference(name))
                throw new InvalidDataException($"Sprite Renderer: missing PNG mask {id}.");
            mask = Path.Combine(project,name);
        }
        return new(image,color,packed?.Entry.Source,packed?.Entry.Canvas,r.FlipX,r.FlipY,r.PixelSnap,mask,r.DrawMode ?? SpriteDrawMode.Simple,r.Slice);
    }

    public void Draw(ICanvas2D canvas, RectangleF local, Matrix3x2 matrix)
    {
        if (Image == null) return;
        if (PixelSnap) matrix.Translation = new(MathF.Round(matrix.M31), MathF.Round(matrix.M32));
        if (FlipX || FlipY)
            matrix = Matrix3x2.CreateScale(FlipX ? -1 : 1, FlipY ? -1 : 1,
                new Vector2(local.X+local.Width/2, local.Y+local.Height/2)) * matrix;
        if (Mode != SpriteDrawMode.Simple && Mask == null && TileSize(canvas) is { IsEmpty: false } tile &&
            (Slice == null || (long)Slice.Left + Slice.Right < tile.Width && (long)Slice.Top + Slice.Bottom < tile.Height))
        {
            if (!canvas.DrawGrid(Image, local, matrix, Color, Source, tile, Slice, Mode == SpriteDrawMode.Tiled))
                canvas.DrawAffine(null, local, matrix, Color.Magenta);
            return;
        }
        if (Source is {} src && Canvas is {} trim) local = trim.Place(local,src);
        bool drawn = Mask == null ? canvas.DrawAffine(Image,local,matrix,Color,Source) :
            canvas.DrawFiltered(new SpriteFilter(Image, Source, Canvas, Mask),local,matrix,Color);
        if (!drawn) canvas.DrawAffine(null,local,matrix,Color.Magenta);
    }

    public void Draw(ICanvas2D canvas, RectangleF bounds) => Draw(canvas,bounds,Matrix3x2.Identity);
}

public sealed class SpriteCache<T>(Func<string, T> load) : IDisposable where T : IDisposable
{
    private readonly Dictionary<string, T> images = new(StringComparer.Ordinal);
    private readonly HashSet<string> missing = new(StringComparer.Ordinal);

    public bool TryGet(string path, out T image)
    {
        if (images.TryGetValue(path, out image!)) return true;
        if (missing.Contains(path)) return false;
        try
        {
            image = load(path);
            images.Add(path, image);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            missing.Add(path);
            return false;
        }
    }

    public bool Contains(string path) => images.ContainsKey(path);
    public int Count => images.Count;

    /// <summary>Stores an image loaded elsewhere, e.g. decoded on a worker thread.</summary>
    public void Add(string path, T image)
    {
        missing.Remove(path);
        images.Add(path, image);
    }

    public void Clear()
    {
        foreach (var image in images.Values) image.Dispose();
        images.Clear();
        missing.Clear();
    }

    public void Invalidate(string path)
    {
        missing.Remove(path);
        if (images.Remove(path, out var previous)) previous.Dispose();
    }

    public void Dispose() => Clear();
}

public readonly record struct SpriteFilter(string Image, SpriteRegion? Source, SpriteCanvas? Canvas, string? Mask);
