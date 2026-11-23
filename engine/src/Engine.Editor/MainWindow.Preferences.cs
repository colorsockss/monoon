using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Engine.Editor;

public partial class MainWindow
{
    [DllImport("BrutalScenePanel")]
    [return: MarshalAs(UnmanagedType.LPUTF8Str)]
    private static extern string? BrutalChooseScriptEditor();

    private async Task EditPreferences()
    {
        var dialog = new Window
        {
            Title = "Preferences", Width = 400, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false
        };
        var path = new TextBox { Text = ExternalScriptEditor.Preference, Watermark = "System default for .cs files" };
        AutomationProperties.SetName(path, "External script editor");
        var error = new TextBlock { Classes = { "error" }, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var browse = new Button { Content = "Browse…" };
        browse.Click += async (_, _) =>
        {
            try
            {
                if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    if (BrutalChooseScriptEditor() is { } application) path.Text = application;
                    return;
                }
                var options = new FilePickerOpenOptions { Title = "Choose a script editor" };
                if (OperatingSystem.IsWindows())
                    options.FileTypeFilter = [new FilePickerFileType("Applications") { Patterns = ["*.exe"] }];
                var files = await dialog.StorageProvider.OpenFilePickerAsync(options);
                if (files.FirstOrDefault()?.TryGetLocalPath() is { } selected) path.Text = selected;
            }
            catch (Exception ex) { error.Text = ex.Message; error.IsVisible = true; }
        };
        var reset = new Button { Content = "Use System Default" };
        reset.Click += (_, _) => path.Text = "";
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => dialog.Close();
        var save = new Button { Content = "Save", Classes = { "primary" }, IsDefault = true };
        save.Click += (_, _) =>
        {
            try { ExternalScriptEditor.SavePreference(path.Text ?? ""); }
            catch (Exception ex) { error.Text = ex.Message; error.IsVisible = true; return; }
            dialog.Close();
        };
        var selection = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        Grid.SetColumn(browse, 1);
        selection.Children.Add(path);
        selection.Children.Add(browse);
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 6 };
        Grid.SetColumn(cancel, 2);
        Grid.SetColumn(save, 3);
        actions.Children.Add(reset);
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(14), Spacing = 8,
            Children = { new TextBlock { Text = "External script editor" }, selection, error, actions }
        };
        await dialog.ShowDialog(this);
    }
}
