using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Engine.Core;
using Cell = System.Drawing.Point;
using RectangleF = System.Drawing.RectangleF;
using Vector2 = System.Numerics.Vector2;
using Matrix3x2 = System.Numerics.Matrix3x2;
using Color = System.Drawing.Color;

namespace Engine.Editor;

public sealed partial class SceneViewport
{
    internal Guid? TileEditOwner { get; private set; }
    internal TileTool Tool { get; set; }
    internal int BrushTile { get; set; }
    internal event Action<Guid, TileChange[]>? TilesChanged;
    internal event Action<int>? TilePicked;
    private readonly Dictionary<Guid, TilePalette?> palettes = [];
    private TileStroke? stroke;
    private Cell? hover;
    private Tilemap2DComponent? EditMap => TileEditOwner != null && Selected?.Id == TileEditOwner
        ? Selected.GetComponent<Tilemap2DComponent>() : null;
    internal TilePalette? Palette(Guid? id)
    {
        if (id == null || Scene.ProjectDirectory == null) return null;
        if (palettes.TryGetValue(id.Value, out var palette)) return palette;
        try { palette = TilePalette.Load(Scene.ProjectDirectory, ProjectAssets.ReadManifest(Scene.ProjectDirectory), id.Value); }
        catch (Exception ex) { PlayFailed?.Invoke(ex.Message); }
        return palettes[id.Value] = palette;
    }
    private AssetEntry[] tileAssets = [];
    internal bool RefreshTileAssets(ProjectAssets assets)
    {
        var files = assets.Entries.Where(e => e.Path.EndsWith(SkeletonRig.Extension) || e.Path.EndsWith(ShaderClip.Extension) || e.Path.EndsWith(SpriteAtlas.Extension) || e.Path.EndsWith(ProjectAssets.Tileset) || SpriteAssets.IsPng(e.Path)).Where(e => File.Exists(Path.Combine(Scene.ProjectDirectory!, e.Path))).ToArray();
        bool changed = !tileAssets.SequenceEqual(files);
        tileAssets = files;
        if (changed) { ClearSprites(); return true; }
        return false;
    }

    internal Bitmap? TileImage(string path) => bitmaps.TryGet(path, out var image) ? image : null;
    internal void EditTiles(Guid? owner)
    {
        EndDrag();
        TileEditOwner = owner;
        hover = null;
        RefreshScene();
    }
    internal bool CancelStroke()
    {
        if (stroke == null) return false;
        stroke.Cancel(); stroke = null; RefreshScene();
        return true;
    }
    internal void LeaveTiles() { if (hover != null) { hover = null; RefreshScene(); } }

    private Cell TileAt(Point world)
    {
        var local = Selected!.WorldToLocal(new((float)world.X,(float)world.Y));
        return EditMap!.CellAt(Vector2.Zero,local.X,local.Y);
    }
    private bool BeginTiles(Point world)
    {
        if (EditMap is not { } map) return false;
        var cell = TileAt(world);
        hover = cell;
        if (Tool == TileTool.Eyedropper)
        {
            if (map.Get(cell) is > 0 and var id) TilePicked?.Invoke(id);
        }
        else if (Tool == TileTool.Eraser || Palette(map.Tileset)?.Tiles.ContainsKey(BrushTile) == true)
        {
            stroke = new(map, Tool, BrushTile, cell);
            stroke.Move(cell);
        }
        RefreshScene();
        return true;
    }

    private void DrawTileGuide(ISceneCanvas canvas, RectangleF view, float unit)
    {
        if (EditMap is not { } map) return;
        var matrix = Selected!.WorldMatrix;
        canvas = new TileGuideCanvas(canvas,matrix);
        view = Affine2D.Bounds(view,Affine2D.Inverse(matrix));
        unit /= Math.Max(.001f,Math.Max(Vector2.TransformNormal(Vector2.UnitX,matrix).Length(),Vector2.TransformNormal(Vector2.UnitY,matrix).Length()));
        var origin = Vector2.Zero;
        if (map.Layout != TileGrid.Orthogonal)
        {
            DrawProjectedGuide(canvas,map,origin,view,unit);
            return;
        }
        // Thin out grid lines when zoomed out; painting still targets the full resolution.
        double sx = map.CellSize.Width, sy = map.CellSize.Height;
        while (sx < 6 * unit) sx *= 2;
        while (sy < 6 * unit) sy *= 2;
        double left = origin.X + Math.Floor((view.Left - origin.X) / sx) * sx;
        double top = origin.Y + Math.Floor((view.Top - origin.Y) / sy) * sy;
        int cols = (int)Math.Ceiling(view.Width / sx) + 1, rows = (int)Math.Ceiling(view.Height / sy) + 1;
        for (int i = 0; i < cols; i++) canvas.FillRectangle(new((float)(left + i * sx), view.Top, unit, view.Height), FrameColor);
        for (int i = 0; i < rows; i++) canvas.FillRectangle(new(view.Left, (float)(top + i * sy), view.Width, unit), FrameColor);
        if (hover is not Cell cell) return;
        var rect = map.Bounds(origin, cell);
        if (Tool == TileTool.Rectangle && stroke != null) rect = RectangleF.Union(rect, map.Bounds(origin, stroke.Start));
        else if (Tool == TileTool.Brush && Palette(map.Tileset) is { } palette && palette.Tiles.TryGetValue(map.DisplayTile(palette,cell,BrushTile), out var tile))
            canvas.DrawSprite(palette.Image, rect, tile.Source);
        Outline(canvas, rect, unit * 2, SelectionColor);
    }

    private void DrawProjectedGuide(ISceneCanvas canvas, Tilemap2DComponent map, Vector2 origin, RectangleF view, float unit)
    {
        var outline = TileGrid.Outline(map.Layout);
        if (Math.Min(map.CellSize.Width,map.CellSize.Height) >= 10 * unit)
        {
            foreach (var cell in TileGrid.VisibleCells(map,origin,view))
            {
                var bounds = map.Bounds(origin,cell);
                // Adjacent cells share each edge. Draw just one from each parallel pair.
                for (int i = 0; i < outline.Length/2; i++)
                    canvas.DrawLine(TileGrid.Vertex(bounds,outline[i]),TileGrid.Vertex(bounds,outline[i+1]),unit,FrameColor);
            }
        }
        if (hover is not Cell current) return;
        var rect = map.Bounds(origin,current);
        if (Tool == TileTool.Brush && Palette(map.Tileset) is { } palette && palette.Tiles.TryGetValue(map.DisplayTile(palette,current,BrushTile),out var tile))
            canvas.DrawTile(palette.Image,rect,tile.Source,map.Layout);

        var start = Tool == TileTool.Rectangle && stroke != null ? stroke.Start : current;
        int left = Math.Min(start.X,current.X), right = Math.Max(start.X,current.X);
        int top = Math.Min(start.Y,current.Y), bottom = Math.Max(start.Y,current.Y);
        var (first,last) = map.CellRange(origin,view);
        // Visit only the four boundary rows/columns, even on very large rectangle drags.
        for (long x = Math.Max(first.X,left); x <= Math.Min(last.X,right); x++)
        {
            Boundary(new((int)x,top));
            if (top != bottom) Boundary(new((int)x,bottom));
        }
        for (long y = Math.Max(first.Y,(long)top+1); y <= Math.Min(last.Y,(long)bottom-1); y++)
        {
            Boundary(new(left,(int)y));
            if (left != right) Boundary(new(right,(int)y));
        }

        void Boundary(Cell cell)
        {
            var box = map.Bounds(origin,cell);
            if (!box.IntersectsWith(view)) return;
            var points = TileGrid.Outline(map.Layout);
            for (int i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i+1)%points.Length];
                var across = (a+b)*.51f - new Vector2(.01f);
                // Sample just beyond this edge in cell-local space to identify its neighbour.
                var neighbor = map.CellAt(Vector2.Zero,across.X*map.CellSize.Width,across.Y*map.CellSize.Height);
                long q = (long)cell.X+neighbor.X, r = (long)cell.Y+neighbor.Y;
                if (q < left || q > right || r < top || r > bottom)
                    canvas.DrawLine(TileGrid.Vertex(box,a),TileGrid.Vertex(box,b),2*unit,SelectionColor);
            }
        }
    }
    private sealed class TileGuideCanvas(ISceneCanvas target, Matrix3x2 matrix) : ISceneCanvas
    {
        public void Clear(Color c) => throw new NotSupportedException();
        public void FillRectangle(RectangleF r, Color c) => target.DrawAffine(null,r,matrix,c);
        public void FillStrip(ReadOnlySpan<Vector2> edges, ReadOnlySpan<Color> colors, Matrix3x2 m) => target.FillStrip(edges,colors,m*matrix);
        public void DrawLine(Vector2 a,Vector2 b,float w,Color c) => target.DrawLine(Vector2.Transform(a,matrix),Vector2.Transform(b,matrix),
            w * Vector2.TransformNormal(Vector2.UnitX,matrix).Length(),c);
        public bool DrawSprite(string p,RectangleF r) => DrawSprite(p,r,null);
        public bool DrawSprite(string p,RectangleF r,SpriteRegion? source) => target.DrawAffine(p,r,matrix,Color.White,source);
        public bool DrawTile(string p,RectangleF r,SpriteRegion source,string layout) => target.DrawAffine(p,r,matrix,Color.White,source,layout);
    }

}
