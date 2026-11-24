using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Engine.Core;

namespace Engine.Editor;

// 13.8: Inspector rows for primitive arrays, List<T>, dictionaries (key/value rows) and one-level [Serializable] structs (a fold
// row). They reuse the reference list's layout: a heading on the label column, one row per element with its index in the label
// column, the input in the value column and "−" in the Auto column; "+" sits in that column of the heading. Every edit writes the
// whole JSON value (ScriptValueEdits.Store); typing stays one pending edit, "+" / "−" / checks record once.
public partial class MainWindow
{
    private readonly HashSet<string> openStructFields = new(StringComparer.Ordinal);

    private static Grid FieldGrid(string rest = "*,Auto") =>
        new() { ColumnDefinitions = EditorTokens.LabelColumns(EditorTokens.FieldLabelWidth, rest), ColumnSpacing = EditorTokens.FieldGap };

    private static TextBlock FieldLabel(string text, double indent = 0) =>
        new() { Text = text, Classes = { "fieldLabel" }, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(indent, 0, 0, 0) };

    private static T At<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }

    // One scalar input: a check box, or a text box that keeps the last valid value (an invalid text goes back on leaving).
    private static Control ScalarInput(string name, Type type, JsonNode? value, Action<JsonNode?, object?> set)
    {
        if (type == typeof(bool))
        {
            var check = new CheckBox { IsChecked = value?.GetValue<bool>() == true, MinWidth = 0 };
            AutomationProperties.SetName(check, name);
            check.IsCheckedChanged += (_, _) => set(JsonValue.Create(check.IsChecked == true), null);
            return check;
        }
        var box = new TextBox { Text = ScriptValueEdits.Text(value), MinWidth = 0 };
        AutomationProperties.SetName(box, name);
        string last = box.Text;
        box.TextChanged += (_, _) =>
        {
            if (box.Text == last || !ScriptValueEdits.TryParse(type, box.Text ?? "", out var parsed)) return;
            last = box.Text ?? "";
            set(parsed, box);
        };
        box.LostFocus += (_, _) => { if (box.Text != last) box.Text = last; };
        return box;
    }

    // A flag enum: the chosen names on a button whose flyout holds one check per non-zero member.
    private static Control FlagsInput(string name, string[] names, string current, Action<string> set)
    {
        var on = ScriptValueEdits.FlagSet(current);
        static string Shown(string s) => s.Length == 0 ? "Nothing" : s;
        var text = new TextBlock { Text = Shown(current), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var list = new StackPanel { Spacing = 2 };
        foreach (var flag in names)
        {
            var check = new CheckBox { Content = flag, IsChecked = on.Contains(flag) };
            AutomationProperties.SetName(check, $"{name} {flag}");
            check.IsCheckedChanged += (_, _) =>
            {
                if (check.IsChecked == true) on.Add(flag); else on.Remove(flag);
                var value = ScriptValueEdits.Flags(names, on);
                text.Text = Shown(value);
                set(value);
            };
            list.Children.Add(check);
        }
        var button = new Button
        {
            Content = text, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Flyout = new Flyout { Content = list, Placement = PlacementMode.BottomEdgeAlignedLeft }
        };
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, "Choose any of: " + string.Join(", ", names));
        return button;
    }

    // A struct member's input (no saved key of its own: the struct's JSON object holds it).
    private Control MemberInput(string name, ScriptField member, JsonNode? value, Action<JsonNode?, object?> set)
    {
        if (member.Flags) return FlagsInput(name, member.EnumNames!, ScriptValueEdits.Text(value), s => set(JsonValue.Create(s), null));
        if (member.EnumNames is { } names)
        {
            var picker = new ComboBox { ItemsSource = names, SelectedItem = ScriptValueEdits.Text(value), HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(picker, name);
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is string chosen) set(JsonValue.Create(chosen), null); };
            return picker;
        }
        if (member.ColorAlpha is bool alpha)
        {
            var color = new ColorField(name, ScriptValueEdits.Text(value), alpha);
            color.Changed += (hex, commit) => { if (commit) set(JsonValue.Create(hex), null); };
            return color;
        }
        if (member.Type == typeof(Vector2))
        {
            var axes = new Grid { ColumnDefinitions = new("Auto,*,Auto,*"), ColumnSpacing = 3 };
            var xy = value?.AsObject() ?? new JsonObject { ["x"] = 0f, ["y"] = 0f };
            foreach (var (axis, column) in new[] { ("x", 0), ("y", 2) })
            {
                axes.Children.Add(At(new TextBlock { Text = axis.ToUpperInvariant(), Classes = { "fieldLabel" } }, column));
                axes.Children.Add(At(ScalarInput($"{name} {axis.ToUpperInvariant()}", typeof(float), xy[axis], (v, group) =>
                {
                    xy[axis] = v;
                    set(JsonNode.Parse(xy.ToJsonString()), group);
                }), column + 1));
            }
            return axes;
        }
        return ScalarInput(name, member.Type, value, set);
    }

    private Control ScriptJsonRows(ScriptField field, ScriptComponent component, string tip, Action<Panel> addHeading)
    {
        var rows = new StackPanel { Spacing = 2 };
        var value = ScriptValueEdits.Current(component, field);
        // Stores the edited copy; typing (group = the text box) records on commit, everything else at once.
        void Commit(object? group = null, bool render = false)
        {
            if (Viewport.IsPlaying || !ScriptValueEdits.Store(component, field, value?.DeepClone())) return;
            MarkDirty(group);
            if (render) Render();
        }

        void Render()
        {
            rows.Children.Clear();
            var heading = FieldGrid();
            heading.Children.Add(FieldLabel(field.Name));
            ToolTip.SetTip(heading, tip);
            rows.Children.Add(heading);
            if (field.Members is { } members)
            {
                string key = component.Script + "." + field.Name;
                bool open = openStructFields.Contains(key);
                var fields = value!.AsObject();
                var summary = new TextBlock { Classes = { "label" }, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
                    Text = string.Join(" · ", members.Select(m => $"{m.Name} {Summary(fields[m.Name])}")) };
                heading.Children.Add(At(summary, 1));
                var fold = EditorTokens.IconButton("IconChevronRight", (open ? "Fold " : "Unfold ") + field.Name);
                fold.RenderTransform = new RotateTransform(open ? 90 : 0);
                fold.Click += (_, _) => { if (!openStructFields.Remove(key)) openStructFields.Add(key); Render(); };
                heading.Children.Add(At(fold, 2));
                addHeading(heading);
                if (!open) return;
                foreach (var member in members)
                {
                    var row = FieldGrid("*");
                    row.Children.Add(FieldLabel(member.Name, indent: 8));
                    row.Children.Add(At(MemberInput($"{field.Name} {member.Name}", member, fields[member.Name], (v, group) =>
                    {
                        fields[member.Name] = v;
                        Commit(group);
                    }), 1));
                    ToolTip.SetTip(row, $"{field.Name}.{member.Name}");
                    rows.Children.Add(row);
                }
                return;
            }

            var element = field.Element!;
            int count = value switch { JsonArray a => a.Count, JsonObject o => o.Count, _ => 0 };
            heading.Children.Add(At(new TextBlock { Text = count == 1 ? "1 item" : $"{count} items", Classes = { "label" }, VerticalAlignment = VerticalAlignment.Center }, 1));
            var add = EditorTokens.IconButton("IconAdd", $"Add to {field.Name}");
            heading.Children.Add(At(add, 2));
            addHeading(heading);
            if (field.Key is { } keyType)
            {
                var map = value as JsonObject ?? new JsonObject();
                add.Click += (_, _) => { map[ScriptValueEdits.NewKey(map, keyType)] = ScriptValueEdits.Blank(element); value = map; Commit(render: true); };
                int index = 0;
                foreach (var (entryKey, entryValue) in map.ToArray())
                {
                    string at = entryKey;
                    var row = FieldGrid("*,*,Auto");
                    row.Children.Add(FieldLabel((++index).ToString(CultureInfo.InvariantCulture)));
                    var keyBox = new TextBox { Text = at, MinWidth = 0 };
                    AutomationProperties.SetName(keyBox, $"{field.Name} key {index}");
                    ToolTip.SetTip(keyBox, keyType == typeof(int) ? "Key (int)" : "Key");
                    void Rename()
                    {
                        var to = keyBox.Text ?? "";
                        if (to == at) return;
                        if (!ScriptValueEdits.RenameKey(map, at, to, keyType))
                        {
                            Status.Text = keyType == typeof(int) ? $"Enter a new int key for {field.Name}." : $"{field.Name} already has the key '{to}'.";
                            keyBox.Text = at;
                            return;
                        }
                        at = to;
                        Commit();
                    }
                    keyBox.LostFocus += (_, _) => Rename();
                    keyBox.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Rename(); };
                    row.Children.Add(At(keyBox, 1));
                    row.Children.Add(At(ScalarInput($"{field.Name} {index}", element, entryValue, (v, group) => { map[at] = v; Commit(group); }), 2));
                    row.Children.Add(At(RemoveButton($"{field.Name} {index}", () => { map.Remove(at); Commit(render: true); }), 3));
                    rows.Children.Add(row);
                }
                return;
            }
            var list = value as JsonArray ?? new JsonArray();
            add.Click += (_, _) => { list.Add(ScriptValueEdits.Blank(element)); value = list; Commit(render: true); };
            for (int i = 0; i < list.Count; i++)
            {
                int at = i;
                var row = FieldGrid();
                row.Children.Add(FieldLabel((i + 1).ToString(CultureInfo.InvariantCulture)));
                row.Children.Add(At(ScalarInput($"{field.Name} {i + 1}", element, list[i], (v, group) => { list[at] = v; Commit(group); }), 1));
                row.Children.Add(At(RemoveButton($"{field.Name} {i + 1}", () => { list.RemoveAt(at); Commit(render: true); }), 2));
                rows.Children.Add(row);
            }
        }

        Render();
        return rows;
    }

    private static Button RemoveButton(string name, Action remove)
    {
        var button = EditorTokens.IconButton("IconClose", "Remove " + name); // the same 28px column as the heading's +
        button.Click += (_, _) => remove();
        return button;
    }

    private static string Summary(JsonNode? value) => value is JsonObject xy && xy.ContainsKey("x")
        ? $"({ScriptValueEdits.Text(xy["x"])}, {ScriptValueEdits.Text(xy["y"])})" : value == null ? "—" : ScriptValueEdits.Text(value);
}
