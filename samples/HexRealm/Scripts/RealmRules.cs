using System;
using System.Collections.Generic;
using System.Drawing;
using Engine.Core;

internal enum UnitKind { Settler, Worker, Scout, Spear, Archer }
internal enum OrderKind { Move, Found, Improve, Train, Research, End }

internal readonly record struct RealmOrder(OrderKind Kind, int Id = 0, int A = 0, int B = 0);

internal sealed class RealmRules
{
    internal const int Width = 30, Height = 20, Count = Width * Height, UnitLimit = 48;
    internal static readonly Point[] Neighbors = [new(1, 0), new(0, 1), new(-1, 1), new(-1, 0), new(0, -1), new(1, -1)];
    internal static readonly Point Start = new(5, 7), Rival = new(22, 12);
    internal static readonly string[] Techs = ["Terraces", "Masonry", "Fletching", "Caravans", "Irrigation", "Foundries",
        "Cartography", "Guilds", "Astronomy", "Civic charter"];
    internal static readonly int[] Prices = [18, 12, 10, 14, 16];
    internal readonly int[] Terrain, Owner = new int[Count], Improvements = new int[Count];
    internal readonly List<Unit> Units = new(UnitLimit);
    internal readonly List<City> Cities = new(16);
    internal readonly List<RealmOrder> Commands = new(256);
    internal readonly GridNavigation Grid;
    private readonly GridNavigation movement;
    private readonly int[] movementCosts;
    private readonly List<Point> blocked = new(UnitLimit + 16);
    private readonly Point[] path = new Point[Count];
    internal int Turn = 1, Gold = 12, Tech, Research, ResearchPoints, Winner;
    internal readonly uint Seed;
    internal bool Legacy;

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
        internal int Side = side, Population = 1, Food, Industry, Health = 13;
        internal UnitKind? Queue;
    }

    internal RealmRules(uint seed, int[] terrain, int[] costs)
    {
        Seed = seed;
        Terrain = terrain;
        Grid = new(Width, Height, costs, GridDirections.Hex);
        movement = new(Width, Height, costs, GridDirections.Hex);
        movementCosts = costs;
        Add(Start, 1, UnitKind.Settler);
        Add(new(6, 7), 1, UnitKind.Scout);
        Add(new(5, 8), 1, UnitKind.Spear);
        Add(new(4, 8), 1, UnitKind.Worker);
        Cities.Add(new(Rival, 2, true));
        Claim(Cities[0]);
        Add(new(21, 12), 2, UnitKind.Spear);
        Add(new(22, 11), 2, UnitKind.Archer);
    }

    internal static int[] Generate(uint seed)
    {
        var board = new int[Count];
        for (int r = 0; r < Height; r++)
        for (int q = 0; q < Width; q++)
        {
            uint n = seed ^ (uint)(q * 73856093) ^ (uint)(r * 19349663);
            n ^= n >> 16; n *= 0x7feb352d; n ^= n >> 15;
            var cell = new Point(q, r);
            bool shore = q == 0 || r == 0 || q == Width - 1 || r == Height - 1;
            board[Index(cell)] = shore ? 4 : Distance(cell, Start) <= 2 || Distance(cell, Rival) <= 2 ? 1 :
                (n % 24) switch { <= 10 => 1, <= 15 => 2, <= 18 => 3, 19 => 4, 20 => 5, _ => 6 };
        }
        return board;
    }

    internal static int Index(Point point) => point.Y * Width + point.X;
    internal static Point PointAt(int index) => new(index % Width, index / Width);
    internal static bool Contains(Point point) => (uint)point.X < Width && (uint)point.Y < Height;
    internal static Point Add(Point cell, Point edge) => new(cell.X + edge.X, cell.Y + edge.Y);
    internal static int Distance(Point a, Point b)
    {
        int dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Max(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Abs(dx + dy));
    }

    private static long Allowance(UnitKind kind) => kind == UnitKind.Scout ? 40 : 25;
    private bool Passable(Point cell) => Contains(cell) && Terrain[Index(cell)] is not (4 or 5);
    private bool HasUnitSpace() => Units.Count < UnitLimit || !Legacy && Units.Exists(unit => !unit.Alive);

    private void Add(Point cell, int side, UnitKind kind)
    {
        var unit = new Unit(cell, side, kind);
        if (Units.Count < UnitLimit) Units.Add(unit);
        else Units[Units.FindIndex(dead => !dead.Alive)] = unit;
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
        int range = unit.Kind == UnitKind.Archer ? 2 : 1;
        bool fighter = unit.Kind is UnitKind.Spear or UnitKind.Archer;
        if (UnitAt(target) is { } guard)
        {
            if (!fighter || guard.Side == unit.Side || Distance(unit.Cell, target) > range) return false;
            guard.Health -= (unit.Kind == UnitKind.Archer ? 3 : 4) - (Terrain[Index(target)] is 2 or 3 ? 1 : 0);
            unit.Moves = 0;
            return true;
        }
        if (CityAt(target) is { } city && city.Side != unit.Side)
        {
            if (!fighter || Distance(unit.Cell, target) > range) return false;
            unit.Moves = 0;
            int damage = unit.Kind == UnitKind.Spear ? 5 : 3;
            if (city.Population >= 3) damage--;
            unit.Health -= city.Population >= 3 ? 3 : 2;
            if ((city.Health -= damage) <= 0)
            {
                city.Health = 13;
                city.Side = unit.Side;
                Claim(city);
                if (Legacy ? city.Cell == Rival && unit.Side == 1 || city.Cell == Start && unit.Side == 2 : city.Capital)
                    Winner = unit.Side;
            }
            return true;
        }
        bool terrainOnly = Legacy || unit.Side == 2;
        var navigation = terrainOnly ? Grid : MovementFor(unit);
        int length = navigation.FindPath(unit.Cell, target, path, out long cost);
        if (length == 0 || cost > unit.Moves) return false;
        if (terrainOnly)
            for (int i = 1; i < length; i++)
                if (UnitAt(path[i]) != null || CityAt(path[i]) is { } block && block.Side != unit.Side) return false;
        unit.Cell = target;
        unit.Moves -= cost;
        return true;
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
        if (unit.Kind != UnitKind.Settler || unit.Moves == 0 ||
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
            Owner[Index(unit.Cell)] != 1 || Improvements[Index(unit.Cell)] != 0) return false;
        Improvements[Index(unit.Cell)] = improvement;
        if (improvement == 3)
        {
            Grid.SetEntryCost(unit.Cell, 1);
            movement.SetEntryCost(unit.Cell, 1);
            movementCosts[Index(unit.Cell)] = 1;
        }
        unit.Moves = 0;
        return true;
    }

    private bool Train(int id, int kind)
    {
        if ((uint)id >= Cities.Count || Cities[id] is not { Side: 1, Queue: null } city ||
            (uint)kind >= Prices.Length || !HasUnitSpace() || kind == (int)UnitKind.Archer && Tech < 2) return false;
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
        foreach (var city in Cities)
        {
            for (int i = 0; i < Count; i++)
            {
                if (Owner[i] != city.Side || Distance(PointAt(i), city.Cell) > 1) continue;
                city.Food += (Terrain[i] is 1 or 2 ? 2 : 1) + (Improvements[i] == 1 ? 1 : 0);
                city.Industry += (Terrain[i] is 2 or 3 ? 2 : 1) + (Improvements[i] == 2 ? 1 : 0);
            }
            if (city.Food >= 12 * city.Population && city.Population < 8)
            {
                city.Food -= 12 * city.Population;
                city.Population++;
                Claim(city);
            }
            city.Health = Math.Min(13, city.Health + 1);
            if (city.Side == 1) Gold += city.Population;
            if (city.Queue is not { } order || city.Industry < Prices[(int)order] || !HasUnitSpace()) continue;
            foreach (var edge in Neighbors)
            {
                Point cell = Add(city.Cell, edge);
                if (!Passable(cell) || UnitAt(cell) != null) continue;
                Add(cell, city.Side, order);
                city.Industry -= Prices[(int)order];
                city.Queue = null;
                break;
            }
        }

        ResearchPoints += Research == 0 ? 2 : 3;
        if (Tech < Techs.Length && ResearchPoints >= 6 + 2 * Tech)
        {
            ResearchPoints = 0;
            Tech++;
        }
        if (Research == 1) Gold = Math.Max(0, Gold - 1);
        foreach (var unit in Units)
            if (unit.Alive) unit.Moves = Allowance(unit.Kind);
        RivalTurn();
        Turn++;
        if (Winner == 0 && Territory(1) >= Count * 2 / 5) Winner = 1;
        if (Winner == 0 && Turn > 60) Winner = Territory(1) + Gold / 4 >= Territory(2) + 16 ? 1 : 2;
        return true;
    }

    private void RivalTurn()
    {
        if (Turn % 8 == 0 && HasUnitSpace() && UnitAt(new(21, 12)) == null)
            Add(new(21, 12), 2, UnitKind.Spear);
        Point target = Cities.Find(city => city.Side == 1)?.Cell ?? Start;
        foreach (var unit in Units)
        {
            if (!unit.Alive || unit.Side != 2) continue;
            Unit? nearby = Units.Find(other => other.Alive && other.Side == 1 && Distance(unit.Cell, other.Cell) <= (unit.Kind == UnitKind.Archer ? 2 : 1));
            if (nearby != null)
            {
                Move(unit, nearby.Cell);
                continue;
            }
            if (Distance(unit.Cell, target) <= 1 && CityAt(target)?.Side == 1)
            {
                Move(unit, target);
                continue;
            }
            int length = Grid.FindPath(unit.Cell, target, path, out _);
            if (length > 2) Move(unit, path[1]);
        }
    }

    private void Claim(City city)
    {
        for (int i = 0; i < Count; i++)
            if (Passable(PointAt(i)) && Distance(PointAt(i), city.Cell) <= 1 + city.Population / 3)
                Owner[i] = city.Side;
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
        Mix(Seed); Mix(Turn); Mix(Gold); Mix(Tech); Mix(Research); Mix(ResearchPoints); Mix(Winner);
        for (int i = 0; i < Count; i++) { Mix(Terrain[i]); Mix(Owner[i]); Mix(Improvements[i]); }
        foreach (var city in Cities)
        {
            Mix(Index(city.Cell)); Mix(city.Side); Mix(city.Population); Mix(city.Food); Mix(city.Industry); Mix(city.Health);
            Mix(city.Queue is { } queued ? (int)queued + 1 : 0);
        }
        foreach (var unit in Units)
        {
            Mix(Index(unit.Cell)); Mix(unit.Side); Mix((int)unit.Kind); Mix(unit.Health); Mix(unit.Moves);
        }
        return hash;
    }
}
