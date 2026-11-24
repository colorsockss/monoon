using Avalonia.Controls;
using Avalonia.Media;
using Engine.Core;

namespace Engine.Editor;

// Prefab override inline display (roadmap 13.6). The inspected object's overrides come from Scene.PropertyOverrides once
// per Inspector rebuild and once per committed edit. Overridden components get a header dot; Transform, name and script
// field rows get a bold label and a 2px bar drawn in the panel padding (no column moves). Right-click such a row for
// Revert or Apply to Source of just its fields, the same commands the Overrides window runs.
public partial class MainWindow
{
    private SceneObject? overrideRoot;
    private PropertyOverride[] inspectorOverrides = [];
    private readonly List<(Panel Row, PrefabProperty[] Properties)> overrideRows = [];

    // The prefab instance root whose link maps this object (the object itself for a root).
    private static SceneObject? PrefabRootOf(SceneObject obj)
    {
        for (var o = obj; o != null; o = o.Parent)
            if (o.Prefab?.Instances.ContainsValue(obj.Id) == true) return o;
        return null;
    }

    private static PrefabProperty[] Fields(params string[] names) => names.Select(n => new PrefabProperty(null, null, n)).ToArray();

    // The fixed rows; script rows add themselves while the Inspector is built.
    private void AddTransformOverrideRows()
    {
        overrideRows.Add(((Panel)ObjectName.Parent!, Fields("name")));
        overrideRows.Add((PositionRow, Fields("x", "y")));
        overrideRows.Add((SizeRow, Fields("width", "height")));
        overrideRows.Add((RotationRow, Fields("rotation")));
        overrideRows.Add((ScaleRow, Fields("scaleX", "scaleY", "shear")));
        overrideRows.Add(((Panel)ObjPivotX.Parent!.Parent!, Fields("pivotX", "pivotY")));
    }

    private void RefreshOverrides()
    {
        var obj = InspectingOne && prefabSource == null && !Viewport.IsPlaying ? Inspected : null;
        overrideRoot = obj == null ? null : PrefabRootOf(obj);
        inspectorOverrides = overrideRoot?.Prefab is { } link && obj != null
            ? Scene.PropertyOverrides(overrideRoot).Where(c => link.Instances.GetValueOrDefault(c.SourceId) == obj.Id).ToArray() : [];
        foreach (var (row, properties) in overrideRows)
        {
            var changes = inspectorOverrides.Where(c => properties.Contains(c.Property)).ToArray();
            MarkOverride(row, changes.Length > 0);
            row.ContextMenu = changes.Length > 0 ? OverrideMenu(changes) : null;
        }
        TransformSection.IsOverridden = inspectorOverrides.Any(c => c.Property.Component == null && c.Property.Field is not ("name" or "tag" or "groups"));
        if (AttachedComponents.ItemsSource is AttachedComponent[] sections)
            foreach (var section in sections) section.Overridden = inspectorOverrides.Any(c => c.Property.Component == MultiEdits.Key(section.Component));
    }

    private static void MarkOverride(Panel row, bool on)
    {
        var bar = row.Children.OfType<Border>().FirstOrDefault(b => b.Classes.Contains("overrideBar"));
        if (bar == null)
        {
            if (!on) return;
            bar = new Border { Classes = { "overrideBar" } };
            row.Children.Add(bar);
        }
        bar.IsVisible = on;
        // The row's label (a bool row's check text) turns bold; a bold label keeps its column width and baseline.
        Control? label = row.Children.OfType<TextBlock>().FirstOrDefault(t => Grid.GetColumn(t) == 0) ?? (Control?)row.Children.OfType<CheckBox>().FirstOrDefault();
        if (label == null) return;
        if (on) label.SetValue(TextBlock.FontWeightProperty, FontWeight.Bold);
        else label.ClearValue(TextBlock.FontWeightProperty);
    }

    private ContextMenu OverrideMenu(PropertyOverride[] changes)
    {
        var menu = new ContextMenu();
        string what = string.Join(", ", changes.Select(c => c.Property.Label));
        void Item(string header, bool apply)
        {
            var item = new MenuItem { Header = header };
            ToolTip.SetTip(item, what);
            item.Click += (_, _) => RunAction(() => { EditOverrides(changes, apply); return Task.CompletedTask; });
            menu.Items.Add(item);
        }
        Item("Revert", false);
        Item("Apply to Source", true);
        return menu;
    }

    // One Undo record: Revert changes the instance; Apply writes the prefab source too (one file edit with the scene).
    private void EditOverrides(PropertyOverride[] changes, bool apply)
    {
        if (Viewport.IsPlaying || overrideRoot is not { Prefab: { } link } root || project == null || Inspected is not { } obj) return;
        Guid rootId = root.Id, selected = obj.Id;
        SceneObject Root() => Scene.Objects.Single(o => o.Id == rootId);
        try
        {
            if (apply)
            {
                var relative = ProjectAssets.Scan(project).PathFor(link.AssetId) ?? throw new IOException("Missing prefab source.");
                RecordPrefabAssetChange(Path.Combine(project, relative), () => { foreach (var change in changes) Scene.EditPrefabProperty(Root(), change, true); });
            }
            else
            {
                foreach (var change in changes) Scene.EditPrefabProperty(Root(), change, false);
                FinishObjectChange(Scene.Objects.Single(o => o.Id == selected));
            }
            Status.Text = $"{(apply ? "Applied" : "Reverted")} {string.Join(", ", changes.Select(c => c.Property.Label))}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException)
        {
            LogError(ex.Message);
            Status.Text = ex.Message;
            Reinspect(Scene.Objects.FirstOrDefault(o => o.Id == selected) ?? Root());
        }
    }
}
