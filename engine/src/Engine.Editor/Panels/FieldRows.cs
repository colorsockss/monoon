using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Engine.Editor;

// Inspector-style rows for code-built editors: the 56px label column, the field gap, and numbers that commit once.
internal static class FieldRows
{
    public static Grid Row(string label, Control field)
    {
        var row = new Grid { ColumnDefinitions = EditorTokens.LabelColumns(EditorTokens.FieldLabelWidth), ColumnSpacing = EditorTokens.FieldGap };
        var title = new TextBlock { Text = label, Classes = { "fieldLabel" }, TextTrimming = TextTrimming.CharacterEllipsis };
        if (label.Length > 0) ToolTip.SetTip(title, label);
        row.Children.Add(title);
        Grid.SetColumn(field, 1);
        row.Children.Add(field);
        return row;
    }

    // Columns of equal width inside a field, e.g. Min and Max.
    public static Grid Split(params Control[] parts)
    {
        var grid = new Grid { ColumnDefinitions = new(string.Join(",", parts.Select(_ => "*"))), ColumnSpacing = EditorTokens.FieldGap };
        for (int i = 0; i < parts.Length; i++) { Grid.SetColumn(parts[i], i); grid.Children.Add(parts[i]); }
        return grid;
    }

    // Typing applies each value at once (commit false) and commits once on Enter or when focus leaves; spinner and
    // arrow-key steps without a focused text commit straight away. Each commit is one Undo step.
    public static NumericUpDown Number(string name, float value, decimal min, decimal max, decimal increment, Action<float, bool> changed)
    {
        var box = new NumericUpDown { Minimum = min, Maximum = max, Increment = increment, Value = decimal.CreateSaturating(value),
            HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(box, name);
        ToolTip.SetTip(box, name);
        bool pending = false;
        void Commit() { if (pending && box.Value is decimal n) { pending = false; changed((float)n, true); } }
        box.ValueChanged += (_, _) =>
        {
            if (Quiet(box) || box.Value is not decimal n) return;
            pending = box.IsKeyboardFocusWithin;
            changed((float)n, !pending);
        };
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        return box;
    }

    // Shows a value without raising a change.
    public static void Set(NumericUpDown box, float value)
    {
        var shown = decimal.CreateSaturating(value);
        if (box.Value == shown) return;
        box.Tag = Silent;
        try { box.Value = shown; }
        finally { box.Tag = null; }
    }

    private static readonly object Silent = new();
    private static bool Quiet(Control control) => ReferenceEquals(control.Tag, Silent);
}
