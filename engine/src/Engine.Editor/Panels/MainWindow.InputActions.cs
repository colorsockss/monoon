using Avalonia.Controls;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private async Task EditInput()
    {
        if (project == null && !await Save()) return;
        if (!await ReplaceEditor("Input Actions", null)) return;
        var dialog = new InputMapWindow(Scene.Settings.Input);
        ShowEditor<InputBindings>("Input Actions", dialog, input =>
        {
            // Save without a change: no file write and no Undo step (the new arrays would not compare equal by reference).
            if (ProjectYaml.Encode(input).AsSpan().SequenceEqual(ProjectYaml.Encode(Scene.Settings.Input.AsMap()))) return;
            var settings = Scene.Settings with { Input = input };
            settings.Save(project!);
            Scene.Settings = settings; MarkDirty();
            RefreshProjectFiles(force: true);
            Status.Text = "Saved project input actions.";
        });
    }
}
