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
        RefreshInspectorActions();
    }

    // The Inspector tab already names the panel; the action row shows only while it has something to offer.
    private void RefreshInspectorActions()
    {
        int count = Viewport.Selection.Count;
        InspectorCaption.Text = count > 1 ? $"{count} selected" : "";
        InspectorActions.IsVisible = count > 1 || ApplyPlayValues.IsVisible || OverridesButton.IsVisible;
    }

    private async void ApplySelectedPlayValues(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (busy || Selected is not {} live || Viewport.PlayWorld is not {} world ||
            AuthoredObject(world, live) is not {} authored) return;
        var changes = playEdits.Where(e => e.Key.Owner == live && e.Value.Selected).ToArray();
        if (changes.Length == 0) return;
        // Local transform values only describe the same placement in the same parent space.
        if (changes.Any(e => e.Key.Script == null) && live.ParentId != authored.ParentId)
        {
            Status.Text = "Restore the original parent or deselect the local transform values.";
            return;
        }
        bool save = project != null && !history.IsDirty;
        busy = true;
        try
        {
            foreach (var (key, edit) in changes) { edit.Store(authored, edit.Value); playEdits.Remove(key); }
            RecordEdit(authored.Id);
            foreach (var refresh in refreshPlayNumbers) refresh();
            if (save && await Save()) Status.Text = $"Applied and saved {changes.Length} selected Play value(s).";
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
        if (instance == null || instance.GetType().GetField(field.Name, BindingFlags.Instance | BindingFlags.Public)
            is not {} member || member.IsInitOnly || member.FieldType != field.Type) return false;
        if (field.Type == typeof(float)) Add<float>();
        else if (field.Type == typeof(double)) Add<double>();
        else if (field.Type == typeof(int)) Add<int>();
        else return false;
        return true;

        ScriptComponent Source(SceneObject o) => o.Components.OfType<ScriptComponent>().Single(c => c.Script == component.Script);
        void Add<T>() where T : struct, INumber<T> =>
            PlayNumber(panel, world, new(owner, component.Script, field.Name), field.Name,
                () => (T)member.GetValue(instance)!, value => { member.SetValue(instance, value); return true; },
                o => (T)field.Value(Source(o))!, (o, value) =>
                {
                    var values = Source(o).Values;
                    if (Equals(value, field.Default)) values.Remove(field.Name);
                    else values[field.Name] = JsonSerializer.SerializeToElement(value);
                });
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
    }
}
