using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private async Task EditGameSettings()
    {
        if (project == null && !await Save()) return;
        var dialog = new Window
        {
            Title = "Game Resolution", Width = 272, SizeToContent = SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false
        };
        var width = Dimension("Width", Scene.Settings.Width);
        var height = Dimension("Height", Scene.Settings.Height);
        var fields = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), ColumnSpacing = 6 };
        fields.Children.Add(width);
        var times = new TextBlock { Text = "×", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(times, 1);
        Grid.SetColumn(height, 2);
        fields.Children.Add(times);
        fields.Children.Add(height);
        var integer = new CheckBox { Content = "Pixel art: integer scale", IsChecked = Scene.Settings.IntegerScale == true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var apply = new Button { Content = "Apply", Classes = { "primary" }, IsDefault = true };
        var error = new TextBlock { Text = $"Enter whole numbers from 1 to {GameSettings.MaxDimension}.", IsVisible = false };
        cancel.Click += (_, _) => dialog.Close();
        apply.Click += (_, _) =>
        {
            if (!int.TryParse(width.Text, out int w) || !int.TryParse(height.Text, out int h) ||
                w is < 1 or > GameSettings.MaxDimension || h is < 1 or > GameSettings.MaxDimension)
            {
                error.IsVisible = true;
                return;
            }
            dialog.Close(Scene.Settings with { Width = w, Height = h, IntegerScale = integer.IsChecked == true ? true : null });
        };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(14), Spacing = 8,
            Children = { new TextBlock { Text = "Game resolution (width × height in pixels)" }, fields, integer, error,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 6, Children = { cancel, apply } } }
        };
        if (await dialog.ShowDialog<GameSettings?>(this) is not { } settings) return;
        ApplyGameSettings(settings);
    }

    private void ApplyGameSettings(GameSettings settings)
    {
        settings.Save(project!);
        Scene.Settings = settings;
        MarkDirty();
        UpdateResolution();
        RefreshProjectFiles(force: true);
        Viewport.RefreshScene();
        Status.Text = $"Saved project game resolution: {ResolutionLabel.Text}.";
    }

    private static TextBox Dimension(string name, int value)
    {
        var field = new TextBox { Text = value.ToString() };
        AutomationProperties.SetName(field, name);
        return field;
    }

    private void UpdateResolution()
    {
        ResolutionLabel.Text = Viewport.Preview is {} preview
            ? $"Preview {preview.Width} × {preview.Height} · {preview.Locale} {preview.TextScale:P0}"
            : $"{Scene.Settings.Width} × {Scene.Settings.Height}";
        SyncResolution();
    }

    private void CreateCamera()
    {
        if (Viewport.IsPlaying) return;
        var center = Viewport.EditCenter;
        var camera = Scene.CreateCamera(new System.Numerics.Vector2((float)center.X, (float)center.Y));
        FinishObjectChange(camera);
    }
}
