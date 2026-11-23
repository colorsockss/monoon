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
        var saved = BuildSettings.Load(root);
        var start = new ComboBox { ItemsSource = scenes, SelectedItem = scenes.FirstOrDefault(e => e.Id == settings.StartScene) ?? scenes.FirstOrDefault() };
        var platforms = BuildSettings.Targets;
        var target = new ComboBox { ItemsSource = platforms, SelectedItem = saved.Target };
        if (target.SelectedItem == null) target.SelectedIndex = 0; // host without a player target, e.g. an Intel Mac
        const string aot = "Native AOT";
        var modePicker = new ComboBox();
        void UpdateModes()
        {
            var previous = modePicker.SelectedItem as string;
            var local = BuildSettings.CanBuildNativeAot((string)target.SelectedItem!);
            modePicker.ItemsSource = local ? new[] { "Release", aot } : new[] { "Release" };
            modePicker.SelectedIndex = local && previous == aot ? 1 : 0;
        }
        target.SelectionChanged += (_, _) => UpdateModes();
        UpdateModes();
        // A saved Native AOT choice for another platform falls back to Release, as switching Platform does.
        if (saved.NativeAot && modePicker.ItemCount > 1) modePicker.SelectedItem = aot;
        var choices = scenes.Select(e => new CheckBox { Content = e.Label, Tag = e.Id,
            IsChecked = settings.IncludedScenes.Length == 0 || settings.IncludedScenes.Contains(e.Id) }).ToArray();
        var rows = new StackPanel { Spacing = 4, Margin = new Thickness(12) };
        var bundles = new TextBox { AcceptsReturn = true, MaxHeight = 80, MinHeight = 40,
            Text = string.Join(Environment.NewLine, settings.ContentBundles ?? []),
            Watermark = "One Assets or Scenes folder per line" };
        var grid = new Grid { ColumnDefinitions = new("78,*"), RowDefinitions = new("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto"), ColumnSpacing = 4, RowSpacing = 4 };
        void Row(int index, string text, Control value)
        {
            var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
            value.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetRow(label, index); Grid.SetRow(value, index); Grid.SetColumn(value, 1);
            grid.Children.Add(label); grid.Children.Add(value);
        }
        Row(0, "Start scene", start); Row(1, "Platform", target); Row(2, "Build type", modePicker);
        var fullContent = new CheckBox { Content = "Rebuild all content", IsChecked = saved.FullContent };
        Row(3, "Bundles", bundles); Row(4, "Content", fullContent);
        var profilePicker = new ComboBox { ItemsSource = new[] { "Distribution", "Development" }, SelectedIndex = 0 };
        var package = new CheckBox { Content = "Sign and package" };
        ToolTip.SetTip(package, $"Signing settings: {SigningSettings.DefaultPath}");
        profilePicker.SelectionChanged += (_, _) => { package.IsEnabled = profilePicker.SelectedIndex == 0; if (!package.IsEnabled) package.IsChecked = false; };
        profilePicker.SelectedIndex = saved.BuildProfile == BuildProfile.Development ? 1 : 0;
        package.IsChecked = saved.Package && package.IsEnabled;
        var noIcon = new ObjectReference(Guid.Empty, "(None)");
        var icons = ProjectAssets.Scan(root).Entries.Where(e => SpriteAssets.IsPng(e.Path)).Select(e => new ObjectReference(e.Id, e.Path)).Prepend(noIcon).ToArray();
        var icon = new ComboBox { ItemsSource = icons, SelectedItem = icons.FirstOrDefault(e => e.Id == settings.Icon) ?? noIcon };
        Row(5, "Profile", profilePicker); Row(6, "Package", package); Row(7, "Icon", icon); rows.Children.Add(grid);
        rows.Children.Add(new TextBlock { Text = "Included scenes" });
        var scenesPanel = new StackPanel();
        foreach (var choice in choices) scenesPanel.Children.Add(choice);
        rows.Children.Add(new ScrollViewer { MaxHeight = 210, Content = scenesPanel });
        var configure = new Button { Content = "Save Settings" };
        var chooseOutput = new Button { Content = "Build To…" };
        var buildButton = new Button { Content = "Build", Classes = { "primary" } };
        var destination = GameBuild.DefaultOutputDirectory(root);
        ToolTip.SetTip(buildButton, destination);
        rows.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 4, Children = { configure, chooseOutput, buildButton } });
        var dialog = new Window { Title = "Build Game", Width = 420, SizeToContent = SizeToContent.Height, Content = rows,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        configure.Click += (_, _) => dialog.Close("settings");
        chooseOutput.Click += (_, _) => dialog.Close("choose");
        buildButton.Click += (_, _) => dialog.Close("build");
        if (await dialog.ShowDialog<string?>(this) is not { } mode || start.SelectedItem is not ObjectReference selected) return;
        var folders = (bundles.Text ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        settings = settings with { StartScene = selected.Id, IncludedScenes = choices.Where(c => c.IsChecked == true).Select(c => (Guid)c.Tag!).ToArray(),
            ContentBundles = folders.Length == 0 ? null : folders, Icon = icon.SelectedItem is ObjectReference { Id: var iconId } && iconId != Guid.Empty ? iconId : null };
        if (!settings.IncludedScenes.Contains(selected.Id)) throw new InvalidDataException("Include the start scene.");
        var build = new BuildSettings { Target = (string)target.SelectedItem!, Type = (string?)modePicker.SelectedItem == aot ? "native-aot" : "release",
            Profile = profilePicker.SelectedIndex == 0 ? "distribution" : "development", Content = fullContent.IsChecked == true ? "full" : "incremental",
            Package = package.IsChecked == true };
        build.Save(root);
        if (mode == "settings") { settings.Save(root); Scene.Settings = settings; MarkDirty(); Status.Text = "Saved build settings."; return; }
        if (mode == "choose")
        {
            var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose build output folder" });
            if (result.FirstOrDefault()?.TryGetLocalPath() is not { } chosen) return;
            destination = chosen;
        }
        Status.Text = $"Building {build.Target} ({modePicker.SelectedItem})…";
        var packageLog = new List<string>();
        var buildResult = await Task.Run(() =>
        {
            var built = Engine.Desktop.DesktopBuild.Export(root, settings, build.Target, destination, build.NativeAot, build.BuildProfile, build.FullContent);
            if (build.Package) DistributionPackage.Create(root, settings, build.Target, built, SigningSettings.Load(), packageLog.Add);
            return built;
        });
        foreach (var decision in buildResult.ContentDecisions) WriteLog(new(GameLogLevel.Info, decision, null, 0));
        foreach (var line in packageLog) WriteLog(new(GameLogLevel.Info, line, null, 0));
        WriteLog(new(GameLogLevel.Info, $"Content report: {buildResult.ContentReport}", null, 0));
        settings.Save(root); Scene.Settings = settings; MarkDirty();
        Status.Text = $"Built {buildResult.Product}";
    }
}
