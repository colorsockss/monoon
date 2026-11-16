using System;
using System.IO;
using System.Text.Json;
using Engine.Core;

public sealed class Checkpoint : GameBehaviour
{
    private const int Version = 2;
    private static readonly string[] Progress = { "key", "chest", "coin" };
    private double remaining;
    private string statusKey = "", statusLocale = "";
    private int statusSlot;

    public override void UpdateUi(UiTick tick)
    {
        var state = tick.Session;
        if (!state.Storage.IsAvailable) return;
        if (tick.Input.Pressed("SlotOne")) state.Clear("slot2");
        if (tick.Input.Pressed("SlotTwo")) state.Set("slot2");
        int slot = state.Has("slot2") ? 2 : 1;
        if (state.Clear("checkpointLoaded")) Show(tick, "slot.loaded", slot);
        else if (tick.Input.Pressed("SlotOne") || tick.Input.Pressed("SlotTwo")) Show(tick, "slot.number", slot);
        else if (remaining > 0 && (remaining -= tick.DeltaTime) <= 0) tick.Ui.SetText(tick.ObjectId, "");

        if (remaining > 0 && statusLocale != tick.Ui.Preferences.Locale) Refresh(tick);

        try
        {
            if (tick.Input.Pressed("SaveGame") && state.SceneId is Guid room)
            {
                var data = new GameData { Version = Version };
                data.Set("room", room.ToString());
                foreach (string flag in Progress) data.Set(flag, state.Has(flag));
                state.SaveWorld(data);
                state.Storage.SaveSlot(slot, data);
                Show(tick, "slot.saved", slot);
            }
            else if (tick.Input.Pressed("LoadGame"))
            {
                var data = state.Storage.LoadSlot(slot, Version, Migrate);
                if (data == null) { Show(tick, "slot.empty", slot); return; }
                if (!Guid.TryParse(data.GetText("room"), out var savedRoom)) throw new InvalidDataException("Invalid room.");
                foreach (string flag in Progress)
                    if (!data.Values.TryGetValue(flag, out var value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        throw new InvalidDataException("Missing checkpoint flag: " + flag);
                state.LoadSavedScene(savedRoom, data, restored =>
                {
                    foreach (string flag in Progress)
                        if (data.GetBool(flag)) restored.Set(flag); else restored.Clear(flag);
                    restored.Set("checkpointLoaded");
                });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.Error.WriteLine(ex.Message);
            Show(tick, "slot.unavailable", slot);
        }
    }

    private static void Migrate(int from, GameData data)
    {
        if (from != 1) throw new InvalidDataException("Unsupported checkpoint version.");
        data.Set("key", data.GetBool("hasKey"));
        data.Values.Remove("hasKey");
        data.Set("chest", false);
        data.Set("coin", false);
    }

    private void Refresh(UiTick tick)
    {
        statusLocale = tick.Ui.Preferences.Locale;
        tick.Ui.SetText(tick.ObjectId, string.Format(tick.Ui.Translate(statusKey), statusSlot));
    }

    private void Show(UiTick tick, string key, int slot)
    {
        statusKey = key;
        statusSlot = slot;
        Refresh(tick);
        remaining = 3;
    }
}
