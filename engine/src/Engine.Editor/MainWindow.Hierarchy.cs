using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private readonly Dictionary<SceneObject, TreeViewItem> treeItems = [];
    private SceneObject? pressedNode;
    private Point pressPoint;
    private bool draggingNode;
    private TreeViewItem? dropTarget;

    private void InitializeHierarchy()
    {
        Hierarchy.SelectionChanged += (_, _) =>
        {
            if (!refreshing) Select((Hierarchy.SelectedItem as TreeViewItem)?.Tag as SceneObject);
        };
        Hierarchy.ContextRequested += HierarchyContextRequested;
        Hierarchy.AddHandler(PointerPressedEvent, HierarchyPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        Hierarchy.AddHandler(PointerMovedEvent, HierarchyMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        Hierarchy.AddHandler(PointerReleasedEvent, HierarchyReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        Hierarchy.PointerCaptureLost += (_, e) => { if (e.Source == Hierarchy) EndHierarchyDrag(); };
    }

    private void RebuildHierarchy(SceneObject? selected)
    {
        refreshing = true;
        var collapsed = treeItems.Where(pair => pair.Value.Items.Count > 0 && !pair.Value.IsExpanded)
            .Select(pair => pair.Key.Id).ToHashSet();
        var objects = Viewport.PlayWorld?.Objects ?? Scene.Objects;
        Hierarchy.Items.Clear();
        treeItems.Clear();
        foreach (var obj in objects)
        {
            var item = new TreeViewItem { Header = obj.Name, Tag = obj, IsExpanded = !collapsed.Contains(obj.Id),
                Opacity = Viewport.PlayWorld?.IsActive(obj.Id) == false ? 0.55 : 1 };
            AutomationProperties.SetName(item, obj.Name);
            treeItems.Add(obj, item);
        }
        foreach (var obj in objects)
        {
            var items = obj.Parent == null ? Hierarchy.Items : treeItems[obj.Parent].Items;
            items.Add(treeItems[obj]);
        }
        refreshing = false;
        Select(selected);
    }

    private void SelectHierarchyItem(SceneObject? obj)
    {
        for (var parent = obj?.Parent; parent != null; parent = parent.Parent) treeItems[parent].IsExpanded = true;
        Hierarchy.SelectedItem = obj != null ? treeItems[obj] : null;
    }

    private static string ObjectPath(SceneObject obj)
    {
        var names = new Stack<string>();
        for (var node = obj; node != null; node = node.Parent) names.Push(node.Name);
        return string.Join(" / ", names);
    }

    private void ChangeParent(SceneObject obj, SceneObject? parent)
    {
        if (Viewport.IsPlaying || !Scene.Reparent(obj, parent)) return;
        RebuildHierarchy(obj);
        MarkDirty();
    }

    private static TreeViewItem? TreeItemAt(Visual? visual) =>
        visual as TreeViewItem ?? visual?.FindAncestorOfType<TreeViewItem>();

    private static bool IsDeleteObjectShortcut(KeyEventArgs e) => OperatingSystem.IsMacOS()
        ? e.KeyModifiers == KeyModifiers.Meta && e.Key is Key.Back or Key.Delete
        : e.KeyModifiers == KeyModifiers.None && e.Key == Key.Delete;

    private static readonly KeyGesture DuplicateGesture = new(Key.D, OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);

    private Task DuplicateObject()
    {
        if (Selected is { } source)
        {
            var copy = Scene.DuplicateObject(source, scripts);
            FinishObjectChange(copy);
            FocusHierarchySelection();
        }
        return Task.CompletedTask;
    }

    private void FocusHierarchySelection()
    {
        // Rebuilt rows join the visual tree after layout, and a context menu restores focus as it closes.
        var selected = Selected;
        Dispatcher.UIThread.Post(() =>
        {
            if (selected == Selected && selected != null && treeItems.TryGetValue(selected, out var item))
            {
                item.BringIntoView();
                item.Focus();
            }
        }, DispatcherPriority.Loaded);
    }

    private void HierarchyContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (busy || Viewport.IsPlaying) return;
        bool atPointer = e.TryGetPosition(Hierarchy, out var point);
        var item = atPointer ? TreeItemAt(Hierarchy.InputHitTest(point) as Visual) : Hierarchy.SelectedItem as TreeViewItem;
        var parent = item?.Tag as SceneObject;
        item?.Focus();
        if (Selected != parent) Select(parent);

        var empty = new MenuItem { Header = parent == null ? "Create Empty Object" : "Create Empty Child" };
        var square = new MenuItem { Header = parent == null ? "Add Square" : "Add Child Square" };
        empty.Click += (_, _) => AddObject(parent, square: false);
        square.Click += (_, _) => AddObject(parent);
        var menu = new ContextMenu();
        menu.Items.Add(empty);
        menu.Items.Add(square);
        var tilemap = new MenuItem { Header="Create Tilemap" };
        tilemap.Click += (_,_)=>CreateTilemap(parent);
        menu.Items.Add(tilemap);
        var ui = new MenuItem { Header = "UI" };
        foreach (var kind in new[] { "Canvas", "Group", "Layout", "Scroll View", "Text", "Text Field", "Image", "Button" })
        {
            var option = new MenuItem { Header = kind };
            option.Click += (_, _) =>
            {
                var host = parent;
                if (kind != "Canvas" && (host == null || !ScreenUi.IsWidget(host) && host.GetComponent<UiCanvasComponent>() == null))
                {
                    host = Scene.Objects.FirstOrDefault(o => o.GetComponent<UiCanvasComponent>() != null)
                        ?? Scene.CreateUi("Canvas", null);
                }
                var created = Scene.CreateUi(kind, kind == "Canvas" ? null : host);
                FinishObjectChange(created);
                FocusHierarchySelection();
            };
            ui.Items.Add(option);
        }
        menu.Items.Add(ui);
        if (parent != null)
        {
            var duplicate = new MenuItem
            {
                Header = "Duplicate", InputGesture = DuplicateGesture
            };
            void Option(string title,Func<Task> action)
            {
                var command=new MenuItem {Header=title};
                command.Click+=(_,_)=>RunAction(action);menu.Items.Add(command);
            }
            if(project!=null){
                Option("Create Prefab",()=>SavePrefab(false));
                if(parent.Prefab!=null){
                    Option("Inspect Overrides…",ShowPrefabOverrides);
                    Option("Create Prefab Variant",()=>SavePrefab(true));
                    Option("Apply to Prefab",ApplyPrefab);
                    Option("Revert Prefab Overrides",RevertPrefab);
                }
            }
            duplicate.Click += (_, _) => { if (Selected == parent) RunAction(DuplicateObject); };
            var remove = new MenuItem
            {
                Header = "Delete Object",
                InputGesture = OperatingSystem.IsMacOS() ? new KeyGesture(Key.Back, KeyModifiers.Meta) : new KeyGesture(Key.Delete)
            };
            remove.Click += (_, _) => { if (Selected == parent) DeleteObject(); };
            menu.Items.Add(new Separator());
            menu.Items.Add(duplicate);
            menu.Items.Add(remove);
        }
        menu.Placement = atPointer ? PlacementMode.Pointer : PlacementMode.BottomEdgeAlignedLeft;
        menu.Open((Control?)item ?? Hierarchy);
        e.Handled = true;
    }

    private void HierarchyPressed(object? sender, PointerPressedEventArgs e)
    {
        pressedNode = null;
        if (Viewport.IsPlaying || !e.GetCurrentPoint(Hierarchy).Properties.IsLeftButtonPressed ||
            e.Source is not Visual visual || visual is ToggleButton || visual.FindAncestorOfType<ToggleButton>() != null) return;
        if (TreeItemAt(visual) is not { Tag: SceneObject obj } item) return;
        if ((e.KeyModifiers & (KeyModifiers.Shift|KeyModifiers.Control|KeyModifiers.Meta))!=0)
        {
            ToggleSelection(obj);e.Handled=true;return;
        }
        item.Focus();
        Select(obj);
        pressedNode = obj;
        pressPoint = e.GetPosition(Hierarchy);
        e.Pointer.Capture(Hierarchy);
        e.Handled = true;
    }

    private void HierarchyMoved(object? sender, PointerEventArgs e)
    {
        if (pressedNode == null || !e.GetCurrentPoint(Hierarchy).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(Hierarchy);
        if (!draggingNode && ((Vector)(point - pressPoint)).Length < 5) return;
        draggingNode = true;
        var item = TreeItemAt(Hierarchy.InputHitTest(point) as Visual);
        HighlightTarget(item?.Tag is SceneObject target && !target.IsWithin(pressedNode) ? item : null);
        bool allowed = new Rect(Hierarchy.Bounds.Size).Contains(point) && (item == null || dropTarget != null);
        bool export = project != null && new Rect(ProjectBrowser.Bounds.Size).Contains(e.GetPosition(ProjectBrowser));
        Hierarchy.Cursor = export ? CopyFileCursor : allowed ? new Cursor(StandardCursorType.DragMove) : NoDropCursor;
        e.Handled = true;
    }

    private void HierarchyReleased(object? sender, PointerReleasedEventArgs e)
    {
        var source = pressedNode;
        var point = e.GetPosition(Hierarchy);
        bool moved = draggingNode || ((Vector)(point - pressPoint)).Length >= 5;
        bool drop = source != null && moved &&
            new Rect(Hierarchy.Bounds.Size).Contains(point);
        var target = TreeItemAt(Hierarchy.InputHitTest(point) as Visual)?.Tag as SceneObject;
        bool export = source != null && moved && project != null && new Rect(ProjectBrowser.Bounds.Size).Contains(e.GetPosition(ProjectBrowser));
        EndHierarchyDrag();
        if (e.Pointer.Captured == Hierarchy) e.Pointer.Capture(null);
        if (export)
        {
            RunAction(() =>
            {
                var folder = Directory.CreateDirectory(Path.Combine(project!, browserFolder));
                var name = string.Concat(source!.Name.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
                var path = Path.Combine(folder.FullName, name + ProjectAssets.Prefab);
                if(File.Exists(path))throw new IOException("A prefab with that name already exists.");
                WritePrefabEdit(path,source,false,false);
                return Task.CompletedTask;
            });
            e.Handled = true;
            return;
        }
        if (!drop) return;
        ChangeParent(source!, target);
        e.Handled = true;
    }

    private void HighlightTarget(TreeViewItem? item)
    {
        if (item == dropTarget) return;
        dropTarget?.Classes.Remove("dropTarget");
        dropTarget = item;
        item?.Classes.Add("dropTarget");
    }

    private void EndHierarchyDrag()
    {
        pressedNode = null;
        draggingNode = false;
        HighlightTarget(null);
        Hierarchy.Cursor = null;
    }
}
