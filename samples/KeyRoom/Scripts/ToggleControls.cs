using Engine.Core;
using System;
using System.IO;

public sealed class ToggleControls : GameBehaviour
{
    public Guid Hint, CheckpointHint;
    private InputDevice? shown;
    private int revision;
    private string locale;
    private GameData settings;

    public override void OnCreate(ScriptLife life)
    {
        if (!life.Session.Storage.IsAvailable) return;
        try
        {
            settings = life.Session.Storage.LoadSettings(1) ?? new GameData();
            life.Session.LoadInputBindings(settings); // Controls chosen on the K screen survive a restart.
            if (settings.GetBool("showControls")) life.Session.Set("controls");
            else life.Session.Clear("controls");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { Console.Error.WriteLine(ex.Message); }
    }

    public override void UpdateUi(UiTick tick)
    {
        var device = tick.Input.ActiveDevice;
        if (shown != device || revision != tick.Session.InputRevision || locale != tick.Ui.Preferences.Locale)
        {
            locale = tick.Ui.Preferences.Locale;
            shown = device;
            revision = tick.Session.InputRevision;
            string Binding(string action) => (tick.Session.BindingFor(action,device) ??
                tick.Session.BindingFor(action,InputDevice.Keyboard) ?? "—").Replace("Mouse.", "Mouse ").Replace("Pad.", "");
            tick.Ui.SetText(CheckpointHint, string.Format(tick.Ui.Translate("hud.slots"), Binding("SlotOne"), Binding("SlotTwo"), Binding("SaveGame"), Binding("LoadGame"), Binding("RoomGuide")));
            tick.Ui.SetText(Hint, string.Format(tick.Ui.Translate("hud.bindings"), Binding("MoveLeft"), Binding("MoveRight"), Binding("Interact"), Binding("ShowControls"), Binding("PauseGame")));
        }
        if (tick.Input.Pressed("ShowControls")) Toggle(tick.Session);
    }

    public override void OnUiClick(UiClick click) => Toggle(click.Session);

    private void Toggle(SessionState state)
    {
        if (!state.Clear("controls")) state.Set("controls");
        if (settings == null) return;
        try
        {
            settings = state.Storage.LoadSettings(1) ?? settings; // Keep bindings saved since this scene started.
            settings.Set("showControls", state.Has("controls"));
            state.Storage.SaveSettings(settings);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { Console.Error.WriteLine(ex.Message); }
    }
}
