using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private readonly Dictionary<string, ComponentFoldout> multiFoldouts = [];
    private ComponentSection? multiTransformSection;
    private readonly StackPanel multiTransform = new() { Spacing = 2 };

    private static void InspectorRow(StackPanel fields, string label, Control editor)
    {
        var row = new Grid { ColumnDefinitions = EditorTokens.LabelColumns(EditorTokens.FieldLabelWidth), ColumnSpacing = EditorTokens.FieldGap };
        var title = new TextBlock { Text = label, Classes = { "fieldLabel" }, TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTip.SetTip(title, label);
        row.Children.Add(title);
        Grid.SetColumn(editor, 1);
        row.Children.Add(editor);
        fields.Children.Add(row);
    }

    private static TextBlock ValueLabel(string text)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTip.SetTip(label, text);
        return label;
    }

    private void RefreshMultiTransform()
    {
        var objects = Viewport.Selection;
        multiTransform.Children.Clear();
        string Value(Func<SceneObject, float> get)
        {
            float first = get(objects[0]);
            return objects.All(o => get(o) == first) ? first.ToString("G", CultureInfo.CurrentCulture) : "Mixed";
        }
        void Pair(string label, string a, Func<SceneObject, float> first, string b, Func<SceneObject, float> second)
        {
            var pair = new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 5 };
            pair.Children.Add(ValueLabel($"{a}  {Value(first)}"));
            var right = ValueLabel($"{b}  {Value(second)}");
            Grid.SetColumn(right, 1);
            pair.Children.Add(right);
            InspectorRow(multiTransform, label, pair);
        }
        Pair("Local pos.", "X", o => o.X, "Y", o => o.Y);
        Pair("Size", "W", o => o.Width, "H", o => o.Height);
        InspectorRow(multiTransform, "Rotation", ValueLabel(Value(o => o.Rotation)));
        Pair("Scale", "X", o => o.ScaleX, "Y", o => o.ScaleY);
        Pair("Pivot", "X", o => o.PivotX, "Y", o => o.PivotY);
    }

    private void RefreshMultiComponents()
    {
        MultiInspector.Children.Clear();
        var selection = Viewport.Selection;
        if (selection.Count < 2) return;

        void Section(string key, string title, StackPanel fields)
        {
            if (!multiFoldouts.TryGetValue(key, out var foldout)) multiFoldouts[key] = foldout = new();
            MultiInspector.Children.Add(new ComponentSection { Header = title, HasSettings = false, Foldout = foldout, Content = fields });
        }
        multiTransformSection ??= new ComponentSection { Header = "Transform", HasSettings = false, Foldout = new(), Content = multiTransform };
        MultiInspector.Children.Add(multiTransformSection);
        // Use the same persisted field and script identities as the prefab override inspector.
        var snapshots = selection.Select(obj => JsonNode.Parse(PrefabMerge.Snapshot(obj).GetRawText())!["components"]!
            .AsArray().ToDictionary(PrefabMerge.ComponentKey, c => c!.AsObject())).ToArray();
        foreach (var (key, component) in snapshots[0].Keys.Zip(selection[0].Components))
        {
            if (!snapshots.All(snapshot => snapshot.ContainsKey(key))) continue;
            var fields = new StackPanel { Spacing = 2 };
            var components = snapshots.Select(snapshot => snapshot[key]).ToArray();
            var colliders = component is Collider2DComponent
                ? selection.Select(obj => obj.Components.OfType<Collider2DComponent>().Single()).ToArray() : null;
            string Display(JsonNode?[] values) => values.Skip(1).Any(v => !JsonNode.DeepEquals(values[0], v))
                ? "Mixed" : values[0]?.ToString() ?? "(default)";
            foreach (string name in components.SelectMany(c => c.Select(p => p.Key)).Distinct())
            {
                if (name is "type" or "script") continue;
                if (name == "mask" && colliders != null)
                {
                    var button = MaskPicker(() => colliders.Select(c => c.Mask).ToArray(), change =>
                    {
                        if (Viewport.IsPlaying || !ReferenceEquals(Viewport.Selection, selection)) return;
                        if (!CollisionMaskSelection.Apply(colliders, change)) return;
                        RecordEdit(Selected?.Id, multiSelection: selection.Select(o => o.Id).ToArray());
                    });
                    button.IsEnabled = !Viewport.IsPlaying;
                    InspectorRow(fields, "Mask", button);
                }
                else if (name == "values" && component is ScriptComponent)
                {
                    foreach (string field in components.SelectMany(c => (c[name] as JsonObject)?.Select(p => p.Key) ?? []).Distinct())
                        InspectorRow(fields, field, ValueLabel(Display(components.Select(c => c[name]?[field]).ToArray())));
                }
                else
                {
                    string label = name switch { "isTrigger" => "Is Trigger", "squareColor" => "Color", _ => char.ToUpperInvariant(name[0]) + name[1..] };
                    InspectorRow(fields, label, ValueLabel(Display(components.Select(c => c[name]).ToArray())));
                }
            }
            string title = component is ScriptComponent script ? script.Script
                : ComponentChoice.Registered.TryGetValue(component.GetType(), out var choice) ? choice.Name : key.TrimEnd('/');
            Section(key, title, fields);
        }
    }
}
