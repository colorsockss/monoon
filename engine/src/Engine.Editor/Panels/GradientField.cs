using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Engine.Core;

namespace Engine.Editor;

// A colour-and-alpha gradient bar. The bar is the runtime's own 64-sample table (ParticleLut.Bake) over a checkerboard; alpha
// keys sit above it and colour keys below. Drag a key along the bar (one change per drag), double-click the upper or lower
// half to add an alpha or colour key there. Selection and drag only repaint.
internal sealed class GradientView : Control
{
    private const double Size = 8, Pad = 5;
    private ParticleGradient gradient = new();
    private bool moved;
    public (bool Alpha, int Index) Selected { get; private set; } = (false, -1);
    public bool Dragging { get; private set; }
    public event Action? SelectionChanged;
    public event Action<ParticleGradient, bool>? KeysChanged;

    public GradientView()
    {
        Height = 36;
        Focusable = true;
        ClipToBounds = true;
    }

    public ParticleGradient Gradient => gradient;
    internal float[] Samples => ParticleLut.Bake(gradient);
    private int Count(bool alpha) => alpha ? gradient.Alphas?.Length ?? 0 : gradient.Colors?.Length ?? 0;
    private float TimeOf(bool alpha, int i) => alpha ? gradient.Alphas![i].Time : gradient.Colors![i].Time;

    public void Show(ParticleGradient value)
    {
        gradient = value;
        if (Selected.Index < 0 || Selected.Index >= Count(Selected.Alpha)) Selected = Count(false) > 0 ? (false, 0) : Count(true) > 0 ? (true, 0) : (false, -1);
        InvalidateVisual();
    }

    public void Select(bool alpha, int index)
    {
        Selected = index >= 0 && index < Count(alpha) ? (alpha, index) : (false, -1);
        InvalidateVisual();
        SelectionChanged?.Invoke();
    }

    private double X(float t) => Pad + t * Math.Max(1, Bounds.Width - 2 * Pad);
    private float T(double x) => (float)((x - Pad) / Math.Max(1, Bounds.Width - 2 * Pad));
    private Rect Bar => new(Pad, Size + 2, Math.Max(1, Bounds.Width - 2 * Pad), Bounds.Height - 2 * Size - 4);

    public override void Render(DrawingContext context)
    {
        var bar = Bar;
        var light = new SolidColorBrush(Color.FromRgb(0x5A, 0x5E, 0x64));
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x42)), null, bar);
        for (double x = bar.X, row = 0; x < bar.Right; x += 6, row++)
            context.DrawRectangle(light, null, new Rect(x, bar.Y + (row % 2) * bar.Height / 2, Math.Min(6, bar.Right - x), bar.Height / 2));
        var lut = Samples;
        int n = ParticleLut.Size;
        for (int k = 0; k < n - 1; k++)
        {
            byte C(int c) => (byte)Math.Clamp((int)(lut[k * 4 + c] * 255 + .5f), 0, 255);
            double x0 = X(k / (float)(n - 1)), x1 = X((k + 1) / (float)(n - 1));
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(C(3), C(0), C(1), C(2))), null, new Rect(x0, bar.Y, x1 - x0 + .5, bar.Height));
        }
        context.DrawRectangle(null, new Pen(EditorTokens.Brush(IsFocused ? "EditorBorderHover" : "EditorBorder")), bar);
        for (int i = 0; i < Count(true); i++)
        {
            byte a = (byte)Math.Clamp((int)(gradient.Alphas![i].Alpha * 255 + .5f), 0, 255);
            Marker(context, true, i, new SolidColorBrush(Color.FromRgb(a, a, a)));
        }
        for (int i = 0; i < Count(false); i++)
        {
            ParticleEffect.TryColor(gradient.Colors![i].Color, out var c);
            Marker(context, false, i, new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)));
        }
    }

    private void Marker(DrawingContext context, bool alpha, int i, IBrush fill)
    {
        double x = X(TimeOf(alpha, i)), y = alpha ? 0 : Bounds.Height - Size;
        bool selected = Selected == (alpha, i);
        context.DrawRectangle(fill, new Pen(EditorTokens.Brush(selected ? "EditorAccent" : "EditorText"), selected ? 2 : 1),
            new Rect(x - Size / 2 + .5, y + .5, Size - 1, Size - 1));
    }

    private (bool Alpha, int Index) KeyAt(Point p)
    {
        bool alpha = p.Y < Bounds.Height / 2;
        int best = -1;
        double nearest = Size;
        for (int i = 0; i < Count(alpha); i++)
        {
            double d = Math.Abs(X(TimeOf(alpha, i)) - p.X);
            if (d <= nearest) (best, nearest) = (i, d);
        }
        return (alpha, best);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        var p = e.GetPosition(this);
        var (alpha, hit) = KeyAt(p);
        if (hit >= 0) { BeginDrag(alpha, hit); e.Pointer.Capture(this); }
        else if (e.ClickCount == 2) AddAt(alpha, T(p.X));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Dragging) DragTo(T(e.GetPosition(this).X));
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

    internal void BeginDrag(bool alpha, int index)
    {
        Select(alpha, index);
        (Dragging, moved) = (true, false);
    }

    // Keys stay between their neighbours, so colour and alpha keys keep their time order.
    internal void DragTo(float time)
    {
        var (alpha, i) = Selected;
        if (i < 0) return;
        int n = Count(alpha);
        float t = Math.Clamp(MathF.Round(time, 3), i > 0 ? TimeOf(alpha, i - 1) : 0, i < n - 1 ? TimeOf(alpha, i + 1) : 1);
        if (t == TimeOf(alpha, i)) return;
        gradient = WithTime(gradient, alpha, i, t);
        moved = true;
        InvalidateVisual();
        KeysChanged?.Invoke(gradient, false);
    }

    internal void EndDrag()
    {
        Dragging = false;
        if (moved) KeysChanged?.Invoke(gradient, true);
        moved = false;
    }

    internal static ParticleGradient WithTime(ParticleGradient g, bool alpha, int i, float t)
    {
        if (alpha) { var a = (ParticleAlphaKey[])g.Alphas!.Clone(); a[i] = a[i] with { Time = t }; return g with { Alphas = a }; }
        var c = (ParticleColorKey[])g.Colors!.Clone(); c[i] = c[i] with { Time = t }; return g with { Colors = c };
    }

    // A new key takes the gradient's current colour or alpha at that time.
    internal void AddAt(bool alpha, float time)
    {
        if (Count(alpha) >= 8) return;
        float t = Math.Clamp(MathF.Round(time, 3), 0, 1);
        var lut = Samples;
        int k = Math.Clamp((int)MathF.Round(t * (ParticleLut.Size - 1)), 0, ParticleLut.Size - 1);
        int at = 0;
        while (at < Count(alpha) && TimeOf(alpha, at) <= t) at++;
        if (alpha)
        {
            var keys = gradient.Alphas ?? [];
            gradient = gradient with { Alphas = [.. keys[..at], new(t, MathF.Round(lut[k * 4 + 3], 3)), .. keys[at..]] };
        }
        else
        {
            static int B(float v) => Math.Clamp((int)(v * 255 + .5f), 0, 255);
            var keys = gradient.Colors ?? [];
            gradient = gradient with { Colors = [.. keys[..at], new(t, $"#{B(lut[k * 4]):X2}{B(lut[k * 4 + 1]):X2}{B(lut[k * 4 + 2]):X2}"), .. keys[at..]] };
        }
        Selected = (alpha, at);
        KeysChanged?.Invoke(gradient, true);
    }

    // The last colour key leaves the start colour; the last alpha key goes back to End alpha.
    internal void DeleteSelected()
    {
        var (alpha, i) = Selected;
        if (i < 0) return;
        if (alpha) { var a = gradient.Alphas!; gradient = gradient with { Alphas = a.Length == 1 ? null : [.. a[..i], .. a[(i + 1)..]] }; }
        else { var c = gradient.Colors!; gradient = gradient with { Colors = c.Length == 1 ? null : [.. c[..i], .. c[(i + 1)..]] }; }
        Selected = i - 1 >= 0 ? (alpha, i - 1) : Count(alpha) > 0 ? (alpha, 0) : Count(!alpha) > 0 ? (!alpha, 0) : (false, -1);
        KeysChanged?.Invoke(gradient, true);
    }
}

// A gradient field in label-column rows: the bar, the selected key's time and its colour (ColorField) or alpha, and
// add/delete buttons. Changed reports the whole gradient; commit true is one Undo step.
internal sealed class GradientField : StackPanel
{
    private readonly GradientView view = new();
    private readonly NumericUpDown time, alpha;
    private readonly ColorField color;
    private readonly Button remove;
    private bool quiet;
    public ParticleGradient Gradient { get; private set; }
    public event Action<ParticleGradient, bool>? Changed;
    internal GradientView View => view;
    internal ColorField Color => color;

    public GradientField(string label, ParticleGradient gradient)
    {
        Gradient = gradient;
        Spacing = 2;
        AutomationProperties.SetName(view, label + " gradient");
        time = FieldRows.Number("Key time (0–1)", 0, 0, 1, .05m, (v, c) => Edit(k => GradientView.WithTime(Gradient, k.Alpha, k.Index, v), c));
        alpha = FieldRows.Number("Key alpha (0–1)", 1, 0, 1, .05m, (v, c) => Edit(k =>
        {
            var a = (ParticleAlphaKey[])Gradient.Alphas!.Clone(); a[k.Index] = a[k.Index] with { Alpha = v }; return Gradient with { Alphas = a };
        }, c));
        color = new ColorField("Key colour", "#FFFFFF", alpha: false);
        color.Changed += (hex, c) => Edit(k =>
        {
            var keys = (ParticleColorKey[])Gradient.Colors!.Clone(); keys[k.Index] = keys[k.Index] with { Color = hex }; return Gradient with { Colors = keys };
        }, c);
        var value = new Grid();
        value.Children.Add(color);
        value.Children.Add(alpha);
        var addColor = new Button { Content = "Add color" };
        var addAlpha = new Button { Content = "Add alpha" };
        remove = EditorTokens.IconButton("IconClose", "Delete key");
        var tools = new Grid { ColumnDefinitions = new("Auto,Auto,*,Auto"), ColumnSpacing = EditorTokens.FieldGap };
        EditorControls.Put(tools, addColor, 0, 0); EditorControls.Put(tools, addAlpha, 0, 1); EditorControls.Put(tools, remove, 0, 3);
        Children.Add(FieldRows.Row(label, view));
        Children.Add(FieldRows.Row("Key", FieldRows.Split(time, value)));
        Children.Add(FieldRows.Row("", tools));
        view.SelectionChanged += Refresh;
        view.KeysChanged += Report;
        addColor.Click += (_, _) => view.AddAt(false, Gap(false));
        addAlpha.Click += (_, _) => view.AddAt(true, Gap(true));
        remove.Click += (_, _) => view.DeleteSelected();
        Show(gradient);
    }

    public void Show(ParticleGradient gradient)
    {
        Gradient = gradient;
        view.Show(gradient);
        Refresh();
    }

    private void Report(ParticleGradient gradient, bool commit)
    {
        Show(gradient);
        Changed?.Invoke(gradient, commit);
    }

    private void Refresh()
    {
        quiet = true;
        var (isAlpha, i) = view.Selected;
        time.IsEnabled = remove.IsEnabled = i >= 0;
        color.IsVisible = !isAlpha;
        alpha.IsVisible = isAlpha;
        color.IsEnabled = i >= 0;
        if (i >= 0)
        {
            FieldRows.Set(time, isAlpha ? Gradient.Alphas![i].Time : Gradient.Colors![i].Time);
            if (isAlpha) FieldRows.Set(alpha, Gradient.Alphas![i].Alpha);
            else color.Show(Gradient.Colors![i].Color);
        }
        quiet = false;
    }

    private void Edit(Func<(bool Alpha, int Index), ParticleGradient> change, bool commit)
    {
        var selected = view.Selected;
        if (quiet || selected.Index < 0) return;
        var next = change(selected);
        // A typed time stays between the neighbouring keys.
        var (a, i) = selected;
        int n = a ? next.Alphas!.Length : next.Colors!.Length;
        float t = a ? next.Alphas![i].Time : next.Colors![i].Time;
        float lo = i > 0 ? (a ? next.Alphas![i - 1].Time : next.Colors![i - 1].Time) : 0, hi = i < n - 1 ? (a ? next.Alphas![i + 1].Time : next.Colors![i + 1].Time) : 1;
        if (t < lo || t > hi) next = GradientView.WithTime(next, a, i, Math.Clamp(t, lo, hi));
        if (next == Gradient && !commit) return;
        Report(next, commit);
    }

    // The middle of the widest gap between keys of that kind (0.5 with none).
    private float Gap(bool isAlpha)
    {
        var times = (isAlpha ? Gradient.Alphas?.Select(k => k.Time) : Gradient.Colors?.Select(k => k.Time))?.ToArray() ?? [];
        if (times.Length == 0) return .5f;
        float a = 0, b = times[0];
        for (int i = 1; i < times.Length; i++) if (times[i] - times[i - 1] > b - a) (a, b) = (times[i - 1], times[i]);
        if (1 - times[^1] > b - a) (a, b) = (times[^1], 1);
        return (a + b) / 2;
    }
}
