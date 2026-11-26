using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

public sealed partial class Expedition
{
    public Guid PartyPanel, ProgressPanel, PulsePanel, PromptPanel, WavePanel, BossPanel, ChapterPanel, MenuPanel;
    public Guid[] PartySlots = [], PartyFill = [], PartyDown = [], PulseSegments = [], WaveMarks = [];
    public Guid Crowns, Level, ExperienceTrack, Experience, PulseCaption, PulseShine, BannerSkin, ScrollSkin, Prompt, PromptKeyText, GateChip, RestChip, FoeCount, BossTell, BossFill,
        ChapterTitle, ChapterLine, ChapterActions, ChapterGate, ChapterRetreat, MenuTag, MenuTitle, MenuLine, MenuLine2, MenuRule, MenuSeal, ShopPanel, HireLine, GearLine;
    public Guid[] Sounds = [], CombatEmitters = [], Numbers = [], NumberInk = [], NumberOutline = [],
        Arrows = [], Loot = [], MenuRecord = [], MenuRecordLabels = [], MenuButtons = [],
        ButtonFocus = [], ButtonDown = [], MenuHelpRows = [], ShopPrices = [];
    public Guid Ring, ShieldRing, HealGlyph, Warning, Cracks, Saber, WarFlag, Waypoint;

    private int nextNumber, nextArrow, nextLoot, lastImpact;
    private readonly Floating[] numbers = new Floating[12];
    private readonly Arrow[] arrows = new Arrow[8];
    private readonly CrownDrop[] drops = new CrownDrop[12];
    private static readonly string[] PulseCaptions = ["Pulse Ready", "Pulse 1s", "Pulse 2s", "Pulse 3s", "Pulse 4s", "Pulse 5s", "Pulse 6s"];
    private static readonly Vector2[] UnitCircle = Circle();
    private static readonly Vector2[] LabelOffsets =
    [
        Vector2.Zero, new(-23, 1), new(23, 1), new(-42, 5), new(42, 5),
        new(-59, -3), new(59, -3), new(0, -20), new(-31, -18), new(31, -18),
        new(-66, -21), new(66, -21)
    ];
    private const int PulseMedalSize = 26;
    private const int ExperienceBarWidth = 98;
    private const int NumberDuration = 36;
    private const int OutlinePasses = 4;
    private static readonly string[] NumberLabels = Values(100, "", "");
    private static readonly string[] DamageLabels = Values(32, "<b><color=#FFE5A3>", "</color></b>");
    private static readonly string[] HurtLabels = Values(32, "<b><color=#FFADAA>", "</color></b>");
    private static readonly string[] HealLabels = Values(32, "<b><color=#ACFFBE>+", "</color></b>");
    private static readonly string[] DamageOutline = Values(32, "<b>", "</b>");
    private static readonly string[] HealOutline = Values(32, "<b>+", "</b>");
    private string? currentPrompt;
    private enum PromptKey { None, Gate, Rest }
    private PromptKey promptKey;
    private bool wavePrompt, waveShown, promptShown;
    private int shownGroup = -1, shownDefeated = -1, shownCrowns = -1, shownLevel = -1, shownXp = -1;
    private float badgeScale;
    private readonly ScriptTween[] healthMotion = new ScriptTween[5];
    private ScriptTween crownMotion, xpMotion, menuMotion;
    private int primaryAction = -1, secondaryAction = -1, selectedAction = -1, armedAction = -1, pressedAction = -1;
    private enum LedgerAction { Muster, Resume, Haven, NewRun, Close }
    private Vector2 lastMenuPointer = new(float.NaN);
    private int hoveredAction = -1;

    private int lastRoster = -1;
    private readonly float[] lastHealth = [-1, -1, -1, -1, -1];
    private Vector2 ringCenter;
    private int ringTicks, shieldTicks, healTicks, healTarget = -1;
    private Vector2 shieldCenter, healCenter;
    private int peakParticles;
    private bool measureParticles;
    private int bowShots, nextSoul;
    private readonly int[] nextImpact = new int[3];
    private ScriptTween bannerMotion, scrollMotion, shieldFade, healFade, warningFade;
    private bool warningShown;
    private const int RingDuration = 41;
    private enum Effect { Steel, Brass, Feathers, Critical, Jade, Mud, WispSmoke, Stone, Soul, Bannerglow }

    private IGameObjects sceneObjects = null!;
    private bool scrollNotice;
    private float hudTextScale;
    private int progressInset, pulseExtraWidth;
    private int chargeStep = -1, shineTicks, cooldownLabel = -1;

    private struct Floating
    {
        public Vector2 Start;
        public float HalfWidth, Scale;
        public int Age, Target;
    }

    private struct Arrow
    {
        public Vector2 Position;
        public int Target, Damage, Ticks;
        public bool Critical;
    }

    private struct CrownDrop
    {
        public Vector2 Origin, Drift;
        public int Age;
    }

    private static Vector2[] Circle()
    {
        var points = new Vector2[32];
        for (int i = 0; i < 32; i++)
        {
            float radians = i * (MathF.Tau / 32);
            points[i] = new(MathF.Cos(radians), MathF.Sin(radians));
        }
        return points;
    }

    private static string NumberLabel(int value) => (uint)value < (uint)NumberLabels.Length
        ? NumberLabels[value] : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string[] Values(int length, string start, string end)
    {
        var strings = new string[length];
        for (int i = 0; i < length; i++) strings[i] = start + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + end;
        return strings;
    }

    private void OpenMenu(IGameUi ui, Menu mode)
    {
        menu = mode;
        bool showing = mode != Menu.None;
        ui.SetVisible(MenuPanel, showing);
        ui.SetVisible(PartyPanel, !showing);
        ui.SetVisible(ProgressPanel, !showing);
        ui.SetVisible(PulsePanel, !showing && Zone != 0);
        ui.SetVisible(BossPanel, !showing && GuardianActive);
        ui.SetVisible(ChapterPanel, !showing && chapterTicks > 0 && !GuardianActive);
        ui.SetVisible(ShopPanel, !showing && nearShop);
        UpdateObjective(ui);

        bool record = mode == Menu.Ending;
        foreach (Guid line in MenuRecordLabels) ui.SetVisible(line, record);
        foreach (Guid line in MenuRecord) ui.SetVisible(line, record);
        ui.SetVisible(MenuRule, record);
        ui.SetVisible(MenuSeal, record);
        bool help = mode == Menu.Help;
        for (int i = 0; i < MenuHelpRows.Length; i++)
            ui.SetVisible(MenuHelpRows[i], help && (i == 0 || (Zone == 0 ? i < 4 : i >= 4)));
        ui.SetVisible(MenuLine, !help);
        ui.SetVisible(MenuLine2, !help);

        primaryAction = mode switch
        {
            Menu.Title => (int)(Read("route") == 0 ? LedgerAction.Muster : LedgerAction.Resume),
            Menu.Ending => (int)LedgerAction.Haven,
            Menu.Help => (int)LedgerAction.Close,
            _ => -1
        };
        secondaryAction = record ? (int)LedgerAction.NewRun : -1;
        selectedAction = pressedAction = armedAction = hoveredAction = -1;
        lastMenuPointer = new(float.NaN);
        for (int i = 0; i < MenuButtons.Length; i++)
        {
            ui.SetVisible(MenuButtons[i], i == primaryAction || i == secondaryAction);
            ui.SetVisible(ButtonFocus[i], false);
            ui.SetVisible(ButtonDown[i], false);
        }
        UpdateActionLook(ui, primaryAction, -1);

        switch (mode)
        {
            case Menu.Title:
                MenuCopy(ui, "LANTERN COMPANY", "IsoBand", "Three wards remain.", "Recover the relic.");
                break;
            case Menu.Ending:
                MenuCopy(ui, "Expedition complete", "Light returns", "The ward is broken.", $"{coins} crowns recovered");
                ShowExpeditionRecord(ui);
                break;
            case Menu.Help:
                MenuCopy(ui, "LANTERN COMPANY", "Field notes", "", "");
                break;
        }
        menuMotion.Stop();
        if (showing)
            menuMotion = sceneObjects.Tween(MenuPanel, TweenClock.Ui).UiRect(new(0, 6, 0, 0), 0)
                .UiRect(RectangleF.Empty, .16f, TweenEase.EaseOut);
    }

    private void MenuCopy(IGameUi ui, string tag, string title, string line, string detail)
    {
        ui.SetText(MenuTag, tag);
        ui.SetText(MenuTitle, title);
        ui.SetText(MenuLine, line);
        ui.SetText(MenuLine2, detail);
    }

    private void ShowExpeditionRecord(IGameUi ui)
    {
        for (int i = 0; i < RegionTotals.Length; i++)
            ui.SetText(MenuRecord[i], $"{RegionTotals[i]}");
        ui.SetText(MenuRecord[3], $"{Read("finalLevel", level)}");
        ui.SetText(MenuRecord[4], $"{Read("finalBand", PartyCount)}");
        int seconds = fieldTicks / 60;
        ui.SetText(MenuRecord[5], fieldTicks < 0 ? "--" :
            FormattableString.Invariant($"{seconds / 60}:{seconds % 60:00}"));
    }

    private void UpdateActionLook(IGameUi ui, int selected, int pressed)
    {
        if (selectedAction != selected)
        {
            if (selectedAction >= 0) ui.SetVisible(ButtonFocus[selectedAction], false);
            if (selected >= 0) ui.SetVisible(ButtonFocus[selected], true);
            selectedAction = selected;
        }
        if (pressedAction != pressed)
        {
            if (pressedAction >= 0) ui.SetVisible(ButtonDown[pressedAction], false);
            if (pressed >= 0) ui.SetVisible(ButtonDown[pressed], true);
            pressedAction = pressed;
        }
    }

    private bool PointerOver(ScriptTick tick, int index) =>
        index >= 0 && tick.Input.PointerInside && tick.Ui.Bounds(MenuButtons[index]) is { } rect &&
        rect.Contains(tick.Input.PointerPosition.X, tick.Input.PointerPosition.Y);

    private void UpdateMenu(ScriptTick tick)
    {
        bool down = tick.Input.Pressed("ClickMove"), up = tick.Input.Released("ClickMove");
        Vector2 pointer = tick.Input.PointerInside ? tick.Input.PointerPosition : new(-1, -1);
        if (pointer != lastMenuPointer || down || up)
        {
            hoveredAction = PointerOver(tick, secondaryAction) ? secondaryAction :
                PointerOver(tick, primaryAction) ? primaryAction : -1;
            lastMenuPointer = pointer;
        }
        int hover = hoveredAction;
        if (down) armedAction = hover;
        int clicked = -1;
        if (up)
        {
            if (armedAction == hover) clicked = hover;
            armedAction = -1;
        }
        UpdateActionLook(tick.Ui, hover >= 0 ? hover : primaryAction, hover == armedAction ? armedAction : -1);

        if (menu == Menu.Help)
        {
            if (clicked == primaryAction) OpenMenu(tick.Ui, Menu.None);
            return;
        }
        if (menu == Menu.Ending && (tick.Input.Pressed("NewExpedition") || clicked == (int)LedgerAction.NewRun))
        {
            RestartExpedition(tick);
            return;
        }
        if (!tick.Input.Pressed("Travel") && clicked != primaryAction) return;
        if (menu == Menu.Title)
        {
            tick.Session.Set(Started);
            Notice(tick.Ui, "Muster the band", "Visit Ysra by the fire.");
        }
        OpenMenu(tick.Ui, Menu.None);
        PlaySound(tick, Sound.Crown);
    }

    private void Notice(IGameUi ui, string title, string line, bool scroll = false, bool gate = false)
    {
        scrollNotice = scroll;
        ui.SetVisible(BannerSkin, !scroll);
        ui.SetVisible(ScrollSkin, scroll);
        ui.SetVisible(ChapterTitle, !scroll);
        ui.SetVisible(ChapterLine, !scroll && !gate);
        ui.SetVisible(ChapterActions, gate);
        if (gate)
        {
            ui.SetText(ChapterGate, Zone == 3 ? "To Haven" : "Gate");
            ui.SetVisible(ChapterRetreat, Zone != 3);
        }
        ui.SetText(ChapterTitle, scroll ? $"<color=#432719>{title}</color>" : title);
        ui.SetText(ChapterLine, scroll ? $"<color=#56331F>{line}</color>" : line);
        ui.SetVisible(ChapterPanel, menu == Menu.None && !GuardianActive);
        ref var motion = ref (scroll ? ref scrollMotion : ref bannerMotion);
        motion.Stop();
        motion = sceneObjects.Tween(scroll ? ScrollSkin : ChapterPanel);
        motion.UiRect(scroll ? new(87, 0, -174, 0) : new(0, -54, 0, 0), 0)
            .UiRect(RectangleF.Empty, .20f, TweenEase.EaseOut);
        chapterTicks = 108;
        UpdateObjective(ui);
    }

    private void UpdateNotice(ScriptTick tick)
    {
        if (chapterTicks <= 0) return;
        if (--chapterTicks == 0)
        {
            tick.Ui.SetVisible(ChapterPanel, false);
            UpdateObjective(tick.Ui);
        }
        else if (scrollNotice && chapterTicks == 94)
        {
            tick.Ui.SetVisible(ChapterTitle, true);
            tick.Ui.SetVisible(ChapterLine, true);
        }
        else if (chapterTicks == 12)
        {
            if (scrollNotice)
            {
                tick.Ui.SetVisible(ChapterTitle, false);
                tick.Ui.SetVisible(ChapterLine, false);
                scrollMotion = tick.Objects.Tween(ScrollSkin).UiRect(new(87, 0, -174, 0), .18f);
            }
            else bannerMotion = tick.Objects.Tween(ChapterPanel).UiRect(new(0, -54, 0, 0), .18f);
        }
    }

    private void SetPrompt(IGameUi ui, string? text, PromptKey key = PromptKey.None, bool wave = false)
    {
        if (currentPrompt == text && promptKey == key && wavePrompt == wave) return;
        currentPrompt = text;
        promptKey = key;
        wavePrompt = wave;
        ui.SetVisible(GateChip, key == PromptKey.Gate);
        ui.SetVisible(RestChip, key == PromptKey.Rest);
        ui.SetVisible(Prompt, key == PromptKey.None);
        ui.SetVisible(PromptKeyText, key != PromptKey.None);
        if (text != null) ui.SetText(key == PromptKey.None ? Prompt : PromptKeyText, text);
        UpdateObjective(ui);
    }

    private void UpdateObjective(IGameUi ui)
    {
        bool showWave = menu == Menu.None && wavePrompt && !GuardianActive && chapterTicks == 0;
        bool showPrompt = menu == Menu.None && currentPrompt != null && (Zone == 0 || chapterTicks == 0);
        if (waveShown != showWave)
        {
            ui.SetVisible(WavePanel, showWave);
            waveShown = showWave;
        }
        if (promptShown != showPrompt)
        {
            ui.SetVisible(PromptPanel, showPrompt);
            promptShown = showPrompt;
        }
    }

    private void RefreshHud(IGameObjects objects, IGameUi ui)
    {
        int roster = PartyCount;
        if (roster != lastRoster)
        {
            for (int i = 0; i < party.Length; i++) ui.SetVisible(PartySlots[i], i < roster);
            objects.Tween(PartyPanel).UiRect(new(0, 0, -22 * (party.Length - roster), 0), 0);
            lastRoster = roster;
        }
        for (int i = 0; i < roster; i++)
        {
            ref var member = ref party[i];
            float fraction = member.Health / (float)member.Max;
            if (lastHealth[i] == fraction) continue;
            if (lastHealth[i] < 0 || (lastHealth[i] == 0) != (fraction == 0))
            {
                ui.SetVisible(PartyDown[i], fraction == 0);
                ui.SetVisible(PartyFill[i], fraction > 0);
            }
            healthMotion[i].Stop();
            healthMotion[i] = objects.Tween(PartyFill[i]).UiRect(
                new(0, 0, -18 * (1 - Math.Clamp(fraction, 0, 1)), 0), lastHealth[i] < 0 ? 0 : .14f);
            lastHealth[i] = fraction;
        }
        if (shownCrowns != coins)
        {
            ui.SetText(Crowns, coins > 9999 ? "9k+" : NumberLabel(coins));
            if (shownCrowns >= 0)
            {
                crownMotion.Stop();
                crownMotion = objects.Tween(Crowns).UiRect(new(0, -2, 0, 0), 0).UiRect(RectangleF.Empty, .14f);
            }
            shownCrowns = coins;
            ResizeProgress(objects, ui.Preferences.TextScale, TweenClock.Game);
        }
        if (shownLevel != level)
        {
            shownLevel = level;
            ui.SetText(Level, NumberLabels[Math.Min(level, 99)]);
            badgeScale = 0;
        }
        if (shownXp != xp)
        {
            if (shownXp < 0 || (shownXp % 12 == 0) != (xp % 12 == 0)) ui.SetVisible(Experience, xp % 12 != 0);
            UpdateExperience(objects, shownXp < 0 ? 0 : .16f, TweenClock.Game);
            shownXp = xp;
        }
        if (shownGroup != group)
        {
            for (int i = 0; i < WaveMarks.Length; i++) ui.SetVisible(WaveMarks[i], i <= group);
            shownGroup = group;
        }
        if (shownDefeated != defeated)
        {
            ui.SetText(FoeCount, $"{defeated}/{foes.Length}");
            shownDefeated = defeated;
        }
    }

    public override void UpdateUi(UiTick tick) => LayoutHud(tick.Ui);

    private void LayoutHud(IGameUi ui)
    {
        float scale = ui.Preferences.TextScale;
        if (badgeScale != scale)
        {
            float extra = shownLevel >= 10 ? 5 * scale : 0;
            sceneObjects.Tween(Level, TweenClock.Ui).UiRect(new(-extra * .5f, 0, extra, 0), 0);
            badgeScale = scale;
        }
        if (hudTextScale == scale) return;
        hudTextScale = scale;
        ResizePulse(scale, TweenClock.Ui);
        ResizeProgress(sceneObjects, scale, TweenClock.Ui);
    }

    private void ResizePulse(float scale, TweenClock clock)
    {
        // The authored field after the medal has 60 px plus 8 px of padding.
        // GowunDodum's longest ready and cooldown captions measure 49 and 34 px.
        int extra = Math.Max(0, (int)MathF.Ceiling((cooldownLabel == 0 ? 49 : 34) * scale) - 60);
        if (pulseExtraWidth == extra) return;
        pulseExtraWidth = extra;
        sceneObjects.Tween(PulsePanel, clock).UiRect(new(0, 0, extra, 0), 0);
    }

    private void ResizeProgress(IGameObjects objects, float scale, TweenClock clock)
    {
        int inset = scale <= 1.2f ? 0 : coins switch { < 100 => 20, < 1000 or > 9999 => 8, _ => 0 };
        if (progressInset == inset) return;
        progressInset = inset;
        objects.Tween(ProgressPanel, clock).UiRect(new(inset, 0, -inset, 0), 0);
        objects.Tween(ExperienceTrack, clock).UiRect(new(0, 0, -inset, 0), 0);
        UpdateExperience(objects, 0, clock);
    }

    private void UpdateExperience(IGameObjects objects, float seconds, TweenClock clock)
    {
        float fraction = (xp % 12) / 12f;
        xpMotion.Stop();
        xpMotion = objects.Tween(Experience, clock).UiRect(new(0, 0, -(ExperienceBarWidth - progressInset) * (1 - fraction) - progressInset, 0), seconds);
    }

    private void PulseHud(IGameUi ui, TweenClock clock = TweenClock.Game)
    {
        int seconds = (skill + 59) / 60;
        if (seconds != cooldownLabel)
        {
            bool readyChanged = (cooldownLabel == 0) != (seconds == 0);
            cooldownLabel = seconds;
            ui.SetText(PulseCaption, PulseCaptions[seconds]);
            if (readyChanged) ResizePulse(ui.Preferences.TextScale, clock);
        }

        int step = (PulseInterval - skill) * 12 / PulseInterval;
        if (step == chargeStep) return;
        // A zero-width segment is skipped by the UI draw. Offset the two affected boxes
        // together so a cooldown step does not force multiple immediate layouts.
        var hidden = new RectangleF(0, 0, -PulseMedalSize, 0);
        if (chargeStep < 0)
        {
            for (int i = 0; i < PulseSegments.Length; i++)
                if (i + 1 != step) sceneObjects.Tween(PulseSegments[i], clock).UiRect(hidden, 0);
            ui.SetVisible(PulseShine, false);
        }
        else
        {
            if (chargeStep > 0) sceneObjects.Tween(PulseSegments[chargeStep - 1], clock).UiRect(hidden, 0);
            if (step > 0) sceneObjects.Tween(PulseSegments[step - 1], clock).UiRect(RectangleF.Empty, 0);
            bool wasShining = shineTicks > 0;
            shineTicks = step == 12 ? 24 : 0;
            if (wasShining != (shineTicks > 0)) ui.SetVisible(PulseShine, shineTicks > 0);
        }
        chargeStep = step;
    }

    private static void Fill(IGameObjects objects, Guid bar, int full, float fraction) =>
        objects.Tween(bar).UiRect(new(0, 0, -full * (1 - Math.Clamp(fraction, .01f, 1)), 0), 0);

    private void PlaySound(ScriptTick tick, Sound sound) => tick.Objects.Audio.Play(Sounds[(int)sound]);

    private void Burst(IGameObjects objects, Effect effect, Vector2 position) =>
        Burst(objects, CombatEmitters[(int)effect], position);

    private static void Burst(IGameObjects objects, Guid emitter, Vector2 position)
    {
        objects.SetLocalTransform(emitter, new(position, 0, Vector2.One));
        objects.EmitParticles(emitter);
    }

    private void FoeImpact(IGameObjects objects, int kind, Vector2 position)
    {
        // A second emitter per material lets simultaneous hits keep separate origins.
        int type = Math.Min(kind, 2);
        int index = (nextImpact[type]++ & 1) == 0 ? (int)Effect.Mud + type : 11 + type;
        Burst(objects, CombatEmitters[index], position);
    }

    private void RiseSoul(IGameObjects objects, Vector2 position)
    {
        int slot = nextSoul++ % 3;
        Burst(objects, CombatEmitters[slot == 0 ? (int)Effect.Soul : 13 + slot], position);
    }

    private void ShowWaypoint(IGameObjects objects, Vector2 target)
    {
        objects.SetLocalTransform(Waypoint, new(target - new Vector2(10, 13), 0, Vector2.One));
        objects.Tween(Waypoint).Scale(new(.78f, .78f), .22f, TweenEase.EaseOut);
    }

    private void StartEffects(IGameObjects objects)
    {
        // These are draw-only sprites. Keeping their bodies registered avoids a
        // scene-wide hierarchy walk when a tween or pickup changes visibility.
        objects.Tween(Ring, TweenClock.Ui).Alpha(0, 0);
        objects.Tween(ShieldRing, TweenClock.Ui).Alpha(0, 0);
        objects.Tween(HealGlyph, TweenClock.Ui).Alpha(0, 0);
        objects.Tween(Cracks, TweenClock.Ui).Alpha(0, 0);
        objects.Tween(Warning, TweenClock.Ui).Alpha(0, 0);
        foreach (Guid crown in Loot) objects.Tween(crown, TweenClock.Ui).Alpha(0, 0);
    }

    private static void PlaceAccent(IGameObjects objects, Guid id, Vector2 center, Vector2 size, float scale = 1, float rotation = 0) =>
        objects.SetLocalTransform(id, new(center - size * .5f, rotation, new(scale)));

    private void HideWarning(IGameObjects objects)
    {
        if (!warningShown) return;
        warningShown = false;
        warningFade.Stop();
        objects.Tween(Warning).Alpha(0, 0);
    }

    private void StartRing(ScriptTick tick, Vector2 origin)
    {
        ringCenter = origin;
        ringTicks = RingDuration;
        PlaceAccent(tick.Objects, Ring, origin, new(204, 104), .14f);
        tick.Objects.Tween(Ring).Alpha(1, 0).Delay(.30f).Alpha(0, .36f);
        PlaceAccent(tick.Objects, WarFlag, origin - new Vector2(15, 23), new(44, 50));
        tick.Objects.SetActive(WarFlag, true);
        tick.Objects.Animator(WarFlag)!.PlayThenShowSprite();
        Burst(tick.Objects, Effect.Bannerglow, origin - new Vector2(0, 8));
    }

    private void ShowSaber(ScriptTick tick, Vector2 feet, Vector2 target)
    {
        var direction = target - feet;
        PlaceAccent(tick.Objects, Saber, feet + AboveFeet, new(52),
            1, MathF.Atan2(direction.Y, direction.X) * (180 / MathF.PI));
        tick.Objects.Animator(Saber)!.PlayThenShowSprite();
        Burst(tick.Objects, Effect.Steel, target + AboveFeet);
    }

    private void ShowShield(ScriptTick tick, Vector2 at)
    {
        shieldCenter = at;
        shieldTicks = 22;
        PlaceAccent(tick.Objects, ShieldRing, at, new(52, 28), .35f);
        shieldFade.Stop();
        shieldFade = tick.Objects.Tween(ShieldRing).Alpha(1, 0).Alpha(0, .35f);
        Burst(tick.Objects, Effect.Brass, at + AboveFeet);
    }

    private void ShowHeal(ScriptTick tick, Vector2 at, int target = -1)
    {
        healCenter = at;
        healTarget = target;
        healTicks = 34;
        PlaceAccent(tick.Objects, HealGlyph, at, new(44,26));
        healFade.Stop();
        healFade = tick.Objects.Tween(HealGlyph).Alpha(1, 0).Delay(.19f).Alpha(0, .35f);
    }

    private void FloatNumber(ScriptTick tick, Vector2 feet, int target, int value, bool heal = false, bool ally = false,
        bool emphasized = false)
    {
        string[] labels = heal ? HealLabels : ally ? HurtLabels : DamageLabels;
        string[] outline = heal ? HealOutline : DamageOutline;
        int amount = Math.Clamp(value, 0, labels.Length - 1);
        FloatText(tick, feet, target, labels[amount], outline[amount], heal ? 11 : 9, emphasized ? 1.32f : 1);
    }

    private void FloatText(ScriptTick tick, Vector2 feet, int target, string ink, string outline, float halfWidth,
        float scale = 1, int lift = 0)
    {
        int index = nextNumber++ % numbers.Length;
        var origin = feet - new Vector2(0, 40 + lift);
        halfWidth *= scale;
        foreach (var offset in LabelOffsets)
        {
            var start = origin + offset;
            if (!LabelFits(start, target, halfWidth, scale, index)) continue;
            numbers[index] = new() { Start = start, HalfWidth = halfWidth, Age = 1, Scale = scale, Target = target };
            SetNumber(tick.Objects, index, ink, outline);
            PlaceNumber(tick.Objects, index, in numbers[index]);
            return;
        }
    }

    private bool LabelFits(Vector2 start, int target, float halfWidth, float scale, int replace)
    {
        for (int i = 0; i < numbers.Length; i++)
        {
            ref readonly var other = ref numbers[i];
            if (i == replace || other.Age == 0) continue;
            var delta = start - NumberPosition(in other);
            if (MathF.Abs(delta.X) >= other.HalfWidth + halfWidth + 3) continue;
            if (target == other.Target || MathF.Abs(delta.Y) < 9 * (scale + other.Scale)) return false;
        }
        return true;
    }

    private void SetNumber(IGameObjects objects, int index, string ink, string outline)
    {
        objects.SetWorldText(NumberInk[index], ink);
        for (int pass = 0; pass < OutlinePasses; pass++)
            objects.SetWorldText(NumberOutline[index * OutlinePasses + pass], outline);
    }

    private static Vector2 NumberPosition(in Floating number)
    {
        float t = (number.Age - 1) / (float)(NumberDuration - 1);
        return number.Start - new Vector2(0, 20 * t * (2 - t));
    }

    private void PlaceNumber(IGameObjects objects, int index, in Floating number)
    {
        float pop = 1 - .18f * Math.Max(0, 5 - number.Age) / 4;
        objects.SetLocalTransform(Numbers[index], new(NumberPosition(in number), 0, new(number.Scale * pop)));
    }

    private void LaunchArrow(ScriptTick tick, Vector2 origin, int target, int damage)
    {
        int index = nextArrow++ % arrows.Length;
        bool critical = ++bowShots % 4 == 0;
        arrows[index] = new() { Position = origin, Target = target, Damage = critical ? damage + 1 : damage,
            Ticks = 36, Critical = critical };
        tick.Objects.SetActive(Arrows[index], true);
        PointArrow(tick.Objects, Arrows[index], origin, foes[target].Feet + AboveFeet - origin);
        PlaySound(tick, Sound.Bow);
    }

    private static void PointArrow(IGameObjects objects, Guid arrow, Vector2 position, Vector2 direction) =>
        objects.SetLocalTransform(arrow, new(position - new Vector2(8, 8),
            MathF.Atan2(direction.Y, direction.X) * (180 / MathF.PI), Vector2.One));

    private void TossLoot(ScriptTick tick, Vector2 origin)
    {
        int index = nextLoot++ % drops.Length;
        drops[index] = new() { Origin = origin - new Vector2(6, 6), Drift = new(index % 2 == 0 ? -15 : 15, index % 3 * 5 - 3), Age = 1 };
        tick.Objects.SetLocalTransform(Loot[index], new(drops[index].Origin, 0, Vector2.One));
        tick.Objects.Tween(Loot[index]).Alpha(1, 0);
    }

    private void UpdateEffects(ScriptTick tick)
    {
        if (ringTicks > 0)
        {
            float t = 1 - (--ringTicks / (float)RingDuration);
            PlaceAccent(tick.Objects, Ring, ringCenter, new(204, 104), .14f + .86f * MathF.Sin(t * MathF.PI * .5f));
            if (ringTicks == 0) tick.Objects.SetActive(WarFlag, false);
        }
        if (shieldTicks > 0)
        {
            PlaceAccent(tick.Objects, ShieldRing, shieldCenter, new(52,28), 1 - --shieldTicks * .03f);
        }
        if (healTicks > 0)
        {
            Vector2 ground = healTarget >= 0 ? party[healTarget].Feet : healCenter;
            PlaceAccent(tick.Objects, HealGlyph, ground, new(44,26));
            if (--healTicks % 5 == 4)
            {
                float rise = (34 - healTicks) * .95f;
                var orbit = UnitCircle[(34 - healTicks) & 31];
                Burst(tick.Objects, Effect.Jade, ground + new Vector2(orbit.X * 12, orbit.Y * 6 - rise));
            }
        }
        if (measureParticles) RecordParticles(tick.Objects);
        for (int i = 0; i < numbers.Length; i++)
        {
            ref var number = ref numbers[i];
            if (number.Age == 0) continue;
            if (++number.Age > NumberDuration)
            {
                number.Age = 0;
                // An empty Text2D draws nothing and keeps its pooled body registered.
                SetNumber(tick.Objects, i, "", "");
            }
            else PlaceNumber(tick.Objects, i, in number);
        }
        for (int i = 0; i < drops.Length; i++)
        {
            ref var drop = ref drops[i];
            if (drop.Age == 0) continue;
            int age = ++drop.Age;
            if (age == 44)
            {
                drop.Age = 0;
                tick.Objects.Tween(Loot[i]).Alpha(0, 0);
                PlaySound(tick, Sound.Crown);
                continue;
            }
            float t = age / 24f;
            Vector2 at;
            if (t < 1) at = drop.Origin + drop.Drift * t - new Vector2(0, 75 * t * (1 - t));
            else
            {
                Vector2 ground = drop.Origin + drop.Drift;
                at = ground + (CaptainFeet - new Vector2(6, 20) - ground) * ((age - 24) / 20f);
            }
            tick.Objects.SetLocalTransform(Loot[i], new(at, 0, Vector2.One));
        }
        for (int i = 0; i < arrows.Length; i++)
        {
            ref var arrow = ref arrows[i];
            if (arrow.Ticks == 0) continue;
            ref var foe = ref foes[arrow.Target];
            var delta = foe.Feet + AboveFeet - arrow.Position;
            float length = delta.Length();
            if (--arrow.Ticks == 0 || length < 5 || foe.Health <= 0)
            {
                bool hit = arrow.Ticks > 0 && foe.Health > 0;
                arrow.Ticks = 0;
                tick.Objects.SetActive(Arrows[i], false);
                if (hit)
                {
                    Burst(tick.Objects, Effect.Feathers, foe.Feet + AboveFeet);
                    if (arrow.Critical) Burst(tick.Objects, Effect.Critical, foe.Feet + AboveFeet);
                    HitFoe(tick, arrow.Target, arrow.Damage, emphasized: arrow.Critical);
                }
                continue;
            }
            arrow.Position += delta * MathF.Min(1, tick.DeltaTime * 245 / length);
            PointArrow(tick.Objects, Arrows[i], arrow.Position, delta);
        }
    }
    private void RecordParticles(IGameObjects objects)
    {
        int count = 0;
        foreach (Guid emitter in CombatEmitters)
            count += objects.ReadParticles(emitter, Span<Particle2D>.Empty);
        peakParticles = Math.Max(peakParticles, count);
    }

}
