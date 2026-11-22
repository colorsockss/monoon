using System;
using Engine.Core;

public sealed class DisplayOption : GameBehaviour
{
    public bool ControlsVSync;
    public Guid Hint;
    private bool? shown;

    public override void OnCreate(ScriptLife life)
    {
        bool standalone = life.Session.Display != null;
        life.Ui.SetInteractable(life.ObjectId, standalone);
        if (!ControlsVSync) life.Ui.SetVisible(Hint, !standalone);
    }

    public override void UpdateUi(UiTick tick)
    {
        if (tick.Session.Display is not {} display) return;
        bool actual = ControlsVSync ? display.VSync : display.Fullscreen;
        if (shown == actual) return;
        tick.Ui.SetToggleValue(tick.ObjectId, actual);
        shown = actual;
        tick.Session.Log($"{(ControlsVSync ? "VSync" : "Fullscreen")} = {actual}");
    }

    public override void OnUiValueChanged(UiValueChanged change)
    {
        if (change.Session.Display is not {} display) return;
        bool enabled = change.Value != 0;
        if (ControlsVSync ? display.SetVSync(enabled) : display.SetFullscreen(enabled)) return;
        shown = null;
        change.Ui.SetText(Hint, "The desktop could not apply this setting.");
        change.Ui.SetVisible(Hint, true);
    }
}
