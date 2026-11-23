using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;

namespace Engine.Editor;

// An asset or project editor shown in a dock panel instead of its own window. It keeps the Window members
// the former dialogs use (Title, Close(result), Closing, Closed, Opened, Activated, IsActive) so their code stays the same.
internal class EditorPanel : UserControl
{
    private string? title;
    private bool opened, closed;
    public string? Title { get => title; set { title = value; TitleChanged?.Invoke(value); } }
    // Identifies the open asset (or null for a new one); opening the same asset again focuses this editor.
    internal string? Asset { get; set; }
    internal event Action<string?>? TitleChanged;
    // Raised once the editor really closed, with the value passed to Close(result).
    internal event Action<object?>? Finished;
    internal event Action? ActivateRequested;
    public event EventHandler<CancelEventArgs>? Closing;
    public event EventHandler? Closed, Opened, Activated, Deactivated;

    // Dialogs opened from the editor belong to the window that currently shows it: the main window or a floating panel window.
    protected Window Owner => TopLevel.GetTopLevel(this) as Window ?? throw new InvalidOperationException("The editor panel is not shown.");
    internal bool IsShown => VisualRoot != null;
    public bool IsActive => IsKeyboardFocusWithin && TopLevel.GetTopLevel(this) is Window { IsActive: true };

    public void Close() => Close(null);

    public void Close(object? result)
    {
        var e = new CancelEventArgs();
        Closing?.Invoke(this, e);
        if (e.Cancel) return;
        closed = true;
        Finished?.Invoke(result);
        Closed?.Invoke(this, EventArgs.Empty);
    }

    // Closing handlers may cancel and ask about unsaved changes; the caller tries again after the answer.
    public virtual Task<bool> RequestClose()
    {
        Close();
        return Task.FromResult(closed);
    }

    public void Activate() { ActivateRequested?.Invoke(); Focus(); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (opened) return;
        opened = true;
        Opened?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsKeyboardFocusWithinProperty)
            (change.GetNewValue<bool>() ? Activated : Deactivated)?.Invoke(this, EventArgs.Empty);
    }
}
