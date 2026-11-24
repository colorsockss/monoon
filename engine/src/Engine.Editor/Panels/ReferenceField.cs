using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Engine.Editor;

// One entry of a reference picker. Guid.Empty is None; File is a PNG's full path, shown as its Project thumbnail.
internal sealed record ReferenceChoice(Guid Id, string Label, string? File = null, Geometry? Icon = null)
{
    public override string ToString() => Label;
}

// A place in the Inspector or the Particle panel that takes what is dragged onto it from the Project panel, the Hierarchy or
// Finder. Asset null takes scene objects; otherwise it says which project paths fit. List is the reference list a row belongs
// to (its rows reorder within it), null for a single field. Many: an array takes every dropped asset in order.
internal sealed record ReferenceDrop(StackPanel? List, int Index, Func<string, bool>? Asset, Action<Guid[]> Insert, bool Many = false);

// The editor window behind a reference field: Ping flashes the target's Project or Hierarchy row; select also selects it.
internal interface IReferenceHost
{
    void Ping(Guid id, bool asset, bool select);
}

// A Unity-style object field (13.7, 13.17): [type icon or thumbnail · name · ● picker] in one input-high row. Click pings the
// target, double-click selects it, Delete or Backspace clears it, and ● opens a searchable picker of the accepted choices.
// Drag highlighting only recolours the 1px border, so the row and its neighbours never move.
internal sealed class ReferenceField : Border
{
    private readonly Func<IReadOnlyList<ReferenceChoice>> choices;
    private readonly Action<Guid?> set;
    private readonly bool asset;
    private ReferenceChoice[] all = [];
    internal ReferenceChoice Shown { get; }
    internal TextBlock Text { get; }
    internal Button PickButton { get; }
    internal Flyout Picker { get; }
    internal TextBox Search { get; } = new() { Classes = { "toolbarSearch" }, Watermark = "Search…" };
    internal ListBox List { get; } = new() { MaxHeight = 320 };
    // How long the last search took to filter and lay out the list (13.7 measures it on Field10000).
    internal TimeSpan LastSearch { get; private set; }
    // Listing the choices and showing them when the picker opens.
    internal TimeSpan LastOpen { get; private set; }

    public ReferenceField(string name, bool asset, ReferenceChoice shown, Func<IReadOnlyList<ReferenceChoice>> choices, Action<Guid?> set,
        Func<string, bool>? accepts = null, bool many = false)
    {
        (this.choices, this.set, this.asset, Shown) = (choices, set, asset, shown);
        Classes.Add("referenceField");
        Focusable = true;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Tag = new ReferenceDrop(null, 0, asset ? accepts ?? (_ => false) : null, ids => { if (ids.Length > 0) set(ids[0]); }, many);
        AutomationProperties.SetName(this, name);
        ToolTip.SetTip(this, shown.Id == Guid.Empty ? $"{name}: drag {(asset ? "an asset from the Project panel" : "an object from the Hierarchy")} here"
            : $"{shown.Label}\nClick: show in {(asset ? "Project" : "Hierarchy")} · Double-click: select");
        Text = new TextBlock { Text = shown.Label, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        Text.Classes.Set("label", shown.Id == Guid.Empty);
        PickButton = new Button { Classes = { "icon", "quiet", "picker" }, Content = EditorTokens.Icon("IconTarget"), Focusable = false };
        ToolTip.SetTip(PickButton, "Choose…");
        AutomationProperties.SetName(PickButton, name + " picker");
        var row = new Grid { ColumnDefinitions = new("Auto,*,Auto"), ColumnSpacing = EditorTokens.FieldGap };
        EditorControls.Put(row, Glyph(shown, 6), 0, 0);
        EditorControls.Put(row, Text, 0, 1);
        EditorControls.Put(row, PickButton, 0, 2);
        Child = row;

        var lens = EditorTokens.Icon("IconSearch");
        lens.Margin = new(6, 0, 0, 0);
        lens.Foreground = EditorTokens.Brush("EditorMuted");
        Search.InnerLeftContent = lens;
        AutomationProperties.SetName(Search, name + " search");
        AutomationProperties.SetName(List, name + " choices");
        List.ItemTemplate = new FuncDataTemplate<ReferenceChoice>((c, _) =>
        {
            var line = new Grid { ColumnDefinitions = new("Auto,*"), ColumnSpacing = 6 };
            if (c == null) return line;
            EditorControls.Put(line, Glyph(c, 0), 0, 0);
            EditorControls.Put(line, new TextBlock { Text = c.Label, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }, 0, 1);
            ToolTip.SetTip(line, c.Label);
            return line;
        });
        var body = new Grid { RowDefinitions = new("Auto,*"), RowSpacing = EditorTokens.Gap, Width = 300 };
        EditorControls.Put(body, Search, 0);
        EditorControls.Put(body, List, 1);
        Picker = new Flyout { Content = body, Placement = PlacementMode.BottomEdgeAlignedRight, FlyoutPresenterClasses = { "pickerFlyout" } };
        PickButton.Click += (_, _) => Picker.ShowAt(this);
        Picker.Opened += (_, _) =>
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            all = [.. choices()];
            Search.Text = "";
            Filter();
            LastOpen = clock.Elapsed;
            Search.Focus();
        };
        Picker.Closed += (_, _) => { all = []; List.ItemsSource = null; };
        Search.TextChanged += (_, _) => Filter();
        Search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && List.ItemCount > 0) { List.SelectedIndex = Math.Min(1, List.ItemCount - 1); List.ContainerFromIndex(List.SelectedIndex)?.Focus(); e.Handled = true; }
            else if (e.Key == Key.Enter) { Choose(List.SelectedItem as ReferenceChoice ?? List.Items.OfType<ReferenceChoice>().FirstOrDefault(c => c.Id != Guid.Empty)); e.Handled = true; }
            else if (e.Key == Key.Escape) { Picker.Hide(); e.Handled = true; }
        };
        List.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Choose(List.SelectedItem as ReferenceChoice); e.Handled = true; } };
        List.Tapped += (_, _) => Choose(List.SelectedItem as ReferenceChoice);

        AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Source is Visual v && v.FindAncestorOfType<Button>(includeSelf: true) == PickButton) return;
            Focus();
            if (Shown.Id != Guid.Empty) Host?.Ping(Shown.Id, asset, e.ClickCount >= 2);
            e.Handled = true;
        });
        KeyDown += (_, e) =>
        {
            if (e.Key is Key.Delete or Key.Back && Shown.Id != Guid.Empty) { set(null); e.Handled = true; }
            else if (e.Key is Key.Enter or Key.Space) { Picker.ShowAt(this); e.Handled = true; }
        };
    }

    // The window (or, for a torn-off panel, its owner) that pings and selects.
    private IReferenceHost? Host => TopLevel.GetTopLevel(this) switch { IReferenceHost host => host, Window { Owner: IReferenceHost owner } => owner, _ => null };

    // Applies the search: None stays first, the rest match the text anywhere in the label (ordinal, ignoring case).
    internal void Filter()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var text = Search.Text?.Trim() ?? "";
        List.ItemsSource = text.Length == 0 ? all : all.Where(c => c.Id == Guid.Empty || c.Label.Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray();
        List.SelectedIndex = -1;
        List.UpdateLayout();
        LastSearch = clock.Elapsed;
    }

    internal void Choose(ReferenceChoice? choice)
    {
        if (choice == null) return;
        Picker.Hide();
        if (choice.Id != Shown.Id) set(choice.Id == Guid.Empty ? null : choice.Id);
    }

    // A PNG thumbnail, else the type icon, in the 16px icon column.
    private static Control Glyph(ReferenceChoice c, double left)
    {
        if (c.File is { } file && MainWindow.ProjectThumbnails.Get(file) is { } image)
        {
            var thumb = new Image { Source = image, Width = 16, Height = 16, Margin = new(left, 0, 0, 0), Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapInterpolationMode(thumb, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
            return thumb;
        }
        var icon = EditorTokens.Icon("IconFile");
        if (c.Icon is { } geometry) icon.Data = geometry;
        icon.Margin = new(left, 0, 0, 0);
        icon.Foreground = EditorTokens.Brush("EditorMuted");
        icon.Opacity = c.Id == Guid.Empty ? .5 : 1;
        return icon;
    }
}
