using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private async Task EditAnimator(string? relative)
    {
        if (project == null && !await Save()) return;
        if (!await ReplaceEditor("Animator", relative)) return;
        var root = project!;
        var dialog = new AnimatorWindow(root,relative == null ? CreationFolder("Assets") : ParentPath(relative),relative) { Asset = relative };
        ShowEditor<string>("Animator", dialog, saved =>
        {
            ProjectAssets.Scan(root);
            BrowseFolder(ParentPath(saved),saved);
            RefreshComponentList();
            Status.Text = $"Saved {saved}.";
        });
    }
}
