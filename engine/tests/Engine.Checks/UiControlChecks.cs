using Engine.Core;

internal static class UiControlChecks
{
    [Check(9200)] static void Registered(CheckRun c) => Run();
    internal static void Run()
    {
        void Check(bool ok, string label)
        {
            if (!ok) throw new Exception("UI widgets: " + label);
        }
        var scene = SceneDocument.Empty(new());
        var canvas = scene.CreateUi("Canvas", null);
        var slider = scene.CreateUi("Slider", canvas);
        var toggle = scene.CreateUi("Toggle", canvas);
        var menu = scene.CreateUi("Dropdown", canvas);
        var button = scene.CreateUi("Button", canvas);
        slider.Y = 10; toggle.Y = 50; menu.Y = 90; button.Y = 122;
        var range = slider.GetComponent<UiSliderComponent>()!;
        range.Minimum = -2; range.Maximum = 5; range.Step = 3; range.Initial = 1;
        menu.GetComponent<UiDropdownComponent>()!.Options = ["A", "B", "C"];
        scene.Validate();
        var ui = new ScreenUi(scene, null);
        var changes = new List<(Guid, float?)>();
        void Flush() => ui.Dispatch(id => changes.Add((id, null)), (id, value) => changes.Add((id, value)));
        void Click(SceneObject o)
        {
            var b = ui.Bounds(o.Id)!.Value;
            ui.Pointer(true,b.X + 5,b.Y + 5); ui.Pointer(false,b.X + 5,b.Y + 5);
        }
        void Tap(string key) { ui.Key(key,true); ui.Key(key,false); }
        Check(ui.FocusWidget(slider.Id) && ui.SliderValue(slider.Id) == 1, "initial value and focus");
        Tap("Right"); Tap("Right"); Tap("Left");
        Check(ui.SliderValue(slider.Id) == 4, "non-divisible final interval");
        var box = ui.Bounds(slider.Id)!.Value;
        ui.Pointer(true,box.Right - 1,box.Top + 2);
        ui.Drag(-100,box.Y); ui.Pointer(false,-100,box.Y);
        Check(ui.SliderValue(slider.Id) == -2, "drag capture clamps outside the track");
        Flush(); changes.Clear();
        Click(button); Click(toggle); Flush();
        Check(changes.SequenceEqual(new (Guid,float?)[] {(button.Id,null), (toggle.Id,1)}), "input order and one change per click");
        Check(ui.ToggleValue(toggle.Id) == true && ui.SetToggleValue(toggle.Id,false) && ui.ToggleValue(toggle.Id) == false, "silent script setter");
        ui.FocusWidget(menu.Id); ui.PadKey("Pad.South"); ui.PadKey("Pad.DpadDown"); ui.PadKey("Pad.East");
        Check(!ui.DropdownOpen && ui.SelectedIndex(menu.Id) == 0, "cancel without committing");
        ui.PadKey("Pad.South"); ui.PadKey("Pad.DpadDown"); ui.PadKey("Pad.South");
        Check(ui.SelectedIndex(menu.Id) == 1, "pad confirms selected option");
        Click(menu); Click(button);
        Check(ui.SelectedIndex(menu.Id) == 0, "open list receives click above overlapping button");
        Flush();
        Check(changes.Count == 4 && changes[^1] == (menu.Id,0), "programmatic change did not emit or click through");
        ui.SetInteractable(slider.Id,false); Click(slider);
        Check(!ui.FocusWidget(slider.Id) && ui.SliderValue(slider.Id) == -2, "disabled control");
        Check(!ui.SetSelectedIndex(menu.Id,3) && !ui.SetSliderValue(slider.Id,float.NaN), "reject invalid script values");
        Click(menu); ui.SetVisible(menu.Id,false);
        Check(!ui.DropdownOpen, "hiding widget dismisses its options");
        Check(scene.SchemaVersion == 26 && range.Initial == 1 && !toggle.GetComponent<UiToggleComponent>()!.Initial,
            "runtime values are separate from the authored defaults");
        Console.WriteLine("PASS UI controls: drag, snapping, pad navigation, popup capture, cancellation, value events and authored state");
    }
}
