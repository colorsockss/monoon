using System.Drawing;

namespace Engine.Core;

public sealed partial class ScreenUi
{
    private Guid? dropdownId;
    private int optionCursor, firstOption, pressedOption = -1;
    private bool popupPointer;
    public bool DropdownOpen => dropdownId.HasValue;

    public float? SliderValue(Guid id) => byId.GetValueOrDefault(id) is { Control: UiSliderComponent } n ? n.ControlValue : null;
    public bool? ToggleValue(Guid id) => byId.GetValueOrDefault(id) is { Control: UiToggleComponent } n ? n.ControlValue != 0 : null;
    public int? SelectedIndex(Guid id) => byId.GetValueOrDefault(id) is { Control: UiDropdownComponent } n ? (int)n.ControlValue : null;
    public bool SetSliderValue(Guid id, float value) => float.IsFinite(value) && SetControl<UiSliderComponent>(id, value);
    public bool SetToggleValue(Guid id, bool value) => SetControl<UiToggleComponent>(id, value ? 1 : 0);
    public bool SetSelectedIndex(Guid id, int index) =>
        byId.GetValueOrDefault(id)?.Control is UiDropdownComponent d && index >= 0 && index < d.Options.Length && SetControl<UiDropdownComponent>(id, index);

    private bool SetControl<T>(Guid id, float value) where T : UiControlComponent
    {
        if (!byId.TryGetValue(id, out var n) || n.Control is not T c) return false;
        n.ControlValue = c.Normalize(value);
        return true;
    }

    public bool FocusWidget(Guid id)
    {
        Arrange();
        if (lists.ContainsKey(id)) return FocusList(id);
        if (!byId.TryGetValue(id, out var n) || !Enabled(n) || !Visible(n)) return false;
        SetFocus(id);
        Reveal(n);
        return true;
    }

    private void Change(Node n, float value)
    {
        value = n.Control!.Normalize(value);
        if (n.ControlValue == value) return;
        n.ControlValue = value;
        controlEvents.Enqueue((n.Object.Id, value));
    }

    // Leave a half-thumb of travel space at each end; pointer coordinates and paint use the same track.
    private static float SliderInset(float width) => Math.Min(8, width * .25f);
    private void Slide(Node n, float x)
    {
        var slider = (UiSliderComponent)n.Control!;
        float inset = SliderInset(n.Box.Width);
        double t = Math.Clamp((x - n.Box.X - inset) / (n.Box.Width - 2 * inset), 0, 1);
        Change(n, (float)(slider.Minimum + t * ((double)slider.Maximum - slider.Minimum)));
    }

    private void UseControl(Node n)
    {
        switch (n.Control)
        {
            case UiToggleComponent: Change(n, n.ControlValue == 0 ? 1 : 0); break;
            case UiDropdownComponent:
                dropdownId = n.Object.Id;
                optionCursor = (int)n.ControlValue;
                firstOption = 0;
                OptionBounds(n);
                break;
            case UiButtonComponent: controlEvents.Enqueue((n.Object.Id, null)); break;
        }
    }

    private void CloseOptions()
    {
        dropdownId = null;
        pressedOption = -1;
    }

    private RectangleF OptionBounds(Node n)
    {
        var options = ((UiDropdownComponent)n.Control!).Options;
        var clip = n.Clip;
        float row = n.Box.Height;
        float above = Math.Max(0, n.Box.Top - clip.Top), below = Math.Max(0, clip.Bottom - n.Box.Bottom);
        bool upwards = below < row * options.Length && above > below;
        float available = upwards ? above : below;
        int count = Math.Min(options.Length, Math.Max(1, (int)(available / row)));
        firstOption = Math.Clamp(firstOption, Math.Max(0, optionCursor - count + 1), Math.Min(optionCursor, options.Length - count));
        return RectangleF.Intersect(clip, new(n.Box.Left, upwards ? n.Box.Top - count * row : n.Box.Bottom, n.Box.Width, count * row));
    }

    private int OptionAt(Node n, float x, float y)
    {
        var box = OptionBounds(n);
        return box.Contains(x, y) ? firstOption + (int)((y - box.Top) / n.Box.Height) : -1;
    }

    private bool ControlKey(string key)
    {
        if (dropdownId is Guid id && byId.TryGetValue(id, out var n) && n.Control is UiDropdownComponent d)
        {
            switch (key)
            {
                case "Up": optionCursor = Math.Max(0, optionCursor - 1); break;
                case "Down": optionCursor = Math.Min(d.Options.Length - 1, optionCursor + 1); break;
                case "Return": case "Space": Change(n, optionCursor); CloseOptions(); break;
                case "Escape": CloseOptions(); break;
                case "Tab": CloseOptions(); return false;
                case "Left": case "Right": break;
                default: return false;
            }
            return true;
        }
        if (key is "Left" or "Right" && focus is Guid sliderId && byId[sliderId] is { Control: UiSliderComponent slider } control)
        {
            Change(control, slider.Advance(control.ControlValue, key == "Left" ? -1 : 1));
            return true;
        }
        return false;
    }

    private void DrawControl(ICanvas2D canvas, Node n, RectangleF box, float scale, ButtonPaint? paint)
    {
        if (n.Control is null or UiButtonComponent) return;
        using var clip = canvas.PushClip(box);
        var ink = Preferences.HighContrast ? Color.White : paint?.Ink ?? Color.White;
        float inset = SliderInset(n.Box.Width) * scale;
        float mid = box.Top + box.Height / 2;
        switch (n.Control)
        {
            case UiSliderComponent s:
                float fraction = s.Maximum > s.Minimum ? (float)((n.ControlValue - (double)s.Minimum) / (s.Maximum - (double)s.Minimum)) : 0;
                float thumb = box.Left + inset + (box.Width - 2 * inset) * fraction;
                canvas.FillRectangle(new(box.Left + inset, mid - scale, box.Width - 2 * inset, 2 * scale), ink);
                canvas.FillRectangle(new(thumb - 3 * scale, box.Top + 3 * scale, 6 * scale, Math.Max(scale, box.Height - 6 * scale)), ink);
                break;
            case UiToggleComponent:
                float side = Math.Min(14 * scale, box.Height * .7f);
                var border = new RectangleF(box.Left + inset, mid - side / 2, side, side);
                canvas.FillRectangle(border, ink);
                var inside = RectangleF.Inflate(border, -scale, -scale);
                var surface = Preferences.HighContrast ? Color.Black : paint?.Fill ?? Color.DarkSlateGray;
                if (inside.Width > 0) canvas.FillRectangle(inside, surface);
                var mark = RectangleF.Inflate(border, -3 * scale, -3 * scale);
                if (n.ControlValue != 0 && mark.Width > 0) canvas.FillRectangle(mark, ink);
                break;
            case UiDropdownComponent d when d.Options.Length > 0:
                float font = 16 * scale * Preferences.TextScale;
                using (canvas.PushClip(new(box.Left, box.Top, Math.Max(0, box.Width - 24 * scale), box.Height)))
                    canvas.DrawText(d.Options[(int)n.ControlValue], box.Left + inset, box.Top + 2 * scale, font, ink);
                canvas.DrawText(dropdownId == n.Object.Id ? "-" : "+", box.Right - 18 * scale, box.Top + 2 * scale, font, ink);
                break;
        }
    }

    private void DrawOptions(ICanvas2D canvas, float x, float y, float scale)
    {
        if (dropdownId is not Guid id || byId[id] is not { Control: UiDropdownComponent d } n) return;
        var box = OptionBounds(n);
        int hover = pointer is {} p ? OptionAt(n, p.X, p.Y) : -1;
        var area = new RectangleF(x + box.X * scale, y + box.Y * scale, box.Width * scale, box.Height * scale);
        using var clip = canvas.PushClip(area);
        for (int i = firstOption; i < d.Options.Length; i++)
        {
            float top = area.Top + (i - firstOption) * n.Box.Height * scale;
            if (top >= area.Bottom) break;
            bool selected = i == optionCursor;
            var state = selected ? ButtonState.Focused : i == hover ? ButtonState.Hover : ButtonState.Normal;
            var paint = n.Palette?[(int)state];
            var fill = Preferences.HighContrast ? Color.Black : paint?.Fill ?? (selected ? Color.SteelBlue : Color.DarkSlateGray);
            var ink = Preferences.HighContrast ? Color.White : paint?.Ink ?? Color.White;
            canvas.FillRectangle(new(area.X, top, area.Width, n.Box.Height * scale), fill);
            if (selected) canvas.FillRectangle(new(area.X, top, 3 * scale, n.Box.Height * scale), ink);
            canvas.DrawText(d.Options[i], area.X + 8 * scale, top + 2 * scale, 16 * scale * Preferences.TextScale, ink);
        }
    }
}
