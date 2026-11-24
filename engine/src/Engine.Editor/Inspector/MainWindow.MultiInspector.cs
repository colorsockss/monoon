using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private readonly Dictionary<string, ComponentFoldout> multiFoldouts = [];
    private ComponentSection? multiTransformSection;
    private readonly StackPanel multiTransform = new() { Spacing = 2 };

    // Asset fields of built-in components that a multi-selection edits together (13.17): the saved keys they stand for, the
    // files they take, and how each component reads and writes the asset (the sprite keeps its path beside the ID).
    private sealed record AssetSlot(string Label, string[] Keys, Func<string, bool> Accepts, Func<SceneComponent, Guid?> Get, Action<SceneComponent, Guid?, string?> Set);

    private static AssetSlot[] AssetSlots(SceneComponent component) => component switch
    {
        SpriteRendererComponent => [new("Sprite", ["asset", "assetId"], SpriteAssets.IsPng, c => ((SpriteRendererComponent)c).AssetId,
            (c, id, path) => { var s = (SpriteRendererComponent)c; (s.Asset, s.AssetId) = (id == null ? null : path, id); })],
        AudioSourceComponent => [new("Sound", ["sound"], AudioFile.IsSupported, c => ((AudioSourceComponent)c).Sound, (c, id, _) => ((AudioSourceComponent)c).Sound = id)],
        ParticleEmitter2DComponent => [new("Particles", ["particles"], EndsWith(ParticleEffect.Extension), c => ((ParticleEmitter2DComponent)c).Particles,
            (c, id, _) => ((ParticleEmitter2DComponent)c).Particles = id)],
        SpriteShader2DComponent => [new("Shader", ["shader"], EndsWith(ShaderClip.Extension), c => ((SpriteShader2DComponent)c).Shader, (c, id, _) => ((SpriteShader2DComponent)c).Shader = id)],
        UiImageComponent => [new("Sprite", ["sprite"], SpriteAssets.IsPng, c => ((UiImageComponent)c).Sprite,
            (c, id, _) => { var image = (UiImageComponent)c; if (image.Sprite != id) (image.Sprite, image.Borders) = (id, null); })],
        _ => [],
    };

    private static void InspectorRow(StackPanel fields, string label, Control editor) => fields.Children.Add(InspectorRowControl(label, editor));

    // The one labelled Inspector row (13.13: EditorContext.Row uses it too).
    internal static Grid InspectorRowControl(string label, Control editor)
    {
        var row = new Grid { ColumnDefinitions = EditorTokens.LabelColumns(EditorTokens.FieldLabelWidth), ColumnSpacing = EditorTokens.FieldGap };
        var title = new TextBlock { Text = label, Classes = { "fieldLabel" }, TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTip.SetTip(title, label);
        row.Children.Add(title);
        Grid.SetColumn(editor, 1);
        row.Children.Add(editor);
        return row;
    }

    private static TextBlock ValueLabel(string text)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTip.SetTip(label, text);
        return label;
    }

    // 13.5: every multi-selection value is an input; a value the objects do not share shows "—". A committed value (Enter,
    // focus leaving, a spinner step, a label scrub) goes to every selected object and is one Undo record.
    private void RefreshMultiTransform()
    {
        var objects = Viewport.Selection;
        multiTransform.Children.Clear();
        double? Shared(Func<SceneObject, float> get) => objects.All(o => get(o) == get(objects[0])) ? get(objects[0]) : null;
        void Apply(Action<SceneObject, float> set, float value)
        {
            if (Viewport.IsPlaying || !ReferenceEquals(Viewport.Selection, objects)) return;
            foreach (var o in objects) set(o, value);
            RecordEdit(Selected?.Id, multiSelection: objects.Select(o => o.Id).ToArray());
            Viewport.RefreshScene();
            Dispatcher.UIThread.Post(() => { if (ReferenceEquals(Viewport.Selection, objects) && scrub == null) RefreshMultiTransform(); });
        }
        Control Axis(string axis, string name, Func<SceneObject, float> get, Action<SceneObject, float> set, decimal increment = 1, decimal min = -1000000, decimal max = 1000000)
        {
            var box = MultiNumber(name, Shared(get), v => Apply(set, (float)v), min, max, increment);
            if (axis.Length == 0) return box;
            var pair = new Grid { ColumnDefinitions = new("12,*"), ColumnSpacing = 2 };
            pair.Children.Add(new TextBlock { Text = axis, Classes = { "fieldLabel" } });
            Grid.SetColumn(box, 1);
            pair.Children.Add(box);
            return pair;
        }
        void Pair(string label, Control a, Control b) => InspectorRow(multiTransform, label, FieldRows.Split(a, b));
        Pair("Local pos.", Axis("X", "Position X", o => o.X, (o, v) => o.X = v), Axis("Y", "Position Y", o => o.Y, (o, v) => o.Y = v));
        Pair("Size", Axis("W", "Width", o => o.Width, (o, v) => Scene.Resize(o, v, o.Height), 1, 1),
            Axis("H", "Height", o => o.Height, (o, v) => Scene.Resize(o, o.Width, v), 1, 1));
        InspectorRow(multiTransform, "Rotation", Axis("", "Rotation degrees", o => o.Rotation, (o, v) => o.Rotation = v));
        Pair("Scale", Axis("X", "Scale X", o => o.ScaleX, (o, v) => { if (v != 0) o.ScaleX = v; }, .1m),
            Axis("Y", "Scale Y", o => o.ScaleY, (o, v) => { if (v != 0) o.ScaleY = v; }, .1m));
        Pair("Pivot", Axis("X", "Pivot X", o => o.PivotX, (o, v) => o.PivotX = v, .1m, 0, 1),
            Axis("Y", "Pivot Y", o => o.PivotY, (o, v) => o.PivotY = v, .1m, 0, 1));
    }

    // A number box that commits once: Enter, focus leaving, or a step while the text is not being typed. Mixed shows "—".
    private static NumericUpDown MultiNumber(string name, double? value, Action<double> commit, decimal min = -1000000, decimal max = 1000000, decimal increment = 1)
    {
        var box = new NumericUpDown { Minimum = min, Maximum = max, Increment = increment, Watermark = "—", HorizontalAlignment = HorizontalAlignment.Stretch,
            Value = value is double v ? Math.Clamp(decimal.CreateSaturating(v), min, max) : null };
        AutomationProperties.SetName(box, name);
        ToolTip.SetTip(box, value == null ? $"{name}: the selected objects differ" : name);
        decimal? committed = box.Value;
        void Commit() { if (box.Value is decimal n && n != committed) { committed = n; commit((double)n); } }
        box.ValueChanged += (_, _) => { if (!box.IsKeyboardFocusWithin) Commit(); };
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        return box;
    }

    // One editor for a scalar field of every selected component: the typed value per object, and write for the new one.
    private static Control MultiValue(string label, Type type, string[]? names, object?[] values, Action<object> write, float? min = null, float? max = null)
    {
        bool mixed = values.Skip(1).Any(v => !Equals(v, values[0]));
        object? shown = mixed ? null : values[0];
        string tip = mixed ? $"{label}: the selected objects differ" : label;
        if (type == typeof(bool))
        {
            var check = new CheckBox { IsChecked = mixed ? null : shown is true, MinHeight = 0, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(check, label);
            ToolTip.SetTip(check, tip);
            check.IsCheckedChanged += (_, _) => { if (check.IsChecked is bool on) write(on); };
            return check;
        }
        if (names != null || type.IsEnum)
        {
            var picker = new ComboBox { ItemsSource = names ?? Enum.GetNames(type), SelectedItem = shown?.ToString(), PlaceholderText = "—",
                HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(picker, label);
            ToolTip.SetTip(picker, tip);
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is string name) write(type.IsEnum ? Enum.Parse(type, name) : name); };
            return picker;
        }
        if (type == typeof(string))
        {
            var box = new TextBox { Text = shown as string ?? "", Watermark = mixed ? "—" : null };
            AutomationProperties.SetName(box, label);
            ToolTip.SetTip(box, tip);
            string? committed = mixed ? null : box.Text;
            void Commit() { if (box.Text is { } text && text != committed) { committed = text; write(text); } }
            box.LostFocus += (_, _) => Commit();
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
            return box;
        }
        decimal low = min is float l ? (decimal)l : type == typeof(int) ? int.MinValue : -1000000000;
        decimal high = max is float h ? (decimal)h : type == typeof(int) ? int.MaxValue : 1000000000;
        return MultiNumber(label, shown == null ? null : Convert.ToDouble(shown, CultureInfo.InvariantCulture),
            v => write(type == typeof(int) ? (int)Math.Round(v) : type == typeof(float) ? (float)v : v), low, high);
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
                ? "—" : values[0]?.ToString() ?? "(default)";
            var slots = AssetSlots(component);
            var builtIns = MultiEdits.Fields(component.GetType());
            var owned = selection.Select((obj, i) => snapshots[i].Keys.Zip(obj.Components).First(p => p.First == key).Second).ToArray();
            void Write(string? group, string field, JsonNode? value)
            {
                if (Viewport.IsPlaying || !ReferenceEquals(Viewport.Selection, selection)) return;
                if (!MultiEdits.Set(selection, key, group, field, value)) return;
                RecordEdit(Selected?.Id, multiSelection: selection.Select(o => o.Id).ToArray());
                Viewport.RefreshScene();
                // The components were replaced: rebuild so every row reads the new ones (after a scrub, at its end).
                Dispatcher.UIThread.Post(() => { if (ReferenceEquals(Viewport.Selection, selection) && scrub == null) RefreshComponentList(); });
            }
            foreach (var slot in slots)
            {
                var values = owned.Select(slot.Get).Distinct().ToArray();
                var ids = selection.Select(o => o.Id).ToArray();
                var field = AssetField(slot.Label, slot.Accepts, values.Length == 1 ? values[0] : null, id =>
                {
                    if (Viewport.IsPlaying || !ReferenceEquals(Viewport.Selection, selection)) return;
                    string? path = id is Guid g && project != null ? ProjectAssets.Scan(project).PathFor(g) : null;
                    foreach (var c in owned) slot.Set(c, id, path);
                    RecordEdit(Selected?.Id, multiSelection: ids);
                    Viewport.ClearSprites();
                    RefreshComponentList();
                });
                if (values.Length > 1) { field.Text.Text = "—"; ToolTip.SetTip(field, $"{slot.Label}: the selected objects differ. Drop or pick to set them all."); }
                InspectorRow(fields, slot.Label, field);
            }
            foreach (var builtIn in builtIns)
            {
                if (slots.Any(s => s.Keys.Contains(builtIn.Key)) || builtIn.Key == "mask") continue;
                var values = owned.Select(c => builtIn.Property.GetValue(c)).ToArray();
                var editor = MultiValue(builtIn.Label, builtIn.Type, null, values, v => Write(null, builtIn.Key, builtIn.Node(component.GetType(), v)));
                editor.IsEnabled = !Viewport.IsPlaying;
                InspectorRow(fields, builtIn.Label, editor);
            }
            foreach (string name in components.SelectMany(c => c.Select(p => p.Key)).Distinct())
            {
                if (name is "type" or "script" or "values" || slots.Any(s => s.Keys.Contains(name)) || builtIns.Any(f => f.Key == name)) continue;
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
                else
                {
                    string label = name switch { "squareColor" => "Color", _ => char.ToUpperInvariant(name[0]) + name[1..] };
                    InspectorRow(fields, label, ValueLabel(Display(components.Select(c => c[name]).ToArray())));
                }
            }
            if (component is ScriptComponent shared && scripts != null) MultiScriptRows(fields, shared.Script, owned.Cast<ScriptComponent>().ToArray(), Write);
            string title = component is ScriptComponent script ? script.Script
                : ComponentChoice.Registered.TryGetValue(component.GetType(), out var choice) ? choice.Name : key.TrimEnd('/');
            Section(key, title, fields);
        }
    }

    // A script's Inspector fields for every selected copy: scalars are inputs (kept inside [Range]/[Min]); references and
    // vectors show their shared value or "—". A value equal to the script's default removes the saved key, as one edit does.
    private void MultiScriptRows(StackPanel fields, string name, ScriptComponent[] instances, Action<string?, string, JsonNode?> write)
    {
        try
        {
            var definition = scripts!.Get(name);
            var presentation = scripts.Inspector(name);
            foreach (var field in definition.Fields)
            {
                var ui = presentation?.Fields.GetValueOrDefault(field.Name);
                if (ui?.Hidden == true) continue;
                // 13.8: arrays, dictionaries and structs compare by their JSON text and stay read-only here, as flag sets do.
                var values = instances.Select(c => field.Value(c) is System.Text.Json.JsonElement j ? j.GetRawText() : field.Value(c)).ToArray();
                bool scalar = field.EnumNames != null && !field.Flags || field.Type == typeof(bool) || field.Type == typeof(int) || field.Type == typeof(float) ||
                    field.Type == typeof(double) || field.Type == typeof(string);
                object Limit(object v) => v switch
                {
                    int i => (int)Math.Clamp(i, ui?.Min is float a ? MathF.Ceiling(a) : int.MinValue, ui?.Max is float b ? MathF.Floor(b) : int.MaxValue),
                    float f => Math.Clamp(f, ui?.Min ?? float.MinValue, ui?.Max ?? float.MaxValue),
                    double d => Math.Clamp(d, ui?.Min ?? double.MinValue, ui?.Max ?? double.MaxValue),
                    _ => v
                };
                Control editor = scalar
                    ? MultiValue(field.Name, field.Type, field.EnumNames, values, v =>
                    {
                        v = Limit(v);
                        write("values", field.Name, Equals(v, field.Default) ? null : JsonSerializer.SerializeToNode(v, v.GetType()));
                    }, ui?.Min, ui?.Max)
                    : ValueLabel(values.Skip(1).Any(v => !Equals(v, values[0])) ? "—" : Convert.ToString(values[0], CultureInfo.InvariantCulture) ?? "(default)");
                if (ui?.Tooltip is { } tip) ToolTip.SetTip(editor, tip);
                editor.IsEnabled = !Viewport.IsPlaying;
                InspectorRow(fields, field.Name, editor);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            fields.Children.Add(new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, Classes = { "error" } });
        }
    }
}
