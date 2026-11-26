using System;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Text;
using Engine.Core;

public sealed class RealmBoard : GameBehaviour
{
    private const string NewCampaignFlag = "HexRealm.NewCampaign";
    public Guid Ground, Cities, Fog, Orders, Camera, CloseCamera, Summary, Selection, Notice;
    public PrefabReference[] UnitArt = [];
    public SceneReference Board;
    public int MapSeed = 18427;

    private readonly bool[] visible = new bool[RealmRules.Count], explored = new bool[RealmRules.Count];
    private readonly int[] cityTiles = new int[RealmRules.Count], fogTiles = new int[RealmRules.Count], orderTiles = new int[RealmRules.Count];
    private readonly GridReach[] reached = new GridReach[RealmRules.Count];
    private readonly Guid[] actors = new Guid[RealmRules.UnitLimit];
    private readonly RealmRules.Unit?[] displayed = new RealmRules.Unit?[RealmRules.UnitLimit];
    private readonly Vector2[] lastPositions = new Vector2[RealmRules.UnitLimit];
    // Units glide along the path the rules already took; the logical cell changes at once.
    private const float StepSeconds = 0.12f;
    private const int TrailCells = 8;
    private readonly Vector2[] centers = new Vector2[RealmRules.Count];
    private readonly Vector2[] trails = new Vector2[RealmRules.UnitLimit * TrailCells];
    private readonly int[] trailLengths = new int[RealmRules.UnitLimit];
    private readonly float[] trailTimes = new float[RealmRules.UnitLimit];
    private static readonly string[] TrainInputs = ["Train1", "Train2", "Train3", "Train4", "Train5"];
    private RealmRules rules = null!;
    private int unit = 0, city = -1;
    private Point? inspected;
    private Guid activeCamera;
    private Vector2 cameraAt;
    private string notice = "";
    private int legacyOrders;

    public int Turn => rules.Turn;
    public ulong StateHash => rules.Hash();
    public int CityCount => rules.Cities.Count;
    public Vector2 DrawnAt(int index) => ViewOf(index);

    public override void OnCreate(ScriptLife life)
    {
        bool fresh = life.Session.Clear(NewCampaignFlag);
        var saved = !fresh && life.Session.Storage.IsAvailable ? life.Session.Storage.LoadSlot(1, 1) : null;
        uint seed = saved == null ? unchecked((uint)MapSeed) : (uint)saved.GetNumber("seed");
        var terrain = RealmRules.Generate(seed);
        life.Objects.ReplaceTiles(Ground, RealmRules.Width, RealmRules.Height, terrain);
        var costs = new int[RealmRules.Count];
        for (int i = 0; i < costs.Length; i++)
            if (life.Objects.TryGetTile(Ground, RealmRules.PointAt(i), out var tile)) costs[i] = tile.MoveCost;
        for (int i = 0; i < centers.Length; i++)
            centers[i] = life.Objects.TileCellCenter(Ground, RealmRules.PointAt(i))!.Value - new Vector2(12, 17);
        rules = new(seed, terrain, costs);
        if (saved != null) RestoreCommands(saved);
        rules.Moved = TrailMove;
        rules.Struck = TrailStrike;
        if (rules.Units[0].Health <= 0) unit = -1;
        city = rules.Cities.FindIndex(town => town is { Side: 1, Capital: true });
        activeCamera = Camera;
        cameraAt = new(478, 368);
        Refresh(life.Objects, life.Ui);
        SetNotice(life.Ui, "");
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var input = tick.Input;
        var objects = tick.Objects;
        Animate(objects, tick.DeltaTime);
        var pan = tick.Direction * (500 * tick.DeltaTime);
        if (pan != Vector2.Zero)
        {
            cameraAt = Vector2.Clamp(cameraAt + pan, new(80, 75), new(1890, 850));
            SetCamera(objects, activeCamera);
        }
        if (input.Pressed("ZoomNear") || input.Pressed("ZoomFar"))
        {
            activeCamera = input.Pressed("ZoomNear") ? CloseCamera : Camera;
            SetCamera(objects, activeCamera);
            tick.Cameras.TrySetActive(activeCamera);
        }

        if (input.Pressed("Save") && tick.Session.Storage.IsAvailable)
        {
            Save(tick.Session);
            SetNotice(tick.Ui, "Saved turn and commands.");
        }
        if (input.Pressed("Load") && tick.Session.Storage.IsAvailable)
        {
            if (tick.Session.Storage.LoadSlot(1, 1) == null)
            {
                SetNotice(tick.Ui, "No saved campaign. Current campaign kept.");
                return;
            }
            tick.Session.LoadScene(Board.Id);
            return;
        }
        if (input.Pressed("NewCampaign") && rules.Winner != 0)
        {
            tick.Session.Set(NewCampaignFlag);
            tick.Session.LoadScene(Board.Id);
            return;
        }
        if (input.Pressed("NextUnit"))
        {
            for (int i = 0; i < rules.Units.Count; i++)
            {
                unit = (unit + 1) % rules.Units.Count;
                if (rules.Units[unit] is not { Alive: true, Side: 1 } selected) continue;
                city = -1;
                inspected = selected.Cell;
                cameraAt = objects.TileCellCenter(Ground, selected.Cell)!.Value;
                SetCamera(objects, activeCamera);
                Refresh(objects, tick.Ui);
                break;
            }
        }

        if ((input.Pressed("Select") || input.Pressed("Order")) && input.PointerInside &&
            tick.Cameras.TryGetView(activeCamera, out var camera) &&
            objects.TileCellAt(Ground, camera.ScreenToWorld(input.PointerPosition)) is { } tile &&
            RealmRules.Contains(tile))
        {
            if (input.Pressed("Select"))
            {
                inspected = tile;
                unit = rules.UnitAt(tile) is { Side: 1 } picked ? rules.Units.IndexOf(picked) : -1;
                city = rules.CityAt(tile) is { Side: 1 } town ? rules.Cities.IndexOf(town) : -1;
                Refresh(objects, tick.Ui);
            }
            else if (unit >= 0)
                Submit(tick, new(OrderKind.Move, unit, tile.X, tile.Y));
        }

        if (input.Pressed("Found") && unit >= 0) Submit(tick, new(OrderKind.Found, unit));
        if (unit >= 0)
        {
            if (input.Pressed("Farm")) Submit(tick, new(OrderKind.Improve, unit, 1));
            if (input.Pressed("Mine")) Submit(tick, new(OrderKind.Improve, unit, 2));
            if (input.Pressed("Road")) Submit(tick, new(OrderKind.Improve, unit, 3));
        }
        if (city >= 0)
            for (int kind = 0; kind < TrainInputs.Length; kind++)
                if (input.Pressed(TrainInputs[kind])) Submit(tick, new(OrderKind.Train, city, kind));
        if (input.Pressed("Research")) Submit(tick, new(OrderKind.Research, A: 1 - rules.Research));
        if (input.Pressed("EndTurn")) Submit(tick, new(OrderKind.End));
    }

    private void SetCamera(IGameObjects objects, Guid camera) => objects.SetLocalTransform(camera, new(cameraAt, 0, Vector2.One));

    private void Submit(ScriptTick tick, RealmOrder order)
    {
        if (rules.Winner != 0) return;
        var selected = unit < 0 ? null : rules.Units[unit];
        if (order.Kind == OrderKind.End) Array.Clear(trailLengths); // the turn ends with every unit at its cell
        if (!rules.Apply(order))
        {
            SetNotice(tick.Ui, "Order unavailable.");
            return;
        }
        if (selected != null)
        {
            inspected = selected.Cell;
            if (!selected.Alive) unit = -1;
        }
        if (inspected is { } focus && rules.CityAt(focus) is { Side: 1 } town)
            city = rules.Cities.IndexOf(town);
        SetNotice(tick.Ui, "");
        Refresh(tick.Objects, tick.Ui);
        if (order.Kind == OrderKind.End)
            tick.Session.Log(FormattableString.Invariant($"HexRealm turn {rules.Turn - 1:D2}: {StateHash:x16}"));
    }

    private void Visibility()
    {
        Array.Clear(visible);
        foreach (var actor in rules.Units)
            if (actor.Alive && actor.Side == 1) Reveal(actor.Cell, actor.Kind == UnitKind.Scout ? 3 : 2);
        foreach (var town in rules.Cities)
            if (town.Side == 1) Reveal(town.Cell, 2 + town.Population / 3);
    }

    private void Reveal(Point center, int radius)
    {
        for (int r = Math.Max(0, center.Y - radius); r < Math.Min(RealmRules.Height, center.Y + radius + 1); r++)
        for (int q = Math.Max(0, center.X - radius); q < Math.Min(RealmRules.Width, center.X + radius + 1); q++)
        {
            if (RealmRules.Distance(center, new(q, r)) > radius) continue;
            int index = r * RealmRules.Width + q;
            visible[index] = explored[index] = true;
        }
    }

    private void Refresh(IGameObjects objects, IGameUi ui)
    {
        Visibility();
        Array.Clear(cityTiles);
        Array.Clear(orderTiles);
        foreach (var town in rules.Cities)
        {
            int index = RealmRules.Index(town.Cell);
            if (explored[index]) cityTiles[index] = town.Side + 6;
        }
        for (int i = 0; i < fogTiles.Length; i++) fogTiles[i] = visible[i] ? 0 : explored[i] ? 2 : 1;
        if (rules.Winner == 0 && unit >= 0 && rules.Units[unit] is { Alive: true, Side: 1 } selected)
        {
            int count = rules.Reachable(selected, reached);
            for (int i = 0; i < count; i++)
            {
                Point cell = reached[i].Cell;
                int index = RealmRules.Index(cell);
                if (visible[index] && cell != selected.Cell) orderTiles[index] = 3;
            }
        }
        if (inspected is { } location && explored[RealmRules.Index(location)])
            orderTiles[RealmRules.Index(location)] = 4;
        objects.ReplaceTiles(Cities, RealmRules.Width, RealmRules.Height, cityTiles);
        objects.ReplaceTiles(Fog, RealmRules.Width, RealmRules.Height, fogTiles);
        objects.ReplaceTiles(Orders, RealmRules.Width, RealmRules.Height, orderTiles);
        UpdateUnits(objects);
        ui.SetText(Summary, $"TURN {Math.Min(60, rules.Turn):00}/60     GOLD {rules.Gold}     LAND {rules.Territory(1)}/{RealmRules.Count}     " +
            (rules.Tech < RealmRules.Techs.Length ? $"RESEARCH  {RealmRules.Techs[rules.Tech]} ({rules.ResearchPoints}/{6 + 2 * rules.Tech})" : "RESEARCH COMPLETE"));
        ui.SetText(Selection, Description());
    }

    private void UpdateUnits(IGameObjects objects)
    {
        for (int i = 0; i < rules.Units.Count; i++)
        {
            var actor = rules.Units[i];
            bool display = actor.Alive && (actor.Side == 1 || visible[RealmRules.Index(actor.Cell)]);
            if (actors[i] != Guid.Empty && (!display || displayed[i] != actor))
            {
                objects.Return(actors[i]);
                actors[i] = Guid.Empty;
                displayed[i] = null;
            }
            if (!display) continue;
            Vector2 center = ViewOf(i);
            if (actors[i] == Guid.Empty)
            {
                int art = actor.Side == 1 ? (int)actor.Kind : actor.Kind == UnitKind.Archer ? 6 : 5;
                actors[i] = objects.Rent(UnitArt[art], center);
                displayed[i] = actor;
                lastPositions[i] = center;
            }
            else Place(objects, i, center);
        }
    }

    private void Place(IGameObjects objects, int i, Vector2 at)
    {
        if (lastPositions[i] == at) return;
        objects.SetLocalTransform(actors[i], new(at, 0, Vector2.One));
        lastPositions[i] = at;
    }

    private void Animate(IGameObjects objects, float seconds)
    {
        for (int i = 0; i < rules.Units.Count; i++)
        {
            if (trailLengths[i] == 0) continue;
            if ((trailTimes[i] += seconds) >= (trailLengths[i] - 1) * StepSeconds) trailLengths[i] = 0;
            if (actors[i] != Guid.Empty) Place(objects, i, ViewOf(i));
        }
    }

    private Vector2 ViewOf(int i)
    {
        int length = trailLengths[i];
        if (length == 0) return centers[RealmRules.Index(rules.Units[i].Cell)];
        float t = Math.Clamp(trailTimes[i] / ((length - 1) * StepSeconds), 0, 1);
        float along = t * t * (3 - 2 * t) * (length - 1);
        int step = Math.Min((int)along, length - 2), at = i * TrailCells + step;
        // Plain multiply and add: Vector2.Lerp may fuse them (MultiplyAddEstimate), which the determinism scan rejects.
        return trails[at] + (trails[at + 1] - trails[at]) * (along - step);
    }

    // A new order for a unit still gliding starts from where it is drawn, so it never jumps.
    private int StartTrail(RealmRules.Unit actor, int cells)
    {
        int i = rules.Units.IndexOf(actor);
        trails[i * TrailCells] = trailLengths[i] > 0 ? ViewOf(i) : centers[RealmRules.Index(actor.Cell)];
        trailLengths[i] = cells;
        trailTimes[i] = 0;
        return i * TrailCells;
    }

    private void TrailMove(RealmRules.Unit actor, ReadOnlySpan<Point> cells)
    {
        int count = Math.Min(cells.Length, TrailCells), start = StartTrail(actor, count);
        for (int k = 1; k < count; k++) trails[start + k] = centers[RealmRules.Index(cells[cells.Length - count + k])];
    }

    private void TrailStrike(RealmRules.Unit actor, Point target)
    {
        Vector2 from = centers[RealmRules.Index(actor.Cell)], toward = centers[RealmRules.Index(target)] - from;
        int start = StartTrail(actor, 3);
        trails[start + 1] = from + toward * Math.Min(0.45f, 16 / toward.Length());
        trails[start + 2] = from;
    }

    private string Description()
    {
        if (unit >= 0 && rules.Units[unit] is { Alive: true, Side: 1 } chosen)
            return $"{chosen.Kind}   HP {chosen.Health}   MOVES {chosen.Moves / 10f:0.0}    " +
                (chosen.Kind == UnitKind.Settler ? "B Found city" : chosen.Kind == UnitKind.Worker ? "I / M / O  Farm / Mine / Road" : "Right-click to move or attack");
        if (city >= 0 && rules.Cities[city] is { Side: 1 } town)
            return $"CITY   POP {town.Population}   FOOD {town.Food}   INDUSTRY {town.Industry}    " +
                (town.Queue is { } queued ? $"Training {queued}" : "1 Wagon  2 Worker  3 Scout  4 Spear  5 Archer");
        if (inspected is { } tile)
        {
            int i = RealmRules.Index(tile);
            if (!explored[i]) return "Unexplored";
            string terrain = rules.Terrain[i] switch { 1 => "Meadow", 2 => "Forest", 3 => "Hills", 4 => "Water", 5 => "Mountain", _ => "Desert" };
            return $"{terrain}   {tile.X}, {tile.Y}";
        }
        return "Select a teal unit or city.";
    }

    private void SetNotice(IGameUi ui, string text)
    {
        if (rules.Winner != 0)
        {
            string result = rules.Winner == 1 ? "Victory" : "Defeat";
            text = text.Length == 0 ? $"{result}    N New campaign    F5 Save" : $"{result}    N New campaign    |    {text}";
        }
        if (text == notice) return;
        notice = text;
        ui.SetText(Notice, text);
    }

    private void Save(SessionState session)
    {
        var data = new GameData();
        var log = new StringBuilder();
        foreach (var item in rules.Commands)
            log.Append(FormattableString.Invariant($"{(int)item.Kind},{item.Id},{item.A},{item.B};"));
        data.Set("seed", rules.Seed);
        data.Set("orders", log.ToString());
        data.Set("legacyOrders", legacyOrders);
        data.Set("hash", StateHash.ToString("x16", CultureInfo.InvariantCulture));
        session.Storage.SaveSlot(1, data);
    }

    private void RestoreCommands(GameData data)
    {
        Visibility();
        var orders = data.GetText("orders").Split(';', StringSplitOptions.RemoveEmptyEntries);
        // Older slots replay their original orders before switching to the current rules.
        legacyOrders = (int)Math.Clamp(data.GetNumber("legacyOrders", orders.Length), 0, orders.Length);
        for (int i = 0; i < orders.Length; i++)
        {
            rules.Legacy = i < legacyOrders;
            var fields = orders[i].Split(',');
            if (fields.Length != 4 ||
                !int.TryParse(fields[0], CultureInfo.InvariantCulture, out int kind) ||
                !int.TryParse(fields[1], CultureInfo.InvariantCulture, out int id) ||
                !int.TryParse(fields[2], CultureInfo.InvariantCulture, out int a) ||
                !int.TryParse(fields[3], CultureInfo.InvariantCulture, out int b) ||
                !rules.Apply(new((OrderKind)kind, id, a, b)))
                throw new InvalidOperationException("Saved HexRealm orders no longer replay.");
            Visibility();
        }
        rules.Legacy = false;
        if (StateHash.ToString("x16", CultureInfo.InvariantCulture) != data.GetText("hash"))
            throw new InvalidOperationException("Replayed HexRealm state does not match the save.");
    }
}
