using System;
using System.IO;
using Engine.Core;

// Shows the result of each rebind and stores the player's controls in player.settings.
public sealed class KeyBindingsMenu : GameBehaviour
{
    public Guid StatusLabel, FirstButton;
    private (string Capturing, InputRebindResult Last, int Revision, string Locale)? shown;

    public static string Label(string control) => (control ?? "—").Replace("Mouse.", "Mouse ").Replace("Pad.", "");

    public override void UpdateUi(UiTick tick)
    {
        var session = tick.Session;
        var state = (session.RebindingAction, session.LastRebind, session.InputRevision, tick.Ui.Preferences.Locale);
        if (shown == state) return;
        var ui = tick.Ui;
        string Name(string action) => ui.Translate("action." + action, action);
        string text;
        if (shown == null)
        {
            ui.FocusWidget(FirstButton); // Keyboard and pad players start on the first control.
            text = ui.Translate("rebind.choose");
        }
        else if (state.RebindingAction is { } action)
            text = string.Format(ui.Translate(session.RebindingDevice == InputDevice.Gamepad ? "rebind.waitPad" : "rebind.waitKey"), Name(action));
        else if (state.LastRebind is { Cancelled: true } cancelled)
            text = string.Format(ui.Translate("rebind.cancelled"), Name(cancelled.Action), Label(cancelled.Previous));
        else if (state.LastRebind is { } done)
            text = done.Swapped.Length == 0 ? string.Format(ui.Translate("rebind.done"), Name(done.Action), Label(done.Control)) :
                string.Format(ui.Translate("rebind.swapped"), Name(done.Action), Label(done.Control), Name(done.Swapped[0]), Label(done.Previous));
        else text = ui.Translate(state.InputRevision != shown.Value.Revision ? "rebind.restored" : "rebind.choose");
        ui.SetText(StatusLabel, text);
        if (shown != null && state.InputRevision != shown.Value.Revision) Save(session);
        shown = state;
    }

    private static void Save(SessionState session)
    {
        if (!session.Storage.IsAvailable) return;
        try
        {
            var settings = session.Storage.LoadSettings(1) ?? new GameData();
            session.SaveInputBindings(settings);
            session.Storage.SaveSettings(settings);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { session.Log(ex.Message, GameLogLevel.Error); }
    }
}
