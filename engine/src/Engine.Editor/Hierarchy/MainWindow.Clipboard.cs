using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private readonly NativeMenuItem cutItem = new() { Header = "Cut" };
    private readonly NativeMenuItem copyItem = new() { Header = "Copy" };
    private readonly NativeMenuItem pasteItem = new() { Header = "Paste" };
    // Used when the OS clipboard is unavailable, so copies still move between scenes of this session.
    private string? copiedObjects;

    private void InitializeClipboardMenus(NativeMenu editMenu, KeyModifiers modifier)
    {
        BindMenuAction(cutItem, new KeyGesture(Key.X, modifier), () => CopySelection(true), () => EditText(box => box.Cut()));
        BindMenuAction(copyItem, new KeyGesture(Key.C, modifier), () => CopySelection(false), () => EditText(box => box.Copy()));
        BindMenuAction(pasteItem, new KeyGesture(Key.V, modifier), PasteObjects, () => EditText(box => box.Paste()));
        editMenu.Add(new NativeMenuItemSeparator());
        editMenu.Add(cutItem);
        editMenu.Add(copyItem);
        editMenu.Add(pasteItem);
    }

    // The Edit menu owns these keys, so a focused text field in any window gets its own Cut, Copy and Paste.
    private bool EditText(Action<TextBox> edit)
    {
        var active = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows.FirstOrDefault(w => w.IsActive) ?? this;
        if (active.FocusManager?.GetFocusedElement() is not TextBox box) return active != this;
        edit(box);
        return true;
    }

    private async Task CopySelection(bool cut)
    {
        var roots = Viewport.Selection.Append(Selected).OfType<SceneObject>().Distinct().ToArray();
        if (roots.Length == 0 || cut && roots.Any(o => o.Id == prefabSource?.RootId)) return;
        copiedObjects = Scene.CopyObjects(roots);
        try { if (Clipboard is { } clipboard) await clipboard.SetTextAsync(copiedObjects); }
        catch (Exception ex) { LogError("Objects stay on the editor clipboard only: " + ex.Message); }
        string what = roots.Length == 1 ? roots[0].Name : $"{roots.Length} objects";
        if (!cut) { Status.Text = $"Copied {what}."; return; }
        bool resetCamera = Scene.Camera is { } camera && roots.Any(camera.IsWithin);
        var parent = Selected?.Parent;
        // Children first, so removing one object never moves another cut object to a new parent.
        foreach (var obj in Scene.ParentFirst().Where(o => roots.Any(o.IsWithin)).Reverse().ToArray()) Scene.RemoveObject(obj);
        FinishObjectChange(parent ?? Scene.Objects.LastOrDefault());
        if (resetCamera) ReportStartCamera();
        FocusHierarchySelection();
        Status.Text = $"Cut {what}.";
    }

    private async Task PasteObjects()
    {
        string? text = copiedObjects;
        // An OS clipboard that hands back nothing (e.g. X11 without a clipboard owner) is unavailable too: keep this session's copy.
        try { if (Clipboard is { } clipboard) text = await clipboard.TryGetTextAsync() ?? copiedObjects; }
        catch (Exception ex) { LogError("Reading the editor clipboard instead: " + ex.Message); }
        if (!SceneDocument.IsObjectClipboard(text)) { Status.Text = "Copy objects in the Hierarchy or Scene before pasting."; return; }
        var pasted = Scene.PasteObjects(text!, Selected?.Parent, scripts);
        FinishObjectChange(pasted[^1]);
        if (pasted.Count > 1) ShowSelection(pasted);
        FocusHierarchySelection();
        Status.Text = pasted.Count == 1 ? $"Pasted {pasted[0].Name}." : $"Pasted {pasted.Count} objects.";
    }
}
