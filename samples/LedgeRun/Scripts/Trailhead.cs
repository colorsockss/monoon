using System;
using System.IO;
using System.Drawing;
using Engine.Core;

public sealed class Trailhead : GameBehaviour
{
    public SceneReference[] Stages = [];
    public Guid Selection, Record, Accessibility, MotionStatus, DepartLabel;
    public Guid LogoDefault, LogoLargeText;
    public Guid[] SlotMarks = [], SlotBacks = [], SlotTime = [], SlotGems = [], SlotGemIcons = [], SlotStages = [], SlotNew = [];
    public Guid[] SlotStatus = [], SlotTrails = [];
    private static readonly string[] Names = ["Windward Meadow", "Ember Cavern", "Stormwatch Tower", "Stormwatch Summit"];
    private int slot, stage = 1, unlocked;
    private int shownSlot = -1;
    private readonly ScriptTween[] slotTweens = new ScriptTween[3];
    private double departAt;
    private readonly GameData?[] saves = new GameData?[3];
    private GameData? Progress => saves[slot - 1];

    public override void OnCreate(ScriptLife life)
    {
        TrailExpedition.Begin(life.Session);
        life.Session.Screen.Fade(Color.Black, 0);
        life.Session.Screen.Fade(Color.Transparent, .32);
        if (life.Session.Storage.IsAvailable)
        {
            try
            {
                if (life.Session.Storage.LoadSettings(1) is {} settings)
                    life.Ui.Preferences.LoadFrom(settings);
            }
            catch (InvalidDataException)
            {
                life.Session.Log("LedgeRun could not read saved display settings.");
            }
        }
        ShowAccessibility(life.Ui);
        slot = TrailProfile.Selected(life.Session);
        for (int i = 0; i < saves.Length; i++)
        {
            try { saves[i] = TrailProfile.Load(life.Session, i + 1); }
            catch (InvalidDataException) { saves[i] = null; }
            ShowCard(life.Ui, i, saves[i]);
        }
        SelectStage();
        Show(life.Ui, life.Objects);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (departAt > 0)
        {
            if (tick.Session.Time.UiSeconds >= departAt) tick.Session.LoadScene(Stages[stage - 1].Id);
            return;
        }
        if (tick.Input.Pressed("TextSize") || tick.Input.Pressed("Comfort"))
        {
            var prefs = tick.Ui.Preferences;
            if (tick.Input.Pressed("TextSize")) prefs.TextScale = prefs.TextScale < 2 ? 2 : 1;
            if (tick.Input.Pressed("Comfort"))
            {
                prefs.ShakeScale = prefs.ShakeScale > 0 ? 0 : 1;
                prefs.FlashScale = prefs.ShakeScale;
                prefs.Rumble = prefs.ShakeScale > 0;
            }
            ShowAccessibility(tick.Ui);
            if (tick.Session.Storage.IsAvailable)
            {
                var settings = new GameData();
                prefs.SaveTo(settings);
                tick.Session.Storage.SaveSettings(settings);
            }
        }
        if (tick.Input.Pressed("ChangeSlot"))
        {
            slot = slot % 3 + 1;
            TrailProfile.Select(tick.Session, slot);
            SelectStage();
            Show(tick.Ui, tick.Objects);
        }
        int direction = (tick.Input.Pressed("MoveRight") ? 1 : 0) - (tick.Input.Pressed("MoveLeft") ? 1 : 0);
        if (direction != 0)
        {
            stage = Math.Clamp(stage + direction, 1, unlocked);
            Show(tick.Ui, tick.Objects);
        }
        if (Progress != null && tick.Input.Pressed("Proceed") && stage <= Stages.Length)
        {
            tick.Ui.SetText(DepartLabel, "<color=#19516a>Depart</color>");
            tick.Session.Screen.Fade(Color.Black, .14);
            departAt = tick.Session.Time.UiSeconds + .15;
        }
    }

    private void ShowAccessibility(IGameUi ui)
    {
        var prefs = ui.Preferences;
        string motion = prefs.ShakeScale > 0 ? "Full" : "Low";
        ui.SetText(Accessibility, $"{prefs.TextScale:0}x");
        ui.SetText(MotionStatus, motion);
        ui.SetVisible(LogoDefault, prefs.TextScale <= 1);
        ui.SetVisible(LogoLargeText, prefs.TextScale > 1);
    }

    private static int Reached(GameData? data) => Math.Clamp((int)(data?.GetNumber("unlocked", 1) ?? 1), 1, Names.Length);

    private void SelectStage() => stage = unlocked = Reached(Progress);

    private void ShowCard(IGameUi ui, int index, GameData? data)
    {
        int reached = Reached(data);
        bool started = data != null && (reached > 1 || data.GetBool("stage1.clear"));
        for (int leg = 0; leg < Names.Length; leg++)
            ui.SetVisible(SlotStages[index * Names.Length + leg], started && leg < reached);
        ui.SetVisible(SlotNew[index], !started);
        ui.SetVisible(SlotTrails[index], started);
        ui.SetVisible(SlotTime[index], started);
        ui.SetVisible(SlotGemIcons[index], started);
        ui.SetVisible(SlotGems[index], started);
        if (data == null || !started)
        {
            ui.SetText(SlotStatus[index], data == null ? "<b>ERR</b>" : "<b>NEW</b>");
            return;
        }
        double seconds = data.GetNumber("bestExpeditionTicks") / 60.0;
        string time = seconds <= 0 ? "--" : seconds < 999.95
            ? FormattableString.Invariant($"{seconds:0.0}s")
            : FormattableString.Invariant($"{Math.Min(999, (int)(seconds / 60))}m");
        ui.SetText(SlotTime[index], time);
        double gems = data.GetNumber("stage1.bestGems") + data.GetNumber("stage2.bestGems") + data.GetNumber("stage3.bestGems");
        ui.SetText(SlotGems[index], Math.Clamp((int)gems, 0, 9).ToString());
    }

    private void Show(IGameUi ui, IGameObjects objects)
    {
        if (shownSlot != slot)
        {
            for (int i = 0; i < SlotMarks.Length; i++)
            {
                bool selected = i + 1 == slot;
                ui.SetVisible(SlotMarks[i], selected);
                ui.SetVisible(SlotBacks[i], !selected);
                slotTweens[i].Stop();
                if (selected)
                    slotTweens[i] = objects.Tween(SlotMarks[i], TweenClock.Ui).UiRect(RectangleF.Empty, 0)
                        .UiRect(new RectangleF(-4, -4, 8, 8),
                                ui.Preferences.ShakeScale > 0 ? .17f : 0, TweenEase.EaseOut);
            }
            shownSlot = slot;
        }
        if (Progress is not { } progress)
        {
            ui.SetText(Selection, "Unreadable save");
            ui.SetText(Record, "Choose another slot.");
            return;
        }
        ui.SetText(Selection, Names[stage - 1]);
        string prefix = FormattableString.Invariant($"stage{stage}.");
        string record = progress.GetBool(prefix + "clear")
            ? FormattableString.Invariant($"Leg best  {progress.GetNumber(prefix + "bestTicks") / 60:0.0} s")
            : "First delivery";
        if (stage < Names.Length && progress.GetBool(prefix + "clear"))
            record += FormattableString.Invariant($"  /  {progress.GetNumber(prefix + "bestTokens")} T  /  {progress.GetNumber(prefix + "bestGems")} G");
        ui.SetText(Record, record);
    }
}
