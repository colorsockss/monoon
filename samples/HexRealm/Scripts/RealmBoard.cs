using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using Engine.Core;

public sealed partial class RealmBoard : GameBehaviour
{
    private const int Slot = 1, SaveVersion = 4;
    private const float FadeHalf = .22f, CameraTravel = .40f;
    private const float AtlasZoom = 1.26f, DetailZoom = 1.62f;
    private static readonly Color TransitionInk = Color.FromArgb(112, 109, 92);
    private enum Transition { None, Start, Title, Restore, Result, Reveal }
    private static readonly string[] TrainInputs = ["Train1", "Train2", "Train3", "Train4", "Train5"];
    private static readonly string[] UnitNames = ["Settler", "Worker", "Scout", "Spearguard", "Bowguard"];
    private static readonly string[] TerrainNames = ["", "Meadow", "Forest", "Heath", "Open water", "Stone ridge", "Steppe"];
    private static readonly string[] TownNames = ["Saltmere", "Pinegate", "Gullrest", "Stonecross", "Eastbank", "Windharbor"];
    private static readonly Vector2 AtlasCenter = new(590, 280);
    public Guid Ground, Coast, Districts, Fog, RevealFog, Borders, Orders, Pointer, Route, Camera, CameraFrame;
    public Guid Hud, Title, Help, Result, Objective, ObjectiveLabel, ObjectiveText, ObjectiveKey, ResearchButton,
        TurnLabel, GoldLabel, LandLabel, LoreLabel, LoreFill, Selection, Stats, Advice, Hint,
        Chapter, Crest, EndButton, CommandPanel, CommandChips, CommandNotes, ActionValue, MoveIcon, PeopleIcon, Top, Context, ResultHeading, ResultCause, ResearchGlint,
        Tooltip, TooltipText, Notice, NoticePanel, TurnStamp, StampText, LandGuide, ChartShade;
    public Guid SelectSound, MoveSound, HitSound, BuildSound, TurnSound, VictorySound, BowSound;
    public Guid[] Portraits = [], Tallies = [], ResourceRows = [], AlignedLabels = [];
    public float[] TextOffsets = [];
    public PrefabReference[] UnitArt = [], Fill = [], Signs = [], ClaimInk = [];
    public SpriteReference[] TownSprites = [];
    public PrefabReference TownArt, TownLine, HealthBar, CityBar, Number, Sparks, Smoke, LandGain;
    public int MapSeed = 18427;

    private IGameObjects objects = null!;
    private IGameUi ui = null!;
    private SessionState session = null!;
    private RealmRules rules = null!;
    private int unit, city;
    private Point? inspected, hovered;
    private bool playing, help, finished, resultReady, detail;
    private float noticeTime, stampTime, resultDelay;
    private Transition transition;
    private RealmRules? restoredCampaign;
    private bool[]? restoredExploration;
    private float hudScale = 1;
    private int commandTrim = -1;
    private bool priorityNotice, landGuide;
    private string? queuedNotice;
    private Vector2 cameraAt, cameraFrom, cameraTarget;
    private float cameraZoom, zoomFrom, zoomTarget, cameraTime;
    private RectangleF topBounds, commandBounds, endBounds, objectiveBounds;
    private string noticeText = "";
    private const int Count = RealmRules.Count;
    private readonly bool[] visible = new bool[Count], explored = new bool[Count];
    private readonly Vector2[] centers = new Vector2[Count];
    private readonly int[] tiles = new int[Count];
    private readonly GridReach[] reached = new GridReach[Count];
    private readonly Point[] preview = new Point[Count];
    private readonly Vector2[] routePoints = new Vector2[Count];

    public int Turn => rules.Turn;
    public ulong StateHash => rules.Hash();
    public int CityCount => rules.Cities.Count;
    public int Winner => rules.Winner;
    public bool InCampaign => playing;
    public string Status => noticeText;

    public override void OnCreate(ScriptLife life)
    {
        objects = life.Objects;
        ui = life.Ui;
        session = life.Session;
        cameraAt = cameraTarget = AtlasCenter;
        cameraZoom = zoomTarget = AtlasZoom;
        SetCameraFrame();
        for (int i = 0; i < Count; i++)
            centers[i] = objects.TileCellCenter(Ground, RealmRules.PointAt(i))!.Value;
        objects.SetActive(Route, false);
        ui.SetVisible(Tooltip, false);
        Install(CreateRules(unchecked((uint)MapSeed)), false);
        topBounds = ui.Bounds(Top) ?? default;
        commandBounds = ui.Bounds(CommandPanel) ?? default;
        endBounds = ui.Bounds(EndButton) ?? default;
        objectiveBounds = ui.Bounds(Objective) ?? default;
    }

    private static RealmRules CreateRules(uint seed)
    {
        var terrain = RealmRules.Generate(seed);
        return new(seed, terrain, Array.ConvertAll(terrain, RealmRules.EntryCost));
    }

    public override void UpdateUi(UiTick tick)
    {
        float scale = tick.Ui.Preferences.TextScale;
        if (scale == hudScale) return;
        hudScale = scale;
        for (int i = 0; i < AlignedLabels.Length; i++)
        {
            Vector2 shift = (scale - 1) * new Vector2(TextOffsets[2 * i], TextOffsets[2 * i + 1]);
            objects.Tween(AlignedLabels[i], TweenClock.Ui).UiRect(new(shift.X, shift.Y, 0, 0), 0);
        }
        if (playing && !finished && turnPhase == TurnPhase.None) RefreshSelection();
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        Animate(tick.DeltaTime);
        StepCamera(tick.DeltaTime);
        StepFog(tick.DeltaTime);
        if (stampTime > 0 && (stampTime -= tick.DeltaTime) <= 0) ui.SetVisible(TurnStamp, false);
        if (resultDelay > 0 && (resultDelay -= tick.DeltaTime) <= 0) BeginTransition(Transition.Result);
        if (noticeTime > 0 && (noticeTime -= tick.DeltaTime) <= 0)
        {
            if (queuedNotice is { } queued)
            {
                queuedNotice = null;
                ShowNotice(queued, 2.35f);
            }
            else
            {
                ui.SetVisible(NoticePanel, false);
                noticeText = "";
            }
        }
        if (transition != Transition.None)
        {
            StepTransition();
            return;
        }
        if (arrowCaptureWait)
        {
            ClearButtonClicks();
            return;
        }
        if (turnPhase != TurnPhase.None)
        {
            ClearButtonClicks();
            StepTurn(tick.DeltaTime);
            return;
        }
        if (Clicked("HexRealm.Button.Help") || tick.Input.Pressed("Help"))
        {
            help = !help;
            SetScreen();
            if (help) OpenCard(Help);
            return;
        }
        if (help) return;

        var input = tick.Input;
        if (Clicked("HexRealm.Button.Load") || input.Pressed("Load"))
        {
            Load();
            return;
        }
        if (Clicked("HexRealm.Button.Save") || playing && input.Pressed("Save")) Save();
        if (!playing)
        {
            if (Clicked("HexRealm.Button.Start") || input.Pressed("NewCampaign") || input.Pressed("EndTurn"))
                BeginTransition(Transition.Start);
            return;
        }
        if (finished)
        {
            if (resultReady && (Clicked("HexRealm.Button.Title") || input.Pressed("NewCampaign")))
                BeginTransition(Transition.Title);
            return;
        }
        if (Clicked("HexRealm.Button.End") || input.Pressed("EndTurn"))
        {
            BeginTurn();
            return;
        }

        if (input.Pressed("ZoomNear") || input.Pressed("ZoomFar"))
        {
            detail = input.Pressed("ZoomNear");
            AimCamera(detail ? Focus() : AtlasCenter, detail ? DetailZoom : AtlasZoom);
        }
        if (tick.Direction != Vector2.Zero)
        {
            Vector2 offset = tick.Direction * (380 * tick.DeltaTime);
            cameraFrom = ClampCamera(cameraFrom + offset);
            cameraTarget = ClampCamera(cameraTarget + offset);
            cameraAt = ClampCamera(cameraAt + offset);
            SetCameraFrame();
        }

        bool context = Clicked("HexRealm.Button.Context");
        if (input.Pressed("NextUnit") || context && unit >= 0 &&
            rules.Units[unit].Kind is UnitKind.Spear or UnitKind.Archer or UnitKind.Scout)
        {
            for (int i = 0, j = unit; i < rules.Units.Count; i++)
            {
                j = (j + 1) % rules.Units.Count;
                if (rules.Units[j] is not { Alive: true, Side: 1 } next) continue;
                Select(next.Cell);
                break;
            }
        }

        var pointer = input.PointerPosition;
        bool overLand = input.PointerInside && topBounds.Contains(pointer.X, pointer.Y) &&
            pointer.X >= topBounds.X + 616 && pointer.X < topBounds.X + 796;
        if (landGuide != overLand)
        {
            landGuide = overLand;
            ui.SetVisible(LandGuide, landGuide);
        }
        Point? at = input.PointerInside && !OnHud(pointer) && tick.Cameras.TryGetView(Camera, out var camera)
            ? objects.TileCellAt(Ground, camera.ScreenToWorld(pointer)) : null;
        if (at is { } tile && RealmRules.Contains(tile))
        {
            Hover(tile);
            if (input.Pressed("Select")) Select(tile);
            else if (input.Pressed("Order") && unit >= 0)
            {
                if (visible[RealmRules.Index(tile)] || rules.UnitAt(tile) == null && rules.CityAt(tile) == null)
                    Submit(new(OrderKind.Move, unit, tile.X, tile.Y));
                else Notify("Route unavailable.");
            }
        }
        else ClearCursor();

        if (unit >= 0)
        {
            if (input.Pressed("Found") || context && rules.Units[unit].Kind == UnitKind.Settler)
                Submit(new(OrderKind.Found, unit));
            else if (input.Pressed("Farm") || context && rules.Units[unit].Kind == UnitKind.Worker)
                Submit(new(OrderKind.Improve, unit, 1));
            else if (input.Pressed("Mine")) Submit(new(OrderKind.Improve, unit, 2));
            else if (input.Pressed("Road")) Submit(new(OrderKind.Improve, unit, 3));
        }
        if (city >= 0)
        {
            if (context && unit < 0) Submit(new(OrderKind.Train, city, (int)UnitKind.Spear));
            for (int kind = 0; kind < TrainInputs.Length; kind++)
                if (input.Pressed(TrainInputs[kind])) Submit(new(OrderKind.Train, city, kind));
        }
        if (Clicked("HexRealm.Button.Research") || input.Pressed("Research")) Submit(new(OrderKind.Research, A: 1 - rules.Research));
    }

    private Vector2 Focus() => inspected is { } cell ? centers[RealmRules.Index(cell)] : cameraAt;

    private static Vector2 ClampCamera(Vector2 at) => Vector2.Clamp(at, new(340, 170), new(870, 380));

    private void AimCamera(Vector2 center, float zoom)
    {
        center = ClampCamera(center);
        if (center == cameraTarget && zoom == zoomTarget) return;
        cameraFrom = cameraAt;
        zoomFrom = cameraZoom;
        cameraTarget = center;
        zoomTarget = zoom;
        cameraTime = CameraTravel;
    }

    private void StepCamera(float seconds)
    {
        if (cameraTime <= 0) return;
        cameraTime = Math.Max(0, cameraTime - seconds);
        float t = 1 - cameraTime / CameraTravel;
        // Ease in and out over the outer fifths, keeping the middle of the map move at a steady speed.
        const float ramp = .20f;
        float edge = Math.Min(t, 1 - t);
        float distance = edge < ramp ? edge * edge / (2 * ramp) : edge - ramp / 2;
        float ease = distance / (1 - ramp);
        if (t > .5f) ease = 1 - ease;
        cameraAt = cameraFrom + (cameraTarget - cameraFrom) * ease;
        cameraZoom = zoomFrom + (zoomTarget - zoomFrom) * ease;
        SetCameraFrame();
    }

    private void SetCameraFrame()
    {
        // Camera framing fits this non-rendering rectangle to the 1280×720 logical viewport.
        objects.SetLocalTransform(CameraFrame, new(cameraAt - new Vector2(640, 360) / cameraZoom,
            0, new Vector2(1 / cameraZoom)));
    }

    private bool OnHud(Vector2 p) => topBounds.Contains(p.X, p.Y) ||
        commandBounds.Contains(p.X, p.Y - Math.Max(0, commandTrim)) ||
        endBounds.Contains(p.X, p.Y) || rules.Turn <= 2 && objectiveBounds.Contains(p.X, p.Y);
    private bool Clicked(string action) => session.Clear(action);

    private void BeginTransition(Transition next)
    {
        transition = next;
        ClearCursor();
        if (next != Transition.Result)
        {
            detail = false;
            AimCamera(AtlasCenter, AtlasZoom);
        }
        session.Screen.Fade(TransitionInk, FadeHalf);
    }

    private void ClearButtonClicks()
    {
        // UI clicks are session flags, so discard those made while the screen is covered.
        Clicked("HexRealm.Button.Start");
        Clicked("HexRealm.Button.Title");
        Clicked("HexRealm.Button.Load");
        Clicked("HexRealm.Button.Save");
        Clicked("HexRealm.Button.Help");
        Clicked("HexRealm.Button.Context");
        Clicked("HexRealm.Button.End");
        Clicked("HexRealm.Button.Research");
    }

    private void StepTransition()
    {
        ClearButtonClicks();
        byte alpha = session.Screen.Current.Fade.A;
        if (transition == Transition.Reveal)
        {
            if (alpha == 0) transition = Transition.None;
            return;
        }
        if (alpha < byte.MaxValue) return;

        switch (transition)
        {
            case Transition.Start:
            case Transition.Title:
                Install(CreateRules(unchecked((uint)MapSeed)), transition == Transition.Start);
                break;
            case Transition.Restore:
                var campaign = restoredCampaign!;
                if (restoredExploration is { } history) history.CopyTo(explored, 0);
                Install(campaign, true, restoredExploration != null);
                ShowNotice(restoredExploration != null ? "Campaign restored." :
                    ui.Preferences.TextScale > 1.5f ? "Old save: new game" : "Older save: new campaign", 2.35f);
                if (restoredExploration != null) session.Log($"HexRealm resume {campaign.Hash():x16}");
                restoredCampaign = null;
                restoredExploration = null;
                break;
            case Transition.Result:
                OpenCharter(true);
                break;
        }
        transition = Transition.Reveal;
        session.Screen.Fade(Color.Transparent, FadeHalf);
    }

    private void Select(Point tile)
    {
        bool changed = inspected != tile;
        inspected = tile;
        if (detail) AimCamera(Focus(), DetailZoom);
        city = rules.CityAt(tile) is { Side: 1 } town ? rules.Cities.IndexOf(town) : -1;
        // First click reads the city card; a second click on its tile selects a stationed unit.
        unit = (city < 0 || !changed && unit < 0) && rules.UnitAt(tile) is { Side: 1 } piece
            ? rules.Units.IndexOf(piece) : -1;
        if (changed && (unit >= 0 || city >= 0)) objects.Audio.Play(SelectSound);
        RefreshSelection();
        ClearCursor();
    }

    private void Submit(RealmOrder order)
    {
        var selected = unit >= 0 ? rules.Units[unit] : null;
        if (!rules.Apply(order))
        {
            Notify("Order unavailable.");
            return;
        }
        FollowSelection(selected);
        if (order.Kind is OrderKind.Found or OrderKind.Improve)
        {
            objects.Audio.Play(BuildSound);
            Emit(Sparks, centers[RealmRules.Index(inspected!.Value)], .58f);
            if (order.Kind == OrderKind.Improve) Notify("District built.");
        }
        else if (order.Kind == OrderKind.Research)
            Notify(rules.Research == 1 ? "Research: 2 gold/turn" : "Research paused");
        ClearCursor();
        if (!arrowCaptureWait) RefreshAfterOrder();
    }

    private void RefreshAfterOrder()
    {
        RefreshMap(animateFog: true);
        bool claimed = PresentClaims();
        RefreshHud();
        PresentResult(announce: true, showClaim: claimed);
        if (finished) session.Log($"HexRealm result {rules.Winner}: {StateHash:x16}");
    }

    private void FollowSelection(RealmRules.Unit? selected)
    {
        if (selected != null)
        {
            inspected = selected.Cell;
            if (!selected.Alive || (uint)unit >= rules.Units.Count || rules.Units[unit] != selected) unit = -1;
        }
        city = inspected is { } focus && rules.CityAt(focus) is { Side: 1 } town ? rules.Cities.IndexOf(town) : -1;
    }

    internal void Install(RealmRules campaign, bool start, bool preserveExploration = false)
    {
        ResetPresentation();
        ClearCursor();
        rules = campaign;
        rules.Moved = TrailMove;
        rules.Struck = TrailStrike;
        rules.Claimed = RecordClaim;
        rules.Trained = RecordTraining;
        if (!preserveExploration) Array.Clear(explored);
        unit = start && rules.Units[0] is { Kind: UnitKind.Settler, Alive: true } ? 0 : -1;
        inspected = RealmRules.Start;
        city = rules.Cities.FindIndex(town => town is { Side: 1, Capital: true });
        if (city >= 0 && unit < 0) inspected = rules.Cities[city].Cell;
        playing = start;
        help = finished = resultReady = false;
        noticeTime = stampTime = resultDelay = 0;
        ResetTurnPresentation();
        landGuide = false;
        queuedNotice = null;
        priorityNotice = false;
        noticeText = "";
        ui.SetVisible(NoticePanel, false);
        session.Screen.For(Camera).Clear();
        GroundTiles();
        RefreshMap();
        RefreshHud();
        SetScreen();
        PresentResult();
        if (!playing) OpenCard(Title);
        if (playing && !finished) Stamp($"TURN {rules.Turn}", 1.25f);
    }

    private void SetScreen()
    {
        ui.SetVisible(Hud, playing && !help);
        ui.SetVisible(CommandPanel, !finished);
        ui.SetVisible(EndButton, !finished);
        ui.SetVisible(Objective, !finished && rules.Turn <= 2);
        ui.SetInteractable(ResearchButton, !finished && rules.Tech < RealmRules.Techs.Length);
        bool showTitle = !playing && !help;
        bool showResult = resultReady && !help;
        ui.SetVisible(ChartShade, showTitle || showResult);
        ui.SetVisible(Title, showTitle);
        ui.SetVisible(Help, help);
        ui.SetVisible(Result, showResult);
        ui.SetVisible(NoticePanel, !help && noticeTime > 0);
        ui.SetVisible(TurnStamp, !help && !finished && stampTime > 0);
        ui.SetVisible(LandGuide, !help && !finished && landGuide);
    }

    private void OpenCard(Guid card) =>
        objects.Tween(card, TweenClock.Ui).UiRect(new(0, 4, 0, 0), 0).UiRect(RectangleF.Empty, .15f);

    private void Stamp(string text, float seconds)
    {
        stampTime = seconds;
        ui.SetText(StampText, $"<b>{text}</b>");
        ui.SetVisible(TurnStamp, true);
        objects.Tween(TurnStamp, TweenClock.Ui).UiRect(new(0, 3, 0, 0), 0).UiRect(RectangleF.Empty, .16f);
    }

    private void Notify(string message)
    {
        if (priorityNotice && noticeTime > 0 && !finished) queuedNotice = message;
        else ShowNotice(message, 2.35f);
    }

    private void ShowNotice(string text, float seconds, bool priority = false)
    {
        priorityNotice = priority;
        noticeText = text;
        noticeTime = seconds;
        ui.SetText(Notice, text);
        ui.SetVisible(NoticePanel, !help);
        // Leave room for the result sheet when saving or restoring a finished campaign.
        RectangleF offset = finished && resultDelay <= 0 ? new(-440, -60, 0, 0) : RectangleF.Empty;
        objects.Tween(NoticePanel, TweenClock.Ui).UiRect(new(offset.X, offset.Y + 3, 0, 0), 0).UiRect(offset, .14f);
    }

    private string TownName(RealmRules.City town)
    {
        if (town.OriginalSide == 2) return town.Capital ? "Redwatch" : "Greyharbor";
        if (town.Capital) return "Haven";
        int index = 0;
        foreach (var candidate in rules.Cities)
        {
            if (candidate == town) break;
            if (candidate.OriginalSide == 1 && !candidate.Capital) index++;
        }
        return TownNames[index];
    }

    private void RefreshHud()
    {
        RefreshTurnHud();
        SetResource(1, GoldLabel, rules.Gold.ToString(CultureInfo.InvariantCulture));
        SetResource(2, LandLabel, $"<color=#245d50><b>{rules.Territory(1)}</b></color> / {rules.LandGoal}");
        RefreshResearchHud();
        RefreshSelection();
    }

    private void RefreshTurnHud()
    {
        SetResource(0, TurnLabel, $"{Math.Min(RealmRules.LastTurn, rules.Turn):00} / {RealmRules.LastTurn}");
        ui.SetText(Chapter, rules.Turn < 5 ? "<b>The Salt Coast</b>" : rules.Turn < 15 ? "<b>The Stone Pass</b>" : "<b>The Last Watch</b>");
        ui.SetVisible(Objective, rules.Turn <= 2);
        ui.SetText(ObjectiveLabel, city < 0 && rules.Territory(1) == 0 ? "<b>First orders</b>" : "<b>Survey the coast</b>");
        bool unbuilt = rules.Territory(1) == 0;
        ui.SetVisible(ObjectiveKey, unbuilt);
        ui.SetText(ObjectiveText, unbuilt ? "Found Haven" : "Click Haven to survey.");
    }

    private void RefreshResearchHud()
    {
        bool known = rules.Tech == RealmRules.Techs.Length;
        SetResource(3, LoreLabel, known ? "Complete" : RealmRules.Techs[rules.Tech]);
        objects.GetComponent<RealmButton>(ResearchButton)?.SetOption(known ? 2 : rules.Research, !known, !known && rules.Research == 1);
        float progress = known ? 1 : rules.ResearchPoints / (float)rules.ResearchGoal;
        objects.Tween(LoreFill, TweenClock.Ui).UiRect(new(0, 0, 127 * progress, 0), .20f);
    }

    private void SetResource(int index, Guid label, string value)
    {
        string? previous = ui.TextOf(label);
        if (previous == value) return;
        ui.SetText(label, value);
        if (playing && !finished && !string.IsNullOrEmpty(previous))
            objects.Tween(ResourceRows[index], TweenClock.Ui).UiRect(new(0, 3, 0, 0), 0).UiRect(RectangleF.Empty, .15f);
    }

    private void LayoutCommand(bool stats, bool hint, bool action)
    {
        ui.SetVisible(CommandChips, stats);
        ui.SetVisible(Context, action);
        ui.SetVisible(Hint, hint);
        int trim = (stats ? 0 : 40) + (hint ? 0 : 32) + (action ? 0 : 48);
        if (trim == commandTrim) return;
        commandTrim = trim;
        objects.Tween(CommandPanel, TweenClock.Ui).UiRect(new(0, trim, 0, -trim), .14f);
        objects.Tween(CommandNotes, TweenClock.Ui).UiRect(new(0, stats ? 0 : -40, 0, 0), 0);
        objects.Tween(Context, TweenClock.Ui).UiRect(new(0, (stats ? 0 : -40) + (hint ? 0 : -32), 0, 0), 0);
    }

    private void RefreshSelection()
    {
        Array.Clear(tiles);
        bool selectedUnit = unit >= 0 && rules.Units[unit].Alive;
        for (int i = 0; i < Portraits.Length; i++)
            ui.SetVisible(Portraits[i], selectedUnit && i == (int)rules.Units[unit].Kind);
        ui.SetVisible(Crest, !selectedUnit);
        ui.SetVisible(MoveIcon, selectedUnit);
        ui.SetVisible(PeopleIcon, !selectedUnit);
        var action = objects.GetComponent<RealmButton>(Context);
        if (selectedUnit)
        {
            var actor = rules.Units[unit];
            ui.SetText(Selection, $"<b>{UnitNames[(int)actor.Kind]}</b>");
            RefreshUnitHealth(unit);
            ui.SetText(ActionValue, FormattableString.Invariant($"{actor.Moves / 10f:0.#} MP"));
            ui.SetText(Advice, actor.Moves == 0 ? "Spent for turn" : "Right-click to order");
            string hint = actor.Kind switch
            {
                UnitKind.Worker => "Farm, mine or road",
                UnitKind.Archer => "Range 2",
                UnitKind.Scout => "Sight 3 / Fast",
                UnitKind.Spear => "Range 1 / Siege",
                _ => ""
            };
            ui.SetText(Hint, hint);
            LayoutCommand(true, hint.Length != 0, true);
            action?.SetOption(actor.Kind == UnitKind.Settler ? 0 : actor.Kind == UnitKind.Worker ? 1 : 2,
                actor.Kind is not (UnitKind.Worker or UnitKind.Settler) || actor.Moves > 0);
            int count = rules.Reachable(actor, reached);
            for (int i = 0; i < count; i++)
            {
                int index = RealmRules.Index(reached[i].Cell);
                if (visible[index]) tiles[index] = 5;
            }
        }
        else if (city >= 0)
        {
            var town = rules.Cities[city];
            var growth = rules.Forecast(town);
            ui.SetText(Selection, $"<b>{TownName(town)}</b>");
            ui.SetText(Stats, $"{town.Health}/12");
            ui.SetText(ActionValue, $"{town.Population} pop");
            bool large = ui.Preferences.TextScale > 1.5f;
            SetFood(town.CanGrow, growth.Food, growth.Goal, growth.Harvest);
            string consequence = growth.Next.HasValue ? "Pop +1, Land +1" : "Pop +1";
            ui.SetText(Hint, !town.CanGrow ? "" : large ? $"<b>{consequence}</b>" : $"<b>At full: {consequence}</b>");
            if (growth.Next is { } parcel) tiles[RealmRules.Index(parcel)] = 201;
            LayoutCommand(true, town.CanGrow, true);
            action?.SetOption(town.Queue is { } queued ? 4 + (int)queued : 3, town.Queue == null);
        }
        else
        {
            int i = inspected is { } tile ? RealmRules.Index(tile) : 0;
            ui.SetText(Selection, IsCharted(i) ? TerrainNames[rules.Terrain[i]] : "Uncharted");
            ui.SetText(Advice, "Select a unit");
            ui.SetText(Hint, "Survey or order units");
            LayoutCommand(false, false, false);
        }
        if (inspected is { } focus && IsCharted(RealmRules.Index(focus)))
            tiles[RealmRules.Index(focus)] = 6;
        Replace(Orders);
    }

    private void Hover(Point target)
    {
        if (target == hovered) return;
        ClearCursor();
        int index = RealmRules.Index(target);
        hovered = target;
        int cursorTile = 7;
        if (unit >= 0 && visible[index] && rules.Attack(rules.Units[unit], target) is { } hit)
        {
            cursorTile = 8;
            int left = Math.Max(0, hit.Health - hit.Damage);
            string verdict = left == 0
                ? hit.City ? rules.Units[unit].Health > hit.Retaliation ? " / Breach" : " / Repelled" : " / Routed"
                : "";
            if (verdict == " / Repelled") left = 1;
            string reply = hit.Retaliation == 0 ? "" : $" / Reply {hit.Retaliation}";
            ShowTooltip($"Hit {hit.Damage} / HP {left}{reply}{verdict}");
        }
        else if (unit >= 0 && rules.PreviewPath(rules.Units[unit], target, preview, out long cost) is > 1 and var count)
        {
            for (int i = 0; i < count; i++) routePoints[i] = centers[RealmRules.Index(preview[i])];
            objects.SetLinePoints(Route, routePoints.AsSpan(0, count));
            objects.SetActive(Route, true);
            ShowTooltip(FormattableString.Invariant($"Route {cost / 10f:0.#} / {(rules.Units[unit].Moves - cost) / 10f:0.#} moves left"));
        }
        objects.SetTile(Pointer, target, cursorTile);
    }

    private void ShowTooltip(string text)
    {
        ui.SetText(TooltipText, text);
        ui.SetVisible(Tooltip, true);
    }

    private void ClearCursor()
    {
        if (hovered is not { } old) return;
        objects.SetTile(Pointer, old, 0);
        hovered = null;
        objects.SetActive(Route, false);
        ui.SetVisible(Tooltip, false);
    }

    private void PresentResult(bool announce = false, bool showClaim = false)
    {
        if (!playing || finished || rules.Winner == 0) return;
        finished = true;
        ui.SetText(ResultHeading, rules.Winner == 1 ? "<b>The coast is yours</b>" : "<b>The coast is lost</b>");
        queuedNotice = null;
        ui.SetText(ResultCause, rules.CapitalTaken ? "The capital changed hands." :
            rules.Turn <= RealmRules.LastTurn ? "Haven holds two fifths of the coast." : "Final reckoning after 25 turns.");
        for (int side = 1; side <= 2; side++)
        {
            var score = side == 1 ? rules.CoastResult : rules.RivalResult;
            int offset = (side - 1) * 5;
            ui.SetText(Tallies[offset], score.Land.ToString(CultureInfo.InvariantCulture));
            ui.SetText(Tallies[offset + 1], score.Population.ToString(CultureInfo.InvariantCulture));
            ui.SetText(Tallies[offset + 2], score.Treasury.ToString(CultureInfo.InvariantCulture));
            ui.SetText(Tallies[offset + 3], score.Force.ToString(CultureInfo.InvariantCulture));
            ui.SetText(Tallies[offset + 4], $"<b>{score.Total}</b>");
        }
        if (showClaim)
        {
            resultDelay = 1.03f;
            SetScreen();
        }
        else if (announce) BeginTransition(Transition.Result);
        else OpenCharter(false);
    }

    private void OpenCharter(bool announce)
    {
        noticeTime = 0;
        resultReady = true;
        SetScreen();
        objects.Tween(Result, TweenClock.Ui).UiRect(new(0, 24, 0, 0), 0).UiRect(RectangleF.Empty, .30f);
        if (announce && rules.Winner == 1) objects.Audio.Play(VictorySound);
    }

    private void Save()
    {
        if (!playing || !session.Storage.IsAvailable) return;
        var data = new GameData { Version = SaveVersion };
        var log = new StringBuilder();
        foreach (var item in rules.Commands)
            log.Append(FormattableString.Invariant($"{(int)item.Kind},{item.Id},{item.A},{item.B};"));
        data.Set("seed", rules.Seed);
        data.Set("orders", log.ToString());
        data.Set("hash", StateHash.ToString("x16", CultureInfo.InvariantCulture));
        try
        {
            session.Storage.SaveSlot(Slot, data);
            Notify("Campaign saved.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notify("Save failed.");
            session.Log(ex.Message, GameLogLevel.Warning);
        }
    }

    private void Load()
    {
        try
        {
            bool outdated = false;
            var data = session.Storage.IsAvailable
                ? session.Storage.LoadSlot(Slot, SaveVersion, (_, _) => outdated = true) : null;
            if (outdated)
            {
                restoredCampaign = CreateRules(unchecked((uint)MapSeed));
                BeginTransition(Transition.Restore);
                return;
            }
            if (data == null)
            {
                ShowNotice("No saved campaign.", 2.35f);
                return;
            }
            var campaign = CreateRules((uint)data.GetNumber("seed"));
            var history = new bool[Count];
            var sight = new bool[Count];
            Visibility(campaign, sight, history);
            foreach (var order in data.GetText("orders").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = order.Split(',');
                if (fields.Length != 4 ||
                    !int.TryParse(fields[0], CultureInfo.InvariantCulture, out int kind) ||
                    !int.TryParse(fields[1], CultureInfo.InvariantCulture, out int id) ||
                    !int.TryParse(fields[2], CultureInfo.InvariantCulture, out int a) ||
                    !int.TryParse(fields[3], CultureInfo.InvariantCulture, out int b) ||
                    !campaign.Apply(new((OrderKind)kind, id, a, b)))
                    throw new InvalidDataException("Saved orders did not replay.");
                Visibility(campaign, sight, history);
            }
            if (campaign.Hash().ToString("x16", CultureInfo.InvariantCulture) != data.GetText("hash"))
                throw new InvalidDataException("Replayed HexRealm state does not match the save.");
            restoredCampaign = campaign;
            restoredExploration = history;
            BeginTransition(Transition.Restore);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Notify("Restore failed.");
            session.Log(ex.Message, GameLogLevel.Warning);
        }
    }
}
