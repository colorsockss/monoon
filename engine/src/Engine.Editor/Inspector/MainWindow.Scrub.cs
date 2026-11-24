using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Engine.Editor;

// Number-label scrubbing (roadmap 13.10): drag a number row's label (or an X/Y/W/H axis label) left or right to change the
// value, one step per pixel. The value applies live through the field's own handler; RecordEdit keeps the whole drag as
// one pending edit (grouped by the scrub) and the release commits it: one Undo record.
public partial class MainWindow
{
    // What a script number label scrubs (its partner is a TextBox, so the label says how): limits and whole numbers.
    internal sealed record ScrubSpec(double? Min, double? Max, bool Integer);

    private sealed class Scrub(TextBlock label, Func<double?> read, Action<double> write, double step, double? min, double? max, IPointer pointer)
    {
        public readonly TextBlock Label = label;
        public readonly Func<double?> Read = read;
        public readonly Action<double> Write = write;
        public readonly double Step = step;
        public readonly double? Min = min, Max = max;
        public readonly IPointer Pointer = pointer;
        public double Start, StartX;
    }

    private Scrub? scrub;

    private void InitializeScrub()
    {
        InspectorPanel.AddHandler(PointerPressedEvent, ScrubPressed, RoutingStrategies.Tunnel);
        InspectorPanel.AddHandler(PointerMovedEvent, ScrubMoved, RoutingStrategies.Tunnel);
        InspectorPanel.AddHandler(PointerReleasedEvent, (_, e) => EndScrub(e.Pointer), RoutingStrategies.Tunnel);
        InspectorPanel.AddHandler(PointerCaptureLostEvent, (_, e) => EndScrub(e.Pointer), RoutingStrategies.Tunnel);
    }

    // The value a label scrubs: the NumericUpDown in the next column of its grid, or a script number's TextBox (ScrubSpec).
    private (Func<double?> Read, Action<double> Write, double Step, double? Min, double? Max)? ScrubTarget(TextBlock label)
    {
        // noScrub: a field whose every commit writes a file (the asset Inspector pivot) is typed or stepped instead.
        if (!label.Classes.Contains("fieldLabel") || label.Classes.Contains("noScrub") || label.Parent is not Grid grid) return null;
        var partner = grid.Children.FirstOrDefault(c => Grid.GetColumn(c) == Grid.GetColumn(label) + 1 && Grid.GetRow(c) == Grid.GetRow(label));
        if (partner is NumericUpDown { IsEffectivelyEnabled: true } number)
            return (() => number.Value is decimal v ? (double)v : null, v => number.Value = (decimal)v, (double)number.Increment, (double)number.Minimum, (double)number.Maximum);
        if (label.Tag is not ScrubSpec spec) return null;
        var box = partner as TextBox ?? (partner as Grid)?.Children.OfType<TextBox>().LastOrDefault();
        if (box is not { IsEffectivelyEnabled: true }) return null;
        return (() => double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null,
            v => box.Text = spec.Integer ? ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture) : v.ToString("R", CultureInfo.InvariantCulture),
            1, spec.Min, spec.Max);
    }

    private void ScrubPressed(object? sender, PointerPressedEventArgs e)
    {
        if (busy || Viewport.IsPlaying || e.Source is not TextBlock label || !e.GetCurrentPoint(label).Properties.IsLeftButtonPressed ||
            ScrubTarget(label) is not { } target || target.Read() is not double start) return;
        CommitEdit(); // a typed edit in another field ends before the drag begins
        scrub = new(label, target.Read, target.Write, target.Step, target.Min, target.Max, e.Pointer) { Start = start, StartX = e.GetPosition(InspectorPanel).X };
        e.Pointer.Capture(label);
        e.Handled = true;
    }

    private void ScrubMoved(object? sender, PointerEventArgs e)
    {
        if (scrub is not { } drag)
        {
            // The resize cursor shows which labels scrub.
            if (e.Source is TextBlock hover && hover.Cursor == null && ScrubTarget(hover) != null)
            {
                hover.Cursor = new Cursor(StandardCursorType.SizeWestEast);
                hover.Background ??= Avalonia.Media.Brushes.Transparent;
            }
            return;
        }
        if (e.Pointer != drag.Pointer) return;
        double steps = Math.Round(e.GetPosition(InspectorPanel).X - drag.StartX);
        double value = drag.Start + steps * drag.Step;
        value = Math.Round(value / drag.Step) * drag.Step; // no 0.30000000000000004 from adding steps
        if (drag.Min is double min) value = Math.Max(min, value);
        if (drag.Max is double max) value = Math.Min(max, value);
        if (drag.Read() != value) drag.Write(value);
        e.Handled = true;
    }

    private void EndScrub(IPointer pointer)
    {
        if (scrub is not { } drag || drag.Pointer != pointer) return;
        scrub = null;
        if (pointer.Captured == drag.Label) pointer.Capture(null);
        CommitEdit();
        if (MultiInspector.IsVisible) { RefreshMultiTransform(); RefreshComponentList(); }
    }
}
