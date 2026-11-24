using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private sealed record PlayField(SceneObject Owner, string? Script, string Name);
    private sealed record PlayEdit(object Value, bool Selected, Action<SceneObject, object> Store);
    private readonly Dictionary<PlayField, PlayEdit> playEdits = [];
    private readonly List<Action> refreshPlayNumbers = [];

    private SceneObject? AuthoredObject(SceneGame world, SceneObject owner)
    {
        if (world != Viewport.PlayWorld || world.State.SceneId != Scene.AssetId ||
            world.Inspect(owner.Id) is not { SpawnedFrom: null } state || state.SceneId != Scene.AssetId) return null;
        return Scene.Objects.FirstOrDefault(o => o.Id == owner.Id);
    }

    private void RefreshApplyButton()
    {
        ApplyPlayValues.IsVisible = Viewport.IsPlaying && Viewport.Selection.Count == 1;
        ApplyPlayValues.IsEnabled = !busy && playEdits.Any(e => e.Key.Owner == Selected && e.Value.Selected);
        // 13.11: every edit of the session, checked or not, on every object.
        ApplyAllPlayValues.IsVisible = ApplyPlayValues.IsVisible;
        ApplyAllPlayValues.IsEnabled = !busy && playEdits.Count > 0;
        RefreshInspectorActions();
    }

    // The Inspector tab already names the panel; the action row shows only while it has something to offer.
    private void RefreshInspectorActions()
    {
        int count = Viewport.Selection.Count;
        InspectorCaption.Text = count > 1 ? $"{count} selected" : "";
        InspectorActions.IsVisible = count > 1 || ApplyPlayValues.IsVisible || OverridesButton.IsVisible;
    }

    private void ApplySelectedPlayValues(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => ApplyPlayValuesTo(all: false);
    private void ApplyAllPlayValuesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => ApplyPlayValuesTo(all: true);

    // Apply selected: the checked values of the selected object. Apply all: every value edited in this Play session. One Undo step.
    private async void ApplyPlayValuesTo(bool all)
    {
        if (busy || Viewport.PlayWorld is not {} world || !all && Selected == null) return;
        var changes = playEdits.Where(e => all || e.Key.Owner == Selected && e.Value.Selected)
            .Select(e => (e.Key, e.Value, Authored: AuthoredObject(world, e.Key.Owner))).Where(c => c.Authored != null).ToArray();
        if (changes.Length == 0) return;
        // Local transform values only describe the same placement in the same parent space.
        if (changes.Any(c => c.Key.Script == null && c.Key.Owner.ParentId != c.Authored!.ParentId))
        {
            Status.Text = "Restore the original parent or deselect the local transform values.";
            return;
        }
        bool save = project != null && !history.IsDirty;
        busy = true;
        try
        {
            foreach (var (key, edit, authored) in changes) { edit.Store(authored!, edit.Value); playEdits.Remove(key); }
            var ids = changes.Select(c => c.Authored!.Id).Distinct().ToArray();
            RecordEdit(ids[0], multiSelection: ids.Length > 1 ? ids : null);
            foreach (var refresh in refreshPlayNumbers) refresh();
            string what = all ? "all" : "selected";
            if (save && await Save()) Status.Text = $"Applied and saved {changes.Length} {what} Play value(s).";
            else Status.Text = "Applied to the edited scene. Save Scene after Stop to write the changes.";
        }
        catch (Exception ex) { LogError(ex.Message); Status.Text = ex.Message; }
        finally { busy = false; RefreshApplyButton(); UpdateMenuState(); }
    }

    private void PlayNumber<T>(StackPanel panel, SceneGame world, PlayField key, string label,
        Func<T> read, Func<T, bool> write, Func<SceneObject, T> authoredValue, Action<SceneObject, T> store)
        where T : struct, INumber<T>
    {
        var authored = AuthoredObject(world, key.Owner);
        var box = new TextBox { MinWidth = 0 };
        var check = new CheckBox { MinWidth = 0, Padding = new(0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        var row = new Grid { ColumnDefinitions = new("*,24"), ColumnSpacing = 3 };
        row.Children.Add(box);
        Grid.SetColumn(check, 1);
        row.Children.Add(check);
        InspectorRow(panel, label, row);
        string name = (key.Script ?? "Transform") + "." + key.Name;
        AutomationProperties.SetName(box, name);
        AutomationProperties.SetName(check, "Apply " + name);
        ToolTip.SetTip(box, "Enter a value and press Enter or leave the field to test it in Play.");
        ToolTip.SetTip(check, "Include this edited value in Apply selected.");
        bool updating = false, dirty = false;
        string Format() => read().ToString(null, CultureInfo.InvariantCulture);
        void Sync()
        {
            updating = true;
            if (!dirty && box.Text != Format()) box.Text = Format();
            playEdits.TryGetValue(key, out var edit);
            check.IsEnabled = edit != null;
            check.IsChecked = edit?.Selected == true;
            updating = false;
        }
        void Commit()
        {
            if (!dirty || Selected != key.Owner || Viewport.PlayWorld != world) return;
            dirty = false;
            var style = typeof(T) == typeof(int) ? NumberStyles.Integer : NumberStyles.Float;
            if (!T.TryParse(box.Text, style, CultureInfo.InvariantCulture, out var value) || !T.IsFinite(value) || !write(value))
                Status.Text = $"Enter a valid {label} value.";
            else if (authored != null && value != authoredValue(authored))
                playEdits[key] = new(value, true, (target, v) => store(target, (T)v));
            else playEdits.Remove(key);
            Sync();
            RefreshApplyButton();
        }
        Sync();
        refreshPlayNumbers.Add(Sync);
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !updating && box.IsKeyboardFocusWithin) dirty = true;
        };
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); e.Handled = true; } };
        box.LostFocus += (_, _) => Commit();
        check.IsCheckedChanged += (_, _) =>
        {
            if (updating || !playEdits.TryGetValue(key, out var edit)) return;
            playEdits[key] = edit with { Selected = check.IsChecked == true };
            RefreshApplyButton();
        };
    }

    private bool AddLiveScriptNumber(StackPanel panel, SceneGame world, SceneObject owner, ScriptComponent component,
        GameBehaviour? instance, ScriptField field)
    {
        if (instance == null || instance.GetType().GetField(field.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            is not {} member || member.IsInitOnly) return false;
        var key = new PlayField(owner, component.Script, field.Name);
        ScriptComponent Source(SceneObject o) => o.Components.OfType<ScriptComponent>().Single(c => c.Script == component.Script);
        void Store(SceneObject o, object value)
        {
            var values = Source(o).Values;
            if (Equals(value, field.Default)) values.Remove(field.Name);
            else values[field.Name] = value is Vector2 v ? JsonSerializer.SerializeToElement(new { x = v.X, y = v.Y }) : JsonSerializer.SerializeToElement(value);
        }
        object? Live() => member.GetValue(instance);
        bool Set(object value) { member.SetValue(instance, value); return true; }
        object? Authored(SceneObject o) => field.Value(Source(o));
        var type = member.FieldType;
        if (type == field.Type && field.Type == typeof(float)) Add<float>();
        else if (type == field.Type && field.Type == typeof(double)) Add<double>();
        else if (type == field.Type && field.Type == typeof(int)) Add<int>();
        // 13.11: bool, enum, Vector2, string and colour fields.
        else if (type == typeof(bool) && field.Type == typeof(bool)) PlayValue(panel, world, key, field.Name, PlayInput.Check, Live, Set, Authored, Store);
        else if (type.IsEnum && field.EnumNames is { } names && !field.Flags)
            PlayValue(panel, world, key, field.Name, PlayInput.Choice, () => Live()?.ToString(), v => Set(Enum.Parse(type, (string)v)), Authored, Store, names);
        else if (type == typeof(Vector2) && field.Type == typeof(Vector2)) PlayValue(panel, world, key, field.Name, PlayInput.Vector, Live, Set, Authored, Store);
        else if (type == typeof(System.Drawing.Color) && field.ColorAlpha is bool alpha)
            PlayValue(panel, world, key, field.Name, PlayInput.Color, () => Live() is System.Drawing.Color c ? Hex(c, alpha) : null,
                v => Engine.Core.ScriptValues.TryColor((string)v, out var c) && Set(c), Authored, Store, alpha: alpha);
        else if (type == typeof(string) && field.Type == typeof(string) && field.EnumNames == null)
            PlayValue(panel, world, key, field.Name, field.ColorAlpha != null ? PlayInput.Color : PlayInput.Text, Live,
                v => (field.ColorAlpha == null || Engine.Core.ScriptValues.TryColor((string)v, out _)) && Set(v), Authored, Store, alpha: field.ColorAlpha ?? true);
        else return false;
        return true;

        void Add<T>() where T : struct, INumber<T> =>
            PlayNumber(panel, world, key, field.Name,
                () => (T)member.GetValue(instance)!, value => { member.SetValue(instance, value); return true; },
                o => (T)field.Value(Source(o))!, (o, value) => Store(o, value));
    }

    private static string Hex(System.Drawing.Color c, bool alpha = false) =>
        alpha && c.A != 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}" : $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private enum PlayInput { Check, Choice, Text, Vector, Color }

    // 13.11: a Play row for a check, a choice, text, a Vector2 or a colour. The input applies to the running game at once (text on
    // Enter or leaving it); a value that differs from the authored one becomes a play edit with its keep check, which Apply selected
    // takes when checked and Apply all always. Stop discards what was not applied.
    private void PlayValue(StackPanel panel, SceneGame world, PlayField key, string label, PlayInput kind,
        Func<object?> read, Func<object, bool> write, Func<SceneObject, object?> authoredValue, Action<SceneObject, object> store,
        string[]? names = null, bool alpha = false)
    {
        var authored = AuthoredObject(world, key.Owner);
        string name = (key.Script ?? "Transform") + "." + key.Name;
        bool updating = false;
        var check = new CheckBox { MinWidth = 0, Padding = new(0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        Control input;
        Action show = () => { };
        void Commit(object value)
        {
            if (Selected != key.Owner || Viewport.PlayWorld != world) return;
            if (!write(value)) Status.Text = $"Enter a valid {label} value.";
            else if (authored != null && !Equals(value, authoredValue(authored)))
                playEdits[key] = new(value, !playEdits.TryGetValue(key, out var was) || was.Selected, store);
            else playEdits.Remove(key);
            Sync();
            RefreshApplyButton();
        }
        switch (kind)
        {
            case PlayInput.Check:
                var toggle = new CheckBox { MinWidth = 0 };
                toggle.IsCheckedChanged += (_, _) => { if (!updating) Commit(toggle.IsChecked == true); };
                show = () => toggle.IsChecked = read() is true;
                input = toggle;
                break;
            case PlayInput.Choice:
                var picker = new ComboBox { ItemsSource = names, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
                picker.SelectionChanged += (_, _) => { if (!updating && picker.SelectedItem is string chosen) Commit(chosen); };
                show = () => picker.SelectedItem = read();
                input = picker;
                break;
            case PlayInput.Color:
                var color = new ColorField(name, (string?)read() ?? "#FFFFFF", alpha);
                color.Changed += (hex, commit) => { if (updating) return; if (commit) Commit(hex); else write(hex); };
                show = () => { if (!color.Flyout.IsOpen && !color.IsKeyboardFocusWithin && read() is string hex) color.Show(hex); };
                input = color;
                break;
            case PlayInput.Vector:
                var axes = new Grid { ColumnDefinitions = new("Auto,*,Auto,*"), ColumnSpacing = 3 };
                var boxes = new TextBox[2];
                for (int i = 0; i < 2; i++)
                {
                    var axis = i == 0 ? "X" : "Y";
                    var caption = new TextBlock { Text = axis, Classes = { "fieldLabel" } };
                    Grid.SetColumn(caption, i * 2);
                    var box = boxes[i] = new TextBox { MinWidth = 0 };
                    AutomationProperties.SetName(box, $"{name} {axis}");
                    Grid.SetColumn(box, i * 2 + 1);
                    axes.Children.Add(caption);
                    axes.Children.Add(box);
                    box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitVector(); e.Handled = true; } };
                    box.LostFocus += (_, _) => CommitVector();
                }
                void CommitVector()
                {
                    if (float.TryParse(boxes[0].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && float.IsFinite(x) &&
                        float.TryParse(boxes[1].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) && float.IsFinite(y)) Commit(new Vector2(x, y));
                    else { Status.Text = $"Enter a valid {label} value."; Sync(); }
                }
                show = () =>
                {
                    if (axes.IsKeyboardFocusWithin || read() is not Vector2 v) return;
                    boxes[0].Text = v.X.ToString("R", CultureInfo.InvariantCulture);
                    boxes[1].Text = v.Y.ToString("R", CultureInfo.InvariantCulture);
                };
                input = axes;
                break;
            default:
                var text = new TextBox { MinWidth = 0 };
                text.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(text.Text ?? ""); e.Handled = true; } };
                text.LostFocus += (_, _) => { if ((text.Text ?? "") != (string?)read()) Commit(text.Text ?? ""); };
                show = () => { if (!text.IsKeyboardFocusWithin) text.Text = (string?)read() ?? ""; };
                input = text;
                break;
        }
        if (kind != PlayInput.Vector && kind != PlayInput.Color) AutomationProperties.SetName(input, name);
        AutomationProperties.SetName(check, "Apply " + name);
        ToolTip.SetTip(input, "Changes apply to the running game. Check the box to keep the value with Apply selected.");
        ToolTip.SetTip(check, "Include this edited value in Apply selected.");
        var row = new Grid { ColumnDefinitions = new("*,24"), ColumnSpacing = 3 };
        row.Children.Add(input);
        Grid.SetColumn(check, 1);
        row.Children.Add(check);
        InspectorRow(panel, label, row);
        void Sync()
        {
            updating = true;
            show();
            playEdits.TryGetValue(key, out var edit);
            check.IsEnabled = edit != null;
            check.IsChecked = edit?.Selected == true;
            updating = false;
        }
        check.IsCheckedChanged += (_, _) =>
        {
            if (updating || !playEdits.TryGetValue(key, out var edit)) return;
            playEdits[key] = edit with { Selected = check.IsChecked == true };
            RefreshApplyButton();
        };
        Sync();
        refreshPlayNumbers.Add(Sync);
    }

    private void BuildPlayTransform(SceneGame world, SceneObject owner)
    {
        LocalTransform2D Pose() => world.TryGetLocalTransform(owner.Id, out var pose) ? pose : default;
        void Add(string label, Func<LocalTransform2D, float> get, Func<LocalTransform2D, float, LocalTransform2D> replace,
            Func<SceneObject, float> original, Action<SceneObject, float> store) =>
            PlayNumber(LiveTransform, world, new(owner, null, label), label,
                () => get(Pose()), value => world.SetLocalTransform(owner.Id, replace(Pose(), value)), original, store);

        Add("Local X", p => p.Position.X, (p,x) => p with { Position = p.Position with { X = x } }, o => o.X, (o,x) => o.X = x);
        Add("Local Y", p => p.Position.Y, (p,y) => p with { Position = p.Position with { Y = y } }, o => o.Y, (o,y) => o.Y = y);
        Add("Rotation", p => p.Rotation, (p,r) => p with { Rotation = r }, o => o.Rotation, (o,r) => o.Rotation = r);
        Add("Scale X", p => p.Scale.X, (p,x) => p with { Scale = p.Scale with { X = x } }, o => o.ScaleX, (o,x) => o.ScaleX = x);
        Add("Scale Y", p => p.Scale.Y, (p,y) => p with { Scale = p.Scale with { Y = y } }, o => o.ScaleY, (o,y) => o.ScaleY = y);
        BuildPlayBuiltIns(world, owner);
    }

    // 13.11: simple built-in values in Play: the object's active state, the sprite tint and the collider's on/off.
    private void BuildPlayBuiltIns(SceneGame world, SceneObject owner)
    {
        var id = owner.Id;
        PlayValue(LiveTransform, world, new(owner, "Object", "Active"), "Active", PlayInput.Check, () => world.IsActive(id),
            v => world.SetActive(id, (bool)v), o => o.Active, (o, v) => o.Active = (bool)v);
        if (owner.GetComponent<SpriteRendererComponent>() != null)
        {
            static string Tint(SpriteRendererComponent r) => (r.Tint ?? (r.Asset == null && r.AssetId == null ? r.SquareColor : "#FFFFFF")).ToUpperInvariant();
            PlayValue(LiveTransform, world, new(owner, "Sprite Renderer", "Tint"), "Tint", PlayInput.Color,
                () => world.TryGetSpriteColor(id, out var c) ? Hex(c) : null, v => world.SetSpriteTint(id, (string)v),
                o => o.GetComponent<SpriteRendererComponent>() is { } r ? Tint(r) : null, (o, v) => o.GetComponent<SpriteRendererComponent>()!.Tint = (string)v);
        }
        if (owner.GetComponent<Collider2DComponent>() != null)
            PlayValue(LiveTransform, world, new(owner, "Collider", "Enabled"), "Collider", PlayInput.Check, () => world.IsColliderEnabled(id),
                v => world.SetColliderEnabled(id, (bool)v), o => o.GetComponent<Collider2DComponent>()?.Enabled, (o, v) => o.GetComponent<Collider2DComponent>()!.Enabled = (bool)v);
    }
}
