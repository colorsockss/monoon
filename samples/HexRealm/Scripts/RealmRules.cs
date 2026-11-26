using System;
using System.Collections.Generic;
using System.Drawing;
using Engine.Core;

internal enum UnitKind { Settler, Worker, Scout, Spear, Archer }
internal enum OrderKind { Move, Found, Improve, Train, Research, End }
internal enum ClaimCause { Founding, Growth, Capture }

internal readonly record struct RealmOrder(OrderKind Kind, int Id = 0, int A = 0, int B = 0);

internal sealed class RealmRules
{
    internal const int Width = 16, Height = 12, Count = Width * Height, UnitLimit = 24, LastTurn = 25, CityLimit = 8;
    internal static readonly Point[] Neighbors = [new(1, 0), new(0, 1), new(-1, 1), new(-1, 0), new(0, -1), new(1, -1)];
    internal static readonly Point Start = new(3, 5), Rival = new(12, 6), Greyharbor = new(13, 3);
    internal static readonly string[] Techs = ["Terraces", "Fletching", "Caravans", "Charter"];
    internal static readonly int[] Prices = [18, 12, 10, 14, 16];
    internal readonly int[] Terrain, Owner = new int[Count], Improvements = new int[Count];
    private readonly int[] holdings = new int[Count];
    internal readonly List<Unit> Units = new(UnitLimit);
    internal readonly List<City> Cities = new(16);
    internal readonly List<RealmOrder> Commands = new(256);
    private readonly GridNavigation movement;
    private readonly int[] movementCosts;
    private readonly List<Point> blocked = new(UnitLimit + 16);
    private readonly Point[] path = new Point[Count];
    internal int Turn = 1, Gold = 12, Tech, Research, ResearchPoints, Winner, RivalGold = 12;
    internal Tally CoastResult, RivalResult;
    internal bool CapitalTaken;
    internal readonly uint Seed;
    internal readonly int LandGoal;
    internal int ResearchGoal => 6 + 3 * Tech;

    internal readonly record struct Battle(int Damage, int Retaliation, int Health, bool City);
    internal readonly record struct Growth(int Food, int Goal, int Harvest, Point? Next)
    {
        internal int Turns => Harvest == 0 ? 0 : (Math.Max(0, Goal - Food) + Harvest - 1) / Harvest;
    }
    internal readonly record struct Tally(int Land, int Population, int Treasury, int Force)
    {
        internal int Total => Land + Population + Treasury + Force;
    }
    // Presentation hooks only: they never change the state the hash, saves and replays see.
    internal delegate void PathTaken(Unit unit, ReadOnlySpan<Point> cells);
    internal PathTaken? Moved;
    internal Action<Unit, Point, Unit?, Battle>? Struck;
    internal Action<City, Unit>? Trained;
    internal delegate void LandClaimed(City city, ClaimCause cause, ReadOnlySpan<Point> cells);
    internal LandClaimed? Claimed;

    internal sealed class Unit(Point cell, int side, UnitKind kind)
    {
        internal Point Cell = cell;
        internal readonly int Side = side;
        internal readonly UnitKind Kind = kind;
        internal int Health = kind is UnitKind.Settler or UnitKind.Worker ? 4 : 7;
        internal long Moves = Allowance(kind);
        internal bool Alive => Health > 0;
    }

    internal sealed class City(Point cell, int side, bool capital)
    {
        internal readonly Point Cell = cell;
        internal readonly bool Capital = capital;
        internal readonly int OriginalSide = side;
        internal int Side = side, Population = 1, Food, Industry, Health = 12;
        internal UnitKind? Queue;
        internal int FoodGoal => 12 * Population;
        internal bool CanGrow => Population < 8;
    }

    internal RealmRules(uint seed, int[] terrain, int[] costs)
    {
        Seed = seed;
        foreach (int tile in terrain)
            if (tile is not (4 or 5)) LandGoal++;
        LandGoal = (LandGoal * 2 + 4) / 5;
        Terrain = terrain;
        movement = new(Width, Height, costs, GridDirections.Hex);
        movementCosts = costs;
        Add(Start, 1, UnitKind.Settler);
        Add(new(4, 5), 1, UnitKind.Scout);
        Add(new(3, 6), 1, UnitKind.Spear);
        Add(new(2, 6), 1, UnitKind.Worker);
        Cities.Add(new(Rival, 2, true));
        Claim(Cities[0]);
        Add(new(11, 6), 2, UnitKind.Spear);
        Add(new(12, 5), 2, UnitKind.Archer);
        Add(Greyharbor, 2, UnitKind.Settler);
    }

    internal static int[] Generate(uint seed)
    {
        var board = new int[Count];
        for (int r = 0; r < Height; r++)
        for (int q = 0; q < Width; q++)
        {
            var cell = new Point(q, r);
            bool shore = r == 0 || r == Height - 1 || q == 0 || q == Width - 1 ||
                q == 1 && (r < 3 || r > 8) || q == 14 && (r < 3 || r > 8) ||
                q < 5 && r >= 9 || q > 11 && r <= 2 ||
                r == 1 && q is 6 or 7 || r == 10 && q is 8 or 9;
            int ridge = Math.Min(Distance(cell, new(8, 3)), Distance(cell, new(7, 4)));
            int woods = Math.Min(Distance(cell, new(5, 7)), Distance(cell, new(9, 8)));
            uint variation = Noise(seed, cell) % 12;
            board[Index(cell)] = shore ? 4 :
                Distance(cell, Start) <= 1 || Distance(cell, Rival) <= 1 ? 1 :
                ridge == 0 || ridge == 1 && r < 4 && variation < 8 ? 5 :
                ridge <= 2 && r <= 5 || r >= 8 && q is >= 10 and <= 12 ? 3 :
                woods <= 1 || woods == 2 && variation < 8 ? 2 :
                q >= 10 && r <= 5 && variation < 10 ? 6 : 1;
        }
        return board;
    }

    internal static uint Noise(uint seed, Point cell)
    {
        uint n = seed ^ (uint)(cell.X * 73856093) ^ (uint)(cell.Y * 19349663);
        n ^= n >> 16;
        n *= 0x7feb352d;
        return n ^ (n >> 15);
    }

    internal static int EntryCost(int terrain) => terrain switch { 4 or 5 => 0, 1 => 1, _ => 2 };

    internal static int Index(Point point) => point.Y * Width + point.X;
    internal static Point PointAt(int index) => new(index % Width, index / Width);
    internal static bool Contains(Point point) => (uint)point.X < Width && (uint)point.Y < Height;
    internal static Point Add(Point cell, Point edge) => new(cell.X + edge.X, cell.Y + edge.Y);
    internal static int Distance(Point a, Point b)
    {
        int dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Max(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Abs(dx + dy));
    }

    private static long Allowance(UnitKind kind) => kind == UnitKind.Scout ? 40 : 30;
    internal bool Passable(Point cell) => Contains(cell) && Terrain[Index(cell)] is not (4 or 5);
    private bool HasUnitSpace(int side)
    {
        int alive = 0;
        foreach (var unit in Units)
            if (unit.Alive && unit.Side == side && ++alive >= UnitLimit / 2) return false;
        return true;
    }

    private Unit Add(Point cell, int side, UnitKind kind)
    {
        var unit = new Unit(cell, side, kind);
        if (Units.Count < UnitLimit) Units.Add(unit);
        else Units[Units.FindIndex(dead => !dead.Alive)] = unit;
        return unit;
    }

    internal Growth Forecast(City city)
    {
        if (!city.CanGrow) return default;
        return new(city.Food, city.FoodGoal, Yield(city).Food, Frontier(city));
    }

    private (int Food, int Industry) Yield(City city)
    {
        int food = 0, industry = 0;
        for (int r = city.Cell.Y - 1; r <= city.Cell.Y + 1; r++)
        for (int q = city.Cell.X - 1; q <= city.Cell.X + 1; q++)
        {
            Point cell = new(q, r);
            if (!Contains(cell) || Distance(cell, city.Cell) > 1) continue;
            int i = Index(cell);
            if (Owner[i] != city.Side) continue;
            int farm = Improvements[i] == 1 ? city.Side == 1 && Tech >= 1 ? 2 : 1 : 0;
            food += (Terrain[i] is 1 or 2 ? 2 : 1) + farm;
            industry += (Terrain[i] is 2 or 3 ? 2 : 1) + (Improvements[i] == 2 ? 2 : 0);
        }
        return (food, industry);
    }

    private Point? Frontier(City city)
    {
        int district = Cities.IndexOf(city) + 1;
        int best = int.MaxValue;
        Point? result = null;
        for (int i = 0; i < Count; i++)
        {
            if (Owner[i] != 0 || Terrain[i] is 4 or 5) continue;
            Point cell = PointAt(i);
            int distance = Distance(cell, city.Cell);
            int rank = distance * 8 + Terrain[i];
            if (rank >= best || !BordersHolding(cell, district)) continue;
            // Shared frontiers go to the closest city, then to the one founded first.
            bool reserved = false;
            for (int j = 0; j < Cities.Count; j++)
            {
                if (j + 1 == district) continue;
                City other = Cities[j];
                int otherDistance = Distance(cell, other.Cell);
                if (!other.CanGrow || otherDistance > distance ||
                    otherDistance == distance && j + 1 > district || !BordersHolding(cell, j + 1)) continue;
                reserved = true;
                break;
            }
            if (reserved) continue;
            best = rank;
            result = cell;
        }
        return result;
    }

    private bool BordersHolding(Point cell, int district)
    {
        foreach (var edge in Neighbors)
        {
            Point neighbor = Add(cell, edge);
            if (Contains(neighbor) && holdings[Index(neighbor)] == district) return true;
        }
        return false;
    }
    internal Unit? UnitAt(Point cell)
    {
        foreach (var unit in Units)
            if (unit.Alive && unit.Cell == cell) return unit;
        return null;
    }

    internal City? CityAt(Point cell)
    {
        foreach (var city in Cities)
            if (city.Cell == cell) return city;
        return null;
    }

    internal bool Apply(RealmOrder order)
    {
        if (Winner != 0) return false;
        bool changed = order.Kind switch
        {
            OrderKind.Move => PlayerUnit(order.Id) is { } unit && Move(unit, new(order.A, order.B)),
            OrderKind.Found => PlayerUnit(order.Id) is { } settler && Found(settler),
            OrderKind.Improve => PlayerUnit(order.Id) is { } worker && Improve(worker, order.A),
            OrderKind.Train => Train(order.Id, order.A),
            OrderKind.Research => SetResearch(order.A),
            OrderKind.End => EndTurn(),
            _ => false,
        };
        if (changed) Commands.Add(order);
        return changed;
    }

    private Unit? PlayerUnit(int index) => (uint)index < Units.Count && Units[index] is { Alive: true, Side: 1 } unit ? unit : null;

    private bool Move(Unit unit, Point target)
    {
        if (!Passable(target) || target == unit.Cell || unit.Moves == 0) return false;
        if (Attack(unit, target) is { } hit)
        {
            var defender = hit.City ? null : UnitAt(target);
            unit.Moves = 0;
            unit.Health -= hit.Retaliation;
            if (hit.City)
            {
                var city = CityAt(target)!;
                city.Health -= hit.Damage;
                if (city.Health <= 0 && unit.Alive)
                {
                    city.Health = 12;
                    city.Side = unit.Side;
                    city.Queue = null;
                    Claim(city, ClaimCause.Capture);
                    if (city.Capital) Finish(unit.Side, capital: true);
                }
                else city.Health = Math.Max(1, city.Health);
            }
            else defender!.Health -= hit.Damage;
            Struck?.Invoke(unit, target, defender, hit);
            return true;
        }

        int length = PreviewPath(unit, target, path, out long cost);
        if (length == 0) return false;
        Moved?.Invoke(unit, path.AsSpan(0, length));
        unit.Cell = target;
        unit.Moves -= cost;
        return true;
    }

    internal Battle? Attack(Unit unit, Point target)
    {
        if (!unit.Alive || unit.Moves == 0 || !Passable(target) ||
            unit.Kind is not (UnitKind.Spear or UnitKind.Archer) ||
            Distance(unit.Cell, target) > (unit.Kind == UnitKind.Archer ? 2 : 1)) return null;
        if (UnitAt(target) is { } guard)
        {
            if (guard.Side == unit.Side) return null;
            int cover = Terrain[Index(target)] is 2 or 3 ? 1 : 0;
            int damage = (unit.Kind == UnitKind.Archer ? 3 : 4) - cover;
            int reply = guard.Health > damage && Distance(unit.Cell, target) == 1 &&
                guard.Kind is UnitKind.Spear or UnitKind.Archer ? 2 : 0;
            return new(damage, reply, guard.Health, false);
        }
        if (CityAt(target) is { } city && city.Side != unit.Side)
        {
            int walls = city.Population >= 3 ? 1 : 0;
            int charter = city.Side == 1 && Tech >= 4 ? 1 : 0;
            return new((unit.Kind == UnitKind.Spear ? 5 : 3) - walls - charter, 1 + walls, city.Health, true);
        }
        return null;
    }

    internal int PreviewPath(Unit unit, Point target, Span<Point> output, out long cost)
    {
        cost = 0;
        if (!Passable(target) || target == unit.Cell || unit.Moves == 0) return 0;
        int count = MovementFor(unit).FindPath(unit.Cell, target, output, out cost);
        return count != 0 && cost <= unit.Moves ? count : 0;
    }

    internal int Reachable(Unit unit, Span<GridReach> output) => MovementFor(unit).Reachable(unit.Cell, unit.Moves, output);

    private GridNavigation MovementFor(Unit unit)
    {
        foreach (Point cell in blocked) movement.SetEntryCost(cell, movementCosts[Index(cell)]);
        blocked.Clear();
        foreach (var other in Units)
            if (other.Alive && other != unit) Block(other.Cell);
        foreach (var city in Cities)
            if (city.Side != unit.Side && city.Cell != unit.Cell) Block(city.Cell);
        return movement;
    }

    private void Block(Point cell)
    {
        movement.SetEntryCost(cell, 0);
        blocked.Add(cell);
    }

    private bool Found(Unit unit)
    {
        if (unit.Kind != UnitKind.Settler || unit.Moves == 0 || Cities.Count >= CityLimit ||
            Owner[Index(unit.Cell)] != 0 && Owner[Index(unit.Cell)] != unit.Side ||
            Cities.Exists(city => Distance(unit.Cell, city.Cell) < 3)) return false;
        var founded = new City(unit.Cell, unit.Side, !Cities.Exists(city => city.Side == unit.Side));
        Cities.Add(founded);
        Claim(founded);
        unit.Health = 0;
        return true;
    }

    private bool Improve(Unit unit, int improvement)
    {
        if (unit.Kind != UnitKind.Worker || unit.Moves == 0 || improvement is < 1 or > 3 ||
            Owner[Index(unit.Cell)] != unit.Side || Improvements[Index(unit.Cell)] != 0) return false;
        Improvements[Index(unit.Cell)] = improvement;
        if (improvement == 3)
        {
            movement.SetEntryCost(unit.Cell, 1);
            movementCosts[Index(unit.Cell)] = 1;
        }
        unit.Moves = 0;
        return true;
    }

    private bool Train(int id, int kind)
    {
        if ((uint)id >= Cities.Count || Cities[id] is not { Side: 1, Queue: null } city ||
            (uint)kind >= Prices.Length || !HasUnitSpace(city.Side) || kind == (int)UnitKind.Archer && Tech < 2) return false;
        city.Queue = (UnitKind)kind;
        return true;
    }

    private bool SetResearch(int amount)
    {
        if (amount is < 0 or > 1 || Tech >= Techs.Length || amount == Research) return false;
        Research = amount;
        return true;
    }

    private bool EndTurn()
    {
        // Snapshot every forecast before applying any growth so one city's new tile
        // cannot change another city's previously shown target during the same harvest.
        Span<Point?> frontiers = stackalloc Point?[CityLimit];
        Span<(int Food, int Industry)> harvests = stackalloc (int, int)[CityLimit];
        for (int i = 0; i < Cities.Count; i++)
        {
            if (Cities[i].CanGrow) frontiers[i] = Frontier(Cities[i]);
            harvests[i] = Yield(Cities[i]);
        }
        for (int i = 0; i < Cities.Count; i++)
        {
            City city = Cities[i];
            var harvest = harvests[i];
            city.Industry += harvest.Industry;
            if (city.CanGrow) city.Food += harvest.Food;
            if (city.CanGrow && city.Food >= city.FoodGoal)
            {
                city.Food -= city.FoodGoal;
                city.Population++;
                if (frontiers[i] is { } next)
                {
                    int index = Index(next);
                    holdings[index] = i + 1;
                    Owner[index] = city.Side;
                    ReadOnlySpan<Point> claimed = [next];
                    Claimed?.Invoke(city, ClaimCause.Growth, claimed);
                }
            }
            city.Health = Math.Min(12, city.Health + 1);
            city.Industry = Math.Min(24, city.Industry);
            if (city.Side == 1) Gold += city.Population + (Tech >= 3 ? 2 : 0);
            else RivalGold += city.Population;
            if (city.Queue is not { } order || city.Industry < Prices[(int)order] || !HasUnitSpace(city.Side)) continue;
            foreach (var edge in Neighbors)
            {
                Point cell = Add(city.Cell, edge);
                if (!Passable(cell) || UnitAt(cell) != null || CityAt(cell) != null) continue;
                var recruit = Add(cell, city.Side, order);
                city.Industry -= Prices[(int)order];
                city.Queue = null;
                Trained?.Invoke(city, recruit);
                break;
            }
        }

        if (Tech < Techs.Length)
        {
            bool funded = Research == 1 && Gold >= 2;
            if (funded) Gold -= 2;
            ResearchPoints += funded ? 5 : 2;
            if (ResearchPoints >= ResearchGoal)
            {
                ResearchPoints -= ResearchGoal;
                Tech++;
            }
        }
        foreach (var unit in Units)
            if (unit.Alive) unit.Moves = Allowance(unit.Kind);
        RivalTurn();
        Turn++;
        if (Winner == 0 && Territory(1) >= LandGoal) Finish(1);
        if (Winner == 0 && Turn > LastTurn) Finish(Score(1).Total > Score(2).Total ? 1 : 2);
        return true;
    }

    private void RivalTurn()
    {
        var stronghold = Cities.Find(city => city.Side == 2 && city.Capital);
        if (stronghold == null) return;
        // Greyharbor starts with a settler. Founding and production use the same rules as Haven.
        int workers = 0;
        foreach (var unit in Units)
            if (unit is { Alive: true, Side: 2, Kind: UnitKind.Worker }) workers++;
        foreach (var city in Cities)
        {
            if (city.Side != 2) continue;
            if (city.Queue == UnitKind.Worker) workers++;
            if (city.Queue != null || !HasUnitSpace(2)) continue;
            if (workers == 0)
            {
                city.Queue = UnitKind.Worker;
                workers++;
            }
            else city.Queue = Turn >= 6 && (Turn + city.Cell.X) % 3 == 0 ? UnitKind.Archer : UnitKind.Spear;
        }

        Point target = Cities.Find(city => city.Side == 1 && city.Capital)?.Cell ?? Start;
        for (int i = 0; i < Units.Count && Winner == 0; i++)
        {
            var unit = Units[i];
            if (!unit.Alive || unit.Side != 2) continue;
            if (unit.Kind == UnitKind.Settler)
            {
                if (Turn >= 2) Found(unit);
                continue;
            }
            if (unit.Kind == UnitKind.Worker)
            {
                BuildDistrict(unit);
                continue;
            }
            Unit? nearby = null;
            foreach (var other in Units)
                if (other.Alive && other.Side == 1 && Attack(unit, other.Cell) != null &&
                    (nearby == null || nearby.Health > other.Health))
                    nearby = other;
            if (nearby != null) { Move(unit, nearby.Cell); continue; }
            if (Attack(unit, target) != null) { Move(unit, target); continue; }
            if (Turn < 3 || i == 5 && Turn < 8) continue;
            int size = Reachable(unit, approach);
            Point advance = unit.Cell;
            int best = Distance(advance, target);
            for (int j = 0; j < size; j++)
            {
                int distance = Distance(approach[j].Cell, target);
                if (distance >= best) continue;
                advance = approach[j].Cell;
                best = distance;
            }
            if (advance != unit.Cell) Move(unit, advance);
        }
    }

    private void BuildDistrict(Unit unit)
    {
        if (Improve(unit, Terrain[Index(unit.Cell)] is 2 or 3 ? 2 : 1)) return;
        int count = Reachable(unit, approach);
        for (int i = 0; i < count; i++)
        {
            var next = approach[i];
            int index = Index(next.Cell);
            if (next.Cost >= unit.Moves || Owner[index] != 2 || Improvements[index] != 0 || CityAt(next.Cell) != null)
                continue;
            if (Move(unit, next.Cell)) Improve(unit, Terrain[index] is 2 or 3 ? 2 : 1);
            return;
        }
    }

    private void Finish(int side, bool capital = false)
    {
        CoastResult = Score(1);
        RivalResult = Score(2);
        CapitalTaken = capital;
        Winner = side;
    }

    private readonly GridReach[] approach = new GridReach[Count];

    internal Tally Score(int side)
    {
        int population = 0, force = 0;
        foreach (var city in Cities) if (city.Side == side) population += city.Population * 3;
        foreach (var unit in Units)
            if (unit.Alive && unit.Side == side && unit.Kind is UnitKind.Spear or UnitKind.Archer) force += 2;
        return new(Territory(side), population, (side == 1 ? Gold : RivalGold) / 5, force);
    }

    private void Claim(City city, ClaimCause cause = ClaimCause.Founding)
    {
        int district = Cities.IndexOf(city) + 1;
        Span<Point> changed = stackalloc Point[Count];
        int count = 0;
        for (int i = 0; i < Count; i++)
        {
            Point cell = PointAt(i);
            if (cause == ClaimCause.Capture ? holdings[i] != district
                : cell != city.Cell && (Owner[i] != 0 || !Passable(cell) || Distance(cell, city.Cell) > 1)) continue;
            holdings[i] = district;
            if (Owner[i] == city.Side) continue;
            Owner[i] = city.Side;
            changed[count++] = cell;
        }
        if (count != 0) Claimed?.Invoke(city, cause, changed[..count]);
    }

    internal int Territory(int side)
    {
        int cells = 0;
        foreach (int tile in Owner) if (tile == side) cells++;
        return cells;
    }

    internal ulong Hash()
    {
        ulong hash = 14695981039346656037ul;
        void Mix(long value) { hash ^= (ulong)value; hash *= 1099511628211ul; }
        Mix(Seed); Mix(Turn); Mix(Gold); Mix(Tech); Mix(Research); Mix(ResearchPoints); Mix(Winner); Mix(RivalGold);
        for (int i = 0; i < Count; i++) { Mix(Terrain[i]); Mix(Owner[i]); Mix(holdings[i]); Mix(Improvements[i]); }
        foreach (var city in Cities)
        {
            Mix(Index(city.Cell)); Mix(city.Capital ? 1 : 0); Mix(city.Side); Mix(city.Population); Mix(city.Food); Mix(city.Industry); Mix(city.Health);
            Mix(city.Queue is { } queued ? (int)queued + 1 : 0);
        }
        foreach (var unit in Units)
        {
            Mix(Index(unit.Cell)); Mix(unit.Side); Mix((int)unit.Kind); Mix(unit.Health); Mix(unit.Moves);
        }
        return hash;
    }
}
