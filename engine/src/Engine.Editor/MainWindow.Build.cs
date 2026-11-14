using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private async Task BuildGame()
    {
        if (!await Save() || project == null) return;
        var root = project;
        var scenes = ProjectAssets.Scan(root).Entries.Where(e => e.Path.EndsWith(Suffix) && File.Exists(Path.Combine(root, e.Path)))
            .Select(e => new ObjectReference(e.Id, e.Path)).ToArray();
        var settings = Scene.Settings;
        var start = new ComboBox { ItemsSource = scenes, SelectedItem = scenes.FirstOrDefault(e => e.Id == settings.StartScene) ?? scenes.FirstOrDefault() };
        var platforms = new[] { "osx-arm64", "win-x64", "linux-x64" };
        var target = new ComboBox { ItemsSource = platforms, SelectedIndex = OperatingSystem.IsMacOS() ? 0 : OperatingSystem.IsWindows() ? 1 : 2 };
        const string aot = "Native AOT";
        var modePicker = new ComboBox();
        void UpdateModes()
        {
            var previous = modePicker.SelectedItem as string;
            var local = (string?)target.SelectedItem == System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
            modePicker.ItemsSource = local ? new[] { "Release", aot } : new[] { "Release" };
            modePicker.SelectedIndex = local && previous == aot ? 1 : 0;
        }
        target.SelectionChanged += (_, _) => UpdateModes();
        UpdateModes();
        var choices = scenes.Select(e => new CheckBox { Content = e.Label, Tag = e.Id,
            IsChecked = settings.IncludedScenes.Length == 0 || settings.IncludedScenes.Contains(e.Id) }).ToArray();
        var rows = new StackPanel { Spacing = 4, Margin = new Thickness(12) };
        var bundles = new TextBox { AcceptsReturn = true, MaxHeight = 80, MinHeight = 40,
            Text = string.Join(Environment.NewLine, settings.ContentBundles ?? []),
            Watermark = "One Assets or Scenes folder per line" };
        var grid = new Grid { ColumnDefinitions = new("78,*"), RowDefinitions = new("Auto,Auto,Auto,Auto"), ColumnSpacing = 4, RowSpacing = 4 };
        void Row(int index, string text, Control value)
        {
            var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
            value.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetRow(label, index); Grid.SetRow(value, index); Grid.SetColumn(value, 1);
            grid.Children.Add(label); grid.Children.Add(value);
        }
        Row(0, "Start scene", start); Row(1, "Platform", target); Row(2, "Build type", modePicker);
        Row(3, "Bundles", bundles); rows.Children.Add(grid);
        rows.Children.Add(new TextBlock { Text = "Included scenes" });
        var scenesPanel = new StackPanel();
        foreach (var choice in choices) scenesPanel.Children.Add(choice);
        rows.Children.Add(new ScrollViewer { MaxHeight = 210, Content = scenesPanel });
        var configure = new Button { Content = "Save Settings" };
        var chooseOutput = new Button { Content = "Build To…" };
        var build = new Button { Content = "Build", Classes = { "primary" } };
        var destination = GameBuild.DefaultOutputDirectory(root);
        ToolTip.SetTip(build, destination);
        rows.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 4, Children = { configure, chooseOutput, build } });
        var dialog = new Window { Title = "Build Game", Width = 420, SizeToContent = SizeToContent.Height, Content = rows,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        configure.Click += (_, _) => dialog.Close("settings");
        chooseOutput.Click += (_, _) => dialog.Close("choose");
        build.Click += (_, _) => dialog.Close("build");
        if (await dialog.ShowDialog<string?>(this) is not { } mode || start.SelectedItem is not ObjectReference selected) return;
        var folders = (bundles.Text ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        settings = settings with { StartScene = selected.Id, IncludedScenes = choices.Where(c => c.IsChecked == true).Select(c => (Guid)c.Tag!).ToArray(),
            ContentBundles = folders.Length == 0 ? null : folders };
        if (!settings.IncludedScenes.Contains(selected.Id)) throw new InvalidDataException("Include the start scene.");
        if (mode == "settings") { settings.Save(root); Scene.Settings = settings; MarkDirty(); Status.Text = "Saved build settings."; return; }
        var rid = (string)target.SelectedItem!;
        if (mode == "choose")
        {
            var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose build output folder" });
            if (result.FirstOrDefault()?.TryGetLocalPath() is not { } chosen) return;
            destination = chosen;
        }
        var native = (string?)modePicker.SelectedItem == aot;
        Status.Text = $"Building {rid} ({modePicker.SelectedItem})…";
        var output = await Task.Run(() => Engine.Desktop.DesktopBuild.Export(root, settings, rid, destination, native));
        settings.Save(root); Scene.Settings = settings; MarkDirty();
        Status.Text = $"Built {output}";
    }
}
