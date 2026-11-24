using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Engine.Core;

namespace Engine.Editor;

// A colour field: a 16px swatch (the Sprite row's column) that opens the Avalonia.Controls.ColorPicker package's ColorView in
// a flyout, and the #RRGGBB[AA] text. Both apply while editing (commit false) and commit once: when the flyout closes, or on
// Enter or focus leaving the text. An invalid text goes back to the last colour.
internal sealed class ColorField : Grid
{
    private static bool themed;
    private readonly Border swatch = new() { CornerRadius = new(2), BorderThickness = new(1) };
    private readonly TextBox text;
    private readonly bool alpha;
    private bool live, quiet;
    public string Value { get; private set; }
    public event Action<string, bool>? Changed;
    internal ColorView Picker { get; }
    internal Flyout Flyout { get; }
    internal Button Swatch { get; }

    public ColorField(string name, string hex, bool alpha)
    {
        // The package theme loads on first use, so editors that never show a colour field pay nothing for it.
        if (!themed && Application.Current is { } app)
        {
            themed = true;
            var uri = new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml");
            app.Styles.Add(new StyleInclude(uri) { Source = uri });
        }
        this.alpha = alpha;
        Value = hex;
        ColumnDefinitions = new("16,*");
        ColumnSpacing = EditorTokens.FieldGap;
        swatch.BorderBrush = EditorTokens.Brush("EditorBorder");
        Picker = new ColorView
        {
            IsAlphaEnabled = alpha, IsAlphaVisible = alpha, IsColorPaletteVisible = false, IsAccentColorsVisible = false,
            IsColorModelVisible = false, Width = 280,
        };
        Flyout = new Flyout { Content = Picker, Placement = PlacementMode.BottomEdgeAlignedLeft, FlyoutPresenterClasses = { "colorFlyout" } };
        Swatch = new Button { Content = swatch, Padding = new(0), MinWidth = 0, MinHeight = 0, BorderThickness = new(0), Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Flyout = Flyout };
        AutomationProperties.SetName(Swatch, name + " picker");
        ToolTip.SetTip(Swatch, "Pick a colour");
        text = new TextBox { Text = hex };
        AutomationProperties.SetName(text, name);
        Grid.SetColumn(text, 1);
        Children.Add(Swatch);
        Children.Add(text);
        Paint();

        Flyout.Opening += (_, _) => { quiet = true; Picker.Color = ToColor(Value); quiet = false; };
        Picker.ColorChanged += (_, e) => { if (!quiet) Apply(Hex(e.NewColor), false); };
        Flyout.Closed += (_, _) => Commit();
        text.TextChanged += (_, _) => { if (!quiet && Valid(text.Text)) Apply(text.Text!.ToUpperInvariant(), false); };
        text.LostFocus += (_, _) => { if (!Valid(text.Text)) Show(Value); Commit(); };
        text.KeyDown += (_, e) => { if (e.Key == Key.Enter) { if (!Valid(text.Text)) Show(Value); Commit(); } };
    }

    // Shows a colour without raising a change.
    public void Show(string hex)
    {
        Value = hex;
        quiet = true;
        if (text.Text != hex) text.Text = hex;
        quiet = false;
        Paint();
    }

    private void Apply(string hex, bool commit)
    {
        live = !commit;
        if (hex == Value && !commit) return;
        Value = hex;
        quiet = true;
        if (!string.Equals(text.Text, hex, StringComparison.OrdinalIgnoreCase)) text.Text = hex;
        quiet = false;
        Paint();
        Changed?.Invoke(hex, commit);
    }

    private void Commit()
    {
        if (!live) return;
        live = false;
        Changed?.Invoke(Value, true);
    }

    private void Paint() => swatch.Background = new SolidColorBrush(ToColor(Value));

    private bool Valid(string? hex) => ParticleEffect.TryColor(hex, out _) && (alpha || hex!.Length == 7);

    private static Color ToColor(string hex)
    {
        ParticleEffect.TryColor(hex, out var c);
        return Color.FromArgb(c.A, c.R, c.G, c.B);
    }

    private string Hex(Color c) => alpha && c.A != 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}" : $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
