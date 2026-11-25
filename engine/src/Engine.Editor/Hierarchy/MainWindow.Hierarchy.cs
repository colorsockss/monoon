using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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
    // One object's place in the Hierarchy. The ListBox lists only the rows under expanded, matching parents and realizes
    // containers just for the rows on screen, so a 10,000-object scene costs about one screen of rows (a TreeView made all).
    private sealed class HierarchyNode(SceneObject obj)
    {
        public SceneObject Obj { get; } = obj;
        public HierarchyNode? Parent { get; set; }
        public List<HierarchyNode> Children { get; } = [];
        public int Depth => Parent == null ? 0 : Parent.Depth + 1;
        public bool IsExpanded { get; set; } = true;
        public bool Shown { get; set; } = true;
        public double Opacity { get; set; } = 1;
        public bool Listed => Shown && (Parent == null || Parent.IsExpanded && Parent.Listed);
    }

    private readonly Dictionary<SceneObject, HierarchyNode> treeItems = [];
    private readonly List<HierarchyNode> hierarchyRoots = [];
    private List<HierarchyNode> hierarchyRows = [];
    private (SceneObject Obj, SceneObject? Parent, string Name)[] hierarchyShape = [];
    private SceneObject? pressedNode;
    private Point pressPoint;
    private SceneObject? rangeAnchor;
    private bool draggingNode;
    private SceneObject? dropTarget;

    private void InitializeHierarchy()
    {
        Hierarchy.SelectionChanged += (_, _) =>
        {
            if (!refreshing) Select((Hierarchy.SelectedItem as HierarchyNode)?.Obj);
        };
        Hierarchy.ItemTemplate = new FuncDataTemplate<HierarchyNode>((node, _) => node == null ? null : HierarchyItem(node), supportsRecycling: false);
        Hierarchy.ContainerPrepared += (_, e) =>
        {
            var node = hierarchyRows[e.Index];
            var row = e.Container;
            row.Tag = node.Obj;
            row.Opacity = node.Opacity;
            AutomationProperties.SetName(row, node.Obj.Name);
            row.Classes.Set("groupSelected", GroupSelected(node.Obj));
            row.Classes.Set("dropTarget", node.Obj == dropTarget);
            row.Classes.Remove("ping");
        };
        // Tunnel: the ListBox would otherwise take Left and Right first.
        Hierarchy.AddHandler(KeyDownEvent, HierarchyKeyDown, RoutingStrategies.Tunnel);
        Hierarchy.ContextRequested += HierarchyContextRequested;
        Hierarchy.AddHandler(PointerPressedEvent, HierarchyPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        Hierarchy.AddHandler(PointerMovedEvent, HierarchyMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        Hierarchy.AddHandler(PointerReleasedEvent, HierarchyReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        Hierarchy.PointerCaptureLost += (_, e) => { if (e.Source == Hierarchy) EndHierarchyDrag(); };
        HierarchySearch.TextChanged += (_, _) => ApplyHierarchyFilter();
        HierarchySearch.KeyDown += (_, e) => { if (e.Key == Key.Escape) { HierarchySearch.Text = ""; e.Handled = true; } };
    }

    // Matching objects stay visible with their parents, and parents of matches are expanded.
    private void ApplyHierarchyFilter()
    {
        var query = HierarchySearch.Text?.Trim() ?? "";
        int matches = 0;
        bool Show(HierarchyNode node)
        {
            bool child = false;
            foreach (var row in node.Children) child |= Show(row);
            bool self = query.Length == 0 || node.Obj.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
            if (self && query.Length > 0) matches++;
            if (child && query.Length > 0) node.IsExpanded = true;
            return node.Shown = self || child;
        }
        foreach (var root in hierarchyRoots) Show(root);
        HierarchyNoMatch.IsVisible = query.Length > 0 && matches == 0;
        ShowHierarchyRows();
    }

    // Lists the shown rows under expanded parents in tree order and keeps the selected object's row selected.
    private void ShowHierarchyRows()
    {
        var rows = new List<HierarchyNode>(treeItems.Count);
        void Add(HierarchyNode node)
        {
            if (!node.Shown) return;
            rows.Add(node);
            if (node.IsExpanded) foreach (var child in node.Children) Add(child);
        }
        foreach (var root in hierarchyRoots) Add(root);
        bool was = refreshing, focused = Hierarchy.IsKeyboardFocusWithin;
        refreshing = true;
        hierarchyRows = rows;
        Hierarchy.ItemsSource = rows;
        var selected = Selected != null && treeItems.TryGetValue(Selected, out var node) && node.Listed ? node : null;
        Hierarchy.SelectedItem = selected;
        refreshing = was;
        // Replacing the rows recycles the focused row; give keyboard focus back to the selected one.
        if (focused) Dispatcher.UIThread.Post(() => ((selected != null ? Hierarchy.ContainerFromItem(selected) : null) ?? Hierarchy).Focus(), DispatcherPriority.Loaded);
    }

    private void SetExpanded(HierarchyNode node, bool expanded)
    {
        if (node.IsExpanded == expanded) return;
        node.IsExpanded = expanded;
        ShowHierarchyRows();
    }

    private static bool ExpandParents(HierarchyNode node)
    {
        bool changed = false;
        for (var parent = node.Parent; parent != null; parent = parent.Parent)
            if (!parent.IsExpanded) changed = parent.IsExpanded = true;
        return changed;
    }

    private bool GroupSelected(SceneObject obj) => Viewport.Selection.Count > 1 && Viewport.Selection.Contains(obj);

    // The keys a TreeView row answered: Right/+ expand (then go to the first child), Left/- collapse (then go to the parent).
    private void HierarchyKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || Hierarchy.SelectedItem is not HierarchyNode node) return;
        bool parent = node.Children.Count > 0;
        if (e.Key is Key.Right or Key.Add && parent && !node.IsExpanded) SetExpanded(node, true);
        else if (e.Key is Key.Left or Key.Subtract && parent && node.IsExpanded) SetExpanded(node, false);
        else if (e.Key == Key.Right && node.Children.FirstOrDefault(c => c.Shown) is { } child) { Select(child.Obj); FocusHierarchySelection(); }
        else if (e.Key == Key.Left && node.Parent != null) { Select(node.Parent.Obj); FocusHierarchySelection(); }
        else return;
        e.Handled = true;
    }

    // Editor-only marks for the current scene, kept in the user's editor state rather than the scene file.
    private string? MarksKey => prefabSource == null && project != null ? ScenePath : null;

    private void LoadSceneMarks()
    {
        var state = EditorUserState.Current;
        Viewport.HiddenIds.Clear();
        Viewport.LockedIds.Clear();
        Viewport.HiddenIds.UnionWith(EditorUserState.Marks(state.Hidden, MarksKey));
        Viewport.LockedIds.UnionWith(EditorUserState.Marks(state.Locked, MarksKey));
        inspectorLock = EditorUserState.Marks(state.InspectorLock, MarksKey).Cast<Guid?>().FirstOrDefault();
        SyncTabTools();
        Viewport.InvalidateUi();
    }

    private void SetSceneMark(SceneObject obj, bool hide, bool on)
    {
        var ids = hide ? Viewport.HiddenIds : Viewport.LockedIds;
        if (!(on ? ids.Add(obj.Id) : ids.Remove(obj.Id))) return;
        var state = EditorUserState.Current;
        state.SetMarks(hide ? state.Hidden : state.Locked, MarksKey, ids.Where(id => Scene.Objects.Any(o => o.Id == id)));
        RefreshRowMarks();
        if (hide) Viewport.InvalidateUi(); else Viewport.RefreshScene();
        Status.Text = $"{obj.Name}: {(hide ? on ? "hidden" : "shown" : on ? "locked" : "unlocked")} in the Scene view (editor only).";
    }

    private void RefreshRowMarks()
    {
        foreach (var row in Hierarchy.GetVisualDescendants().OfType<Grid>().Where(g => g.Classes.Contains("hierarchyRow")))
            if (row.Tag is SceneObject obj) row.Children[1].Opacity = Viewport.PlayWorld == null && Viewport.IsEyeHidden(obj) ? 0.5 : 1;
    }

    // Icon, name, then the Scene eye and lock. Their columns are always reserved, so hovering or setting them never moves the name.
    // Each row gets its own copy of the icon geometry: a Path subscribes to its geometry's Changed event, and detaching
    // 10,000 rows from one shared geometry copies that subscriber list once per row.
    private Control HierarchyRow(SceneObject obj)
    {
        // Narrow toggles with no gap between them keep the name column as wide as possible in a 176px panel.
        var row = new Grid { ColumnDefinitions = new("16,4,*,2,14,14"), Background = Brushes.Transparent, Tag = obj, Classes = { "hierarchyRow" } };
        row.Children.Add(new PathIcon { Theme = (Avalonia.Styling.ControlTheme)this.FindResource("EditorIcon")!, Data = EditorTokens.ObjectIcon(obj).Clone(), Foreground = EditorTokens.Brush("EditorMuted") });
        var name = new TextBlock { Text = obj.Name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Opacity = Viewport.PlayWorld == null && Viewport.IsEyeHidden(obj) ? 0.5 : 1 };
        ToolTip.SetTip(name, obj.Name);
        Grid.SetColumn(name, 2);
        row.Children.Add(name);
        if (Viewport.PlayWorld == null)
        {
            row.Children.Add(RowToggle(obj, hide: true));
            row.Children.Add(RowToggle(obj, hide: false));
        }
        return row;
    }

    // Indent, expander and row at the TreeView's places: 16px per level, the chevron 13px in, the icon 27px after it.
    private Control HierarchyItem(HierarchyNode node)
    {
        var item = new Grid { ColumnDefinitions = new("12,15,*"), Margin = new Thickness(12 + 16 * node.Depth, 0, 3, 0) };
        var chevron = new ToggleButton { Theme = (Avalonia.Styling.ControlTheme)this.FindResource("FluentTreeViewExpandCollapseChevron")!,
            Focusable = false, MinHeight = 0, Height = 20, Padding = new Thickness(0), IsChecked = node.IsExpanded, IsVisible = node.Children.Count > 0 };
        chevron.IsCheckedChanged += (_, _) => SetExpanded(node, chevron.IsChecked == true);
        item.Children.Add(chevron);
        var row = HierarchyRow(node.Obj);
        row.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Grid.SetColumn(row, 2);
        item.Children.Add(row);
        return item;
    }

    private ToggleButton RowToggle(SceneObject obj, bool hide)
    {
        var toggle = new ToggleButton { Classes = { "rowToggle" }, IsChecked = (hide ? Viewport.HiddenIds : Viewport.LockedIds).Contains(obj.Id) };
        Grid.SetColumn(toggle, hide ? 4 : 5);
        AutomationProperties.SetName(toggle, hide ? $"Hide {obj.Name} in Scene" : $"Lock {obj.Name} in Scene");
        void Show()
        {
            bool on = toggle.IsChecked == true;
            var icon = EditorTokens.Icon(hide ? on ? "IconEyeOff" : "IconEye" : on ? "IconLock" : "IconUnlock");
            icon.Data = icon.Data.Clone();
            toggle.Content = icon;
            ToolTip.SetTip(toggle, hide ? on ? "Hidden in the Scene view · click to show" : "Hide in the Scene view (editor only)"
                : on ? "Locked: Scene clicks skip it · click to unlock" : "Lock: Scene clicks skip it (editor only)");
        }
        Show();
        toggle.IsCheckedChanged += (_, _) => { Show(); SetSceneMark(obj, hide, toggle.IsChecked == true); };
        return toggle;
    }

    private void RebuildHierarchy(SceneObject? selected) => RebuildHierarchyRows(selected, keepInspector: false);

    // keepInspector: selected is already shown with current Inspector rows (InspectorRowsCurrent); only values are refreshed.
    private void RebuildHierarchyRows(SceneObject? selected, bool keepInspector)
    {
        refreshing = true;
        var collapsed = treeItems.Values.Where(node => node.Children.Count > 0 && !node.IsExpanded).Select(node => node.Obj.Id).ToHashSet();
        var objects = Viewport.PlayWorld?.Objects ?? Scene.Objects;
        treeItems.Clear();
        hierarchyRoots.Clear();
        foreach (var obj in objects)
            treeItems.Add(obj, new HierarchyNode(obj) { IsExpanded = !collapsed.Contains(obj.Id),
                Opacity = (Viewport.PlayWorld is { } world ? !world.IsActive(obj.Id) : !obj.ActiveInHierarchy) ? 0.55 : 1 });
        foreach (var obj in objects)
        {
            var node = treeItems[obj];
            if (obj.Parent == null) { hierarchyRoots.Add(node); continue; }
            node.Parent = treeItems[obj.Parent];
            node.Parent.Children.Add(node);
        }
        hierarchyShape = objects.Select(o => (o, o.Parent, o.Name)).ToArray();
        Viewport.InvalidateDrawOrder();
        ApplyHierarchyFilter();
        if (keepInspector)
        {
            SelectHierarchyItem(selected);
            refreshing = false;
            RefreshInspector();
        }
        else
        {
            refreshing = false;
            Select(selected);
        }
    }

    // Pool Rent/Return and active toggles bump the play world's HierarchyRevision without changing its rows.
    // When the objects, parents and names are the rows already shown, only the inactive dimming is updated.
    private bool RefreshHierarchyRows(SceneGame world)
    {
        var objects = world.Objects;
        if (objects.Count != hierarchyShape.Length) return false;
        for (int i = 0; i < objects.Count; i++)
        {
            var (obj, parent, name) = hierarchyShape[i];
            if (objects[i] != obj || obj.Parent != parent || obj.Name != name) return false;
        }
        foreach (var (obj, node) in treeItems) node.Opacity = world.IsActive(obj.Id) ? 1 : 0.55;
        foreach (var row in Hierarchy.GetRealizedContainers()) if (row.Tag is SceneObject obj) row.Opacity = treeItems[obj].Opacity;
        return true;
    }

    private void SelectHierarchyItem(SceneObject? obj)
    {
        var node = obj != null ? treeItems[obj] : null;
        if (node != null && ExpandParents(node)) ShowHierarchyRows();
        Hierarchy.SelectedItem = node is { Listed: true } ? node : null;
        if (Hierarchy.SelectedItem != null) Hierarchy.ScrollIntoView(node!);
    }

    private static string ObjectPath(SceneObject obj)
    {
        var names = new Stack<string>();
        for (var node = obj; node != null; node = node.Parent) names.Push(node.Name);
        return string.Join(" / ", names);
    }

    private void ChangeParent(SceneObject obj, SceneObject? parent)
    {
        if (prefabSource != null && (obj.Id == prefabSource.RootId || parent == null)) return;
        if (Viewport.IsPlaying || !Scene.Reparent(obj, parent)) return;
        RebuildHierarchy(obj);
        MarkDirty();
    }

    private static ListBoxItem? TreeItemAt(Visual? visual) =>
        visual as ListBoxItem ?? visual?.FindAncestorOfType<ListBoxItem>();

    private static bool IsDeleteObjectShortcut(KeyEventArgs e) => OperatingSystem.IsMacOS()
        ? e.KeyModifiers == KeyModifiers.Meta && e.Key is Key.Back or Key.Delete
        : e.KeyModifiers == KeyModifiers.None && e.Key == Key.Delete;

    private static readonly KeyGesture DuplicateGesture = new(Key.D, OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);

    private Task DuplicateObject()
    {
        if (Selected is { } source && source.Id != prefabSource?.RootId)
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
            if (selected == Selected && selected != null && treeItems.TryGetValue(selected, out var node) && node.Listed)
            {
                Hierarchy.ScrollIntoView(node);
                Hierarchy.ContainerFromItem(node)?.Focus();
            }
        }, DispatcherPriority.Loaded);
    }

    private void HierarchyContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (busy || Viewport.IsPlaying) return;
        bool atPointer = e.TryGetPosition(Hierarchy, out var point);
        var item = atPointer ? TreeItemAt(Hierarchy.InputHitTest(point) as Visual)
            : Hierarchy.SelectedItem is { } row ? Hierarchy.ContainerFromItem(row) : null;
        var parent = item?.Tag as SceneObject;
        if (parent == null && prefabSource != null) parent = Scene.Objects.Single(o => o.Id == prefabSource.RootId);
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
        foreach (var kind in new[] { "Canvas", "Group", "Layout", "Scroll View", "Text", "Text Field", "Image", "Button", "Slider", "Toggle", "Dropdown" })
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
            if(project!=null && prefabSource == null){
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
            duplicate.IsEnabled = remove.IsEnabled = parent.Id != prefabSource?.RootId;
            menu.Items.Add(ClipboardOption(cutItem, () => CopySelection(true), duplicate.IsEnabled));
            menu.Items.Add(ClipboardOption(copyItem, () => CopySelection(false)));
            menu.Items.Add(ClipboardOption(pasteItem, PasteObjects));
            menu.Items.Add(duplicate);
            menu.Items.Add(remove);
        }
        else
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(ClipboardOption(pasteItem, PasteObjects));
        }
        menu.Placement = atPointer ? PlacementMode.Pointer : PlacementMode.BottomEdgeAlignedLeft;
        menu.Open((Control?)item ?? Hierarchy);
        e.Handled = true;
    }

    private MenuItem ClipboardOption(NativeMenuItem command, Func<Task> action, bool enabled = true)
    {
        var item = new MenuItem { Header = command.Header, InputGesture = command.Gesture, IsEnabled = enabled };
        item.Click += (_, _) => RunAction(action);
        return item;
    }

    private void HierarchyPressed(object? sender, PointerPressedEventArgs e)
    {
        pressedNode = null;
        if (Viewport.IsPlaying || !e.GetCurrentPoint(Hierarchy).Properties.IsLeftButtonPressed ||
            e.Source is not Visual visual || visual is ToggleButton || visual.FindAncestorOfType<ToggleButton>() != null) return;
        if (TreeItemAt(visual) is not { Tag: SceneObject obj } item) return;
        if ((e.KeyModifiers & (KeyModifiers.Shift|KeyModifiers.Control|KeyModifiers.Meta))!=0)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                var visible = hierarchyRows.Select(row => row.Obj).ToList();
                int end = visible.IndexOf(obj);
                int start = visible.IndexOf(rangeAnchor ?? Selected ?? obj);
                if (start < 0) start = end;
                var range = visible.GetRange(Math.Min(start, end), Math.Abs(end - start) + 1);
                if (start > end) range.Reverse();
                ShowSelection(range);
            }
            else
            {
                ToggleSelection(obj);
                rangeAnchor = obj;
            }
            e.Handled = true;
            return;
        }
        // Keep the current Inspector visible while dragging a different object into a field.
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
        HighlightTarget(item?.Tag is SceneObject target && !target.IsWithin(pressedNode) ? target : null);
        bool allowed = new Rect(Hierarchy.Bounds.Size).Contains(point) && (item == null || dropTarget != null);
        bool export = prefabSource == null && project != null && new Rect(ProjectBrowser.Bounds.Size).Contains(e.GetPosition(ProjectBrowser));
        var reference = ReferenceTarget(e);
        bool takesObject = reference?.Tag is ReferenceDrop { Asset: null };
        HighlightReference(reference, takesObject);
        Hierarchy.Cursor = export || takesObject ? CopyFileCursor : reference != null ? NoDropCursor : allowed ? new Cursor(StandardCursorType.DragMove) : NoDropCursor;
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
        var reference = moved ? ReferenceTarget(e)?.Tag as ReferenceDrop : null;
        bool export = prefabSource == null && source != null && moved && project != null && new Rect(ProjectBrowser.Bounds.Size).Contains(e.GetPosition(ProjectBrowser));
        EndHierarchyDrag();
        if (e.Pointer.Captured == Hierarchy) e.Pointer.Capture(null);
        if (source != null && !moved)
        {
            Select(source);
            Hierarchy.ContainerFromItem(treeItems[source])?.Focus();
            e.Handled = true;
            return;
        }
        if (source != null && reference is { Asset: null })
        {
            reference.Insert([source.Id]);
            e.Handled = true;
            return;
        }
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

    private void HighlightTarget(SceneObject? target)
    {
        if (target == dropTarget) return;
        dropTarget = target;
        foreach (var row in Hierarchy.GetRealizedContainers()) row.Classes.Set("dropTarget", row.Tag == target && target != null);
    }

    private void EndHierarchyDrag()
    {
        pressedNode = null;
        draggingNode = false;
        HighlightTarget(null);
        HighlightReference(null);
        Hierarchy.Cursor = null;
    }
}
