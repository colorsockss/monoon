using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Automation;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private void RefreshInspector()
    {
        refreshing = true;
        RefreshLiveInspector();
        foreach (var sync in refreshPlayNumbers) sync();
        ResolveInspectorLock();
        bool asset = InspectingAsset;
        AssetInspector.IsVisible = asset;
        MultiInspector.IsVisible = !asset && lockedObject == null && Viewport.Selection.Count > 1;
        Inspector.IsVisible = !asset && Inspected != null && !MultiInspector.IsVisible;
        EmptyInspector.IsVisible = !asset && Inspected == null;
        OverridesButton.IsVisible = !asset && prefabSource == null && !Viewport.IsPlaying && InspectingOne && Inspected?.Prefab != null;
        RefreshInspectorActions();
        if (MultiInspector.IsVisible) RefreshMultiTransform();
        if (Inspector.IsVisible && Inspected is { } obj)
        {
            ObjectName.Text = obj.Name;
            ObjectActive.IsChecked = obj.Active;
            ObjectActive.IsEnabled = !Viewport.IsPlaying;
            RefreshRoleFields(obj);
            bool canvas = obj.GetComponent<UiCanvasComponent>() != null;
            bool widget = ScreenUi.IsWidget(obj);
            bool live = Viewport.IsPlaying && !canvas && !widget;
            PositionRow.IsVisible = !canvas && !live;
            PositionRowLabel.Text = ScreenUi.IsWidget(obj) ? "Offset" : obj.Parent == null ? "Position" : "Local pos.";
            SizeRow.IsVisible = obj.GetComponent<SpriteRendererComponent>() != null || obj.GetComponent<SortingGroup2DComponent>() != null || obj.GetComponent<Collider2DComponent>() != null || ScreenUi.IsWidget(obj);
            AffineRows.IsVisible = !widget && !canvas;
            RotationRow.IsVisible = ScaleRow.IsVisible = !live;
            var local = Viewport.PlayWorld is { } world && world.TryGetLocalTransform(obj.Id, out var pose)
                ? pose : new LocalTransform2D(new(obj.X, obj.Y), obj.Rotation, new(obj.ScaleX, obj.ScaleY), obj.Shear);
            ObjRotation.Value = decimal.CreateSaturating(local.Rotation);
            ObjScaleX.Value = decimal.CreateSaturating(local.Scale.X);
            ObjScaleY.Value = decimal.CreateSaturating(local.Scale.Y);
            ObjPivotX.Value = decimal.CreateSaturating(obj.PivotX);
            ObjPivotY.Value = decimal.CreateSaturating(obj.PivotY);
            PosX.Value = decimal.CreateSaturating(local.Position.X);
            PosY.Value = decimal.CreateSaturating(local.Position.Y);
            ObjWidth.Value = decimal.CreateSaturating(obj.Width);
            ObjHeight.Value = decimal.CreateSaturating(obj.Height);
        }
        refreshing = false;
    }

    private void RefreshComponentList()
    {
        componentMenu?.Hide();
        ResolveInspectorLock();
        var owner = Inspected;
        inspectorObjectFields.Clear();
        inspectorSourceStamp = null;
        RefreshMultiComponents();
        if (!InspectingOne) owner = null;
        bool debug = InspectorDebug;
        DebugSection.IsVisible = debug && owner != null;
        DebugSection.Foldout ??= new ComponentFoldout(); // opened; a fold lasts for the session
        DebugObjectRows.Children.Clear();
        if (debug && owner != null) DebugObjectRows.Children.Add(DebugRows(ObjectKeys(owner), null));
        refreshPlayNumbers.Clear();
        LiveTransform.Children.Clear();
        bool liveTransform = Viewport.PlayWorld != null && owner != null && !ScreenUi.IsWidget(owner) && owner.GetComponent<UiCanvasComponent>() == null;
        LiveTransform.IsVisible = liveTransform;
        if (liveTransform) BuildPlayTransform(Viewport.PlayWorld!, owner!);
        RefreshApplyButton();
        TransformSection.Foldout = owner == null ? null : FoldoutFor(owner, null);
        inspectorAssets = null;
        overrideRows.Clear();
        AddTransformOverrideRows();
        AttachedComponents.ItemsSource = owner?.Components.Select(c => new AttachedComponent(c, project, owner, FoldoutFor(owner, c), () =>
        {
            // The sprite's number fields are bound in XAML: group their keystrokes by the focused field.
            MarkDirty(c is Camera2DComponent ? c : (FocusManager?.GetFocusedElement() as Visual)?.FindAncestorOfType<NumericUpDown>(includeSelf: true));
            Viewport.RefreshScene();
        }, ScriptInspector, PresentationFields, !Viewport.IsPlaying && !debug, debug ? DebugFields : null, SpriteFieldsFor) { Toggled = () => { MarkDirty(); Viewport.RefreshScene(); } }).ToArray();
        warningAssets = project == null ? null : inspectorAssets ?? ProjectAssets.Scan(project);
        inspectorAssets = null;
        RefreshComponentWarnings();
        RefreshOverrides();
        inspectedShape = (owner, owner?.Components ?? default, Viewport.PlayWorld);
    }

    // What the component rows were built for, and for each object field whether it would still show the same target.
    private (SceneObject? Owner, System.Collections.Immutable.ImmutableArray<SceneComponent> Components, SceneGame? World) inspectedShape;
    private readonly List<Func<bool>> inspectorObjectFields = [];

    // True when a structural Play change leaves the Inspector's rows as they are: one selected object (or none) with the
    // same components in the same world, and object fields that would show the same names. Then only values are refreshed.
    private bool InspectorRowsCurrent(SceneObject? selected) =>
        selected == Selected && lockedObject == null && Viewport.Selection.Count == (selected == null ? 0 : 1)
        && inspectedShape.Owner == selected && inspectedShape.World == Viewport.PlayWorld
        && (selected?.Components ?? default) == inspectedShape.Components && inspectorObjectFields.All(current => current());

    // Inspector lock (13.12): the Inspector keeps showing one object while the selection changes. Edit mode only; the
    // locked object is kept per scene in the user's editor-state.json, never in the scene.
    private Guid? inspectorLock;
    private SceneObject? lockedObject;
    private SceneObject? Inspected => lockedObject ?? Selected;
    private bool InspectingOne => lockedObject != null || Viewport.Selection.Count == 1;
    private bool InspectorDebug => EditorUserState.Current.InspectorDebug && !Viewport.IsPlaying;

    private void ResolveInspectorLock() => lockedObject = inspectorLock is Guid id && !Viewport.IsPlaying ? Scene.Objects.FirstOrDefault(o => o.Id == id) : null;

    private void SetInspectorLock(bool on)
    {
        inspectorLock = on ? Inspected?.Id : null;
        var state = EditorUserState.Current;
        state.SetMarks(state.InspectorLock, MarksKey, inspectorLock is Guid id ? [id] : []);
        Status.Text = inspectorLock == null ? "Inspector follows the selection." : $"Inspector locked to {Inspected?.Name}.";
        SyncTabTools();
        RefreshInspector();
        RefreshComponentList();
    }

    private void SetInspectorDebug(bool on)
    {
        EditorUserState.Current.InspectorDebug = on;
        EditorUserState.Current.Save();
        RefreshInspector();
        RefreshComponentList();
    }

    // The tab is built again whenever the dock layout changes: the toggles read the current state and are kept for SyncTabTools.
    private ToggleButton? lockTool, debugTool;
    private bool syncingTools;

    private Control InspectorTabTools()
    {
        ToggleButton Tool(string name, string tip, Action<bool> set)
        {
            var toggle = new ToggleButton { Name = name, Classes = { "tabTool" } };
            AutomationProperties.SetName(toggle, tip);
            ToolTip.SetTip(toggle, tip);
            toggle.IsCheckedChanged += (_, _) => { if (!syncingTools) set(toggle.IsChecked == true); };
            return toggle;
        }
        lockTool = Tool("InspectorLockToggle", "Lock the Inspector to this object (edit mode)", SetInspectorLock);
        debugTool = Tool("InspectorDebugToggle", "Debug view: every saved key, read-only (edit mode)", SetInspectorDebug);
        SyncTabTools();
        return new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 2, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children = { lockTool, debugTool } };
    }

    private void SyncTabTools()
    {
        syncingTools = true;
        if (lockTool != null)
        {
            lockTool.IsChecked = inspectorLock != null;
            lockTool.Content = EditorTokens.Icon(inspectorLock != null ? "IconLock" : "IconUnlock");
        }
        if (debugTool != null)
        {
            debugTool.IsChecked = EditorUserState.Current.InspectorDebug;
            debugTool.Content = EditorTokens.Icon("IconInfo");
        }
        syncingTools = false;
    }

    // A command that changes the inspected object's components rebuilds the Inspector without moving a locked Inspector's selection.
    private void Reinspect(SceneObject owner)
    {
        if (lockedObject == owner) ShowSelection(Viewport.Selection.ToList());
        else Select(owner);
    }

    // Debug view rows: each saved key and its raw JSON value, read-only. Script fields the Inspector leaves out (private,
    // static, runtime-initialized) follow with their declared initializer.
    private static System.Text.Json.JsonElement ObjectKeys(SceneObject owner)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(ProjectYaml.Decode<System.Text.Json.JsonElement>(ProjectYaml.Encode(owner)).GetRawText())!.AsObject();
        node.Remove("components");
        return System.Text.Json.JsonSerializer.SerializeToElement(node, GameJson.Default.JsonNode);
    }

    private Control DebugFields(SceneComponent component)
    {
        var json = ProjectYaml.Decode<System.Text.Json.JsonElement>(ProjectYaml.Encode<SceneComponent>(component));
        IEnumerable<ScriptFieldNote>? notes = null;
        if (component is ScriptComponent script && scripts != null)
            try { notes = scripts.Inspector(script.Script)?.Skipped; }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { }
        return DebugRows(json, notes);
    }

    private static Control DebugRows(System.Text.Json.JsonElement node, IEnumerable<ScriptFieldNote>? notes)
    {
        var panel = new StackPanel { Spacing = 2, Classes = { "debugFields" } };
        void Row(string key, string value, string tip, bool muted)
        {
            var text = new SelectableTextBlock { Text = value, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontFamily = new Avalonia.Media.FontFamily("Menlo, Consolas, monospace"), Classes = { muted ? "label" : "debugValue" } };
            ToolTip.SetTip(text, tip);
            AutomationProperties.SetName(text, key);
            InspectorRow(panel, key, text);
        }
        foreach (var property in node.EnumerateObject())
        {
            var value = property.Value.ValueKind == System.Text.Json.JsonValueKind.String ? property.Value.GetString()! : property.Value.GetRawText();
            Row(property.Name, value, property.Value.GetRawText(), false);
        }
        foreach (var note in notes ?? [])
            Row(note.Name, note.Initializer ?? "(default)", $"{note.Type} · not saved: {note.Reason}", true);
        return panel;
    }

    // Component warnings (13.14): problems Play or Save would hit, shown while editing. Recomputed for the inspected object
    // after every edit (RecordEdit) and Inspector rebuild, so an Undo that fixes the problem also clears the line.
    private ProjectAssets? warningAssets;

    private void RefreshComponentWarnings()
    {
        bool noCamera = !Viewport.IsPlaying && prefabSource == null && Scene.Camera == null && !Scene.IsUiOnly;
        SceneWarning.Text = noCamera ? "No start camera: Play needs one (Scene → Create Camera, or a primary Camera 2D)." : null;
        SceneWarning.IsVisible = noCamera;
        if (AttachedComponents.ItemsSource is not AttachedComponent[] rows) return;
        foreach (var row in rows) row.Warning = Viewport.IsPlaying ? null : ComponentWarning(row.Owner, row.Component);
    }

    private string? ComponentWarning(SceneObject owner, SceneComponent component)
    {
        try { SceneDocument.ValidateComponent(owner, component); }
        catch (InvalidDataException ex) { return "Save will fail: " + ex.Message; }
        if (component is Rigidbody2DComponent && !Scene.Objects.Any(o => o.IsWithin(owner) && o.GetComponent<Collider2DComponent>() != null))
            return "Rigidbody 2D without a collider on this object or its children: it does not collide.";
        if (component is ScriptComponent || project is not { } root || warningAssets is not { } assets) return null;
        if (component is SpriteRendererComponent { Asset: { } png } sprite && !File.Exists(Path.Combine(root, png)) && assets.Sprites(root).Find(sprite.AssetId) == null)
            return $"Missing sprite: {png}";
        foreach (var property in component.GetType().GetProperties())
        {
            if (property.PropertyType != typeof(Guid?) || property.Name == nameof(SpriteRendererComponent.AssetId) ||
                property.GetValue(component) is not Guid id || id == Guid.Empty) continue;
            if (assets.PathFor(id) is { } path ? File.Exists(Path.Combine(root, path)) : Scene.Objects.Any(o => o.Id == id) || assets.Sprites(root).Find(id) != null) continue;
            return $"Missing {property.Name}: {assets.PathFor(id) ?? id.ToString()}";
        }
        return null;
    }

    // Asset pickers built in one Inspector rebuild share one project scan.
    private ProjectAssets? inspectorAssets;
    private ProjectAssets InspectorAssets(string root) => inspectorAssets ??= ProjectAssets.Scan(root);

    private void SetStartCamera(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Inspected?.GetComponent<Camera2DComponent>() == null || Viewport.IsPlaying) return;
        Scene.DefaultCameraId = Inspected.Id;
        MarkDirty();
        RefreshComponentList();
        Viewport.RefreshScene();
    }

    private ComponentFoldout FoldoutFor(SceneObject owner, SceneComponent? component)
    {
        var key = component switch { null => "Transform", ScriptComponent script => "Script:" + script.Script, _ => component.GetType().Name };
        if (!foldouts.TryGetValue(key, out var foldout)) foldouts.Add(key, foldout = new(key));
        return foldout;
    }

    private static bool HasScript(SceneObject owner, string name) => owner.Components.OfType<ScriptComponent>()
        .Any(script => StringComparer.OrdinalIgnoreCase.Equals(script.Script, name));

    private bool AttachComponent(SceneObject owner, SceneComponent component)
    {
        if (Viewport.IsPlaying || Inspected != owner || !owner.AddComponent(component)) return false;
        if (component is UiImageComponent or UiTextComponent or UiControlComponent or UiLayoutComponent or UiScrollComponent or UiTextFieldComponent) owner.AddComponent(new UiRectComponent());
        Reinspect(owner);
        AddComponentButton.Focus();
        MarkDirty();
        return true;
    }

    private Flyout? componentMenu;
    // Copy Component: the component's YAML entry, for Paste Component Values / Paste Component as New in this editor session.
    private string? copiedComponent;

    // Add Component: a search box over the built-in components in groups and the project's scripts. Rows share one
    // icon column and trim long names, so a wider or narrower menu keeps them aligned.
    private void ShowComponentMenu()
    {
        if (busy || Viewport.IsPlaying || Inspected is not { } owner) return;
        var search = new TextBox { Classes = { "toolbarSearch" }, Watermark = "Search components…" };
        AutomationProperties.SetName(search, "Search components");
        var list = new StackPanel { Spacing = 1 };
        var items = new List<(Button Item, string Name, TextBlock Group)>();
        var menu = new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        TextBlock Group(string name)
        {
            var header = new TextBlock { Text = name, Classes = { "label", "componentGroup" }, FontWeight = Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(4, 6, 0, 2) };
            list.Children.Add(header);
            return header;
        }
        void Item(TextBlock group, string name, Avalonia.Media.Geometry? icon, bool attached, Action pick)
        {
            var row = new Grid { ColumnDefinitions = new("16,*"), ColumnSpacing = EditorTokens.FieldGap };
            if (icon != null) row.Children.Add(new PathIcon { Theme = (Avalonia.Styling.ControlTheme)this.FindResource("EditorIcon")!, Data = icon, Foreground = EditorTokens.Brush("EditorMuted") });
            // Choices read as menu items (text colour); ones already attached are muted.
            var label = new TextBlock { Text = attached ? $"{name} (Added)" : name, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Foreground = EditorTokens.Brush(attached ? "EditorMuted" : "EditorText"),
                Opacity = attached ? 0.6 : 1 };
            Grid.SetColumn(label, 1);
            row.Children.Add(label);
            var button = new Button { Content = row, Classes = { "quiet", "componentChoice" }, IsEnabled = !attached, Padding = new Thickness(4, 2),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
            AutomationProperties.SetName(button, name);
            ToolTip.SetTip(button, name);
            button.Click += (_, _) => { menu.Hide(); if (Inspected == owner) pick(); };
            list.Children.Add(button);
            items.Add((button, name, group));
        }
        bool Attached(ComponentChoice choice) => owner.Components.Any(c => c.GetType() == choice.Type ||
            c is Collider2DComponent && typeof(Collider2DComponent).IsAssignableFrom(choice.Type) ||
            c is UiControlComponent && typeof(UiControlComponent).IsAssignableFrom(choice.Type));
        foreach (var category in ComponentChoice.Categories[..^1])
        {
            var group = Group(category);
            foreach (var choice in ComponentChoice.Registered.Values.Where(c => c.Category == category).OrderBy(c => c.Name, StringComparer.Ordinal))
                Item(group, choice.Name, EditorTokens.ComponentIcon(choice.Create()), Attached(choice), () => AttachComponent(owner, choice.Create()));
        }
        var scriptGroup = Group("Scripts");
        var scriptIcon = EditorTokens.Geometry("IconScript");
        foreach (var name in AttachableScripts())
            Item(scriptGroup, name, scriptIcon, HasScript(owner, name), () => AttachComponent(owner, new ScriptComponent { Script = name }));
        Item(scriptGroup, "New C# Component…", null, false, () => RunAction(() => CreateScript(owner)));
        void Filter()
        {
            var query = search.Text?.Trim() ?? "";
            foreach (var (item, name, _) in items) item.IsVisible = query.Length == 0 || name.Contains(query, StringComparison.OrdinalIgnoreCase);
            foreach (var group in items.Select(i => i.Group).Distinct()) group.IsVisible = items.Any(i => i.Group == group && i.Item.IsVisible);
        }
        search.TextChanged += (_, _) => Filter();
        search.KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Enter || items.FirstOrDefault(i => i.Item.IsVisible && i.Item.IsEnabled).Item is not { } first) return;
            e.Handled = true;
            first.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, first));
        };
        menu.Content = new StackPanel
        {
            Spacing = 4, Width = Math.Max(AddComponentButton.Bounds.Width, 220),
            Children = { search, new ScrollViewer { MaxHeight = 360, Content = list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } }
        };
        componentMenu = menu;
        menu.Closed += (_, _) => { if (componentMenu == menu) componentMenu = null; };
        menu.Opened += (_, _) => search.Focus();
        menu.ShowAt(AddComponentButton);
    }

    // Gear menu: each command is one Undo record (Copy Component changes nothing). The last menu stays reachable for probes.
    private MenuFlyout? settingsMenu;
    private void ComponentSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (busy || Viewport.IsPlaying || sender is not Button button) return;
        var row = button.DataContext as AttachedComponent;
        var menu = settingsMenu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        bool Current() => row != null && !Viewport.IsPlaying && Inspected == row.Owner &&
            row.Owner.Components.Contains(row.Component);
        void Add(string header, bool enabled, Func<SceneObject, SceneComponent, bool> edit)
        {
            var item = new MenuItem { Header = header, IsEnabled = row != null && enabled };
            item.Click += (_, _) =>
            {
                if (!Current()) return;
                bool resetCamera = row!.Owner == Scene.Camera && row.Component is Camera2DComponent;
                if (!edit(row.Owner, row.Component)) return;
                Reinspect(row.Owner);
                AddComponentButton.Focus();
                MarkDirty();
                Viewport.RefreshScene();
                if (resetCamera) ReportStartCamera();
            };
            menu.Items.Add(item);
        }
        if (row?.Component is ScriptComponent script)
        {
            var edit = new MenuItem { Header = "Edit Script" };
            edit.Click += (_, _) => { if (Current()) RunAction(() => OpenProjectSource(scripts!.RelativePath(script.Script))); };
            menu.Items.Add(edit);
            menu.Items.Add(new Separator());
        }
        Add("Reset", true, ComponentEdits.Reset);
        menu.Items.Add(new Separator());
        var copy = new MenuItem { Header = "Copy Component", IsEnabled = row != null };
        copy.Click += (_, _) =>
        {
            if (!Current()) return;
            copiedComponent = ComponentEdits.Copy(row!.Component);
            Status.Text = $"Copied {row.Name}";
        };
        menu.Items.Add(copy);
        Add("Paste Component Values", row != null && ComponentEdits.CanPasteValues(row.Component, copiedComponent),
            (owner, component) => ComponentEdits.PasteValues(owner, component, copiedComponent!));
        var pasted = ComponentEdits.Read(copiedComponent);
        Add("Paste Component as New", pasted != null && row != null && row.Owner.Components.All(c => !StringComparer.OrdinalIgnoreCase.Equals(c.Kind, pasted.Kind)),
            (owner, _) => ComponentEdits.Read(copiedComponent) is { } fresh && AttachNew(owner, fresh));
        menu.Items.Add(new Separator());
        int index = row == null ? -1 : row.Owner.Components.IndexOf(row.Component);
        Add("Move Up", index > 0, (owner, component) => ComponentEdits.Move(owner, component, -1));
        Add("Move Down", row != null && index < row.Owner.Components.Length - 1, (owner, component) => ComponentEdits.Move(owner, component, 1));
        menu.Items.Add(new Separator());
        Add("Remove", true, (owner, component) => owner.RemoveComponent(component));
        menu.ShowAt(button);
    }

    // Paste as New adds the UI rect a UI component needs, as Add Component does.
    private static bool AttachNew(SceneObject owner, SceneComponent component)
    {
        if (!owner.AddComponent(component)) return false;
        if (component is UiImageComponent or UiTextComponent or UiControlComponent or UiLayoutComponent or UiScrollComponent or UiTextFieldComponent)
            owner.AddComponent(new UiRectComponent());
        return true;
    }

    private void CommitText()
    {
        CommitEdit();
        if (refreshing || Viewport.IsPlaying || !InspectingOne || Inspected is not { } obj) return;
        var name = ObjectName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            RefreshInspector();
            Status.Text = "Enter an object name.";
            return;
        }
        if (obj.Name == name) return;
        obj.Name = name;
        RebuildHierarchy(obj);
        MarkDirty();
    }

    // One Undo record; the Hierarchy dims the object and its children, the Scene view stops drawing them.
    private void SetObjectActive(bool active)
    {
        if (refreshing || Viewport.IsPlaying || !InspectingOne || Inspected is not { } obj || obj.Active == active) return;
        obj.Active = active;
        MarkDirty();
        RebuildHierarchy(Selected);
        Viewport.RefreshScene();
    }

    private void ChangeBounds(NumericUpDown field)
    {
        if (refreshing || Viewport.IsPlaying || !InspectingOne || Inspected is not { } obj || field.Value is not { } number) return;
        var value = (float)number;
        if (field == ObjWidth || field == ObjHeight)
            Scene.Resize(obj, field == ObjWidth ? value : obj.Width, field == ObjHeight ? value : obj.Height);
        else if (field == PosX) obj.X = value;
        else if (field == PosY) obj.Y = value;
        else if (field == ObjRotation) obj.Rotation = value;
        else if (field == ObjPivotX) obj.PivotX = value;
        else if (field == ObjPivotY) obj.PivotY = value;
        else if (field == ObjScaleX || field == ObjScaleY)
        {
            if (value == 0) { RefreshInspector(); Status.Text = "Scale must be nonzero."; return; }
            if (field == ObjScaleX) obj.ScaleX = value; else obj.ScaleY = value;
        }
        MarkDirty(field);
        RefreshInspector();
        Viewport.RefreshScene();
    }
}
