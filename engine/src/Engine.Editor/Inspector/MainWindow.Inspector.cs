using Avalonia.Controls;
using Avalonia.Interactivity;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private void RefreshInspector()
    {
        refreshing = true;
        RefreshLiveInspector();
        foreach (var sync in refreshPlayNumbers) sync();
        MultiInspector.IsVisible = Viewport.Selection.Count > 1;
        Inspector.IsVisible = Selected != null && !MultiInspector.IsVisible;
        EmptyInspector.IsVisible = Selected == null;
        OverridesButton.IsVisible = prefabSource == null && !Viewport.IsPlaying && Viewport.Selection.Count == 1 && Selected?.Prefab != null;
        RefreshInspectorActions();
        if (MultiInspector.IsVisible) RefreshMultiTransform();
        if (Inspector.IsVisible && Selected is { } obj)
        {
            ObjectName.Text = obj.Name;
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
        var owner = Selected;
        inspectorSourceStamp = null;
        RefreshMultiComponents();
        if (Viewport.Selection.Count > 1) owner = null;
        refreshPlayNumbers.Clear();
        LiveTransform.Children.Clear();
        bool liveTransform = Viewport.PlayWorld != null && owner != null && !ScreenUi.IsWidget(owner) && owner.GetComponent<UiCanvasComponent>() == null;
        LiveTransform.IsVisible = liveTransform;
        if (liveTransform) BuildPlayTransform(Viewport.PlayWorld!, owner!);
        RefreshApplyButton();
        TransformSection.Foldout = owner == null ? null : FoldoutFor(owner, null);
        AttachedComponents.ItemsSource = owner?.Components.Select(c => new AttachedComponent(c, project, owner, FoldoutFor(owner, c), () =>
        {
            MarkDirty(c is Camera2DComponent ? c : null);
            Viewport.RefreshScene();
        }, ScriptInspector, PresentationFields, !Viewport.IsPlaying)).ToArray();
    }

    private void SetStartCamera(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Selected?.GetComponent<Camera2DComponent>() == null || Viewport.IsPlaying) return;
        Scene.DefaultCameraId = Selected.Id;
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
        if (Viewport.IsPlaying || Selected != owner || !owner.AddComponent(component)) return false;
        if (component is UiImageComponent or UiTextComponent or UiControlComponent or UiLayoutComponent or UiScrollComponent or UiTextFieldComponent) owner.AddComponent(new UiRectComponent());
        Select(owner);
        AddComponentButton.Focus();
        MarkDirty();
        return true;
    }

    private MenuFlyout? componentMenu;

    private void ShowComponentMenu()
    {
        if (busy || Viewport.IsPlaying || Selected is not { } owner) return;
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        var builtIns = new MenuItem { Header = "Built-in components" };
        foreach (var choice in ComponentChoice.Registered.Values.OrderBy(c => c.Name, StringComparer.Ordinal))
            AddChoice(builtIns, choice.Name, owner.Components.Any(c => c.GetType() == choice.Type || c is Collider2DComponent && typeof(Collider2DComponent).IsAssignableFrom(choice.Type) ||
                c is UiControlComponent && typeof(UiControlComponent).IsAssignableFrom(choice.Type)), choice.Create);

        var projectScripts = new MenuItem { Header = "Project scripts" };
        foreach (var name in AttachableScripts())
            AddChoice(projectScripts, name, HasScript(owner, name), () => new ScriptComponent { Script = name });
        projectScripts.IsEnabled = projectScripts.Items.Count > 0;

        var create = new MenuItem { Header = "New C# Component…" };
        create.Click += (_, _) => { if (Selected == owner) RunAction(() => CreateScript(owner)); };
        menu.Items.Add(builtIns);
        menu.Items.Add(projectScripts);
        menu.Items.Add(new Separator());
        menu.Items.Add(create);
        componentMenu = menu;
        menu.Closed += (_, _) => { if (componentMenu == menu) componentMenu = null; };
        menu.ShowAt(AddComponentButton);

        void AddChoice(MenuItem group, string name, bool attached, Func<SceneComponent> make)
        {
            var item = new MenuItem { Header = attached ? $"{name} (Added)" : name, IsEnabled = !attached };
            item.Click += (_, _) => AttachComponent(owner, make());
            group.Items.Add(item);
        }
    }

    private void ComponentSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (busy || Viewport.IsPlaying || sender is not Button button) return;
        var row = button.DataContext as AttachedComponent;
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        bool Current() => row != null && !Viewport.IsPlaying && Selected == row.Owner &&
            row.Owner.Components.Contains(row.Component);
        if (row?.Component is ScriptComponent script)
        {
            var edit = new MenuItem { Header = "Edit Script" };
            edit.Click += (_, _) => { if (Current()) RunAction(() => OpenProjectSource(scripts!.RelativePath(script.Script))); };
            menu.Items.Add(edit);
        }
        var remove = new MenuItem { Header = "Remove", IsEnabled = row != null };
        remove.Click += (_, _) =>
        {
            if (!Current()) return;
            bool resetCamera = row!.Owner == Scene.Camera && row.Component is Camera2DComponent;
            if (!row.Owner.RemoveComponent(row.Component)) return;
            Select(row.Owner);
            AddComponentButton.Focus();
            MarkDirty();
            if (resetCamera) ReportStartCamera();
        };
        menu.Items.Add(remove);
        menu.ShowAt(button);
    }

    private void CommitText()
    {
        if (refreshing || Viewport.IsPlaying || Viewport.Selection.Count != 1 || Selected is not { } obj) return;
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

    private void ChangeBounds(NumericUpDown field)
    {
        if (refreshing || Viewport.IsPlaying || Viewport.Selection.Count != 1 || Selected is not { } obj || field.Value is not { } number) return;
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
