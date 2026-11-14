using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private sealed record ObjectReference(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private string? inspectorSourceStamp;
    private readonly DispatcherTimer scriptRefresh = new() { Interval = TimeSpan.FromMilliseconds(750) };

    private string[] AttachableScripts()
    {
        try { return scripts?.Components() ?? []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { LogError(ex.Message); Status.Text = ex.Message; return []; }
    }

    private async Task CreateScript(SceneObject? owner, bool shared = false)
    {
        componentMenu?.Hide();
        if (project == null && !await Save()) return;
        if (scripts == null || (owner != null && !Scene.Objects.Contains(owner))) return;
        var folder = CreationFolder("Scripts");
        var prefix = folder.Length == "Scripts".Length ? "" : folder["Scripts/".Length..] + "/";
        var name = await NewScriptDialog(scripts, prefix, attach: owner != null, shared:shared);
        if (name == null) return;
        BrowseFolder(folder, $"Scripts/{name}.cs");
        var scriptPath = scripts.SourcePath(name);
        var edit = new PrefabFileEdit(scriptPath, new PrefabFiles(null, null), PrefabFileEdit.Read(scriptPath));
        if (owner == null)
        {
            RecordEdit(Selected?.Id, asset: edit);
            Status.Text = $"Created Scripts/{name}.cs";
            return;
        }
        owner.AddComponent(new ScriptComponent { Script = name });
        Select(owner);
        RecordEdit(owner.Id, asset: edit);
    }

    private Control ScriptInspector(ScriptComponent component)
    {
        var panel = new StackPanel { Spacing = 3 };
        ToolTip.SetTip(panel, $"Scripts/{component.Script}.cs");
        if (scripts == null) return panel;

        try
        {
            inspectorSourceStamp = scripts.ChangeStamp();
            var definition = scripts.Get(component.Script);
            foreach (var field in definition.Fields)
                AddScriptField(panel, field, component);
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
            panel.Children.Add(new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightSalmon });
        }
        return panel;
    }

    private void AddScriptField(StackPanel panel, ScriptField field, ScriptComponent component)
    {
        void Store(object? value, object? group = null)
        {
            if (Viewport.IsPlaying || Equals(value, field.Value(component))) return;
            if (Equals(value, field.Default)) component.Values.Remove(field.Name);
            else component.Values[field.Name] = JsonSerializer.SerializeToElement(value);
            MarkDirty(group);
        }

        if (field.Type == typeof(bool))
        {
            var check = new CheckBox { Content = new TextBlock { Text = field.Name, TextTrimming = TextTrimming.CharacterEllipsis },
                Margin = new Thickness(61, 0, 0, 0), IsChecked = (bool?)field.Value(component) };
            ToolTip.SetTip(check, field.Name);
            AutomationProperties.SetName(check, field.Name);
            check.IsCheckedChanged += (_, _) => Store(check.IsChecked == true);
            panel.Children.Add(check);
            return;
        }
        string Format() => Convert.ToString(field.Value(component), CultureInfo.InvariantCulture) ?? "";
        var row = new Grid { ColumnDefinitions = new("56,*"), ColumnSpacing = 5 };
        row.Children.Add(new TextBlock { Text = field.Name, Classes = { "fieldLabel" },
            TextTrimming = TextTrimming.CharacterEllipsis });
        ToolTip.SetTip(row, field.Name);
        string? assetExtension = field.Type == typeof(SceneReference) ? ProjectAssets.Scene :
            field.Type == typeof(PrefabReference) ? ProjectAssets.Prefab :
            field.Type == typeof(DataReference) ? ProjectAssets.Data : field.Type == typeof(TableReference) ? GameTable.Extension : null;
        if (field.Type == typeof(Guid) || assetExtension != null)
        {
            var id = field.Value(component) switch { SceneReference sc => sc.Id, PrefabReference pf => pf.Id, DataReference data => data.Id, TableReference table => table.Id, Guid guid => guid, _ => Guid.Empty };
            var choices = assetExtension != null ? (project == null ? [] : ProjectAssets.Scan(project).Entries
                .Where(e => e.Path.EndsWith(assetExtension, StringComparison.Ordinal) && File.Exists(Path.Combine(project,e.Path))).Select(e => new ObjectReference(e.Id, e.Path)).ToList())
                : (Viewport.PlayWorld?.Objects ?? Scene.Objects).Where(field.Accepts).Select(obj => new ObjectReference(obj.Id, ObjectPath(obj))).ToList();
            choices.Insert(0, new ObjectReference(Guid.Empty, "None"));
            if (choices.All(choice => choice.Id != id)) choices.Add(new ObjectReference(id, "Missing object or component"));
            var picker = new ComboBox { ItemsSource = choices, SelectedItem = choices.First(choice => choice.Id == id),
                HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(picker, field.Name);
            picker.SelectionChanged += (_, _) =>
            {
                if (picker.SelectedItem is not ObjectReference choice) return;
                Store(assetExtension switch
                {
                    ProjectAssets.Scene => new SceneReference(choice.Id),
                    ProjectAssets.Prefab => new PrefabReference(choice.Id),
                    ProjectAssets.Data => new DataReference(choice.Id),
                    GameTable.Extension => new TableReference(choice.Id),
                    _ => (object)choice.Id
                });
            };
            Grid.SetColumn(picker, 1);
            row.Children.Add(picker);
            panel.Children.Add(row);
            return;
        }
        var box = new TextBox { Text = Format() };
        AutomationProperties.SetName(box, field.Name);
        bool valid = true;
        box.TextChanged += (_, _) =>
        {
            var text = box.Text ?? "";
            valid = true;
            if (text == Format()) return;
            if (field.Type == typeof(string)) Store(text, box);
            else if (field.Type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) Store(i, box);
            else if (field.Type == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && float.IsFinite(f)) Store(f, box);
            else if (field.Type == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && double.IsFinite(d)) Store(d, box);
            else valid = false;
        };
        box.LostFocus += (_, _) =>
        {
            if (valid) return;
            box.Text = Format();
            Status.Text = $"Enter a valid number for {field.Name}.";
        };
        Grid.SetColumn(box, 1);
        row.Children.Add(box);
        panel.Children.Add(row);
    }

    private void RefreshChangedScripts()
    {
        if (busy || Viewport.IsPlaying || filePress != null) return;
        RefreshProjectFiles();
        if (inspectorSourceStamp != null && scripts != null)
            try { if (inspectorSourceStamp != scripts.ChangeStamp()) RefreshComponentList(); }
            catch (IOException ex) { Status.Text = ex.Message; }
    }

    private async Task<string?> NewScriptDialog(ProjectScripts library, string prefix, bool attach, bool shared)
    {
        var dialog = new Window
        {
            Title = shared ? "New Shared C# File" : "New C# Component", Width = 320, SizeToContent = SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false
        };
        var stem = shared ? "GameRules" : "NewBehaviour";
        var initialName = stem;
        var names = library.Names().ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int i = 2; names.Contains(prefix + initialName); i++) initialName = $"{stem}{i}";
        var name = new TextBox { Text = initialName };
        var label = shared ? "Type name" : "Component name";
        AutomationProperties.SetName(name, label);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightSalmon, IsVisible = false };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var create = new Button { Content = attach ? "Create & Add" : "Create Script", IsDefault = true };
        cancel.Click += (_, _) => dialog.Close(null);
        bool writing = false;
        dialog.Closing += (_, e) => { if (writing) e.Cancel = true; };
        create.Click += async (_, _) =>
        {
            if (writing) return;
            string scriptName = name.Text?.Trim() ?? "";
            if (!ProjectScripts.ValidName(scriptName))
            {
                error.Text = "Enter a valid C# class name, such as MovingPlatform.";
                error.IsVisible = true;
                return;
            }
            writing = true;
            create.IsEnabled = cancel.IsEnabled = name.IsEnabled = false;
            try { await Task.Run(() => library.Save(prefix + scriptName, shared ? $"public static class {scriptName}\n{{\n}}\n" : ProjectScripts.Template(scriptName), null, requireComponent:!shared)); }
            catch (Exception ex)
            {
                error.Text = ex.Message;
                error.IsVisible = true;
                return;
            }
            finally
            {
                writing = false;
                create.IsEnabled = cancel.IsEnabled = name.IsEnabled = true;
            }
            dialog.Close(prefix + scriptName);
        };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(14), Spacing = 6,
            Children =
            {
                new TextBlock { Text = label }, name, error,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 6, Children = { cancel, create } }
            }
        };
        dialog.Opened += (_, _) => { name.Focus(); name.SelectAll(); };
        return await dialog.ShowDialog<string?>(this);
    }
}
