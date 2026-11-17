using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Engine.Core;

namespace Engine.Editor;

// Grid settings are authoring aids. Only the resulting pixel rectangles enter the clip.
internal sealed record SheetGrid(int Width, int Height, int MarginX, int MarginY, int GapX, int GapY)
{
    public int Columns(int pixels) => Count(pixels, Width, MarginX, GapX);
    public int Rows(int pixels) => Count(pixels, Height, MarginY, GapY);
    private static int Count(int pixels, int cell, int margin, int gap) =>
        (int)(Math.Max(0L, pixels - 2L * margin + gap) / (cell + (long)gap));

    public SpriteRegion Region(int column, int row) =>
        new((int)(MarginX + column * ((long)Width + GapX)), (int)(MarginY + row * ((long)Height + GapY)), Width, Height);
}

internal sealed class SheetGridView : Control
{
    public event Action? SelectionChanged;
    private Bitmap? image;
    private SheetGrid? grid;
    private SheetSlice[]? slices;
    private readonly SortedSet<(int Row, int Col)> selected = [];
    private static readonly IBrush Back = Brush.Parse("#252B32"), Tint = Brush.Parse("#4668C5A9");
    private static readonly Pen Line = new(Brush.Parse("#A8BEC9D4"), 1), SelectedLine = new(Brush.Parse("#9EDAC9"), 2);
    internal bool SingleSelection { get; set; }
    public int Count => selected.Count;
    public SpriteRegion[] Regions() => selected.Select(c => slices == null ? grid!.Region(c.Col, c.Row) : slices[c.Col].Source).ToArray();
    internal bool IsSliceSelected(int index) => selected.Contains((0, index));
    internal void ToggleSlice(int index) => Toggle(0, index);
    internal SheetSlice[] Selection() => selected.Select(c => slices == null
        ? new SheetSlice(grid!.Region(c.Col, c.Row)) : slices[c.Col]).ToArray();

    public void SetSheet(Bitmap? sheet, SheetGrid? layout, SheetSlice[]? packed = null)
    {
        if (ReferenceEquals(image,sheet) && grid == layout && ReferenceEquals(slices, packed)) return;
        image = sheet;
        grid = layout;
        slices = packed;
        selected.Clear();
        InvalidateVisual();
        SelectionChanged?.Invoke();
    }

    internal void RestoreSelection(SpriteRegion[] previous)
    {
        selected.Clear();
        if (image != null)
        foreach (var region in previous)
        {
            if (slices != null)
            {
                int index = Array.FindIndex(slices, s => s.Source == region);
                if (index >= 0) selected.Add((0, index));
            }
            else if (grid != null && region.Fits(image.PixelSize.Width, image.PixelSize.Height))
            {
                int col = (int)((region.X - grid.MarginX) / ((long)grid.Width + grid.GapX));
                int row = (int)((region.Y - grid.MarginY) / ((long)grid.Height + grid.GapY));
                if (col >= 0 && row >= 0 && grid.Region(col, row) == region) selected.Add((row, col));
            }
        }
        InvalidateVisual();
        SelectionChanged?.Invoke();
    }

    private Rect SheetBounds()
    {
        if (image == null) return default;
        var pixels = image.PixelSize;
        double scale = Math.Min(Bounds.Width / pixels.Width, Bounds.Height / pixels.Height);
        var width = pixels.Width * scale;
        var height = pixels.Height * scale;
        return new Rect((Bounds.Width-width)/2, (Bounds.Height-height)/2, width, height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Back, new Rect(Bounds.Size));
        var area = SheetBounds();
        if (image == null || area.Width <= 0) return;
        var pixels = image.PixelSize;
        SpriteCellImage.DrawPixels(context,image,new Rect(0,0,pixels.Width,pixels.Height),area);
        double scale = area.Width / pixels.Width;
        double x(int pixel) => area.X + pixel * scale;
        double y(int pixel) => area.Y + pixel * scale;
        if (slices != null)
        {
            for (int i = 0; i < slices.Length; i++)
            {
                var r = slices[i].Source;
                bool active = selected.Contains((0, i));
                context.DrawRectangle(active ? Tint : null, active ? SelectedLine : Line,
                    new Rect(x(r.X), y(r.Y), r.Width * scale, r.Height * scale));
            }
            return;
        }
        if (grid == null) return;
        int cols = grid.Columns(pixels.Width), rows = grid.Rows(pixels.Height);
        if (cols == 0 || rows == 0) return;
        var last = grid.Region(cols-1, rows-1);
        if (grid.Width * scale >= 2)
            for (int col = 0; col < cols; col++)
            {
                int left = grid.Region(col,0).X;
                context.DrawLine(Line,new(x(left),y(grid.MarginY)),new(x(left),y(last.Y + last.Height)));
                if (grid.GapX != 0 || col == cols-1)
                    context.DrawLine(Line,new(x(left+grid.Width),y(grid.MarginY)),new(x(left+grid.Width),y(last.Y + last.Height)));
            }
        if (grid.Height * scale >= 2)
            for (int row = 0; row < rows; row++)
            {
                int top = grid.Region(0,row).Y;
                context.DrawLine(Line,new(x(grid.MarginX),y(top)),new(x(last.X + last.Width),y(top)));
                if (grid.GapY != 0 || row == rows-1)
                    context.DrawLine(Line,new(x(grid.MarginX),y(top+grid.Height)),new(x(last.X + last.Width),y(top+grid.Height)));
            }
        foreach (var (row,col) in selected)
        {
            var r = grid.Region(col,row);
            context.DrawRectangle(Tint,SelectedLine,new Rect(x(r.X),y(r.Y),scale*r.Width,scale*r.Height));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (image == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(this);
        var area = SheetBounds();
        if (area.Width == 0 || !area.Contains(p)) return;
        var scale = image.PixelSize.Width / area.Width;
        var x = (int)Math.Floor((p.X - area.X)*scale);
        var y = (int)Math.Floor((p.Y - area.Y)*scale);
        if (slices != null)
        {
            int index = Array.FindIndex(slices, slice => x >= slice.Source.X && x < (long)slice.Source.X + slice.Source.Width &&
                y >= slice.Source.Y && y < (long)slice.Source.Y + slice.Source.Height);
            if (index >= 0) { ToggleSlice(index); e.Handled = true; }
            return;
        }
        if (grid == null) return;
        x -= grid.MarginX;
        y -= grid.MarginY;
        if (x < 0 || y < 0) return;
        long stepX = (long)grid.Width + grid.GapX, stepY = (long)grid.Height + grid.GapY;
        int col = (int)(x/stepX), row = (int)(y/stepY);
        if (col >= grid.Columns(image.PixelSize.Width) || row >= grid.Rows(image.PixelSize.Height) ||
            x % stepX >= grid.Width || y % stepY >= grid.Height) return;
        Toggle(row, col);
        e.Handled = true;
    }

    private void Toggle(int row, int col)
    {
        if (SingleSelection) selected.Clear();
        if (!selected.Add((row,col))) selected.Remove((row,col));
        InvalidateVisual();
        SelectionChanged?.Invoke();
    }
}
