using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private void UiPinFields(SceneObject obj, UiRectComponent rect, Action<string, Control> row)
    {
        Add("Left", rect.Left, v => rect.Left = v, () => { var (b,p) = Viewport.UiPlacement(obj); return b.Left - p.Left; });
        Add("Right", rect.Right, v => rect.Right = v, () => { var (b,p) = Viewport.UiPlacement(obj); return p.Right - b.Right; });
        Add("Top", rect.Top, v => rect.Top = v, () => { var (b,p) = Viewport.UiPlacement(obj); return b.Top - p.Top; });
        Add("Bottom", rect.Bottom, v => rect.Bottom = v, () => { var (b,p) = Viewport.UiPlacement(obj); return p.Bottom - b.Bottom; });

        void Add(string edge, float? margin, Action<float?> set, Func<float> measure)
        {
            var line = new Grid { ColumnDefinitions = new("Auto,*"), ColumnSpacing = 4 };
            var pin = new CheckBox { IsChecked = margin.HasValue };
            var value = new NumericUpDown { Value = decimal.CreateSaturating(margin ?? 0), IsEnabled = margin.HasValue, Increment = 1 };
            AutomationProperties.SetName(pin, $"Pin {edge}");
            AutomationProperties.SetName(value, $"{edge} margin");
            ToolTip.SetTip(pin, $"Pin the {edge.ToLowerInvariant()} edge to its parent");
            Grid.SetColumn(value, 1);
            line.Children.Add(pin);
            line.Children.Add(value);
            row(edge, line);
            pin.IsCheckedChanged += (_, _) =>
            {
                bool enabled = pin.IsChecked == true;
                float measured = enabled ? measure() : 0;
                value.IsEnabled = false;
                value.Value = decimal.CreateSaturating(measured);
                set(enabled ? measured : null);
                value.IsEnabled = enabled;
                MarkDirty();
            };
            value.ValueChanged += (_, _) =>
            {
                if (value.IsEnabled && value.Value is decimal number) { set((float)number); MarkDirty(value); }
            };
        }
    }

    private async Task EditUiPreview()
    {
        var settings = Viewport.LayoutSettings;
        var dialog = new Window { Title = "UI Layout Preview", Width = 310, MinWidth = 265, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        var content = new StackPanel { Margin = new Thickness(12), Spacing = 5 };
        dialog.Content = content;
        var width = Dimension("Preview width", settings.Width);
        var height = Dimension("Preview height", settings.Height);
        var locale = new TextBox { Text = settings.Accessibility.Locale, MaxLength = 64 };
        var scale = new NumericUpDown { Minimum = 75, Maximum = 200, Increment = 5,
            Value = (decimal)(settings.Accessibility.TextScale * 100) };
        AutomationProperties.SetName(locale, "Preview language");
        AutomationProperties.SetName(scale, "Preview text size");
        InspectorRow(content, "Width", width);
        InspectorRow(content, "Height", height);
        InspectorRow(content, "Language", locale);
        InspectorRow(content, "Text (%)", scale);
        var reset = new Button { Content = "Use Project" };
        var apply = new Button { Content = "Preview", IsDefault = true, Classes = { "primary" } };
        var error = new TextBlock { IsVisible = false, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        content.Children.Add(error);
        content.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6, Children = { reset, apply } });
        SceneViewport.UiPreview? next = null;
        reset.Click += (_, _) => dialog.Close(true);
        apply.Click += (_, _) =>
        {
            if (!int.TryParse(width.Text, out int w) || !int.TryParse(height.Text, out int h) ||
                w is < 1 or > GameSettings.MaxDimension || h is < 1 or > GameSettings.MaxDimension ||
                !GameLocalization.IsLanguageTag(locale.Text?.Trim()) || scale.Value == null)
            {
                error.Text = $"Enter dimensions from 1 to {GameSettings.MaxDimension}, a language and a text size.";
                error.IsVisible = true;
                return;
            }
            next = new(w, h, locale.Text!.Trim(), (float)scale.Value / 100);
            dialog.Close(true);
        };
        if (!await dialog.ShowDialog<bool>(this)) return;
        Viewport.SetUiPreview(next);
        Viewport.FrameCamera();
        UpdateResolution();
    }
}
