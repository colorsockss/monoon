using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Engine.Editor;

public enum DockZone { Center, Left, Right, Top, Bottom }

// A tab group (Panels) or a split (Row: left to right, Column: top to bottom).
public sealed class DockNode
{
    public string? Split { get; set; }
    public List<DockNode>? Children { get; set; }
    public List<string>? Panels { get; set; }
    public string? Active { get; set; }
    // Pixels along the parent split's axis.
    public double Size { get; set; }

    public static DockNode Tabs(double size, params string[] panels) => new() { Panels = [.. panels], Active = panels[0], Size = size };
    public static DockNode Row(double size, params DockNode[] children) => new() { Split = "Row", Children = [.. children], Size = size };
    public static DockNode Column(double size, params DockNode[] children) => new() { Split = "Column", Children = [.. children], Size = size };
    public DockNode Clone() => JsonSerializer.Deserialize<DockNode>(JsonSerializer.Serialize(this))!;
    internal IEnumerable<DockNode> Descendants() => (Children ?? []).SelectMany(c => c.Descendants()).Prepend(this);
}

// A panel shown in its own window: position in screen pixels, size in window units.
public sealed class FloatingPanel
{
    public string Panel { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

// Panels stay children of this control for their whole life and are arranged over the slot of their tab group.
// Re-parenting them would detach the Scene view (its native SDL view and Play session) on every tab or split change.
public sealed class DockHost : Panel
{
    public static readonly AttachedProperty<string?> PanelProperty = AvaloniaProperty.RegisterAttached<DockHost, Control, string?>("Panel");
    // The main panel takes the remaining space and cannot be closed.
    public static readonly AttachedProperty<bool> FillProperty = AvaloniaProperty.RegisterAttached<DockHost, Control, bool>("Fill");
    public static string? GetPanel(Control control) => control.GetValue(PanelProperty);
    public static void SetPanel(Control control, string? value) => control.SetValue(PanelProperty, value);
    public static bool GetFill(Control control) => control.GetValue(FillProperty);
    public static void SetFill(Control control, bool value) => control.SetValue(FillProperty, value);

    internal const double TabHeight = 24, Gutter = 4, EdgeBand = 16, DragThreshold = 6;
    private readonly Decorator tree = new();
    private readonly Border previewBox = new() { Classes = { "dockPreview" }, IsHitTestVisible = false };
    // A popup window, because the native Scene view covers anything drawn inside the main window.
    private readonly Popup preview;
    private readonly Dictionary<string, Control> slots = [];
    private readonly Dictionary<string, Border> tabs = [];
    private readonly Dictionary<DockNode, Control> views = [];
    private readonly Dictionary<Control, Rect> arranged = [];
    private readonly Dictionary<string, string> titles = [];
    // Torn-off panels: the panel control moves into the window and back, as the Scene spike did with Play running.
    private readonly Dictionary<string, (Window Window, Control Panel)> floats = [];
    private DockNode? root;
    private (string Panel, Point Start, bool Dragging)? drag;
    public event Action? Changed;
    // The tab close button and the tab menu; the owner closes the panel (an editor may ask about unsaved changes first).
    public event Action<string>? CloseRequested;
    // The Dock button or closing a floating window; the owner puts the panel back with Unfloat and its usual placement.
    public event Action<string>? DockRequested;
    public event Action<Window>? WindowCreated;

    public DockHost()
    {
        preview = new Popup
        {
            Child = previewBox, IsHitTestVisible = false, PlacementTarget = this, Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = PopupAnchor.TopLeft, PlacementGravity = PopupGravity.BottomRight
        };
        Children.Add(tree);
        Children.Add(preview);
        SizeChanged += (_, _) => { if (root != null && Fit(root, Bounds.Size)) Rebuild(); };
        LayoutUpdated += (_, _) => { if (Panels.Any(p => Slot(p) != arranged.GetValueOrDefault(p))) InvalidateArrange(); };
    }

    // Panels inside the main window; floating ones are listed by Floating.
    public IEnumerable<Control> Panels => Children.Where(c => GetPanel(c) != null);
    private IEnumerable<Control> AllPanels => Panels.Concat(floats.Values.Select(f => f.Panel));
    private Control PanelControl(string id) => AllPanels.First(p => GetPanel(p) == id);
    public DockNode Layout { get { SyncSizes(); return root!.Clone(); } }
    public bool IsOpen(string id) => GroupOf(id) != null || floats.ContainsKey(id);
    public bool IsFloating(string id) => floats.ContainsKey(id);
    public Window? FloatingWindow(string id) => floats.TryGetValue(id, out var f) ? f.Window : null;
    public IEnumerable<Window> FloatingWindows => floats.Values.Select(f => f.Window);
    // Last shown bounds: shutting down may close the windows before the layout is saved.
    private readonly Dictionary<string, FloatingPanel> floatBounds = [];
    public List<FloatingPanel> Floating => floats.Keys.Where(floatBounds.ContainsKey).Select(id => floatBounds[id]).ToList();
    private void Track(string id, Window window)
    {
        if (window.IsVisible && floats.ContainsKey(id))
            floatBounds[id] = new FloatingPanel { Panel = id, X = window.Position.X, Y = window.Position.Y, Width = window.Width, Height = window.Height };
    }
    private string Label(string id) => titles.GetValueOrDefault(id) ?? id;
    internal Rect? PreviewRect => preview.IsOpen ? new Rect(preview.HorizontalOffset, preview.VerticalOffset, previewBox.Width, previewBox.Height) : null;

    // `floating` names panels that will be shown in their own windows instead of the tree.
    public bool CanApply(DockNode layout, IReadOnlyCollection<string>? floating = null)
    {
        var ids = AllPanels.Select(p => GetPanel(p)!).ToHashSet();
        var seen = new List<string>(floating ?? []);
        foreach (var node in layout.Descendants())
        {
            if (!double.IsFinite(node.Size) || node.Size is < 0 or > 16384 || (node.Panels == null) == (node.Children == null)) return false;
            if (node.Children != null && (node.Split is not ("Row" or "Column") || node.Children.Count == 0 || node.Children.Contains(null!))) return false;
            if (node.Panels != null) seen.AddRange(node.Panels);
        }
        return seen.All(id => id != null && ids.Contains(id)) && seen.Distinct().Count() == seen.Count &&
            AllPanels.Where(GetFill).All(p => seen.Contains(GetPanel(p)!)) && layout.Descendants().Any(n => n.Panels?.Count > 0);
    }

    // Floating panels come back first; the caller floats the ones it lists afterwards.
    public void Apply(DockNode layout, IReadOnlyCollection<string>? floating = null)
    {
        if (!CanApply(layout, floating)) throw new ArgumentException("The dock layout does not match the editor panels.");
        foreach (var id in floats.Keys.ToList()) Unfloat(id);
        root = Prune(layout.Clone());
        Rebuild();
        Changed?.Invoke();
    }

    // Same operation as dropping the tab of `id` on the group holding `target` (or on the window edge when null).
    public void Move(string id, string? target, DockZone zone)
    {
        if (floats.ContainsKey(id)) Unfloat(id);
        var group = target == null ? null : GroupOf(target);
        var source = GroupOf(id);
        if (target != null && group == null) throw new ArgumentException($"{target} is not open.");
        if (source != null && source == group && (zone == DockZone.Center || source.Panels!.Count == 1)) return;
        SyncSizes();
        double span = Span(group ?? root!, zone is DockZone.Left or DockZone.Right);
        Remove(id);
        Insert(id, group, zone, span);
        root = Prune(root!);
        Rebuild();
        Changed?.Invoke();
    }

    public void Close(string id)
    {
        if (GetFill(PanelControl(id)) || !IsOpen(id)) return;
        if (floats.ContainsKey(id))
        {
            Unfloat(id);
            UpdateVisibility();
            Changed?.Invoke();
            return;
        }
        SyncSizes();
        Remove(id);
        Rebuild();
        Changed?.Invoke();
    }

    // Shows a closed panel next to `near` when that is open, otherwise at the window edge.
    public void Open(string id, string? near, DockZone zone, DockZone edge)
    {
        if (IsOpen(id)) { Activate(id); return; }
        if (near != null && IsFloating(near)) near = null;
        if (near != null && IsOpen(near)) Move(id, near, zone);
        else Move(id, null, edge);
    }

    public void Activate(string id)
    {
        if (floats.TryGetValue(id, out var f)) { f.Window.Activate(); return; }
        if (GroupOf(id) is not { } group) return;
        group.Active = id;
        foreach (var other in group.Panels!) tabs[other].Classes.Set("active", other == id);
        tabs[id].BringIntoView();
        UpdateVisibility();
        Changed?.Invoke();
    }

    public void SetTitle(string id, string? title)
    {
        if (title == null) titles.Remove(id); else titles[id] = title;
        if (tabs.TryGetValue(id, out var tab)) ((TextBlock)((StackPanel)tab.Child!).Children[0]).Text = Label(id);
        if (floats.TryGetValue(id, out var f)) f.Window.Title = Label(id);
    }

    // Shows the panel in its own window at `position` (screen pixels), or centred on the main window.
    public void Float(string id, PixelPoint? position = null, Size? size = null)
    {
        if (floats.TryGetValue(id, out var existing)) { existing.Window.Activate(); return; }
        var panel = PanelControl(id);
        // The main window keeps at least one docked panel.
        if (GroupOf(id) != null && root!.Descendants().Sum(n => n.Panels?.Count ?? 0) <= 1) return;
        var client = size ?? (panel.IsVisible && panel.Bounds.Width > 0 ? new Size(panel.Bounds.Width, panel.Bounds.Height + TabHeight) : new Size(640, 420));
        SyncSizes();
        Remove(id);
        if (root != null) Rebuild();
        // Each window has its own layout queue, and one still holding a control that moved to another window throws.
        // Finish the old window's pending layout before the move and the new window's right after it.
        UpdateLayout();
        Children.Remove(panel);
        panel.IsVisible = true;
        var dock = new Button { Classes = { "quiet" }, Content = "Dock", VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 0), MinHeight = 0, Height = 20 };
        AutomationProperties.SetName(dock, $"Dock {id}");
        ToolTip.SetTip(dock, "Put this panel back into the main window");
        dock.Click += (_, _) => DockRequested?.Invoke(id);
        var name = new TextBlock { Text = id, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), FontWeight = Avalonia.Media.FontWeight.SemiBold };
        var bar = new Border { Classes = { "dockBar" }, Child = new Grid { ColumnDefinitions = new("*,Auto"), Children = { name, dock } } };
        Grid.SetColumn(dock, 1);
        Grid.SetRow(panel, 1);
        var window = new Window
        {
            Title = Label(id), Width = client.Width, Height = client.Height, MinWidth = panel.MinWidth, MinHeight = panel.MinHeight + TabHeight,
            ShowInTaskbar = false, WindowStartupLocation = position == null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.Manual,
        };
        if (position is { } p) window.Position = p;
        window.Closing += (_, e) =>
        {
            // Closing the window docks the panel; shutting down the editor closes it with the main window.
            if (e.CloseReason != WindowCloseReason.WindowClosing || e.IsProgrammatic || !floats.ContainsKey(id)) return;
            e.Cancel = true;
            Dispatcher.UIThread.Post(() => DockRequested?.Invoke(id));
        };
        floats[id] = (window, panel);
        // Styles and resources first: the panel's controls pick their themes when they join the window.
        WindowCreated?.Invoke(window);
        window.Content = new Grid { RowDefinitions = new($"{TabHeight},*"), Children = { bar, panel } };
        window.PositionChanged += (_, _) => Track(id, window);
        window.SizeChanged += (_, _) => Track(id, window);
        if (TopLevel.GetTopLevel(this) is Window owner) window.Show(owner); else window.Show();
        panel.UpdateLayout();
        Track(id, window);
        Changed?.Invoke();
    }

    // Moves a floating panel back into this control, closed; Open or Move then places it.
    public void Unfloat(string id)
    {
        if (!floats.Remove(id, out var f)) return;
        floatBounds.Remove(id);
        f.Window.UpdateLayout();
        ((Grid)f.Window.Content!).Children.Remove(f.Panel);
        Grid.SetRow(f.Panel, 0);
        Children.Add(f.Panel);
        UpdateLayout();
        f.Window.Close();
    }

    private DockNode? GroupOf(string id) => root?.Descendants().FirstOrDefault(n => n.Panels?.Contains(id) == true);
    private DockNode? ParentOf(DockNode node) => root?.Descendants().FirstOrDefault(n => n.Children?.Contains(node) == true);
    private bool HasFill(DockNode node) => node.Descendants().Any(n => n.Panels?.Any(id => GetFill(PanelControl(id))) == true);

    private double Span(DockNode node, bool row)
    {
        var size = node == root ? Bounds.Size : views.TryGetValue(node, out var view) ? view.Bounds.Size : default;
        double span = row ? size.Width : size.Height;
        return span > 0 ? span : node.Size;
    }

    private double MinSize(DockNode node, bool row)
    {
        if (node.Panels != null)
            return node.Panels.Max(id => row ? PanelControl(id).MinWidth : PanelControl(id).MinHeight + TabHeight);
        var mins = node.Children!.Select(c => MinSize(c, row)).ToList();
        return (node.Split == "Row") == row ? mins.Sum() + Gutter * (mins.Count - 1) : mins.Max();
    }

    private void Remove(string id)
    {
        if (GroupOf(id) is not { } group) return;
        group.Panels!.Remove(id);
        if (group.Active == id) group.Active = group.Panels.FirstOrDefault();
        root = Prune(root!);
    }

    private void Insert(string id, DockNode? group, DockZone zone, double span)
    {
        if (root == null) { root = DockNode.Tabs(0, id); return; }
        if (group != null && zone == DockZone.Center) { group.Panels!.Add(id); group.Active = id; return; }
        var node = group ?? root;
        bool row = zone is DockZone.Left or DockZone.Right, before = zone is DockZone.Left or DockZone.Top;
        var panel = PanelControl(id);
        double size = Math.Max(row ? panel.MinWidth : panel.MinHeight + TabHeight, group == null ? span / 4 : span / 2);
        var added = DockNode.Tabs(size, id);
        string axis = row ? "Row" : "Column";
        var parent = ParentOf(node);
        if (parent?.Split == axis)
        {
            node.Size = Math.Max(node.Size - size - Gutter, MinSize(node, row));
            parent.Children!.Insert(parent.Children.IndexOf(node) + (before ? 0 : 1), added);
            return;
        }
        var split = new DockNode { Split = axis, Size = node.Size, Children = before ? [added, node] : [node, added] };
        node.Size = Math.Max(span - size - Gutter, MinSize(node, row));
        if (parent == null) root = split;
        else parent.Children![parent.Children.IndexOf(node)] = split;
    }

    // Drops empty groups, unwraps one-child splits and merges nested splits of the same direction.
    private static DockNode? Prune(DockNode node)
    {
        if (node.Panels != null)
        {
            if (node.Panels.Count == 0) return null;
            if (node.Active == null || !node.Panels.Contains(node.Active)) node.Active = node.Panels[0];
            return node;
        }
        var children = new List<DockNode>();
        foreach (var child in node.Children!.Select(Prune).OfType<DockNode>())
        {
            if (child.Split == node.Split) children.AddRange(child.Children!);
            else children.Add(child);
        }
        if (children.Count == 0) return null;
        if (children.Count == 1) { children[0].Size = node.Size; return children[0]; }
        node.Children = children;
        return node;
    }

    private void SyncSizes()
    {
        foreach (var node in root?.Descendants() ?? [])
            foreach (var child in node.Children ?? [])
                if (views.TryGetValue(child, out var view) && view.Bounds.Width > 0)
                    child.Size = node.Split == "Row" ? view.Bounds.Width : view.Bounds.Height;
    }

    // Beside the main panel, side panels shrink (down to their minimum) to leave it room: after the main
    // panel moved out of their split, or when the window got smaller. Returns whether a size changed.
    private bool Fit(DockNode node, Size space)
    {
        if (node.Children == null || space.Width <= 0 || space.Height <= 0) return false;
        bool row = node.Split == "Row", changed = false;
        double length = (row ? space.Width : space.Height) - Gutter * (node.Children.Count - 1);
        var fill = node.Children.FirstOrDefault(HasFill);
        var sized = node.Children.Where(c => c != fill).ToList();
        // The main panel keeps at least 40% of the split (or its minimum).
        double excess = sized.Sum(c => c.Size) - (length - (fill == null ? 0 : Math.Max(MinSize(fill, row), length * .4)));
        double slack = sized.Sum(c => Math.Max(0, c.Size - MinSize(c, row)));
        if (fill != null && excess > .5 && slack > 0)
        {
            // Each panel gives up the same share of what it has above its minimum.
            double share = Math.Min(1, excess / slack);
            foreach (var child in sized) child.Size -= Math.Max(0, child.Size - MinSize(child, row)) * share;
            changed = true;
        }
        double weights = node.Children.Sum(c => Math.Max(c.Size, 1));
        foreach (var child in node.Children)
        {
            double span = fill == null ? length * Math.Max(child.Size, 1) / weights : child == fill ? length - sized.Sum(c => c.Size) : child.Size;
            changed |= Fit(child, row ? new Size(span, space.Height) : new Size(space.Width, span));
        }
        return changed;
    }

    private void Rebuild()
    {
        if (root == null) { tree.Child = null; UpdateVisibility(); return; }
        Fit(root, Bounds.Size);
        slots.Clear();
        tabs.Clear();
        views.Clear();
        tree.Child = Build(root!);
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        var active = root?.Descendants().Select(n => n.Active).ToHashSet() ?? [];
        foreach (var panel in Panels) panel.IsVisible = active.Contains(GetPanel(panel));
        InvalidateMeasure();
        InvalidateArrange();
    }

    private Control Build(DockNode node)
    {
        Control view = node.Panels != null ? BuildGroup(node) : BuildSplit(node);
        views[node] = view;
        return view;
    }

    private Control BuildSplit(DockNode node)
    {
        var children = node.Children!;
        bool row = node.Split == "Row", fill = children.Any(HasFill);
        var grid = new Grid();
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (i > 0)
            {
                var splitter = new GridSplitter
                {
                    Classes = { "dockSplitter" }, ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                    ResizeDirection = row ? GridResizeDirection.Columns : GridResizeDirection.Rows
                };
                splitter.DragCompleted += (_, _) => { SyncSizes(); Changed?.Invoke(); };
                Add(grid, splitter, new GridLength(Gutter), 0, row);
            }
            // Beside the main panel, side panels keep their pixel size when the window resizes. Whole pixels: a half-pixel
            // slot (a drop split at span / 2) is rounded per edge at 100% scale and overhangs the dock by 1px.
            var length = !fill ? new GridLength(Math.Max(child.Size, 1), GridUnitType.Star)
                : HasFill(child) ? new GridLength(1, GridUnitType.Star) : new GridLength(Math.Round(child.Size));
            Add(grid, Build(child), length, MinSize(child, row), row);
        }
        return grid;
    }

    private static void Add(Grid grid, Control child, GridLength length, double min, bool row)
    {
        if (row)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(length) { MinWidth = min });
            Grid.SetColumn(child, grid.ColumnDefinitions.Count - 1);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition(length) { MinHeight = min });
            Grid.SetRow(child, grid.RowDefinitions.Count - 1);
        }
        grid.Children.Add(child);
    }

    private Control BuildGroup(DockNode node)
    {
        var strip = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var id in node.Panels!) strip.Children.Add(Tab(node, id));
        // Tabs that do not fit scroll sideways; the active one is brought into view.
        var scroller = new ScrollViewer
        {
            Content = strip, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        if (node.Active is { } active) Dispatcher.UIThread.Post(() => { if (tabs.TryGetValue(active, out var tab)) tab.BringIntoView(); });
        var bar = new Border { Classes = { "dockBar" }, Child = scroller };
        var slot = new Border { Classes = { "dockSlot" } };
        Grid.SetRow(slot, 1);
        foreach (var id in node.Panels) slots[id] = slot;
        return new Grid { RowDefinitions = new RowDefinitions($"{TabHeight},*"), Children = { bar, slot } };
    }

    private Border Tab(DockNode group, string id)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        content.Children.Add(new TextBlock { Text = Label(id), VerticalAlignment = VerticalAlignment.Center });
        var menu = new ContextMenu();
        var floatItem = new MenuItem { Header = "Float in Window" };
        floatItem.Click += (_, _) => Float(id);
        menu.Items.Add(floatItem);
        if (!GetFill(PanelControl(id)))
        {
            var close = new Button { Classes = { "dockClose", "quiet" }, Content = "×" };
            AutomationProperties.SetName(close, $"Close {id}");
            ToolTip.SetTip(close, $"Close {id}");
            close.Click += (_, e) => { e.Handled = true; RequestClose(id); };
            content.Children.Add(close);
            var closeItem = new MenuItem { Header = "Close" };
            closeItem.Click += (_, _) => RequestClose(id);
            menu.Items.Add(closeItem);
        }
        var tab = new Border { Classes = { "dockTab" }, Child = content, Tag = id, ContextMenu = menu };
        ToolTip.SetTip(tab, "Drag to move · drag outside the window to float · right-click for more");
        tab.Classes.Set("active", group.Active == id);
        AutomationProperties.SetName(tab, $"{id} tab");
        tabs[id] = tab;
        tab.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(tab).Properties.IsLeftButtonPressed) return;
            Activate(id);
            drag = (id, e.GetPosition(this), false);
            e.Pointer.Capture(tab);
            e.Handled = true;
        };
        tab.PointerMoved += (_, e) =>
        {
            if (drag is not { } d || d.Panel != id) return;
            var point = e.GetPosition(this);
            if (!d.Dragging && Math.Abs(point.X - d.Start.X) + Math.Abs(point.Y - d.Start.Y) < DragThreshold) return;
            drag = d with { Dragging = true };
            ShowPreview(Target(point, id));
        };
        tab.PointerReleased += (_, e) =>
        {
            bool dragging = drag is { Dragging: true } d && d.Panel == id;
            drag = null;
            preview.IsOpen = false;
            e.Pointer.Capture(null);
            var point = e.GetPosition(this);
            if (!dragging) return;
            // Released outside the main window: tear the panel off with its title bar under the pointer.
            if (!new Rect(Bounds.Size).Contains(point)) Float(id, this.PointToScreen(point) - new PixelPoint(40, 12));
            else if (Target(point, id) is { } target) Move(id, target.Panel, target.Zone);
        };
        tab.PointerCaptureLost += (_, _) => { drag = null; preview.IsOpen = false; };
        return tab;
    }

    private void RequestClose(string id)
    {
        if (CloseRequested != null) CloseRequested(id); else Close(id);
    }

    // Where a dragged tab would land: a group's tab strip or centre adds a tab, its outer quarters split it,
    // and the window edge splits the whole layout.
    internal (string? Panel, DockZone Zone, Rect Preview)? Target(Point point, string dragged)
    {
        var host = new Rect(Bounds.Size);
        if (!host.Contains(point)) return null;
        DockZone? edge = point.X < EdgeBand ? DockZone.Left : point.X > host.Width - EdgeBand ? DockZone.Right :
            point.Y < EdgeBand ? DockZone.Top : point.Y > host.Height - EdgeBand ? DockZone.Bottom : null;
        if (edge is { } side) return (null, side, Part(host, side, .25));
        foreach (var (node, view) in views)
        {
            if (node.Panels == null || view.TranslatePoint(default, this) is not { } origin) continue;
            var rect = new Rect(origin, view.Bounds.Size);
            if (!rect.Contains(point)) continue;
            var body = rect.Deflate(new Thickness(0, TabHeight, 0, 0));
            var zone = DockZone.Center;
            if (point.Y >= body.Y)
            {
                double fx = (point.X - body.X) / body.Width, fy = (point.Y - body.Y) / body.Height, best = .25;
                foreach (var (candidate, distance) in new[] { (DockZone.Left, fx), (DockZone.Right, 1 - fx), (DockZone.Top, fy), (DockZone.Bottom, 1 - fy) })
                    if (distance < best) (zone, best) = (candidate, distance);
            }
            bool alone = node.Panels.Count == 1 && node.Panels[0] == dragged;
            if (alone || (zone == DockZone.Center && node.Panels.Contains(dragged))) return null;
            return (node.Active, zone, zone == DockZone.Center ? rect : Part(rect, zone, .5));
        }
        return null;
    }

    private static Rect Part(Rect r, DockZone zone, double f) => zone switch
    {
        DockZone.Left => r.WithWidth(r.Width * f),
        DockZone.Right => new Rect(r.Right - r.Width * f, r.Y, r.Width * f, r.Height),
        DockZone.Top => r.WithHeight(r.Height * f),
        DockZone.Bottom => new Rect(r.X, r.Bottom - r.Height * f, r.Width, r.Height * f),
        _ => r,
    };

    private void ShowPreview((string? Panel, DockZone Zone, Rect Preview)? target)
    {
        if (target is not { } t) { preview.IsOpen = false; return; }
        previewBox.Width = t.Preview.Width;
        previewBox.Height = t.Preview.Height;
        preview.HorizontalOffset = t.Preview.X;
        preview.VerticalOffset = t.Preview.Y;
        preview.IsOpen = true;
    }

    private Rect Slot(Control panel) => panel.IsVisible && slots.TryGetValue(GetPanel(panel)!, out var slot) &&
        slot.TranslatePoint(default, this) is { } origin ? new Rect(origin, slot.Bounds.Size) : default;

    protected override Size MeasureOverride(Size availableSize)
    {
        tree.Measure(availableSize);
        preview.Measure(availableSize);
        foreach (var panel in Panels) panel.Measure(arranged.GetValueOrDefault(panel).Size);
        return tree.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        tree.Arrange(new Rect(finalSize));
        preview.Arrange(default);
        foreach (var panel in Panels)
        {
            var rect = Slot(panel);
            panel.Measure(rect.Size);
            panel.Arrange(rect);
            arranged[panel] = rect;
        }
        return finalSize;
    }
}
