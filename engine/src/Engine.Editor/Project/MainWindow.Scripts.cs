using System.Globalization;
using System.Numerics;
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
        if (scripts == null) return panel;

        try
        {
            ToolTip.SetTip(panel, scripts.RelativePath(component.Script));
            inspectorSourceStamp = scripts.ChangeStamp();
            var definition = scripts.Get(component.Script);
            var presentation = scripts.Inspector(component.Script);
            if (ProjectScripts.NormalizeValues(component.Values, definition) && !Viewport.IsPlaying) Scene.MigratedScriptFields = true;
            AddGameEvents(panel, definition, component);
            var world = Viewport.PlayWorld;
            var owner = Inspected;
            var instance = owner != null && world != null
                ? owner.Components.OfType<ScriptComponent>().Zip(world.LiveScripts(owner.Id))
                    .FirstOrDefault(p => ReferenceEquals(p.First, component)).Second : null;
            void AddField(ScriptField field)
            {
                var ui = presentation?.Fields.GetValueOrDefault(field.Name);
                if (ui?.Hidden == true) return;
                if (ui?.Space is float space) panel.Children.Add(new Border { Height = Math.Clamp(space, 0, 200) - panel.Spacing });
                if (ui?.Header is { } header)
                    panel.Children.Add(new TextBlock { Text = header, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis,
                        Margin = new Thickness(0, 4, 0, 0), Classes = { "fieldHeader" } });
                if (world != null && owner != null && AddLiveScriptNumber(panel, world, owner, component, instance, field)) return;
                if (!AddDrawnField(panel, field, component, ui)) AddScriptField(panel, field, component, ui);
                if (world != null) panel.Children[^1].IsEnabled = false;
            }
            if (extensions?.Registry.Inspectors.GetValueOrDefault(component.Script) is {} custom)
                custom(new ScriptInspector(new EditorContext(this), component, panel, name =>
                    AddField(definition.Fields.FirstOrDefault(f => f.Name == name) ?? throw new InvalidDataException($"{component.Script} has no Inspector field '{name}'."))));
            else foreach (var field in definition.Fields) AddField(field);
            // Public or [SerializeField] fields the Inspector cannot show, so their absence is not a mystery.
            if (presentation?.Skipped.Where(n => n.Expected).ToArray() is { Length: > 0 } skipped)
            {
                var note = new TextBlock { Text = "Not shown: " + string.Join(", ", skipped.Select(n => $"{n.Name} ({n.Reason})")),
                    TextWrapping = TextWrapping.Wrap, Classes = { "label", "skippedFields" } };
                ToolTip.SetTip(note, "The Inspector shows int, long, uint, float, double, bool, string, enum (and [Flags]), Vector2, Color, Guid, " +
                    "asset and object references, arrays and List<T> of those numbers, bool or string, Dictionary<string or int, …> and " +
                    "[Serializable] structs, with a constant initializer. These fields keep the value the script gives them.");
                panel.Children.Add(note);
            }
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
            panel.Children.Add(new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, Classes = { "error" } });
        }
        return panel;
    }

    // 13.13: a project's Editor/ field drawer, matched by an attribute of the field first, then by its C# type. The drawer's
    // control goes in the value column of the usual row (override bar and row menu included); an exception shows in its place.
    private bool AddDrawnField(StackPanel panel, ScriptField field, ScriptComponent component, ScriptFieldUi? ui)
    {
        if (extensions?.Registry.Drawers is not { Count: > 0 } drawers || ui == null) return false;
        var build = ui.Attributes.Select(a => drawers.GetValueOrDefault(a)).FirstOrDefault(d => d != null) ?? drawers.GetValueOrDefault(ui.TypeName);
        if (build == null) return false;
        Control input;
        try { input = build(new FieldDrawer(new EditorContext(this), component, field)); }
        catch (Exception ex)
        {
            LogError($"{ProjectScripts.EditorFolder} drawer for {component.Script}.{field.Name}: {ex.GetBaseException().Message}");
            input = new TextBlock { Text = ex.GetBaseException().Message, TextWrapping = TextWrapping.Wrap, Classes = { "error" } };
        }
        if (AutomationProperties.GetName(input) is null or "") AutomationProperties.SetName(input, field.Name);
        var row = InspectorRowControl(field.Name, input);
        ToolTip.SetTip(row, ui.Tooltip ?? field.Name);
        overrideRows.Add((row, [new PrefabProperty("script/" + component.Script, "values", field.Name)]));
        panel.Children.Add(row);
        return true;
    }

    private void AddScriptField(StackPanel panel, ScriptField field, ScriptComponent component, ScriptFieldUi? ui = null)
    {
        string tip = ui?.Tooltip ?? field.Name;
        // [Range]/[Min]: the stored number is kept inside the limits.
        object? Limit(object? value) => value switch
        {
            int i when ui?.Min is float min && i < min => (int)MathF.Ceiling(min),
            int i when ui?.Max is float max && i > max => (int)MathF.Floor(max),
            float f when ui?.Min is float min && f < min => min,
            float f when ui?.Max is float max && f > max => max,
            double d when ui?.Min is float min && d < min => (double)min,
            double d when ui?.Max is float max && d > max => (double)max,
            _ => value
        };
        void Store(object? value, object? group = null)
        {
            value = Limit(value);
            if (Viewport.IsPlaying || Equals(value, field.Value(component))) return;
            if (Equals(value, field.Default)) component.Values.Remove(field.Name);
            else if (value is Vector2 v) component.Values[field.Name] = JsonSerializer.SerializeToElement(new { x = v.X, y = v.Y });
            else component.Values[field.Name] = JsonSerializer.SerializeToElement(value);
            MarkDirty(group);
        }

        // 13.6: the row shows this field's prefab override (bold label, bar) and offers Revert / Apply to Source.
        void AddRow(Panel row)
        {
            overrideRows.Add((row, [new PrefabProperty("script/" + component.Script, "values", field.Name)]));
            panel.Children.Add(row);
        }
        if (field.IsObjectList || field.IsAssetList)
        {
            panel.Children.Add(ReferenceList(field, component));
            return;
        }
        // 13.8: arrays, List<T>, dictionaries and structs (MainWindow.ScriptCollections); the heading carries the override bar.
        if (field.IsJson)
        {
            panel.Children.Add(ScriptJsonRows(field, component, tip, heading =>
                overrideRows.Add((heading, [new PrefabProperty("script/" + component.Script, "values", field.Name)]))));
            return;
        }
        if (field.Type == typeof(bool))
        {
            var check = new CheckBox { Content = new TextBlock { Text = field.Name, TextTrimming = TextTrimming.CharacterEllipsis },
                Margin = new Thickness(61, 0, 0, 0), IsChecked = (bool?)field.Value(component) };
            ToolTip.SetTip(check, tip);
            AutomationProperties.SetName(check, field.Name);
            check.IsCheckedChanged += (_, _) => Store(check.IsChecked == true);
            AddRow(new Panel { Children = { check } }); // a Panel (not a Grid) keeps the check where it was and can hold the override bar
            return;
        }
        string Format() => Convert.ToString(field.Value(component), CultureInfo.InvariantCulture) ?? "";
        var row = new Grid { ColumnDefinitions = EditorTokens.LabelColumns(EditorTokens.FieldLabelWidth), ColumnSpacing = EditorTokens.FieldGap };
        var title = new TextBlock { Text = field.Name, Classes = { "fieldLabel" }, TextTrimming = TextTrimming.CharacterEllipsis };
        // 13.10: a number's label scrubs its box (MainWindow.Scrub), inside [Range]/[Min].
        if (field.Type == typeof(int) || field.Type == typeof(float) || field.Type == typeof(double))
            title.Tag = new ScrubSpec(ui?.Min, ui?.Max, field.Type == typeof(int));
        row.Children.Add(title);
        ToolTip.SetTip(row, tip);
        if (field.Flags)
        {
            row.Children.Add(At(FlagsInput(field.Name, field.EnumNames!, (string?)field.Value(component) ?? "", s => Store(s)), 1));
            AddRow(row);
            return;
        }
        if (field.ColorAlpha is bool alpha)
        {
            // As the built-in colour rows: the picker and typing apply live, closing it / Enter / leaving records once.
            var color = new ColorField(field.Name, (string?)field.Value(component) ?? "#FFFFFF", alpha);
            color.Changed += (hex, commit) =>
            {
                if (Viewport.IsPlaying) return;
                bool same = Engine.Core.ScriptValues.TryColor(hex, out var c) && Engine.Core.ScriptValues.TryColor((string?)field.Default, out var d) && c.ToArgb() == d.ToArgb();
                if (same || hex == (string?)field.Default) component.Values.Remove(field.Name);
                else component.Values[field.Name] = JsonSerializer.SerializeToElement(hex);
                if (commit) MarkDirty(); else Viewport.InvalidateUi();
            };
            row.Children.Add(At(color, 1));
            AddRow(row);
            return;
        }
        if (field.EnumNames is {} names)
        {
            var picker = new ComboBox { ItemsSource = names, SelectedItem = field.Value(component),
                HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "Select value" };
            AutomationProperties.SetName(picker, field.Name);
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is string name) Store(name); };
            Grid.SetColumn(picker, 1);
            row.Children.Add(picker);
            AddRow(row);
            return;
        }
        if (field.Type == typeof(Vector2))
        {
            var axes = new Grid { ColumnDefinitions = new("Auto,*,Auto,*"), ColumnSpacing = 3 };
            Vector2 Current() => (Vector2)field.Value(component)!;
            void Axis(string axis, int column)
            {
                var label = new TextBlock { Text = axis, Classes = { "fieldLabel" } };
                Grid.SetColumn(label,column);
                axes.Children.Add(label);
                string Coordinate() => (column == 0 ? Current().X : Current().Y).ToString("R", CultureInfo.InvariantCulture);
                var input = new TextBox { Text = Coordinate(), MinWidth = 0 };
                AutomationProperties.SetName(input, $"{field.Name} {axis}");
                input.TextChanged += (_, _) =>
                {
                    if (float.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && float.IsFinite(n))
                        Store(column == 0 ? new Vector2(n, Current().Y) : new Vector2(Current().X, n), input);
                };
                input.LostFocus += (_, _) => input.Text = Coordinate();
                Grid.SetColumn(input,column+1);
                axes.Children.Add(input);
            }
            Axis("X", 0);
            Axis("Y", 2);
            Grid.SetColumn(axes, 1);
            row.Children.Add(axes);
            AddRow(row);
            return;
        }
        string? assetExtension = field.AssetExtension;
        if (field.Type == typeof(Guid) || assetExtension != null)
        {
            var id = field.Value(component) switch { SpriteReference sprite => sprite.Id, SceneReference sc => sc.Id, PrefabReference pf => pf.Id, DataReference data => data.Id, TableReference table => table.Id, Guid guid => guid, _ => Guid.Empty };
            void Pick(Guid? picked)
            {
                var choice = picked ?? Guid.Empty;
                Store(assetExtension switch
                {
                    ".png" => new SpriteReference(choice),
                    ProjectAssets.Scene => new SceneReference(choice),
                    ProjectAssets.Prefab => new PrefabReference(choice),
                    ProjectAssets.Data => new DataReference(choice),
                    GameTable.Extension => new TableReference(choice),
                    _ => (object)choice
                });
                RefreshComponentList();
            }
            // Object fields (13.7/13.17): Project or Hierarchy drop, the searchable picker, Ping and Select.
            Control picker = assetExtension != null ? AssetField(field.Name, EndsWith(assetExtension), id, Pick) : ObjectField(field.Name, field.Accepts, id, Pick);
            Grid.SetColumn(picker, 1);
            row.Children.Add(picker);
            AddRow(row);
            return;
        }
        var box = new TextBox { Text = Format() };
        if (field.Type == typeof(string) && ui?.Lines is int lines)
        {
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.MinHeight = box.MaxHeight = 6 + 16 * Math.Clamp(lines, 1, 20);
            box.VerticalContentAlignment = VerticalAlignment.Top;
        }
        AutomationProperties.SetName(box, field.Name);
        bool valid = true;
        box.TextChanged += (_, _) =>
        {
            var text = box.Text ?? "";
            valid = true;
            if (text == Format()) return;
            if (field.Type == typeof(string)) Store(text, box);
            else if (field.Type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) Store(i, box);
            else if (field.Type == typeof(long) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) Store(l, box);
            else if (field.Type == typeof(uint) && uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint u)) Store(u, box);
            else if (field.Type == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && float.IsFinite(f)) Store(f, box);
            else if (field.Type == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && double.IsFinite(d)) Store(d, box);
            else valid = false;
        };
        box.LostFocus += (_, _) =>
        {
            if (!valid) Status.Text = $"Enter a valid number for {field.Name}.";
            if (!valid || box.Text != Format()) box.Text = Format(); // also shows a value kept inside [Range]/[Min]
        };
        Control input = box;
        if (ui is { Min: float low, Max: float high } && high > low && field.Type != typeof(string))
        {
            // [Range]: a slider beside the number box; a drag stays one pending edit (RecordEdit) recorded on release.
            double Number() => Convert.ToDouble(field.Value(component), CultureInfo.InvariantCulture);
            // The input height keeps the row on the same 24px baseline grid as the other fields (Fluent's slider is taller).
            var slider = new Slider { Minimum = low, Maximum = high, Value = Number(), VerticalAlignment = VerticalAlignment.Center,
                IsSnapToTickEnabled = field.Type == typeof(int), TickFrequency = 1, Margin = new Thickness(0), Height = EditorTokens.InputHeight, MinHeight = 0, Padding = new Thickness(0) };
            AutomationProperties.SetName(slider, field.Name + " slider");
            ToolTip.SetTip(slider, $"{tip} ({low.ToString(CultureInfo.InvariantCulture)}–{high.ToString(CultureInfo.InvariantCulture)})");
            slider.ValueChanged += (_, e) =>
            {
                if (Math.Abs(e.NewValue - Number()) < 1e-9) return;
                object value = field.Type == typeof(int) ? (int)Math.Round(e.NewValue) : field.Type == typeof(float) ? (float)e.NewValue : e.NewValue;
                Store(value, slider);
                box.Text = Format();
            };
            slider.AddHandler(PointerReleasedEvent, (_, _) => CommitEdit(), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            slider.AddHandler(PointerCaptureLostEvent, (_, _) => CommitEdit(), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            box.TextChanged += (_, _) => slider.Value = Number(); // typing or a label scrub moves the thumb
            box.LostFocus += (_, _) => slider.Value = Number();
            box.Width = 56;
            var pair = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = EditorTokens.FieldGap, Children = { slider, box } };
            Grid.SetColumn(box, 1);
            input = pair;
        }
        Grid.SetColumn(input, 1);
        row.Children.Add(input);
        AddRow(row);
    }

    private void RefreshChangedScripts()
    {
        if (Viewport.IsPlaying) { ReloadPlayParticles(); ReloadPlayScripts(); return; }
        if (busy || filePress != null) return;
        RefreshProjectFiles();
        RefreshChangedExtensions();
        if (inspectorSourceStamp != null && scripts != null)
            try
            {
                if (inspectorSourceStamp != scripts.ChangeStamp())
                {
                    Scene.NormalizeScriptFields();
                    RefreshComponentList();
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { Status.Text = ex.Message; }
    }

    private bool reloadingScripts;
    // Scene Play: a saved game script compiles off the UI thread and replaces the running code. A failed compile keeps it.
    private async void ReloadPlayScripts()
    {
        if (reloadingScripts) return;
        reloadingScripts = true;
        try
        {
            if (Viewport.ChangedPlayScripts() is not {} next || Viewport.PlaySession is not {} session) return;
            try { await Task.Run(() => next.Current()); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                WriteLog(new(GameLogLevel.Warning, "Script reload failed. Play keeps running the previous code.", null, 0));
                LogError(ex.Message);
                Status.Text = "Script reload failed. See the Console.";
                return;
            }
            if (Viewport.PlaySession != session) return;
            foreach (var (level, message) in ScriptReload.Apply(session, next)) WriteLog(new(level, message, null, 0));
            Status.Text = "Scripts reloaded.";
            RefreshComponentList();
        }
        catch (Exception ex) { LogError("Script reload failed. Play keeps running the previous code. " + ex.GetBaseException().Message); }
        finally { reloadingScripts = false; }
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
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Classes = { "error" }, IsVisible = false };
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
