using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Engine.Core;

namespace Engine.Editor;

// A lifetime-curve graph. Time runs 0–1 across; the value axis spans the keys and 0–1. The line is the runtime's own 64-sample
// table (ParticleLut.Bake), so what the editor shows is what particles evaluate. Drag a key to move it (one change per drag),
// double-click empty space to add one, Delete removes the selected key. Selection and drag only repaint: no layout changes.
internal sealed class CurveView : Control
{
    private const double Pad = 6, Hit = 6;
    private readonly float low, high;
    private ParticleKey[] keys = [new(0, 1)];
    private ParticleKey[]? other;
    private bool smooth, moved;
    private (float Low, float High)? frozen;
    public int Selected { get; private set; }
    public bool Dragging { get; private set; }
    public event Action? SelectionChanged;
    public event Action<ParticleKey[], bool>? KeysChanged;

    public CurveView(float low, float high)
    {
        (this.low, this.high) = (low, high);
        Height = 84;
        Focusable = true;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public ParticleKey[] Keys => keys;
    // What the graph draws: the same table particles read.
    internal float[] Samples => ParticleLut.Bake(keys, smooth);

    public void Show(ParticleKey[] active, ParticleKey[]? second, bool smoothCurve)
    {
        (keys, other, smooth) = (active, second, smoothCurve);
        Selected = Math.Clamp(Selected, 0, keys.Length - 1);
        InvalidateVisual();
    }

    public void Select(int index)
    {
        Selected = Math.Clamp(index, 0, keys.Length - 1);
        InvalidateVisual();
        SelectionChanged?.Invoke();
    }

    private (float Low, float High) Range()
    {
        if (frozen is { } f) return f;
        float lo = 0, hi = 1;
        foreach (var k in keys) (lo, hi) = (MathF.Min(lo, k.Value), MathF.Max(hi, k.Value));
        foreach (var k in other ?? []) (lo, hi) = (MathF.Min(lo, k.Value), MathF.Max(hi, k.Value));
        foreach (var v in Samples) (lo, hi) = (MathF.Min(lo, v), MathF.Max(hi, v));
        float pad = (hi - lo) * .08f;
        return (lo - pad, hi + pad);
    }

    private Point ToScreen(float t, float v)
    {
        var (lo, hi) = Range();
        double w = Math.Max(1, Bounds.Width - 2 * Pad), h = Math.Max(1, Bounds.Height - 2 * Pad);
        return new(Pad + t * w, Pad + (hi - v) / (hi - lo) * h);
    }

    private (float T, float V) FromScreen(Point p)
    {
        var (lo, hi) = Range();
        double w = Math.Max(1, Bounds.Width - 2 * Pad), h = Math.Max(1, Bounds.Height - 2 * Pad);
        return ((float)((p.X - Pad) / w), (float)(hi - (p.Y - Pad) / h * (hi - lo)));
    }

    public override void Render(DrawingContext context)
    {
        var area = new Rect(Bounds.Size);
        context.DrawRectangle(EditorTokens.Brush("EditorField"), new Pen(EditorTokens.Brush(IsFocused ? "EditorBorderHover" : "EditorBorder")), area.Deflate(.5), 3, 3);
        var grid = new Pen(EditorTokens.Brush("EditorDivider"));
        for (int i = 1; i < 4; i++) context.DrawLine(grid, ToScreen(i / 4f, 0).WithY(Pad), ToScreen(i / 4f, 0).WithY(Bounds.Height - Pad));
        var (lo, hi) = Range();
        foreach (var v in new[] { 0f, 1f })
            if (v > lo && v < hi) context.DrawLine(grid, ToScreen(0, v), ToScreen(1, v));
        if (other != null) Line(context, ParticleLut.Bake(other, smooth), new Pen(EditorTokens.Brush("EditorMuted"), 1));
        Line(context, Samples, new Pen(EditorTokens.Brush("EditorAccent"), 1.5));
        foreach (var k in other ?? []) context.DrawRectangle(null, new Pen(EditorTokens.Brush("EditorMuted")), Square(k, 2.5));
        for (int i = 0; i < keys.Length; i++)
            context.DrawRectangle(EditorTokens.Brush(i == Selected ? "EditorAccent" : "EditorText"),
                new Pen(EditorTokens.Brush(i == Selected ? "EditorText" : "EditorField")), Square(keys[i], 3.5));
    }

    private Rect Square(ParticleKey k, double r) { var p = ToScreen(k.Time, k.Value); return new(p.X - r, p.Y - r, 2 * r, 2 * r); }

    private void Line(DrawingContext context, float[] samples, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(ToScreen(0, samples[0]), false);
            for (int k = 1; k < samples.Length; k++) g.LineTo(ToScreen(k / (float)(samples.Length - 1), samples[k]));
            g.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
    }

    private int KeyAt(Point p)
    {
        int best = -1;
        double nearest = Hit;
        for (int i = 0; i < keys.Length; i++)
        {
            var s = ToScreen(keys[i].Time, keys[i].Value);
            double d = Math.Max(Math.Abs(s.X - p.X), Math.Abs(s.Y - p.Y));
            if (d <= nearest) (best, nearest) = (i, d);
        }
        return best;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        var p = e.GetPosition(this);
        int hit = KeyAt(p);
        if (hit >= 0) { BeginDrag(hit); e.Pointer.Capture(this); }
        else if (e.ClickCount == 2) { var (t, v) = FromScreen(p); AddAt(t, v); }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!Dragging) return;
        var (t, v) = FromScreen(e.GetPosition(this));
        DragTo(t, v);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (Dragging) { e.Pointer.Capture(null); EndDrag(); }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (Dragging) EndDrag();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Delete or Key.Back) { DeleteSelected(); e.Handled = true; }
    }

    // The pointer handlers' steps, also used by the editor probe.
    internal void BeginDrag(int index)
    {
        Select(index);
        (Dragging, moved, frozen) = (true, false, Range());
    }

    internal void DragTo(float time, float value)
    {
        int i = Selected;
        float t = Math.Clamp(MathF.Round(time, 3), i > 0 ? keys[i - 1].Time : 0, i < keys.Length - 1 ? keys[i + 1].Time : 1);
        float v = Math.Clamp(MathF.Round(value, 3), low, high);
        if (t == keys[i].Time && v == keys[i].Value) return;
        var copy = (ParticleKey[])keys.Clone();
        copy[i] = copy[i] with { Time = t, Value = v };
        (keys, moved) = (copy, true);
        InvalidateVisual();
        KeysChanged?.Invoke(keys, false);
    }

    internal void EndDrag()
    {
        (Dragging, frozen) = (false, null);
        InvalidateVisual();
        if (moved) KeysChanged?.Invoke(keys, true);
        moved = false;
    }

    internal void AddAt(float time, float value)
    {
        if (keys.Length >= 8) return;
        var key = new ParticleKey(Math.Clamp(MathF.Round(time, 3), 0, 1), Math.Clamp(MathF.Round(value, 3), low, high));
        int at = 0;
        while (at < keys.Length && keys[at].Time <= key.Time) at++;
        keys = [.. keys[..at], key, .. keys[at..]];
        Selected = at;
        KeysChanged?.Invoke(keys, true);
    }

    internal void DeleteSelected()
    {
        if (keys.Length <= 1) return;
        keys = [.. keys[..Selected], .. keys[(Selected + 1)..]];
        Selected = Math.Min(Selected, keys.Length - 1);
        KeysChanged?.Invoke(keys, true);
    }
}

// A curve field in label-column rows: the graph, the selected key's time and value, its smooth-curve slopes, one or two curves
// (each particle picks a point between them), add/delete, and presets. Changed reports the whole curve; commit true is one
// Undo step (a finished drag, a typed value, a button or preset).
internal sealed class CurveField : StackPanel
{
    private static readonly string[] Presets = ["Constant", "Linear up", "Linear down", "Ease in", "Ease out", "Ease in-out"];
    private readonly CurveView view;
    private readonly NumericUpDown time, value, slopeIn, slopeOut;
    private readonly CheckBox smooth = new() { Content = "Smooth" };
    private readonly ComboBox mode = new() { ItemsSource = new[] { "One curve", "Lower curve", "Upper curve" }, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox preset = new() { ItemsSource = Presets, PlaceholderText = "Apply…", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly float low, high;
    private bool upper, quiet;
    public ParticleCurve Curve { get; private set; }
    public event Action<ParticleCurve, bool>? Changed;
    internal CurveView View => view;

    public CurveField(string label, ParticleCurve curve, float low, float high, decimal step)
    {
        (Curve, this.low, this.high) = (curve, low, high);
        Spacing = 2;
        view = new CurveView(low, high);
        AutomationProperties.SetName(view, label + " curve");
        var add = EditorTokens.IconButton("IconAdd", "Add key");
        var remove = EditorTokens.IconButton("IconClose", "Delete key");
        time = FieldRows.Number("Key time (0–1)", 0, 0, 1, .05m, (v, c) => EditKey(k => k with { Time = v }, c));
        value = FieldRows.Number("Key value", 0, (decimal)low, (decimal)high, step, (v, c) => EditKey(k => k with { Value = v }, c));
        slopeIn = FieldRows.Number("Slope in (value per lifetime)", 0, -100000, 100000, step, (v, c) => EditKey(k => k with { In = v }, c));
        slopeOut = FieldRows.Number("Slope out (value per lifetime)", 0, -100000, 100000, step, (v, c) => EditKey(k => k with { Out = v }, c));
        ToolTip.SetTip(mode, "Two curves: each particle takes its own point between them");
        var tools = new Grid { ColumnDefinitions = new("Auto,*,Auto,Auto"), ColumnSpacing = EditorTokens.FieldGap };
        EditorControls.Put(tools, smooth, 0, 0); EditorControls.Put(tools, mode, 0, 1); EditorControls.Put(tools, add, 0, 2); EditorControls.Put(tools, remove, 0, 3);
        Children.Add(FieldRows.Row(label, view));
        Children.Add(FieldRows.Row("Key", FieldRows.Split(time, value)));
        Children.Add(FieldRows.Row("Slope", FieldRows.Split(slopeIn, slopeOut)));
        Children.Add(FieldRows.Row("", tools));
        Children.Add(FieldRows.Row("Preset", preset));

        view.SelectionChanged += Refresh;
        view.KeysChanged += (keys, commit) => Update(keys, commit);
        add.Click += (_, _) => AddInLargestGap();
        remove.Click += (_, _) => view.DeleteSelected();
        smooth.IsCheckedChanged += (_, _) => { if (!quiet) Report(Curve with { Smooth = smooth.IsChecked == true }, true); };
        mode.SelectionChanged += (_, _) =>
        {
            if (quiet) return;
            upper = mode.SelectedIndex == 2;
            var next = mode.SelectedIndex == 0 ? Curve with { Max = null } : Curve.Max == null ? Curve with { Max = (ParticleKey[])Curve.Keys.Clone() } : Curve;
            Report(next, next != Curve);
        };
        preset.SelectionChanged += (_, _) =>
        {
            if (quiet || preset.SelectedIndex < 0) return;
            ApplyPreset(preset.SelectedIndex);
            quiet = true; preset.SelectedIndex = -1; quiet = false;
        };
        Show(curve);
    }

    public void Show(ParticleCurve curve)
    {
        Curve = curve;
        if (curve.Max == null) upper = false;
        view.Show(Active, upper ? curve.Keys : curve.Max, curve.Smooth);
        Refresh();
    }

    private ParticleKey[] Active => upper ? Curve.Max! : Curve.Keys;

    private void Refresh()
    {
        quiet = true;
        smooth.IsChecked = Curve.Smooth;
        mode.SelectedIndex = Curve.Max == null ? 0 : upper ? 2 : 1;
        var k = Active[view.Selected];
        FieldRows.Set(time, k.Time); FieldRows.Set(value, k.Value); FieldRows.Set(slopeIn, k.In); FieldRows.Set(slopeOut, k.Out);
        slopeIn.IsEnabled = slopeOut.IsEnabled = Curve.Smooth;
        quiet = false;
    }

    private void Update(ParticleKey[] keys, bool commit) => Report(upper ? Curve with { Max = keys } : Curve with { Keys = keys }, commit);

    private void Report(ParticleCurve curve, bool commit)
    {
        Show(curve);
        Changed?.Invoke(curve, commit);
    }

    // A typed key value; time stays between the neighbouring keys so the keys keep their order.
    private void EditKey(Func<ParticleKey, ParticleKey> change, bool commit)
    {
        if (quiet) return;
        var keys = (ParticleKey[])Active.Clone();
        int i = view.Selected;
        var k = change(keys[i]);
        k = k with { Time = Math.Clamp(k.Time, i > 0 ? keys[i - 1].Time : 0, i < keys.Length - 1 ? keys[i + 1].Time : 1), Value = Math.Clamp(k.Value, low, high) };
        if (k == keys[i] && !commit) return;
        keys[i] = k;
        Update(keys, commit);
    }

    private void AddInLargestGap()
    {
        var keys = Active;
        float a = 0, b = keys[0].Time;
        for (int i = 1; i < keys.Length; i++) if (keys[i].Time - keys[i - 1].Time > b - a) (a, b) = (keys[i - 1].Time, keys[i].Time);
        if (1 - keys[^1].Time > b - a) (a, b) = (keys[^1].Time, 1);
        float t = (a + b) / 2;
        view.AddAt(t, ParticleLut.Evaluate(keys, Curve.Smooth, t));
    }

    // Presets keep the curve's size: the largest key value (1 when every key is 0) is the top.
    private void ApplyPreset(int index)
    {
        float top = Active.Max(k => MathF.Abs(k.Value));
        top = Math.Clamp(top == 0 ? 1 : top, low, high);
        float bottom = Math.Clamp(0, low, high);
        ParticleKey[] keys = index switch
        {
            0 => [new(0, top)],
            1 => [new(0, bottom), new(1, top)],
            2 => [new(0, top), new(1, bottom)],
            3 => [new(0, bottom), new(1, top, In: 2 * (top - bottom))],
            4 => [new(0, bottom, Out: 2 * (top - bottom)), new(1, top)],
            _ => [new(0, bottom), new(1, top)],
        };
        var curve = upper ? Curve with { Max = keys } : Curve with { Keys = keys };
        view.Select(0);
        Report(index >= 3 ? curve with { Smooth = true } : curve, true);
    }
}
