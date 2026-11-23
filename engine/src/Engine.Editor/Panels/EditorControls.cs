using Avalonia.Automation;
using Avalonia.Controls;

namespace Engine.Editor;

// Small control builders that several code-built panels used as identical private copies.
internal static class EditorControls
{
    public static TextBox Box(string name, string value, Action<string>? change = null)
    {
        var box = new TextBox { Text = value };
        AutomationProperties.SetName(box, name);
        if (change != null) box.TextChanged += (_, _) => change(box.Text ?? "");
        return box;
    }

    public static Button Button(string text, Action action)
    {
        var button = new Button { Content = text }; button.Click += (_, _) => action(); return button;
    }

    public static void Put(Grid grid, Control control, int row, int column = 0)
    { Grid.SetRow(control, row); Grid.SetColumn(control, column); grid.Children.Add(control); }
}
