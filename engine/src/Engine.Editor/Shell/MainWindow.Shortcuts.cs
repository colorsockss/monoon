using Avalonia.Input;

namespace Engine.Editor;

public partial class MainWindow
{
    private void Shortcut(object? sender, KeyEventArgs e)
    {
        if (!busy && !Viewport.IsPlaying && Selected != null &&
            (Hierarchy.IsKeyboardFocusWithin || Viewport.IsKeyboardFocusWithin || FocusManager?.GetFocusedElement() == null))
        {
            if (DuplicateGesture.Matches(e))
            {
                RunAction(DuplicateObject);
                e.Handled = true;
                return;
            }
            if (IsDeleteObjectShortcut(e))
            {
                DeleteObject();
                e.Handled = true;
                return;
            }
        }
        if(!Viewport.IsPlaying && Viewport.IsKeyboardFocusWithin && e.KeyModifiers==KeyModifiers.None)
        {
            if(e.Key==Key.Escape && Viewport.CancelGesture()){e.Handled=true;return;}
            if(e.Key is Key.W or Key.E or Key.R or Key.T){
                SetTool(e.Key==Key.W?GizmoTool.Move:e.Key==Key.E?GizmoTool.Rotate:e.Key==Key.R?GizmoTool.Scale:GizmoTool.Rect);
                e.Handled=true;return;
            }
        }
        if (e.Key==Key.Escape && Viewport.CancelStroke()) { e.Handled=true; return; }
        if (e.Key == Key.Escape && filePress != null)
        {
            EndFileDrag();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && Viewport.IsPlaying) { TogglePlay(); e.Handled = true; }
    }
}
