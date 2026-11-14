using Engine.Core;
using System;
using System.IO;

public sealed class ToggleControls : GameBehaviour
{
    public Guid Hint, CheckpointHint;
    private InputDevice? shown;
    private int revision;
    private GameData settings;

    public override void OnCreate(ScriptLife life)
    {
        if (!life.Session.Storage.IsAvailable) return;
        try
        {
            settings = life.Session.Storage.LoadSettings(1) ?? new GameData();
            if (settings.GetBool("showControls")) life.Session.Set("controls");
            else life.Session.Clear("controls");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { Console.Error.WriteLine(ex.Message); }
    }

    public override void UpdateUi(UiTick tick)
    {
        var device = tick.Input.ActiveDevice;
        if (shown != device || revision != tick.Session.InputRevision)
        {
            shown = device;
            revision = tick.Session.InputRevision;
            string Binding(string action) => (tick.Session.BindingFor(action,device) ??
                tick.Session.BindingFor(action,InputDevice.Keyboard) ?? "—").Replace("Mouse.", "Mouse ").Replace("Pad.", "");
            tick.Ui.SetText(CheckpointHint, $"{Binding("SlotOne")} / {Binding("SlotTwo")}: Slot   {Binding("SaveGame")}: Save   {Binding("LoadGame")}: Load   {Binding("RoomGuide")}: Guide");
            tick.Ui.SetText(Hint, $"Move: {Binding("MoveLeft")} / {Binding("MoveRight")}   Interact: {Binding("Interact")}   Help: {Binding("ShowControls")}   Pause: {Binding("PauseGame")}");
        }
        if (tick.Input.Pressed("ShowControls")) Toggle(tick.Session);
    }

    public override void OnUiClick(UiClick click) => Toggle(click.Session);

    private void Toggle(SessionState state)
    {
        if (!state.Clear("controls")) state.Set("controls");
        if (settings == null) return;
        settings.Set("showControls", state.Has("controls"));
        try { state.Storage.SaveSettings(settings); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { Console.Error.WriteLine(ex.Message); }
    }
}
