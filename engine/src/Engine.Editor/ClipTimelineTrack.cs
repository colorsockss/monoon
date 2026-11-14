using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Engine.Core;

namespace Engine.Editor;

internal sealed class ClipTimelineTrack : Control
{
    internal sealed record Cell(string Label, IImage? Image, bool Invalid);
    private static readonly IBrush Surface = Brush.Parse("#191D23"), Tile = Brush.Parse("#2B323C"),
        SelectedTile = Brush.Parse("#344B45"), Accent = Brush.Parse("#9EDAC9"), Muted = Brush.Parse("#A6B2C0");
    private static readonly Pen Divider = new(Brush.Parse("#52606D"), 1), Head = new(Accent, 2);
    private SpriteClipTimeline axis = new([]);
    private Cell[] cells = [];
    private int selected, insertion = -1, pressed = -1;
    private double cursor, origin;
    private bool scrubbing;
    private IPointer? pointer;
    public Action<int>? SelectFrame;
    public Action<int, int>? MoveFrame;
    public Action<double>? Scrub;

    public ClipTimelineTrack()
    {
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    public void SetFrames(SpriteClipTimeline time, Cell[] items)
    {
        axis = time; cells = items;
        MinWidth = Math.Max(100, items.Length * 76d);
        InvalidateVisual();
    }
    public void SetCursor(int selection, double time)
    {
        selected = selection; cursor = time; InvalidateVisual();
    }
    private double X(double time) => axis.Duration == 0 ? 0 : time / axis.Duration * Bounds.Width;
    private double Time(double x) => Math.Clamp(x / Math.Max(1, Bounds.Width), 0, 1) * axis.Duration;
    public int Slot(double x)
    {
        int i = axis.FrameAt(Time(x));
        return i < 0 ? 0 : i + (x >= (X(axis.Start(i)) + X(axis.Start(i + 1))) / 2 ? 1 : 0);
    }
    public void Mark(int slot) { if (insertion != slot) { insertion = slot; InvalidateVisual(); } }

    private static void Label(DrawingContext c, string s, double x, double y, IBrush brush) =>
        c.DrawText(new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, brush), new Point(x, y));

    public override void Render(DrawingContext c)
    {
        base.Render(c);
        var area = new Rect(Bounds.Size);
        c.FillRectangle(Surface, area);
        if (cells.Length == 0) Label(c, "Drop project PNGs here to add frames", 8, 30, Muted);
        else
        {
            // Choose a readable 1/2/5 ruler interval independently of frame boundaries.
            var target = axis.Duration * 82 / Math.Max(1, Bounds.Width);
            var unit = Math.Pow(10, Math.Floor(Math.Log10(target)));
            var step = (target / unit <= 1 ? 1 : target / unit <= 2 ? 2 : target / unit <= 5 ? 5 : 10) * unit;
            for (int k = 0; k * step < axis.Duration; k++)
            {
                double x = X(k * step);
                Label(c, (k * step).ToString("G4", CultureInfo.InvariantCulture) + "s", x + 4, 1, Muted);
                c.DrawLine(Divider, new(x, 15), new(x, 21));
            }
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                double left = X(axis.Start(i)), width = X(axis.Start(i+1)) - left;
                var rect = new Rect(left, 22, Math.Max(0, width - 1), Math.Max(0, Bounds.Height - 22));
                if (rect.Width <= 0) continue;
                using (c.PushClip(rect))
                {
                    c.FillRectangle(selected == i ? SelectedTile : Tile, rect);
                    if (cell.Image is {} bitmap)
                    {
                        var scale = Math.Min(38 / bitmap.Size.Height, Math.Min(52, width) / bitmap.Size.Width);
                        c.DrawImage(bitmap, new Rect(left + 6, 26, bitmap.Size.Width * scale, bitmap.Size.Height * scale));
                    }
                    Label(c, $"{i+1}  {cell.Label}", left + 6, 66, cell.Invalid ? Brushes.Salmon : Brushes.White);
                    if (selected == i) c.FillRectangle(Accent, new Rect(left, 22, width, 2));
                }
            }
            double head = Math.Clamp(X(cursor), 1, Math.Max(1, Bounds.Width-1));
            c.DrawLine(Head, new(head, 12), new(head, Bounds.Height));
        }
        if (insertion >= 0 && insertion <= axis.Count)
        {
            double x = Math.Clamp(X(axis.Start(insertion)), 2, Math.Max(2, Bounds.Width-2));
            c.DrawLine(new Pen(Accent, 3), new(x, 18), new(x, Bounds.Height));
        }
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (cells.Length == 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(this); Focus(); origin = p.X; scrubbing = p.Y < 22;
        pointer = e.Pointer; pointer.Capture(this);
        if (scrubbing) Scrub?.Invoke(Time(p.X));
        else { pressed = axis.FrameAt(Time(p.X)); SelectFrame?.Invoke(pressed); }
        e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (pointer != e.Pointer) return;
        var x = e.GetPosition(this).X;
        if (scrubbing) Scrub?.Invoke(Time(x));
        else if (Math.Abs(x-origin) >= 4 || insertion >= 0) Mark(Slot(x));
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (pointer != e.Pointer) return;
        var p = e.GetPosition(this);
        var from = pressed; bool move = !scrubbing && Math.Abs(p.X-origin) >= 4 && new Rect(Bounds.Size).Contains(p);
        int to = Slot(p.X);
        pointer.Capture(null);
        if (move && from >= 0) MoveFrame?.Invoke(from, to);
        e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e); pointer = null; pressed = -1; scrubbing = false; Mark(-1);
    }
}
