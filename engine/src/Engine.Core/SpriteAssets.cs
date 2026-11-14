using System.Drawing;
using System.Numerics;

namespace Engine.Core;

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
    bool FlipX = false, bool FlipY = false, bool PixelSnap = false, string? Mask = null)
{
    public static SceneAppearance From(SceneObject obj, string? project, ProjectAssets? assets = null)
    {
        if (obj.GetComponent<SpriteRendererComponent>() is not { } r) return default;
        SceneObject.TryColor(r.Tint ?? (r.Asset == null ? r.SquareColor : "#FFFFFF"), out var color);
        var packed = project != null && r.AssetId.HasValue ? assets?.Sprites(project).Find(r.AssetId) : null;
        var asset = packed?.Sheet ?? r.Asset;
        var image = asset == null ? SpriteAssets.SquarePath : project == null ? asset : Path.Combine(project,asset);
        string? mask = null;
        if (r.Mask is Guid id)
        {
            var name = assets?.PathFor(id);
            if (project == null || name == null || !SpriteAssets.IsReference(name))
                throw new InvalidDataException($"{obj.Name}: missing PNG mask {id}.");
            mask = Path.Combine(project,name);
        }
        return new(image,color,packed?.Entry.Source,packed?.Entry.Canvas,r.FlipX,r.FlipY,r.PixelSnap,mask);
    }

    public void Draw(ICanvas2D canvas, RectangleF local, Matrix3x2 matrix)
    {
        if (Image == null) return;
        if (PixelSnap) matrix.Translation = new(MathF.Round(matrix.M31), MathF.Round(matrix.M32));
        if (FlipX || FlipY)
            matrix = Matrix3x2.CreateScale(FlipX ? -1 : 1, FlipY ? -1 : 1,
                new Vector2(local.X+local.Width/2, local.Y+local.Height/2)) * matrix;
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
