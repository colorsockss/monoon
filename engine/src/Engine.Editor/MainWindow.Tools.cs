using Avalonia.Controls;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private async Task EditInput()
    {
        if (project == null && !await Save()) return;
        var dialog = new InputMapWindow(Scene.Settings.Input);
        if (await dialog.ShowDialog<InputBindings?>(this) is not { } input) return;
        var settings = Scene.Settings with { Input = input };
        settings.Save(project!);
        Scene.Settings = settings; MarkDirty();
        RefreshProjectFiles(force: true);
        Status.Text = "Saved project input actions.";
    }
}
