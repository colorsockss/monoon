using Avalonia.Controls;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private ClipEditorWindow? clipEditor;

    private async Task EditClip(string? relative)
    {
        if (project == null && !await Save()) return;
        if (!await ReplaceEditor("Clip", relative)) return;
        var root = project!;
        var clip = relative == null ? new SpriteClip([],false) : SpriteClip.OpenForEditing(Path.Combine(root,relative));
        var window = new ClipEditorWindow(root,CreationFolder("Assets"),relative,clip) { Asset = relative };
        window.Activated += (_, _) => { spriteMonitor?.Request(); UpdateMenuState(); };
        window.Deactivated += (_, _) => UpdateMenuState();
        window.Saved += path => {
            window.Asset = path;
            BrowseFolder(ParentPath(path),path); RefreshComponentList();
            Status.Text = $"Saved {path}.";
        };
        window.Closed += (_,_) => { if (clipEditor == window) clipEditor = null; };
        clipEditor = window;
        ShowEditor<object>("Clip", window);
    }
}
