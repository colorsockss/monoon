using Avalonia.Controls;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private ClipEditorWindow? clipEditor;

    private async Task EditClip(string? relative)
    {
        if (project == null && !await Save()) return;
        if (clipEditor is {} current)
        {
            if (current.RelativePath == relative) { current.Activate(); return; }
            if (!await current.RequestClose()) return;
        }
        var root = project!;
        var clip = relative == null ? new SpriteClip([],false) : SpriteClip.OpenForEditing(Path.Combine(root,relative));
        var window = new ClipEditorWindow(root,CreationFolder("Assets"),relative,clip);
        window.Activated += (_, _) => spriteMonitor?.Request();
        window.Saved += path => {
            BrowseFolder(ParentPath(path),path); RefreshComponentList();
            Status.Text = $"Saved {path}.";
        };
        window.Closed += (_,_) => { clipEditor = null; ProjectBrowser.SelectionMode = SelectionMode.Single; };
        ProjectBrowser.SelectionMode = SelectionMode.Multiple;
        clipEditor = window; window.Show(this);
    }
}
