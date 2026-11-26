using System;
using System.IO;
using Engine.Core;

public sealed class Trailhead : GameBehaviour
{
    public SceneReference[] Stages = [];
    public Guid Selection, Record;
    private static readonly string[] Names = ["Windward Meadow", "Ember Cavern", "Stormwatch Tower", "Stormwatch Summit"];
    private int slot, stage = 1, unlocked;
    private bool slotReadable;
    private GameData progress = new();

    public override void OnCreate(ScriptLife life)
    {
        slot = TrailProfile.Selected(life.Session);
        ReadSlot(life.Session);
        Show(life.Ui);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("ChangeSlot"))
        {
            slot = slot % 3 + 1;
            TrailProfile.Select(tick.Session, slot);
            ReadSlot(tick.Session);
            Show(tick.Ui);
        }
        int direction = (tick.Input.Pressed("MoveRight") ? 1 : 0) - (tick.Input.Pressed("MoveLeft") ? 1 : 0);
        if (direction != 0)
        {
            stage = Math.Clamp(stage + direction, 1, unlocked);
            Show(tick.Ui);
        }
        if (slotReadable && tick.Input.Pressed("Proceed") && stage <= Stages.Length) tick.Session.LoadScene(Stages[stage - 1].Id);
    }

    private void ReadSlot(SessionState session)
    {
        try
        {
            progress = TrailProfile.Load(session, slot);
            slotReadable = true;
        }
        catch (InvalidDataException)
        {
            progress = new();
            slotReadable = false;
        }
        unlocked = Math.Clamp((int)progress.GetNumber("unlocked", 1), 1, Names.Length);
        stage = unlocked;
    }

    private void Show(IGameUi ui)
    {
        if (!slotReadable)
        {
            ui.SetText(Selection, $"Slot {slot}");
            ui.SetText(Record, "Slot unreadable. Choose another slot.");
            return;
        }
        ui.SetText(Selection, $"Slot {slot}     {stage:D2}  /  {Names[stage - 1]}");
        string prefix = FormattableString.Invariant($"stage{stage}.");
        string record = progress.GetBool(prefix + "clear")
            ? FormattableString.Invariant($"Best   {progress.GetNumber(prefix + "bestTicks") / 60:0.0} s")
            : "No clear recorded";
        if (stage < Names.Length && progress.GetBool(prefix + "clear"))
            record += FormattableString.Invariant($"    /    {progress.GetNumber(prefix + "bestGems")}/3 gems");
        ui.SetText(Record, record);
    }
}
