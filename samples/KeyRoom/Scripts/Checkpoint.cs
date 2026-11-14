using System;
using System.IO;
using System.Text.Json;
using Engine.Core;

public sealed class Checkpoint : GameBehaviour
{
    private const int Version = 2;
    private static readonly string[] Progress = { "key", "chest", "coin" };
    private double remaining;

    public override void UpdateUi(UiTick tick)
    {
        var state = tick.Session;
        if (!state.Storage.IsAvailable) return;
        if (tick.Input.Pressed("SlotOne")) state.Clear("slot2");
        if (tick.Input.Pressed("SlotTwo")) state.Set("slot2");
        int slot = state.Has("slot2") ? 2 : 1;
        if (state.Clear("checkpointLoaded")) Show(tick, $"Slot {slot} loaded");
        else if (tick.Input.Pressed("SlotOne") || tick.Input.Pressed("SlotTwo")) Show(tick, $"Slot {slot}");
        else if (remaining > 0 && (remaining -= tick.DeltaTime) <= 0) tick.Ui.SetText(tick.ObjectId, "");

        try
        {
            if (tick.Input.Pressed("SaveGame") && state.SceneId is Guid room)
            {
                var data = new GameData { Version = Version };
                data.Set("room", room.ToString());
                foreach (string flag in Progress) data.Set(flag, state.Has(flag));
                state.Storage.SaveSlot(slot, data);
                Show(tick, $"Slot {slot} saved");
            }
            else if (tick.Input.Pressed("LoadGame"))
            {
                var data = state.Storage.LoadSlot(slot, Version, Migrate);
                if (data == null) { Show(tick, $"Slot {slot} empty"); return; }
                if (!Guid.TryParse(data.GetText("room"), out var savedRoom)) throw new InvalidDataException("Invalid room.");
                foreach (string flag in Progress)
                    if (!data.Values.TryGetValue(flag, out var value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        throw new InvalidDataException("Missing checkpoint flag: " + flag);
                state.LoadScene(savedRoom, restored =>
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
            Show(tick, $"Slot {slot} unavailable");
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

    private void Show(UiTick tick, string message)
    {
        tick.Ui.SetText(tick.ObjectId, message);
        remaining = 3;
    }
}
