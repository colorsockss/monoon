using Engine.Core;

// One cell of the controls screen: shows and captures the first keyboard or pad control of an action.
public sealed class RebindButton : GameBehaviour
{
    public string Action = "Interact";
    public bool Gamepad;
    private string shown;

    private InputDevice Device => Gamepad ? InputDevice.Gamepad : InputDevice.Keyboard;

    public override void UpdateUi(UiTick tick)
    {
        var session = tick.Session;
        bool waiting = session.RebindingAction == Action && session.RebindingDevice == Device;
        var text = waiting ? "…" : KeyBindingsMenu.Label(session.BindingFor(Action, Device));
        if (text != shown && tick.Ui.SetText(tick.ObjectId, text)) shown = text;
    }

    public override void OnUiClick(UiClick click) => click.Session.BeginRebind(Action, Device);
}
